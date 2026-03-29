using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using ThcMealPlanner.Api.GroceryLists;
using ThcMealPlanner.Api.MealPlans;
using ThcMealPlanner.Api.Recipes;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Tests;

public sealed class MealPlanEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MealPlanEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCurrentMealPlan_WhenNoPlanExists_Returns404()
    {
        var client = CreateAuthenticatedClient([], []);

        var response = await client.GetAsync("/api/meal-plans/current");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCurrentMealPlan_WhenActivePlanExists_Returns200()
    {
        var plan = BuildActivePlan("FAM#test-family", "2026-03-30");
        var client = CreateAuthenticatedClient([plan], []);

        var response = await client.GetAsync("/api/meal-plans/current");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MealPlanDocument>();
        result.Should().NotBeNull();
        result!.WeekStartDate.Should().Be("2026-03-30");
    }

    [Fact]
    public async Task GetMealPlanHistory_ReturnsAllPlans()
    {
        var plan1 = BuildActivePlan("FAM#test-family", "2026-03-23");
        var plan2 = BuildActivePlan("FAM#test-family", "2026-03-30");
        var client = CreateAuthenticatedClient([plan1, plan2], []);

        var response = await client.GetAsync("/api/meal-plans/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<MealPlanDocument>>();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSwapOptions_WithValidQuery_Returns200()
    {
        var plan = new MealPlanDocument
        {
            FamilyId = "FAM#test-family",
            WeekStartDate = "2026-03-30",
            Status = "active",
            Meals =
            [
                new MealSlotDocument
                {
                    Day = "Monday",
                    MealType = "dinner",
                    RecipeId = "rec_current",
                    RecipeName = "Current Dinner"
                }
            ],
            GeneratedBy = "manual",
            ConstraintsUsed = "v1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var recipeRepository = new InMemoryMealPlanRecipeRepository();
        await recipeRepository.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "RECIPE#rec_current"),
            BuildRecipe("rec_current", "FAM#test-family", "Current Dinner", "dinner"));
        await recipeRepository.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "RECIPE#rec_alt"),
            BuildRecipe("rec_alt", "FAM#test-family", "Alt Dinner", "dinner"));

        var client = CreateAuthenticatedClient([plan], [recipeRepository]);

        var response = await client.GetAsync("/api/meal-plans/2026-03-30/swap-options?day=Monday&mealType=dinner&limit=3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<MealSwapSuggestion>>();
        result.Should().NotBeNull();
        result!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSwapOptions_WhenMissingQueryParams_Returns400()
    {
        var plan = BuildActivePlan("FAM#test-family", "2026-03-30");
        var client = CreateAuthenticatedClient([plan], []);

        var response = await client.GetAsync("/api/meal-plans/2026-03-30/swap-options");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostMealPlan_WithValidPayload_Returns201()
    {
        var recipeRepository = new InMemoryMealPlanRecipeRepository();
        await recipeRepository.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "RECIPE#rec_1"),
            BuildRecipe("rec_1", "FAM#test-family", "Dinner Recipe", "dinner"));

        var client = CreateAuthenticatedClient([], [recipeRepository]);

        var request = new CreateMealPlanRequest
        {
            WeekStartDate = "2026-03-30",
            Meals =
            [
                new CreateMealSlotRequest { Day = "Monday", MealType = "dinner", RecipeId = "rec_1" }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/meal-plans", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<MealPlanDocument>();
        result.Should().NotBeNull();
        result!.WeekStartDate.Should().Be("2026-03-30");
        result.Meals.Should().HaveCount(1);
    }

    [Fact]
    public async Task PostMealPlan_WithInvalidWeekDate_Returns400()
    {
        var client = CreateAuthenticatedClient([], []);

        var request = new CreateMealPlanRequest
        {
            WeekStartDate = "2026-03-31", // Tuesday, not Monday
            Meals = []
        };

        var response = await client.PostAsJsonAsync("/api/meal-plans", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostMealPlan_WhenPlanAlreadyExists_Returns409()
    {
        var existing = BuildActivePlan("FAM#test-family", "2026-03-30");
        var client = CreateAuthenticatedClient([existing], []);

        var request = new CreateMealPlanRequest
        {
            WeekStartDate = "2026-03-30",
            Meals = []
        };

        var response = await client.PostAsJsonAsync("/api/meal-plans", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PutMealPlan_UpdatesMeals()
    {
        var plan = BuildActivePlan("FAM#test-family", "2026-03-30");
        var recipeRepository = new InMemoryMealPlanRecipeRepository();
        await recipeRepository.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "RECIPE#rec_new"),
            BuildRecipe("rec_new", "FAM#test-family", "New Dinner", "dinner"));

        var client = CreateAuthenticatedClient([plan], [recipeRepository]);

        var request = new UpdateMealPlanRequest
        {
            Meals = [new CreateMealSlotRequest { Day = "Monday", MealType = "dinner", RecipeId = "rec_new" }]
        };

        var response = await client.PutAsJsonAsync("/api/meal-plans/2026-03-30", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MealPlanDocument>();
        result!.Meals.Should().ContainSingle(m => m.RecipeId == "rec_new");
    }

    [Fact]
    public async Task PutMealPlan_WhenPlanNotFound_Returns404()
    {
        var client = CreateAuthenticatedClient([], []);

        var response = await client.PutAsJsonAsync("/api/meal-plans/2026-03-30", new UpdateMealPlanRequest());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteMealPlan_WhenExists_Returns204()
    {
        var plan = BuildActivePlan("FAM#test-family", "2026-03-30");
        var client = CreateAuthenticatedClient([plan], []);

        var response = await client.DeleteAsync("/api/meal-plans/2026-03-30");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteMealPlan_WhenNotFound_Returns404()
    {
        var client = CreateAuthenticatedClient([], []);

        var response = await client.DeleteAsync("/api/meal-plans/2026-03-30");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCurrentMealPlan_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/meal-plans/current");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HttpClient CreateAuthenticatedClient(
        IEnumerable<MealPlanDocument> seedPlans,
        IEnumerable<InMemoryMealPlanRecipeRepository> recipeRepositories)
    {
        var planRepository = new InMemoryMealPlanRepository();
        foreach (var plan in seedPlans)
        {
            planRepository.PutAsync(
                new DynamoDbKey($"FAMILY#{plan.FamilyId}", $"PLAN#{plan.WeekStartDate}"),
                plan).GetAwaiter().GetResult();
        }

        var recipeRepository = recipeRepositories.FirstOrDefault() ?? new InMemoryMealPlanRecipeRepository();
        var favoriteRepository = new InMemoryMealPlanFavoriteRepository();
        var groceryRepository = new InMemoryMealPlanGroceryRepository();
        var pantryRepository = new InMemoryMealPlanPantryRepository();

        var constraintEngine = new ConstraintEngine(Options.Create(new ConstraintConfig
        {
            NoCookDays = ["Wednesday"],
            MaxWeekdayPrepMinutes = 45,
            MaxWeekendPrepMinutes = 180
        }));

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });

                services.AddSingleton<IDynamoDbRepository<MealPlanDocument>>(planRepository);
                services.AddSingleton<IDynamoDbRepository<RecipeDocument>>(recipeRepository);
                services.AddSingleton<IDynamoDbRepository<FavoriteRecipeDocument>>(favoriteRepository);
                services.AddSingleton<IDynamoDbRepository<GroceryListDocument>>(groceryRepository);
                services.AddSingleton<IDynamoDbRepository<PantryStaplesDocument>>(pantryRepository);
                services.AddScoped<IRecipeService>(_ => new RecipeService(recipeRepository, favoriteRepository));
                services.AddSingleton<IConstraintEngine>(constraintEngine);
                services.AddSingleton<IMealPlanAiService, NoOpMealPlanAiService>();
                services.AddScoped<IGroceryListService, GroceryListService>();
                services.AddScoped<IMealPlanService>(sp => new MealPlanService(
                    planRepository,
                    favoriteRepository,
                    sp.GetRequiredService<IRecipeService>(),
                    constraintEngine,
                    sp.GetRequiredService<IMealPlanAiService>()));
                services.AddScoped<IValidator<CreateMealPlanRequest>, CreateMealPlanRequestValidator>();
                services.AddScoped<IValidator<UpdateMealPlanRequest>, UpdateMealPlanRequestValidator>();
                services.AddScoped<IValidator<GenerateMealPlanRequest>, GenerateMealPlanRequestValidator>();
                services.AddScoped<IValidator<GenerateGroceryListRequest>, GenerateGroceryListRequestValidator>();
                services.AddScoped<IValidator<ToggleGroceryItemRequest>, ToggleGroceryItemRequestValidator>();
                services.AddScoped<IValidator<AddGroceryItemRequest>, AddGroceryItemRequestValidator>();
                services.AddScoped<IValidator<SetInStockRequest>, SetInStockRequestValidator>();
                services.AddScoped<IValidator<RemoveGroceryItemRequest>, RemoveGroceryItemRequestValidator>();
                services.AddScoped<IValidator<ReplacePantryStaplesRequest>, ReplacePantryStaplesRequestValidator>();
                services.AddScoped<IValidator<AddPantryStapleItemRequest>, AddPantryStapleItemRequestValidator>();
            });
        }).CreateClient();
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

    private static RecipeDocument BuildRecipe(string recipeId, string familyId, string name, string category) =>
        new()
        {
            RecipeId = recipeId,
            FamilyId = familyId,
            Name = name,
            Category = category,
            Ingredients = [new RecipeIngredientModel { Name = "Ingredient" }],
            Instructions = ["Step 1"],
            CreatedByUserId = "test-user-123",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private sealed class InMemoryMealPlanRepository : IDynamoDbRepository<MealPlanDocument>
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

    private sealed class InMemoryMealPlanGroceryRepository : IDynamoDbRepository<GroceryListDocument>
    {
        private readonly Dictionary<string, GroceryListDocument> _store = new(StringComparer.Ordinal);

        public Task<GroceryListDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(ToCompositeKey(key), out var value);
            return Task.FromResult(value);
        }

        public Task PutAsync(DynamoDbKey key, GroceryListDocument document, CancellationToken cancellationToken = default)
        {
            _store[ToCompositeKey(key)] = document;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.Remove(ToCompositeKey(key));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<GroceryListDocument>> QueryByPartitionKeyAsync(string partitionKey, int? limit = null, CancellationToken cancellationToken = default)
        {
            var results = _store
                .Where(kvp => kvp.Key.StartsWith($"{partitionKey}|", StringComparison.Ordinal))
                .Select(kvp => kvp.Value);

            if (limit.HasValue)
            {
                results = results.Take(limit.Value);
            }

            return Task.FromResult<IReadOnlyList<GroceryListDocument>>(results.ToList());
        }

        public Task<IReadOnlyList<GroceryListDocument>> QueryByIndexPartitionKeyAsync(
            string indexName,
            string partitionKeyName,
            string partitionKeyValue,
            IReadOnlyDictionary<string, string>? equalsFilters = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<GroceryListDocument>>([]);
        }
    }

    private sealed class InMemoryMealPlanPantryRepository : IDynamoDbRepository<PantryStaplesDocument>
    {
        private readonly Dictionary<string, PantryStaplesDocument> _store = new(StringComparer.Ordinal);

        public Task<PantryStaplesDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(ToCompositeKey(key), out var value);
            return Task.FromResult(value);
        }

        public Task PutAsync(DynamoDbKey key, PantryStaplesDocument document, CancellationToken cancellationToken = default)
        {
            _store[ToCompositeKey(key)] = document;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.Remove(ToCompositeKey(key));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PantryStaplesDocument>> QueryByPartitionKeyAsync(string partitionKey, int? limit = null, CancellationToken cancellationToken = default)
        {
            var results = _store
                .Where(kvp => kvp.Key.StartsWith($"{partitionKey}|", StringComparison.Ordinal))
                .Select(kvp => kvp.Value);

            if (limit.HasValue)
            {
                results = results.Take(limit.Value);
            }

            return Task.FromResult<IReadOnlyList<PantryStaplesDocument>>(results.ToList());
        }

        public Task<IReadOnlyList<PantryStaplesDocument>> QueryByIndexPartitionKeyAsync(
            string indexName,
            string partitionKeyName,
            string partitionKeyValue,
            IReadOnlyDictionary<string, string>? equalsFilters = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PantryStaplesDocument>>([]);
        }
    }

    private static string ToCompositeKey(DynamoDbKey key) => $"{key.PartitionKey}|{key.SortKey}";

    private sealed class InMemoryMealPlanRecipeRepository : IDynamoDbRepository<RecipeDocument>
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
                .ToList();

            return Task.FromResult<IReadOnlyList<RecipeDocument>>(items);
        }

        private static string ToMapKey(DynamoDbKey key) => $"{key.PartitionKey}|{key.SortKey}";
    }

    private sealed class InMemoryMealPlanFavoriteRepository : IDynamoDbRepository<FavoriteRecipeDocument>
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
}
