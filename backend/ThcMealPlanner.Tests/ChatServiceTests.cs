using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ThcMealPlanner.Api.Chat;
using ThcMealPlanner.Api.GroceryLists;
using ThcMealPlanner.Api.MealPlans;
using ThcMealPlanner.Api.Profiles;
using ThcMealPlanner.Api.Recipes;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Tests;

public sealed class ChatServiceTests
{
    private const string FamilyId = "FAM#test-family";
    private const string UserId = "adult_1";

    [Fact]
    public async Task SendMessageAsync_WhenOutOfDomain_ReturnsDomainGuidance()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
        var service = CreateService(chatRepo, apiKey: null, httpResponse: null);

        var response = await service.SendMessageAsync(
            FamilyId,
            UserId,
            "Adult 1",
            new ChatMessageRequest { Message = "Tell me a joke" });

        response.AssistantMessage.Content.Should().Contain("I can help with meal planning");
        response.AssistantMessage.RequiresConfirmation.Should().BeFalse();

        var history = await chatRepo.QueryByPartitionKeyAsync($"USER#{UserId}");
        history.Should().HaveCount(2);
    }

    [Fact]
    public async Task SendMessageAsync_WhenDestructiveIntent_RequiresConfirmation()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
        var service = CreateService(chatRepo, apiKey: null, httpResponse: null);

        var response = await service.SendMessageAsync(
            FamilyId,
            UserId,
            "Adult 1",
            new ChatMessageRequest
            {
                ConversationId = "conv_destructive",
                Message = "Please delete this meal plan"
            });

        response.AssistantMessage.RequiresConfirmation.Should().BeTrue();
        response.AssistantMessage.PendingActionType.Should().Be("destructive_action");
        response.AssistantMessage.Content.Should().Contain("Reply with **Confirm**");
    }

    [Fact]
    public async Task SendMessageAsync_WhenConfirmWithoutPending_ReturnsNoPendingActionMessage()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
        var service = CreateService(chatRepo, apiKey: null, httpResponse: null);

        var response = await service.SendMessageAsync(
            FamilyId,
            UserId,
            "Adult 1",
            new ChatMessageRequest
            {
                ConversationId = "conv_none",
                Message = "Confirm"
            });

        response.AssistantMessage.Content.Should().Be("There is no pending action to confirm right now.");
    }

    [Fact]
    public async Task SendMessageAsync_WhenOpenAiReturnsMessage_UsesAssistantContent()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
        var openAiBody = """
            {
              "choices": [
                {
                  "message": {
                    "content": "Try a quick taco bowl with pantry beans tonight."
                  }
                }
              ]
            }
            """;

        var service = CreateService(chatRepo, "sk-test", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(openAiBody, Encoding.UTF8, "application/json")
        });

        var response = await service.SendMessageAsync(
            FamilyId,
            UserId,
            "Adult 1",
            new ChatMessageRequest { Message = "Can you suggest a dinner meal plan?" });

        response.AssistantMessage.Content.Should().Be("Try a quick taco bowl with pantry beans tonight.");
    }

    [Fact]
    public async Task SendMessageAsync_WhenOpenAiReturnsUnsupportedToolCall_ReturnsToolExecutionMessage()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
        var openAiBody = """
            {
              "choices": [
                {
                  "message": {
                    "tool_calls": [
                      {
                        "function": {
                          "name": "unsupported_function",
                          "arguments": "{}"
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """;

        var service = CreateService(chatRepo, "sk-test", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(openAiBody, Encoding.UTF8, "application/json")
        });

        var response = await service.SendMessageAsync(
            FamilyId,
            UserId,
            "Adult 1",
            new ChatMessageRequest { Message = "Please plan meals for this week" });

        response.AssistantMessage.Content.Should().Contain("I cannot execute the requested function");

        var history = await chatRepo.QueryByPartitionKeyAsync($"USER#{UserId}");
        var assistant = history.Single(x => x.Role == ChatConstants.AssistantRole);
        assistant.Actions.Should().ContainSingle();
        assistant.Actions[0].Type.Should().Be("unsupported_function");
        assistant.Actions[0].Status.Should().Be("ignored");
    }

    [Fact]
    public async Task SendMessageAsync_WhenOpenAiStatusNotSuccess_FallsBackToDeterministicMessage()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
        var service = CreateService(chatRepo, "sk-test", new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });

        var response = await service.SendMessageAsync(
            FamilyId,
            UserId,
            "Adult 1",
            new ChatMessageRequest { Message = "Give me a recipe for dinner" });

        response.AssistantMessage.Content.Should().Contain("Try asking for a weekly meal plan");
    }

    [Fact]
    public async Task GetHistoryAsync_FiltersConversationAndAppliesLimit()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();

        await chatRepo.PutAsync(
            new DynamoDbKey($"USER#{UserId}", "MSG#1"),
            new ChatHistoryMessageDocument
            {
                FamilyId = FamilyId,
                UserId = UserId,
                ConversationId = "conv_a",
                Role = ChatConstants.UserRole,
                Content = "one",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-3)
            });

        await chatRepo.PutAsync(
            new DynamoDbKey($"USER#{UserId}", "MSG#2"),
            new ChatHistoryMessageDocument
            {
                FamilyId = FamilyId,
                UserId = UserId,
                ConversationId = "conv_a",
                Role = ChatConstants.AssistantRole,
                Content = "two",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
            });

        await chatRepo.PutAsync(
            new DynamoDbKey($"USER#{UserId}", "MSG#3"),
            new ChatHistoryMessageDocument
            {
                FamilyId = FamilyId,
                UserId = UserId,
                ConversationId = "conv_b",
                Role = ChatConstants.AssistantRole,
                Content = "three",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            });

        var service = CreateService(chatRepo, apiKey: null, httpResponse: null);

        var history = await service.GetHistoryAsync(UserId, "conv_a", limit: 1);

        history.Should().HaveCount(1);
        history[0].Content.Should().Be("two");
    }

    private static ChatService CreateService(
        InMemoryRepository<ChatHistoryMessageDocument> chatRepository,
        string? apiKey,
        HttpResponseMessage? httpResponse)
    {
        var profileRepository = new InMemoryRepository<UserProfileDocument>();
        var apiKeyProvider = new StubApiKeyProvider(apiKey);
        var handler = new StubHttpMessageHandler(httpResponse);

        return new ChatService(
            chatRepository,
            apiKeyProvider,
            Options.Create(new OpenAiOptions()),
            new NoOpMealPlanService(),
            new NoOpRecipeService(),
            new NoOpGroceryListService(),
            profileRepository,
            new NoOpDependentProfileService(),
            new HttpClient(handler),
            NullLogger<ChatService>.Instance);
    }

    private sealed class StubApiKeyProvider : IOpenAiApiKeyProvider
    {
        private readonly string? _apiKey;

        public StubApiKeyProvider(string? apiKey)
        {
            _apiKey = apiKey;
        }

        public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_apiKey);
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;

        public StubHttpMessageHandler(HttpResponseMessage? response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_response is null)
            {
                throw new InvalidOperationException("HTTP response was not configured for this test.");
            }

            request.Headers.Authorization.Should().NotBeNull();
            request.Headers.Authorization!.Scheme.Should().Be("Bearer");

            return Task.FromResult(_response);
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

        public Task<IReadOnlyList<MealSwapSuggestion>> SuggestSwapOptionsAsync(string familyId, string weekStartDate, string day, string mealType, int limit = 5, string? profileContext = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MealSwapSuggestion>>([]);

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
