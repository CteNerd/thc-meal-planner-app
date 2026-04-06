using FluentAssertions;
using Microsoft.Extensions.Options;
using ThcMealPlanner.Api.MealPlans;
using ThcMealPlanner.Api.Recipes;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Tests;

public sealed class MealPlanServiceTests
{
    private const string FamilyId = "FAM#test-family";

    private static MealPlanService CreateService(
        InMemoryMealPlanRepository? planRepo = null,
        InMemoryMealPlanServiceRecipeRepository? recipeRepo = null,
        ConstraintConfig? config = null,
        IMealPlanAiService? mealPlanAiService = null)
    {
        planRepo ??= new InMemoryMealPlanRepository();
        recipeRepo ??= new InMemoryMealPlanServiceRecipeRepository();
        var favoriteRepo = new InMemoryMealPlanServiceFavoriteRepository();
        var recipeService = new RecipeService(recipeRepo, favoriteRepo);
        mealPlanAiService ??= new NoOpMealPlanAiService();
        var constraintEngine = new ConstraintEngine(Options.Create(config ?? new ConstraintConfig
        {
            NoCookDays = ["Wednesday"],
            MaxWeekdayPrepMinutes = 45,
            MaxWeekendPrepMinutes = 180
        }));
        return new MealPlanService(planRepo, favoriteRepo, recipeService, constraintEngine, mealPlanAiService);
    }

    [Fact]
    public async Task GetCurrentAsync_WhenActivePlanExists_ReturnsPlan()
    {
        var planRepo = new InMemoryMealPlanRepository();
        var plan = BuildActivePlan(FamilyId, "2026-03-30");
        await planRepo.PutAsync(ToPlanKey(FamilyId, "2026-03-30"), plan);

        var service = CreateService(planRepo);

        var result = await service.GetCurrentAsync(FamilyId);

        result.Should().NotBeNull();
        result!.WeekStartDate.Should().Be("2026-03-30");
    }

