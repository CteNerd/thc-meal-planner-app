using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using ThcMealPlanner.Api.Chat;
using ThcMealPlanner.Api.GroceryLists;
using ThcMealPlanner.Api.MealPlans;
using ThcMealPlanner.Api.Profiles;
using ThcMealPlanner.Api.Recipes;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Tests;

public sealed class ChatEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ChatEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostMessage_WithValidPayload_Returns200()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/chat/message", new ChatMessageRequest
        {
            Message = "Please help me plan dinners for this week"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        payload.Should().NotBeNull();
        payload!.ConversationId.Should().NotBeNullOrWhiteSpace();
        payload.AssistantMessage.Role.Should().Be(ChatConstants.AssistantRole);
        payload.AssistantMessage.Content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PostMessage_WithEmptyMessage_Returns400()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/chat/message", new ChatMessageRequest
        {
            Message = string.Empty
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistory_WhenConversationExists_ReturnsMessages()
    {
        var client = CreateAuthenticatedClient();

        var sendResponse = await client.PostAsJsonAsync("/api/chat/message", new ChatMessageRequest
        {
            Message = "Suggest a quick lunch",
            ConversationId = "conv_test"
        });

        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var historyResponse = await client.GetAsync("/api/chat/history?conversationId=conv_test&limit=20");

        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await historyResponse.Content.ReadFromJsonAsync<ChatHistoryResponse>();
        payload.Should().NotBeNull();
        payload!.Messages.Should().HaveCountGreaterOrEqualTo(2);
        payload.Messages.Should().Contain(m => m.Role == ChatConstants.UserRole);
        payload.Messages.Should().Contain(m => m.Role == ChatConstants.AssistantRole);
    }

    [Fact]
    public async Task PostMessage_WhenConfirmingPendingDestructiveAction_ReturnsConfirmationOutcome()
    {
        var client = CreateAuthenticatedClient();

        var destructiveResponse = await client.PostAsJsonAsync("/api/chat/message", new ChatMessageRequest
        {
            Message = "Clear completed grocery items",
            ConversationId = "conv_confirm"
        });

        destructiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var confirmResponse = await client.PostAsJsonAsync("/api/chat/message", new ChatMessageRequest
        {
            Message = "Confirm",
            ConversationId = "conv_confirm"
        });

        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await confirmResponse.Content.ReadFromJsonAsync<ChatMessageResponse>();
        payload.Should().NotBeNull();
        payload!.AssistantMessage.Content.Should().Contain("No completed grocery items needed clearing");
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var chatRepository = new InMemoryRepository<ChatHistoryMessageDocument>();

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });

                services.AddSingleton<IDynamoDbRepository<ChatHistoryMessageDocument>>(chatRepository);
                services.AddSingleton<IDynamoDbRepository<UserProfileDocument>, InMemoryRepository<UserProfileDocument>>();
                services.AddScoped<IValidator<ChatMessageRequest>, ChatMessageRequestValidator>();
                services.AddSingleton<IOpenAiApiKeyProvider, NoOpOpenAiApiKeyProvider>();
                services.AddSingleton<IMealPlanService, NoOpMealPlanService>();
                services.AddSingleton<IRecipeService, NoOpRecipeService>();
                services.AddSingleton<IGroceryListService, NoOpGroceryListService>();
                services.AddSingleton<IDependentProfileService, NoOpDependentProfileService>();
                services.AddScoped<IChatService>(sp => new ChatService(
                    chatRepository,
                    sp.GetRequiredService<IOpenAiApiKeyProvider>(),
                    Options.Create(new OpenAiOptions()),
                    sp.GetRequiredService<IMealPlanService>(),
                    sp.GetRequiredService<IRecipeService>(),
                    sp.GetRequiredService<IGroceryListService>(),
                    sp.GetRequiredService<IDynamoDbRepository<UserProfileDocument>>(),
                    sp.GetRequiredService<IDependentProfileService>(),
                    new HttpClient(),
                    sp.GetRequiredService<ILogger<ChatService>>()));
            });
        }).CreateClient();
    }

    private sealed class NoOpOpenAiApiKeyProvider : IOpenAiApiKeyProvider
    {
        public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
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
                .Select(x => x.Value)
                .ToList();

            if (limit.HasValue)
            {
                results = results.Take(limit.Value).ToList();
            }

            return Task.FromResult<IReadOnlyList<TDocument>>(results);
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

    private sealed class NoOpMealPlanService : IMealPlanService
    {
        public Task<MealPlanDocument?> GetCurrentAsync(string familyId, CancellationToken cancellationToken = default) => Task.FromResult<MealPlanDocument?>(null);

        public Task<MealPlanDocument?> GetByWeekAsync(string familyId, string weekStartDate, CancellationToken cancellationToken = default) => Task.FromResult<MealPlanDocument?>(null);

        public Task<IReadOnlyList<MealPlanDocument>> GetHistoryAsync(string familyId, int limit = 10, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MealPlanDocument>>([]);

        public Task<MealPlanDocument> CreateAsync(string familyId, string userId, CreateMealPlanRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new MealPlanDocument());

        public Task<MealPlanDocument> GenerateAsync(string familyId, string userId, GenerateMealPlanRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new MealPlanDocument());

        public Task<IReadOnlyList<MealSwapSuggestion>> SuggestSwapOptionsAsync(string familyId, string weekStartDate, string day, string mealType, int limit = 5, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MealSwapSuggestion>>([]);

        public Task<MealPlanDocument?> UpdateAsync(string familyId, string weekStartDate, UpdateMealPlanRequest request, CancellationToken cancellationToken = default) => Task.FromResult<MealPlanDocument?>(null);

        public Task<bool> DeleteAsync(string familyId, string weekStartDate, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class NoOpRecipeService : IRecipeService
    {
        public Task<IReadOnlyList<RecipeDocument>> ListByFamilyAsync(string familyId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RecipeDocument>>([]);

        public Task<RecipeDocument?> GetByIdAsync(string familyId, string recipeId, CancellationToken cancellationToken = default) => Task.FromResult<RecipeDocument?>(null);

        public Task<RecipeDocument> CreateAsync(string familyId, string userId, CreateRecipeRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new RecipeDocument());

        public Task<RecipeDocument?> UpdateAsync(string familyId, string recipeId, UpdateRecipeRequest request, CancellationToken cancellationToken = default) => Task.FromResult<RecipeDocument?>(null);

        public Task<bool> DeleteAsync(string familyId, string recipeId, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<FavoriteRecipeDocument?> AddFavoriteAsync(string familyId, string userId, string recipeId, FavoriteRecipeRequest request, CancellationToken cancellationToken = default) => Task.FromResult<FavoriteRecipeDocument?>(null);

        public Task RemoveFavoriteAsync(string userId, string recipeId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<FavoriteRecipeDocument>> ListFavoritesAsync(string userId, string? category, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FavoriteRecipeDocument>>([]);
    }

    private sealed class NoOpGroceryListService : IGroceryListService
    {
        public Task<GroceryListDocument?> GetCurrentAsync(string familyId, CancellationToken cancellationToken = default) => Task.FromResult<GroceryListDocument?>(null);

        public Task<GroceryListDocument> GenerateAsync(string familyId, string userId, string? userName, GenerateGroceryListRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new GroceryListDocument());

        public Task<GroceryItemMutationResult> ToggleItemAsync(string familyId, string itemId, string userId, string? userName, ToggleGroceryItemRequest request, CancellationToken cancellationToken = default) => Task.FromResult(GroceryItemMutationResult.NotFoundItem);

        public Task<GroceryItemMutationResult> AddItemAsync(string familyId, AddGroceryItemRequest request, CancellationToken cancellationToken = default) => Task.FromResult(GroceryItemMutationResult.NotFoundList);

        public Task<GroceryItemMutationResult> SetInStockAsync(string familyId, string itemId, SetInStockRequest request, CancellationToken cancellationToken = default) => Task.FromResult(GroceryItemMutationResult.NotFoundItem);

        public Task<GroceryItemMutationResult> RemoveItemAsync(string familyId, string itemId, RemoveGroceryItemRequest request, CancellationToken cancellationToken = default) => Task.FromResult(GroceryItemMutationResult.NotFoundItem);

        public Task<GroceryListPollResult> PollAsync(string familyId, DateTimeOffset? since, CancellationToken cancellationToken = default) => Task.FromResult(GroceryListPollResult.NotFound);

        public Task<PantryStaplesDocument> GetPantryStaplesAsync(string familyId, CancellationToken cancellationToken = default) => Task.FromResult(new PantryStaplesDocument { FamilyId = familyId });

        public Task<PantryStaplesDocument> ReplacePantryStaplesAsync(string familyId, ReplacePantryStaplesRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new PantryStaplesDocument { FamilyId = familyId, Items = request.Items });

        public Task<PantryStaplesDocument> AddPantryStapleAsync(string familyId, AddPantryStapleItemRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new PantryStaplesDocument { FamilyId = familyId });

        public Task<bool> DeletePantryStapleAsync(string familyId, string name, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class NoOpDependentProfileService : IDependentProfileService
    {
        public Task<IReadOnlyList<DependentProfileDocument>> ListByFamilyAsync(string familyId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DependentProfileDocument>>([]);

        public Task<DependentProfileDocument> CreateAsync(string familyId, CreateDependentRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new DependentProfileDocument());

        public Task<DependentProfileDocument?> UpdateAsync(string familyId, string userId, UpdateDependentRequest request, CancellationToken cancellationToken = default) => Task.FromResult<DependentProfileDocument?>(null);

        public Task<bool> DeleteAsync(string familyId, string userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
