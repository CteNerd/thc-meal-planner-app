using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using ThcMealPlanner.Api.Chat;
using ThcMealPlanner.Api.MealPlans;
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
                services.AddScoped<IValidator<ChatMessageRequest>, ChatMessageRequestValidator>();
                services.AddSingleton<IOpenAiApiKeyProvider, NoOpOpenAiApiKeyProvider>();
                services.AddScoped<IChatService>(sp => new ChatService(
                    chatRepository,
                    sp.GetRequiredService<IOpenAiApiKeyProvider>(),
                    Options.Create(new OpenAiOptions()),
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
}
