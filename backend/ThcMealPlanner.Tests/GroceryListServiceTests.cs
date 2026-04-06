using FluentAssertions;
using ThcMealPlanner.Api.GroceryLists;
using ThcMealPlanner.Api.MealPlans;
using ThcMealPlanner.Api.Recipes;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Tests;

public sealed class GroceryListServiceTests
{
    private const string FamilyId = "FAM#test-family";

    [Fact]
    public async Task GenerateAsync_WhenMealPlanChanges_RecalculatesIngredientsAndPreservesManualItems()
    {
        var groceryRepo = new InMemoryRepository<GroceryListDocument>();
        var pantryRepo = new InMemoryRepository<PantryStaplesDocument>();
        var mealPlanRepo = new InMemoryRepository<MealPlanDocument>();
        var recipeRepo = new InMemoryRepository<RecipeDocument>();
        var service = new GroceryListService(groceryRepo, pantryRepo, mealPlanRepo, recipeRepo);

        await recipeRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "RECIPE#rec_a"),
            BuildRecipe("rec_a", "Tofu Bowl", [new RecipeIngredientModel { Name = "Tofu", Quantity = "1", Unit = "block", Section = "protein" }]));

        await recipeRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "RECIPE#rec_b"),
            BuildRecipe("rec_b", "Chicken Rice", [new RecipeIngredientModel { Name = "Chicken", Quantity = "1", Unit = "lb", Section = "protein" }]));

        await mealPlanRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "PLAN#2026-04-06"),
            BuildPlan("2026-04-06", "rec_a", "Tofu Bowl"));

        var first = await service.GenerateAsync(
            FamilyId,
            "test-user-123",
            "Adult 1",
            new GenerateGroceryListRequest { WeekStartDate = "2026-04-06", ClearExisting = true });

        var tofu = first.Items.Single(i => i.Name == "Tofu");
        var toggled = await service.ToggleItemAsync(
            FamilyId,
            tofu.Id,
            "test-user-123",
            "Adult 1",
            new ToggleGroceryItemRequest { Version = first.Version });
        toggled.Status.Should().Be(GroceryItemMutationStatus.Success);

        var afterToggle = toggled.List!;
        var addedManual = await service.AddItemAsync(
            FamilyId,
            new AddGroceryItemRequest
            {
                Name = "Paper towels",
                Section = "household",
                Quantity = 1,
                Unit = "pack",
                Version = afterToggle.Version
            });
        addedManual.Status.Should().Be(GroceryItemMutationStatus.Success);

        await mealPlanRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "PLAN#2026-04-06"),
            BuildPlan("2026-04-06", "rec_b", "Chicken Rice"));

        var regenerated = await service.GenerateAsync(
            FamilyId,
            "test-user-123",
            "Adult 1",
            new GenerateGroceryListRequest { WeekStartDate = "2026-04-06", ClearExisting = false });

        regenerated.Items.Should().ContainSingle(i => i.Name == "Chicken");
        regenerated.Items.Should().NotContain(i => i.Name == "Tofu");
        regenerated.Items.Should().ContainSingle(i => i.Name == "Paper towels" && i.MealAssociations.Count == 0);
    }

    [Fact]
    public async Task GenerateAsync_AppliesPantryStaplesAsInStock()
    {
        var groceryRepo = new InMemoryRepository<GroceryListDocument>();
        var pantryRepo = new InMemoryRepository<PantryStaplesDocument>();
        var mealPlanRepo = new InMemoryRepository<MealPlanDocument>();
        var recipeRepo = new InMemoryRepository<RecipeDocument>();
        var service = new GroceryListService(groceryRepo, pantryRepo, mealPlanRepo, recipeRepo);

        await pantryRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "PANTRY#STAPLES"),
            new PantryStaplesDocument
            {
                FamilyId = FamilyId,
                Items = [new PantryStapleItemDocument { Name = "Salt", Section = "pantry" }],
                PreferredSectionOrder = ["produce", "pantry", "protein"],
                UpdatedAt = DateTimeOffset.UtcNow
            });

        await recipeRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "RECIPE#rec_salt"),
            BuildRecipe("rec_salt", "Seasoned Veg", [new RecipeIngredientModel { Name = "Salt", Quantity = "1", Unit = "tsp", Section = "pantry" }]));

        await mealPlanRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "PLAN#2026-04-06"),
            BuildPlan("2026-04-06", "rec_salt", "Seasoned Veg"));

        var generated = await service.GenerateAsync(
            FamilyId,
            "test-user-123",
            "Adult 1",
            new GenerateGroceryListRequest { WeekStartDate = "2026-04-06", ClearExisting = true });

        generated.Items.Should().ContainSingle(i => i.Name == "Salt" && i.InStock);
    }

    [Fact]
    public async Task ToggleItemAsync_WhenVersionMismatches_ReturnsConflict()
    {
        var groceryRepo = new InMemoryRepository<GroceryListDocument>();
        var pantryRepo = new InMemoryRepository<PantryStaplesDocument>();
        var mealPlanRepo = new InMemoryRepository<MealPlanDocument>();
        var recipeRepo = new InMemoryRepository<RecipeDocument>();
        var service = new GroceryListService(groceryRepo, pantryRepo, mealPlanRepo, recipeRepo);

        await groceryRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "LIST#ACTIVE"),
            new GroceryListDocument
            {
                FamilyId = FamilyId,
                ListId = "LIST#ACTIVE",
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Items =
                [
                    new GroceryItemDocument
                    {
                        Id = "item_1",
                        Name = "Tofu",
                        Section = "protein",
                        Quantity = 1,
                        Unit = "block",
                        MealAssociations = [],
                        CheckedOff = false,
                        InStock = false
                    }
                ],
                Progress = new GroceryProgressDocument { Total = 1, Completed = 0, Percentage = 0 }
            });

        var result = await service.ToggleItemAsync(
            FamilyId,
            "item_1",
            "test-user-123",
            "Adult 1",
            new ToggleGroceryItemRequest { Version = 1 });

        result.Status.Should().Be(GroceryItemMutationStatus.Conflict);
    }

    [Fact]
    public async Task GetCurrentAsync_WhenOnlyExpiredCompletedItems_CleansListAndPersistsVersionBump()
    {
        var groceryRepo = new InMemoryRepository<GroceryListDocument>();
        var service = CreateService(groceryRepo: groceryRepo);

        var oldCheckedAt = DateTimeOffset.UtcNow.AddDays(-10);
        await groceryRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "LIST#ACTIVE"),
            new GroceryListDocument
            {
                FamilyId = FamilyId,
                ListId = "LIST#ACTIVE",
                Version = 5,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-14),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-8),
                Items =
                [
                    new GroceryItemDocument
                    {
                        Id = "item_old",
                        Name = "Old item",
                        Section = "pantry",
                        Quantity = 1,
                        MealAssociations = [],
                        CheckedOff = true,
                        CheckedOffTimestamp = oldCheckedAt,
                        CompletedTTL = oldCheckedAt.ToUnixTimeSeconds(),
                        InStock = false
                    }
                ],
                Progress = new GroceryProgressDocument { Total = 1, Completed = 1, Percentage = 100 }
            });

        var current = await service.GetCurrentAsync(FamilyId);

        current.Should().NotBeNull();
        current!.Items.Should().BeEmpty();
        current.Version.Should().Be(6);
        current.Progress.Total.Should().Be(0);
    }

    [Fact]
    public async Task AddItemAsync_WhenNoCurrentList_ReturnsNotFoundList()
    {
        var service = CreateService();

        var result = await service.AddItemAsync(
            FamilyId,
            new AddGroceryItemRequest { Name = "Milk", Section = "dairy", Version = 1 });

        result.Status.Should().Be(GroceryItemMutationStatus.NotFoundList);
    }

    [Fact]
    public async Task AddItemAsync_WhenVersionMatches_AddsSortedItemAndDefaultsQuantity()
    {
        var groceryRepo = new InMemoryRepository<GroceryListDocument>();
        var service = CreateService(groceryRepo: groceryRepo);

        await groceryRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "LIST#ACTIVE"),
            new GroceryListDocument
            {
                FamilyId = FamilyId,
                ListId = "LIST#ACTIVE",
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Items =
                [
                    new GroceryItemDocument
                    {
                        Id = "item_a",
                        Name = "Bananas",
                        Section = "produce",
                        Quantity = 1,
                        Unit = "bunch",
                        MealAssociations = [],
                        CheckedOff = false,
                        InStock = false
                    }
                ],
                Progress = new GroceryProgressDocument { Total = 1, Completed = 0, Percentage = 0 }
            });

        var result = await service.AddItemAsync(
            FamilyId,
            new AddGroceryItemRequest
            {
                Name = "  Milk  ",
                Section = "  dairy  ",
                Unit = "  carton  ",
                Version = 1
            });

        result.Status.Should().Be(GroceryItemMutationStatus.Success);
        result.Item.Should().NotBeNull();
        result.Item!.Name.Should().Be("Milk");
        result.Item.Section.Should().Be("dairy");
        result.Item.Unit.Should().Be("carton");
        result.Item.Quantity.Should().Be(1);
        result.List!.Version.Should().Be(2);
        result.List.Items.Select(i => i.Name).Should().ContainInOrder("Milk", "Bananas");
    }

    [Fact]
    public async Task SetInStockAsync_WhenMissingOrStale_ReturnsExpectedStatuses()
    {
        var groceryRepo = new InMemoryRepository<GroceryListDocument>();
        var service = CreateService(groceryRepo: groceryRepo);

        var notFoundList = await service.SetInStockAsync(FamilyId, "item_missing", new SetInStockRequest { Version = 1, InStock = true });
        notFoundList.Status.Should().Be(GroceryItemMutationStatus.NotFoundList);

        await groceryRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "LIST#ACTIVE"),
            new GroceryListDocument
            {
                FamilyId = FamilyId,
                ListId = "LIST#ACTIVE",
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Items =
                [
                    new GroceryItemDocument
                    {
                        Id = "item_1",
                        Name = "Yogurt",
                        Section = "dairy",
                        Quantity = 1,
                        Unit = "cup",
                        MealAssociations = [],
                        CheckedOff = false,
                        InStock = false
                    }
                ],
                Progress = new GroceryProgressDocument { Total = 1, Completed = 0, Percentage = 0 }
            });

        var conflict = await service.SetInStockAsync(FamilyId, "item_1", new SetInStockRequest { Version = 1, InStock = true });
        conflict.Status.Should().Be(GroceryItemMutationStatus.Conflict);

        var missingItem = await service.SetInStockAsync(FamilyId, "item_404", new SetInStockRequest { Version = 2, InStock = true });
        missingItem.Status.Should().Be(GroceryItemMutationStatus.NotFoundItem);

        var success = await service.SetInStockAsync(FamilyId, "item_1", new SetInStockRequest { Version = 2, InStock = true });
        success.Status.Should().Be(GroceryItemMutationStatus.Success);
        success.Item!.InStock.Should().BeTrue();
        success.List!.Version.Should().Be(3);
    }

    [Fact]
    public async Task RemoveItemAsync_WhenFound_RemovesItemAndUpdatesProgress()
    {
        var groceryRepo = new InMemoryRepository<GroceryListDocument>();
        var service = CreateService(groceryRepo: groceryRepo);

        await groceryRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "LIST#ACTIVE"),
            new GroceryListDocument
            {
                FamilyId = FamilyId,
                ListId = "LIST#ACTIVE",
                Version = 4,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Items =
                [
                    new GroceryItemDocument
                    {
                        Id = "item_done",
                        Name = "Eggs",
                        Section = "dairy",
                        Quantity = 1,
                        Unit = "dozen",
                        MealAssociations = [],
                        CheckedOff = true,
                        InStock = false
                    },
                    new GroceryItemDocument
                    {
                        Id = "item_keep",
                        Name = "Apples",
                        Section = "produce",
                        Quantity = 4,
                        Unit = null,
                        MealAssociations = [],
                        CheckedOff = false,
                        InStock = false
                    }
                ],
                Progress = new GroceryProgressDocument { Total = 2, Completed = 1, Percentage = 50 }
            });

        var result = await service.RemoveItemAsync(FamilyId, "item_done", new RemoveGroceryItemRequest { Version = 4 });

        result.Status.Should().Be(GroceryItemMutationStatus.Success);
        result.List!.Version.Should().Be(5);
        result.List.Items.Should().ContainSingle(i => i.Id == "item_keep");
        result.List.Progress.Total.Should().Be(1);
        result.List.Progress.Completed.Should().Be(0);
    }

    [Fact]
    public async Task PollAsync_ReturnsNotFoundNoChangesAndHasChanges()
    {
        var groceryRepo = new InMemoryRepository<GroceryListDocument>();
        var service = CreateService(groceryRepo: groceryRepo);

        var missing = await service.PollAsync(FamilyId, since: null);
        missing.Status.Should().Be(GroceryListPollStatus.NotFound);

        var updatedAt = DateTimeOffset.UtcNow;
        await groceryRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "LIST#ACTIVE"),
            new GroceryListDocument
            {
                FamilyId = FamilyId,
                ListId = "LIST#ACTIVE",
                Version = 9,
                CreatedAt = updatedAt.AddDays(-1),
                UpdatedAt = updatedAt,
                Items =
                [
                    new GroceryItemDocument
                    {
                        Id = "item_1",
                        Name = "Garlic",
                        Section = "produce",
                        Quantity = 1,
                        Unit = "bulb",
                        MealAssociations = [],
                        CheckedOff = false,
                        InStock = false
                    }
                ],
                Progress = new GroceryProgressDocument { Total = 1, Completed = 0, Percentage = 0 }
            });

        var noChanges = await service.PollAsync(FamilyId, since: updatedAt.AddMinutes(1));
        noChanges.Status.Should().Be(GroceryListPollStatus.NoChanges);

        var hasChanges = await service.PollAsync(FamilyId, since: updatedAt.AddMinutes(-1));
        hasChanges.Status.Should().Be(GroceryListPollStatus.HasChanges);
        hasChanges.Response!.HasChanges.Should().BeTrue();
        hasChanges.Response.Changes.Should().ContainSingle(c => c.ItemId == "item_1" && c.Action == "updated");
    }

    [Fact]
    public async Task PantryStaplesLifecycle_NormalizesSortsAndDeletesCaseInsensitive()
    {
        var pantryRepo = new InMemoryRepository<PantryStaplesDocument>();
        var service = CreateService(pantryRepo: pantryRepo);

        var defaults = await service.GetPantryStaplesAsync(FamilyId);
        defaults.PreferredSectionOrder.Should().Contain(["produce", "protein", "dairy", "pantry", "frozen", "household", "other"]);

        var replaced = await service.ReplacePantryStaplesAsync(
            FamilyId,
            new ReplacePantryStaplesRequest
            {
                Items =
                [
                    new PantryStapleItemDocument { Name = "  Salt  ", Section = " pantry " },
                    new PantryStapleItemDocument { Name = "salt", Section = "PANTRY" },
                    new PantryStapleItemDocument { Name = "Olive Oil", Section = null }
                ],
                PreferredSectionOrder = ["freezer", "produce", "  produce  "]
            });

        replaced.Items.Should().HaveCount(2);
        replaced.Items.Select(i => i.Name).Should().ContainInOrder("Olive Oil", "Salt");
        replaced.PreferredSectionOrder.Should().Contain("freezer");
        replaced.PreferredSectionOrder.Should().Contain("protein");

        var added = await service.AddPantryStapleAsync(FamilyId, new AddPantryStapleItemRequest { Name = "  pepper ", Section = " pantry " });
        added.Items.Should().Contain(i => i.Name == "pepper" && i.Section == "pantry");

        var deleted = await service.DeletePantryStapleAsync(FamilyId, "PEPPER");
        deleted.Should().BeTrue();

        var deletedMissing = await service.DeletePantryStapleAsync(FamilyId, "does-not-exist");
        deletedMissing.Should().BeFalse();
    }

    private static GroceryListService CreateService(
        InMemoryRepository<GroceryListDocument>? groceryRepo = null,
        InMemoryRepository<PantryStaplesDocument>? pantryRepo = null,
        InMemoryRepository<MealPlanDocument>? mealPlanRepo = null,
        InMemoryRepository<RecipeDocument>? recipeRepo = null)
    {
        return new GroceryListService(
            groceryRepo ?? new InMemoryRepository<GroceryListDocument>(),
            pantryRepo ?? new InMemoryRepository<PantryStaplesDocument>(),
            mealPlanRepo ?? new InMemoryRepository<MealPlanDocument>(),
            recipeRepo ?? new InMemoryRepository<RecipeDocument>());
    }

    private static MealPlanDocument BuildPlan(string weekStartDate, string recipeId, string recipeName) =>
        new()
        {
            FamilyId = FamilyId,
            WeekStartDate = weekStartDate,
            Status = "active",
            Meals =
            [
                new MealSlotDocument
                {
                    Day = "Monday",
                    MealType = "dinner",
                    RecipeId = recipeId,
                    RecipeName = recipeName
                }
            ],
            ConstraintsUsed = "v1",
            GeneratedBy = "manual",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static RecipeDocument BuildRecipe(string recipeId, string name, List<RecipeIngredientModel> ingredients) =>
        new()
        {
            RecipeId = recipeId,
            FamilyId = FamilyId,
            Name = name,
            Category = "dinner",
            Ingredients = ingredients,
            Instructions = ["Cook"],
            CreatedByUserId = "test-user-123",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private sealed class InMemoryRepository<TDocument> : IDynamoDbRepository<TDocument>
        where TDocument : class
    {
        private readonly Dictionary<string, TDocument> _store = new(StringComparer.Ordinal);

        public Task<TDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(ToCompositeKey(key), out var value);
            return Task.FromResult(value);
        }

        public Task PutAsync(DynamoDbKey key, TDocument document, CancellationToken cancellationToken = default)
        {
            _store[ToCompositeKey(key)] = document;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.Remove(ToCompositeKey(key));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TDocument>> QueryByPartitionKeyAsync(
            string partitionKey,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var results = _store
                .Where(kvp => kvp.Key.StartsWith($"{partitionKey}|", StringComparison.Ordinal))
                .Select(kvp => kvp.Value);

            if (limit.HasValue)
            {
                results = results.Take(limit.Value);
            }

            return Task.FromResult<IReadOnlyList<TDocument>>(results.ToList());
        }

        public Task<IReadOnlyList<TDocument>> QueryByIndexPartitionKeyAsync(
            string indexName,
            string partitionKeyName,
            string partitionKeyValue,
            IReadOnlyDictionary<string, string>? equalsFilters = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TDocument>>([]);
        }

        private static string ToCompositeKey(DynamoDbKey key) => $"{key.PartitionKey}|{key.SortKey}";
    }
}
