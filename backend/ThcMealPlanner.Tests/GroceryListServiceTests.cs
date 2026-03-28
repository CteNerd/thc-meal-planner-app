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