    [Fact]
    public async Task GetCurrentAsync_WhenNoPlans_ReturnsNull()
    {
        var service = CreateService();

        var result = await service.GetCurrentAsync(FamilyId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentAsync_WithMultiplePlans_ReturnsLatestActivePlan()
    {
        var planRepo = new InMemoryMealPlanRepository();
        await planRepo.PutAsync(ToPlanKey(FamilyId, "2026-03-23"), BuildActivePlan(FamilyId, "2026-03-23"));
        await planRepo.PutAsync(ToPlanKey(FamilyId, "2026-03-30"), BuildActivePlan(FamilyId, "2026-03-30"));

        var service = CreateService(planRepo);

        var result = await service.GetCurrentAsync(FamilyId);

        result!.WeekStartDate.Should().Be("2026-03-30");
    }

    [Fact]
    public async Task GetCurrentAsync_WhenOnlyArchivedPlan_ReturnsNull()
    {
        var planRepo = new InMemoryMealPlanRepository();
        var archived = new MealPlanDocument
        {
            FamilyId = FamilyId,
            WeekStartDate = "2026-03-30",
            Status = "archived",
            Meals = [],
            GeneratedBy = "manual",
            ConstraintsUsed = "v1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await planRepo.PutAsync(ToPlanKey(FamilyId, "2026-03-30"), archived);

        var service = CreateService(planRepo);

        var result = await service.GetCurrentAsync(FamilyId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByWeekAsync_WhenExists_ReturnsPlan()
    {
        var planRepo = new InMemoryMealPlanRepository();
        await planRepo.PutAsync(ToPlanKey(FamilyId, "2026-03-30"), BuildActivePlan(FamilyId, "2026-03-30"));

        var service = CreateService(planRepo);

        var result = await service.GetByWeekAsync(FamilyId, "2026-03-30");

        result.Should().NotBeNull();
        result!.WeekStartDate.Should().Be("2026-03-30");
    }

    [Fact]
    public async Task GetByWeekAsync_WhenNotExists_ReturnsNull()
    {
        var service = CreateService();

        var result = await service.GetByWeekAsync(FamilyId, "2026-03-30");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsAllPlansOrderedByDateDesc()
    {
        var planRepo = new InMemoryMealPlanRepository();
        await planRepo.PutAsync(ToPlanKey(FamilyId, "2026-03-23"), BuildActivePlan(FamilyId, "2026-03-23"));
        await planRepo.PutAsync(ToPlanKey(FamilyId, "2026-03-30"), BuildActivePlan(FamilyId, "2026-03-30"));
        await planRepo.PutAsync(ToPlanKey(FamilyId, "2026-04-06"), BuildActivePlan(FamilyId, "2026-04-06"));

        var service = CreateService(planRepo);

        var result = await service.GetHistoryAsync(FamilyId);

        result.Should().HaveCount(3);
        result[0].WeekStartDate.Should().Be("2026-04-06");
        result[1].WeekStartDate.Should().Be("2026-03-30");
        result[2].WeekStartDate.Should().Be("2026-03-23");
    }

    [Fact]
    public async Task GetHistoryAsync_RespectsLimit()
    {
        var planRepo = new InMemoryMealPlanRepository();
        for (var i = 0; i < 15; i++)
        {
            var week = DateOnly.FromDateTime(new DateTime(2026, 1, 5).AddDays(i * 7)).ToString("yyyy-MM-dd");
            await planRepo.PutAsync(ToPlanKey(FamilyId, week), BuildActivePlan(FamilyId, week));
        }

        var service = CreateService(planRepo);

        var result = await service.GetHistoryAsync(FamilyId, limit: 5);

        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task CreateAsync_WithValidMeals_BuildsSlotsWithRecipeNames()
    {
        var recipeRepo = new InMemoryMealPlanServiceRecipeRepository();
        await recipeRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "RECIPE#rec_1"),
            BuildRecipe("rec_1", FamilyId, "Pancakes", "breakfast"));

        var service = CreateService(recipeRepo: recipeRepo);

        var request = new CreateMealPlanRequest
        {
            WeekStartDate = "2026-03-30",
            Meals = [new CreateMealSlotRequest { Day = "Monday", MealType = "breakfast", RecipeId = "rec_1" }]
        };

        var result = await service.CreateAsync(FamilyId, "user-123", request);

        result.Meals.Should().ContainSingle();
        result.Meals[0].RecipeName.Should().Be("Pancakes");
        result.WeekStartDate.Should().Be("2026-03-30");
        result.GeneratedBy.Should().Be("manual");
    }

    [Fact]
    public async Task CreateAsync_PersistsPlanToRepository()
    {
        var planRepo = new InMemoryMealPlanRepository();
        var service = CreateService(planRepo);

        var request = new CreateMealPlanRequest { WeekStartDate = "2026-03-30", Meals = [] };

        await service.CreateAsync(FamilyId, "user-123", request);

        var stored = await planRepo.GetAsync(ToPlanKey(FamilyId, "2026-03-30"));
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_WithRecipes_FillsWeeklySlots()
    {
        var recipeRepo = new InMemoryMealPlanServiceRecipeRepository();
        // Add enough recipes for each meal type
        string[] categories = ["breakfast", "lunch", "dinner"];
        for (var i = 0; i < 21; i++)
        {
            var category = categories[i % 3];
            await recipeRepo.PutAsync(
                new DynamoDbKey($"FAMILY#{FamilyId}", $"RECIPE#rec_{i}"),
                BuildRecipe($"rec_{i}", FamilyId, $"Recipe {i}", category));
        }

        var service = CreateService(recipeRepo: recipeRepo);

        var request = new GenerateMealPlanRequest { WeekStartDate = "2026-03-30" };

        var result = await service.GenerateAsync(FamilyId, "user-123", request);

        result.Meals.Should().HaveCount(21);
        result.GeneratedBy.Should().Be("ai");
        result.QualityScore.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_WithNoRecipes_ReturnsEmptyPlan()
    {
        var service = CreateService();

        var request = new GenerateMealPlanRequest { WeekStartDate = "2026-03-30" };

        var result = await service.GenerateAsync(FamilyId, "user-123", request);

        result.Meals.Should().BeEmpty();
        result.GeneratedBy.Should().Be("ai");
        result.WeekStartDate.Should().Be("2026-03-30");
    }

    [Fact]
    public async Task SuggestSwapOptionsAsync_ReturnsConstraintRankedOptions_ExcludingCurrentRecipe()
    {
        var planRepo = new InMemoryMealPlanRepository();
        var existing = new MealPlanDocument
        {
            FamilyId = FamilyId,
            WeekStartDate = "2026-03-30",
            Status = "active",
            Meals =
            [
                new MealSlotDocument
                {
                    Day = "Wednesday",
                    MealType = "dinner",
                    RecipeId = "rec_current",
                    RecipeName = "Current Dinner"
                }
            ],
            GeneratedBy = "ai",
            ConstraintsUsed = "v1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await planRepo.PutAsync(ToPlanKey(FamilyId, "2026-03-30"), existing);

        var recipeRepo = new InMemoryMealPlanServiceRecipeRepository();
        await recipeRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "RECIPE#rec_current"),
            BuildRecipe("rec_current", FamilyId, "Current Dinner", "dinner"));
        await recipeRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "RECIPE#rec_cold"),
            BuildRecipe("rec_cold", FamilyId, "Cold Dinner", "dinner", prep: 10, cook: 0, cookingMethod: ["raw"]));
        await recipeRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "RECIPE#rec_stove"),
            BuildRecipe("rec_stove", FamilyId, "Stove Dinner", "dinner", prep: 10, cook: 15, cookingMethod: ["stovetop"]));

        var service = CreateService(planRepo, recipeRepo);

        var result = await service.SuggestSwapOptionsAsync(FamilyId, "2026-03-30", "Wednesday", "dinner", limit: 5);

        result.Should().NotBeEmpty();
        result.Select(r => r.RecipeId).Should().NotContain("rec_current");
        result.First().RecipeId.Should().Be("rec_cold");
        result.First().ConstraintSafe.Should().BeTrue();
    }

    [Fact]
    public async Task SuggestSwapOptionsAsync_WhenPlanMissing_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = await service.SuggestSwapOptionsAsync(FamilyId, "2026-03-30", "Monday", "dinner");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SuggestSwapOptionsAsync_WhenAiHasFreshIdeas_AppendsAiSuggestions()
    {
        var planRepo = new InMemoryMealPlanRepository();
        await planRepo.PutAsync(
            ToPlanKey(FamilyId, "2026-03-30"),
            new MealPlanDocument
            {
                FamilyId = FamilyId,
                WeekStartDate = "2026-03-30",
                Status = "active",
                Meals =
                [
                    new MealSlotDocument { Day = "Monday", MealType = "dinner", RecipeId = "rec_current", RecipeName = "Current Dinner" }
                ],
                GeneratedBy = "ai",
                ConstraintsUsed = "v1",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var recipeRepo = new InMemoryMealPlanServiceRecipeRepository();
        await recipeRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "RECIPE#rec_other"),
            BuildRecipe("rec_other", FamilyId, "Other Dinner", "dinner", prep: 15, cook: 20));

        var aiService = new StubMealPlanAiService
        {
            FreshIdeas =
            [
                new AiRecipeIdea("Chickpea Curry", "Fast pantry dinner", "dinner")
            ]
        };

        var service = CreateService(planRepo, recipeRepo, mealPlanAiService: aiService);

        var result = await service.SuggestSwapOptionsAsync(FamilyId, "2026-03-30", "Monday", "dinner", limit: 5);

        result.Should().Contain(x => x.RecipeId == "rec_other");
        result.Should().Contain(x => x.IsAiSuggestion && x.RecipeName == "Chickpea Curry");
    }

    [Fact]
    public async Task UpdateAsync_MergesNewSlotsIntoExistingPlan()
    {
        var planRepo = new InMemoryMealPlanRepository();
        var existing = new MealPlanDocument
        {
            FamilyId = FamilyId,
            WeekStartDate = "2026-03-30",
            Status = "active",
            Meals =
            [
                new MealSlotDocument { Day = "Monday", MealType = "breakfast", RecipeId = "rec_old", RecipeName = "Old Recipe" },
                new MealSlotDocument { Day = "Tuesday", MealType = "lunch", RecipeId = "rec_keeper", RecipeName = "Keeper" }
            ],
            GeneratedBy = "manual",
            ConstraintsUsed = "v1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await planRepo.PutAsync(ToPlanKey(FamilyId, "2026-03-30"), existing);

        var recipeRepo = new InMemoryMealPlanServiceRecipeRepository();
        await recipeRepo.PutAsync(
            new DynamoDbKey($"FAMILY#{FamilyId}", "RECIPE#rec_new"),
            BuildRecipe("rec_new", FamilyId, "New Recipe", "breakfast"));

        var service = CreateService(planRepo, recipeRepo);

        var request = new UpdateMealPlanRequest
        {
            Meals = [new CreateMealSlotRequest { Day = "Monday", MealType = "breakfast", RecipeId = "rec_new" }]
        };

        var result = await service.UpdateAsync(FamilyId, "2026-03-30", request);

        result.Should().NotBeNull();
        result!.Meals.Should().HaveCount(2);
        result.Meals.Should().ContainSingle(m => m.Day == "Monday" && m.MealType == "breakfast" && m.RecipeId == "rec_new");
        result.Meals.Should().ContainSingle(m => m.Day == "Tuesday" && m.MealType == "lunch" && m.RecipeId == "rec_keeper");
    }

    [Fact]
    public async Task UpdateAsync_WhenPlanNotFound_ReturnsNull()
    {
        var service = CreateService();

        var result = await service.UpdateAsync(FamilyId, "2026-03-30", new UpdateMealPlanRequest());

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenOnlyStatusProvided_UpdatesStatusAndPreservesMeals()
    {
        var planRepo = new InMemoryMealPlanRepository();
        var existing = new MealPlanDocument
        {
            FamilyId = FamilyId,
            WeekStartDate = "2026-03-30",
            Status = "active",
            Meals = [new MealSlotDocument { Day = "Monday", MealType = "dinner", RecipeId = "rec_1", RecipeName = "Dinner" }]
            ,
            GeneratedBy = "manual",
            ConstraintsUsed = "v1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await planRepo.PutAsync(ToPlanKey(FamilyId, "2026-03-30"), existing);

        var service = CreateService(planRepo);

        var result = await service.UpdateAsync(FamilyId, "2026-03-30", new UpdateMealPlanRequest { Status = "archived" });

        result.Should().NotBeNull();
        result!.Status.Should().Be("archived");
        result.Meals.Should().ContainSingle(m => m.RecipeId == "rec_1");
    }

    [Fact]
    public async Task UpdateAsync_WhenNoMealsOrStatus_ReturnsExistingPlan()
    {
        var planRepo = new InMemoryMealPlanRepository();
        var existing = BuildActivePlan(FamilyId, "2026-03-30");
        await planRepo.PutAsync(ToPlanKey(FamilyId, "2026-03-30"), existing);

        var service = CreateService(planRepo);

        var result = await service.UpdateAsync(FamilyId, "2026-03-30", new UpdateMealPlanRequest());

        result.Should().NotBeNull();
        result!.WeekStartDate.Should().Be(existing.WeekStartDate);
        result.Status.Should().Be(existing.Status);
    }

    [Fact]
    public async Task DeleteAsync_WhenExists_ReturnsTrueAndRemovesPlan()
    {
        var planRepo = new InMemoryMealPlanRepository();
        await planRepo.PutAsync(ToPlanKey(FamilyId, "2026-03-30"), BuildActivePlan(FamilyId, "2026-03-30"));

        var service = CreateService(planRepo);

        var deleted = await service.DeleteAsync(FamilyId, "2026-03-30");

        deleted.Should().BeTrue();
        var gone = await planRepo.GetAsync(ToPlanKey(FamilyId, "2026-03-30"));
        gone.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ReturnsFalse()
    {
        var service = CreateService();

        var deleted = await service.DeleteAsync(FamilyId, "2026-03-30");

        deleted.Should().BeFalse();
    }

    private static MealPlanDocument BuildActivePlan(string familyId, string weekStartDate) =>
        new()
        {
            FamilyId = familyId,
            WeekStartDate = weekStartDate,
            Status = "active",
            Meals = [],
            GeneratedBy = "manual",
            ConstraintsUsed = "v1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static RecipeDocument BuildRecipe(
        string recipeId,
        string familyId,
        string name,
        string category,
        int? prep = null,
        int? cook = null,
        List<string>? cookingMethod = null) =>
        new()
        {
            RecipeId = recipeId,
            FamilyId = familyId,
            Name = name,
            Category = category,
            PrepTimeMinutes = prep,
            CookTimeMinutes = cook,
            CookingMethod = cookingMethod,
            Ingredients = [new RecipeIngredientModel { Name = "Ingredient" }],
            Instructions = ["Step 1"],
            CreatedByUserId = "user-123",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static DynamoDbKey ToPlanKey(string familyId, string weekStartDate)
        => new($"FAMILY#{familyId}", $"PLAN#{weekStartDate}");

    internal sealed class InMemoryMealPlanRepository : IDynamoDbRepository<MealPlanDocument>
    {
        private readonly Dictionary<string, MealPlanDocument> _store = new(StringComparer.Ordinal);

        public Task<MealPlanDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(ToMapKey(key), out var doc);
            return Task.FromResult(doc);
        }

        public Task PutAsync(DynamoDbKey key, MealPlanDocument document, CancellationToken cancellationToken = default)
        {
            _store[ToMapKey(key)] = document;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.Remove(ToMapKey(key));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MealPlanDocument>> QueryByPartitionKeyAsync(
            string partitionKey,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var items = _store
                .Where(e => e.Key.StartsWith(partitionKey + "|", StringComparison.Ordinal))
                .Select(e => e.Value)
                .ToList();

            if (limit.HasValue) items = items.Take(limit.Value).ToList();

            return Task.FromResult<IReadOnlyList<MealPlanDocument>>(items);
        }

        public Task<IReadOnlyList<MealPlanDocument>> QueryByIndexPartitionKeyAsync(
            string indexName,
            string partitionKeyName,
            string partitionKeyValue,
            IReadOnlyDictionary<string, string>? equalsFilters = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MealPlanDocument>>([]);
        }

        private static string ToMapKey(DynamoDbKey key) => $"{key.PartitionKey}|{key.SortKey}";
    }

    internal sealed class InMemoryMealPlanServiceRecipeRepository : IDynamoDbRepository<RecipeDocument>
    {
        private readonly Dictionary<string, RecipeDocument> _store = new(StringComparer.Ordinal);

        public Task<RecipeDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(ToMapKey(key), out var doc);
            return Task.FromResult(doc);
        }

        public Task PutAsync(DynamoDbKey key, RecipeDocument document, CancellationToken cancellationToken = default)
        {
            _store[ToMapKey(key)] = document;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.Remove(ToMapKey(key));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RecipeDocument>> QueryByPartitionKeyAsync(
            string partitionKey,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var items = _store
                .Where(e => e.Key.StartsWith(partitionKey + "|", StringComparison.Ordinal))
                .Select(e => e.Value)
                .ToList();
            return Task.FromResult<IReadOnlyList<RecipeDocument>>(items);
        }

        // RecipeService.ListByFamilyAsync uses index query with partitionKeyValue = familyId
        public Task<IReadOnlyList<RecipeDocument>> QueryByIndexPartitionKeyAsync(
            string indexName,
            string partitionKeyName,
            string partitionKeyValue,
            IReadOnlyDictionary<string, string>? equalsFilters = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var items = _store.Values
                .Where(r => string.Equals(r.FamilyId, partitionKeyValue, StringComparison.Ordinal))
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Task.FromResult<IReadOnlyList<RecipeDocument>>(items);
        }

        private static string ToMapKey(DynamoDbKey key) => $"{key.PartitionKey}|{key.SortKey}";
    }

    private sealed class InMemoryMealPlanServiceFavoriteRepository : IDynamoDbRepository<FavoriteRecipeDocument>
    {
        public Task<FavoriteRecipeDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
            => Task.FromResult<FavoriteRecipeDocument?>(null);

        public Task PutAsync(DynamoDbKey key, FavoriteRecipeDocument document, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<FavoriteRecipeDocument>> QueryByPartitionKeyAsync(
            string partitionKey,
            int? limit = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FavoriteRecipeDocument>>([]);

        public Task<IReadOnlyList<FavoriteRecipeDocument>> QueryByIndexPartitionKeyAsync(
            string indexName,
            string partitionKeyName,
            string partitionKeyValue,
            IReadOnlyDictionary<string, string>? equalsFilters = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FavoriteRecipeDocument>>([]);
    }
    private sealed class NoOpMealPlanAiService : IMealPlanAiService
    {
        public Task<IReadOnlyList<string>> GenerateRecipeIdsAsync(
            string weekStartDate,
            IReadOnlyList<(string Day, string MealType)> slots,
            IReadOnlyList<RecipeDocument> recipes,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> RankSwapCandidatesAsync(
            string day,
            string mealType,
            string? currentRecipeId,
            IReadOnlyList<RecipeDocument> candidates,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<AiRecipeIdea>> SuggestFreshIdeasAsync(
            string day,
            string mealType,
            string? profileContext,
            int count,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiRecipeIdea>>([]);
    }

    private sealed class StubMealPlanAiService : IMealPlanAiService
    {
        public IReadOnlyList<string> GeneratedRecipeIds { get; set; } = [];

        public IReadOnlyList<string> RankedIds { get; set; } = [];

        public IReadOnlyList<AiRecipeIdea> FreshIdeas { get; set; } = [];

        public Task<IReadOnlyList<string>> GenerateRecipeIdsAsync(
            string weekStartDate,
            IReadOnlyList<(string Day, string MealType)> slots,
            IReadOnlyList<RecipeDocument> recipes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(GeneratedRecipeIds);

        public Task<IReadOnlyList<string>> RankSwapCandidatesAsync(
            string day,
            string mealType,
            string? currentRecipeId,
            IReadOnlyList<RecipeDocument> candidates,
            CancellationToken cancellationToken = default)
            => Task.FromResult(RankedIds);

        public Task<IReadOnlyList<AiRecipeIdea>> SuggestFreshIdeasAsync(
            string day,
            string mealType,
            string? profileContext,
            int count,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FreshIdeas);
    }
}
