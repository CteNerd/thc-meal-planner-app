using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using System.Net;
using System.Net.Http.Json;
using ThcMealPlanner.Api.GroceryLists;
using ThcMealPlanner.Api.MealPlans;
using ThcMealPlanner.Api.Recipes;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Tests;

public sealed class GroceryListEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GroceryListEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCurrent_WhenMissingList_Returns404()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/grocery-lists/current");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostGenerate_WhenPlanExists_Returns201WithItems()
    {
        var mealPlanRepo = new InMemoryRepository<MealPlanDocument>();
        var recipeRepo = new InMemoryRepository<RecipeDocument>();

        await mealPlanRepo.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "PLAN#2026-04-06"),
            new MealPlanDocument
            {
                FamilyId = "FAM#test-family",
                WeekStartDate = "2026-04-06",
                Status = "active",
                Meals =
                [
                    new MealSlotDocument
                    {
                        Day = "Monday",
                        MealType = "dinner",
                        RecipeId = "rec_1",
                        RecipeName = "Stir Fry"
                    }
                ],
                GeneratedBy = "manual",
                ConstraintsUsed = "v1",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        await recipeRepo.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "RECIPE#rec_1"),
            new RecipeDocument
            {
                RecipeId = "rec_1",
                FamilyId = "FAM#test-family",
                Name = "Stir Fry",
                Category = "dinner",
                Ingredients =
                [
                    new RecipeIngredientModel { Name = "Tofu", Quantity = "1", Unit = "block", Section = "protein" },
                    new RecipeIngredientModel { Name = "Soy Sauce", Quantity = "2", Unit = "tbsp", Section = "pantry" }
                ],
                Instructions = ["Cook"],
                CreatedByUserId = "test-user-123",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var client = CreateAuthenticatedClient(mealPlanRepo: mealPlanRepo, recipeRepo: recipeRepo);

        var response = await client.PostAsJsonAsync("/api/grocery-lists/generate", new GenerateGroceryListRequest
        {
            WeekStartDate = "2026-04-06",
            ClearExisting = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var list = await response.Content.ReadFromJsonAsync<GroceryListDocument>();
        list.Should().NotBeNull();
        list!.Items.Should().HaveCount(2);
        list.Version.Should().Be(1);
    }

    [Fact]
    public async Task PutToggle_WhenVersionMismatch_Returns409()
    {
        var groceryRepo = new InMemoryRepository<GroceryListDocument>();
        await groceryRepo.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "LIST#ACTIVE"),
            BuildList(version: 2));

        var client = CreateAuthenticatedClient(groceryRepo: groceryRepo);

        var response = await client.PutAsJsonAsync("/api/grocery-lists/items/item_1/toggle", new ToggleGroceryItemRequest
        {
            Version = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PutToggle_WhenValid_UpdatesItemAndVersion()
    {
        var groceryRepo = new InMemoryRepository<GroceryListDocument>();
        await groceryRepo.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "LIST#ACTIVE"),
            BuildList(version: 1));

        var client = CreateAuthenticatedClient(groceryRepo: groceryRepo);

        var response = await client.PutAsJsonAsync("/api/grocery-lists/items/item_1/toggle", new ToggleGroceryItemRequest
        {
            Version = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GroceryItemMutationResponse>();
        body.Should().NotBeNull();
        body!.Item.CheckedOff.Should().BeTrue();
        body.Version.Should().Be(2);
    }

    [Fact]
    public async Task PostItem_WhenValid_AddsManualItem()
    {
        var groceryRepo = new InMemoryRepository<GroceryListDocument>();
        await groceryRepo.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "LIST#ACTIVE"),
            BuildList(version: 1));

        var client = CreateAuthenticatedClient(groceryRepo: groceryRepo);

        var response = await client.PostAsJsonAsync("/api/grocery-lists/items", new AddGroceryItemRequest
        {
            Name = "Paper towels",
            Section = "household",
            Quantity = 1,
            Unit = "pack",
            Version = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<GroceryItemMutationResponse>();
        body.Should().NotBeNull();
        body!.Item.Name.Should().Be("Paper towels");
        body.Version.Should().Be(2);
    }

    [Fact]
    public async Task DeleteItem_WhenValid_Returns204()
    {
        var groceryRepo = new InMemoryRepository<GroceryListDocument>();
        await groceryRepo.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "LIST#ACTIVE"),
            BuildList(version: 2));

        var client = CreateAuthenticatedClient(groceryRepo: groceryRepo);

        var response = await client.DeleteAsync("/api/grocery-lists/items/item_1?version=2");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetPoll_WhenNoChanges_Returns304()
    {
        var now = DateTimeOffset.UtcNow;
        var groceryRepo = new InMemoryRepository<GroceryListDocument>();
        await groceryRepo.PutAsync(
            new DynamoDbKey("FAMILY#FAM#test-family", "LIST#ACTIVE"),
            BuildList(version: 1, updatedAt: now));

        var client = CreateAuthenticatedClient(groceryRepo: groceryRepo);

        var response = await client.GetAsync($"/api/grocery-lists/poll?since={Uri.EscapeDataString(now.ToString("O"))}");

        response.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task PantryEndpoints_GetAndPut_WorkAsExpected()
    {
        var pantryRepo = new InMemoryRepository<PantryStaplesDocument>();
        var client = CreateAuthenticatedClient(pantryRepo: pantryRepo);

        var putResponse = await client.PutAsJsonAsync("/api/pantry/staples", new ReplacePantryStaplesRequest
        {
            Items =
            [
                new PantryStapleItemDocument { Name = "Salt", Section = "spices" },
                new PantryStapleItemDocument { Name = "Olive Oil", Section = "pantry" }
            ],
            PreferredSectionOrder = ["produce", "pantry", "protein"]
        });

        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<PantryStaplesDocument>();
        updated.Should().NotBeNull();
        updated!.Items.Should().HaveCount(2);
        updated.PreferredSectionOrder.Should().ContainInOrder("produce", "pantry", "protein");

        var getResponse = await client.GetAsync("/api/pantry/staples");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var current = await getResponse.Content.ReadFromJsonAsync<PantryStaplesDocument>();
        current!.Items.Should().Contain(i => i.Name == "Salt");
    }

    private HttpClient CreateAuthenticatedClient(
        InMemoryRepository<GroceryListDocument>? groceryRepo = null,
        InMemoryRepository<PantryStaplesDocument>? pantryRepo = null,
        InMemoryRepository<MealPlanDocument>? mealPlanRepo = null,
        InMemoryRepository<RecipeDocument>? recipeRepo = null)
    {
        groceryRepo ??= new InMemoryRepository<GroceryListDocument>();
        pantryRepo ??= new InMemoryRepository<PantryStaplesDocument>();
        mealPlanRepo ??= new InMemoryRepository<MealPlanDocument>();
        recipeRepo ??= new InMemoryRepository<RecipeDocument>();

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });

                services.AddSingleton<IDynamoDbRepository<GroceryListDocument>>(groceryRepo);
                services.AddSingleton<IDynamoDbRepository<PantryStaplesDocument>>(pantryRepo);
                services.AddSingleton<IDynamoDbRepository<MealPlanDocument>>(mealPlanRepo);
                services.AddSingleton<IDynamoDbRepository<RecipeDocument>>(recipeRepo);

                services.AddScoped<IGroceryListService, GroceryListService>();
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

    private static GroceryListDocument BuildList(int version, DateTimeOffset? updatedAt = null)
    {
        var now = updatedAt ?? DateTimeOffset.UtcNow;
        return new GroceryListDocument
        {
            FamilyId = "FAM#test-family",
            ListId = GroceryListConstants.ActiveListId,
            Version = version,
            CreatedAt = now,
            UpdatedAt = now,
            Items =
            [
                new GroceryItemDocument
                {
                    Id = "item_1",
                    Name = "Tofu",
                    Section = "protein",
                    Quantity = 1,
                    Unit = "block",
                    CheckedOff = false,
                    InStock = false,
                    MealAssociations = []
                }
            ],
            Progress = new GroceryProgressDocument
            {
                Total = 1,
                Completed = 0,
                Percentage = 0
            }
        };
    }

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
                .Where(x => x.Key.StartsWith($"{partitionKey}|", StringComparison.Ordinal))
                .Select(x => x.Value);

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
