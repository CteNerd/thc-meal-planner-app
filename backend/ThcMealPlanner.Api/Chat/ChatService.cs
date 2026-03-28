using System.Net.Http.Headers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ThcMealPlanner.Api.MealPlans;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Api.Chat;

public interface IChatService
{
    Task<ChatMessageResponse> SendMessageAsync(
        string familyId,
        string userId,
        string userName,
        ChatMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(
        string userId,
        string? conversationId,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class ChatService : IChatService
{
    private static readonly HashSet<string> DomainKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "meal", "meals", "plan", "recipe", "recipes", "cookbook", "grocery", "groceries", "pantry", "dinner", "lunch", "breakfast", "allergy", "nutrition"
    };

    private readonly IDynamoDbRepository<ChatHistoryMessageDocument> _chatHistoryRepository;
    private readonly IOpenAiApiKeyProvider _apiKeyProvider;
    private readonly OpenAiOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IDynamoDbRepository<ChatHistoryMessageDocument> chatHistoryRepository,
        IOpenAiApiKeyProvider apiKeyProvider,
        Microsoft.Extensions.Options.IOptions<OpenAiOptions> options,
        HttpClient httpClient,
        ILogger<ChatService> logger)
    {
        _chatHistoryRepository = chatHistoryRepository;
        _apiKeyProvider = apiKeyProvider;
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ChatMessageResponse> SendMessageAsync(
        string familyId,
        string userId,
        string userName,
        ChatMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? $"conv_{Guid.NewGuid():N}"
            : request.ConversationId!.Trim();

        var userMessage = request.Message.Trim();
        var requiresConfirmation = IsDestructiveIntent(userMessage);

        var assistantContent = await BuildAssistantContentAsync(userName, userMessage, requiresConfirmation, cancellationToken);
        var assistantRole = ChatConstants.AssistantRole;

        await PersistMessageAsync(
            familyId,
            userId,
            conversationId,
            ChatConstants.UserRole,
            userMessage,
            now,
            pendingConfirmation: null,
            cancellationToken);

        var pendingConfirmation = requiresConfirmation
            ? new PendingConfirmationDocument
            {
                ActionType = "destructive_action",
                Prompt = "Please confirm this destructive action."
            }
            : null;

        var assistantTimestamp = DateTimeOffset.UtcNow;
        await PersistMessageAsync(
            familyId,
            userId,
            conversationId,
            assistantRole,
            assistantContent,
            assistantTimestamp,
            pendingConfirmation,
            cancellationToken);

        return new ChatMessageResponse
        {
            ConversationId = conversationId,
            AssistantMessage = new ChatMessage
            {
                Role = assistantRole,
                Content = assistantContent,
                Timestamp = assistantTimestamp,
                RequiresConfirmation = requiresConfirmation,
                PendingActionType = pendingConfirmation?.ActionType
            }
        };
    }

    public async Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(
        string userId,
        string? conversationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var partitionKey = $"USER#{userId}";
        var records = await _chatHistoryRepository.QueryByPartitionKeyAsync(partitionKey, cancellationToken: cancellationToken);

        var filtered = records
            .Where(record => string.IsNullOrWhiteSpace(conversationId) || string.Equals(record.ConversationId, conversationId, StringComparison.Ordinal))
            .OrderBy(record => record.CreatedAt)
            .ToList();

        if (filtered.Count > limit)
        {
            filtered = filtered.TakeLast(limit).ToList();
        }

        return filtered.Select(record => new ChatMessage
        {
            Role = record.Role,
            Content = record.Content,
            Timestamp = record.CreatedAt,
            RequiresConfirmation = record.PendingConfirmation is not null,
            PendingActionType = record.PendingConfirmation?.ActionType
        }).ToList();
    }

    private async Task<string> BuildAssistantContentAsync(
        string userName,
        string userMessage,
        bool requiresConfirmation,
        CancellationToken cancellationToken)
    {
        if (requiresConfirmation)
        {
            return "This looks like a destructive action. Reply with **Confirm** to proceed, or tell me what you want to change.";
        }

        if (!IsInDomain(userMessage))
        {
            return "I can help with meal planning, recipes, grocery lists, pantry staples, and nutrition. Tell me what you want to plan or change.";
        }

        var openAiReply = await TryGetOpenAiReplyAsync(userName, userMessage, cancellationToken);
        if (!string.IsNullOrWhiteSpace(openAiReply))
        {
            return openAiReply.Trim();
        }

        return "I can help with that. Try asking for a weekly meal plan, grocery list updates, recipe ideas, or pantry management.";
    }

    private async Task<string?> TryGetOpenAiReplyAsync(
        string userName,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var apiKey = await _apiKeyProvider.GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var systemContent = string.Join('\n',
        [
            "You are a family meal planning assistant.",
            "Only help with meal planning, recipes, grocery lists, pantry staples, and nutrition.",
            "Never execute destructive actions without explicit user confirmation.",
            "Respond in concise markdown."
        ]);

        var userContent = $"User: {userName}\\nMessage: {userMessage}";
        var payload = BuildChatPayloadJson(_options.Model, _options.Temperature, systemContent, userContent);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Chat completion request failed with status {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ExtractMessageContent(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat completion request failed. Falling back to deterministic assistant response.");
            return null;
        }
    }

    private async Task PersistMessageAsync(
        string familyId,
        string userId,
        string conversationId,
        string role,
        string content,
        DateTimeOffset createdAt,
        PendingConfirmationDocument? pendingConfirmation,
        CancellationToken cancellationToken)
    {
        var key = new DynamoDbKey(
            $"USER#{userId}",
            $"MSG#{createdAt.UtcDateTime:O}#{Guid.NewGuid():N}");

        var doc = new ChatHistoryMessageDocument
        {
            FamilyId = familyId,
            UserId = userId,
            ConversationId = conversationId,
            Role = role,
            Content = content,
            CreatedAt = createdAt,
            TTL = createdAt.AddDays(30).ToUnixTimeSeconds(),
            PendingConfirmation = pendingConfirmation
        };

        await _chatHistoryRepository.PutAsync(key, doc, cancellationToken);
    }

    private static string? ExtractMessageContent(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var first = choices[0];
            if (!first.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return content.GetString();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsInDomain(string message)
    {
        return DomainKeywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDestructiveIntent(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.ToLowerInvariant();
        var hasAction = normalized.Contains("delete", StringComparison.Ordinal)
            || normalized.Contains("clear", StringComparison.Ordinal)
            || normalized.Contains("remove", StringComparison.Ordinal);

        if (!hasAction)
        {
            return false;
        }

        return normalized.Contains("meal plan", StringComparison.Ordinal)
            || normalized.Contains("grocery", StringComparison.Ordinal)
            || normalized.Contains("conversation", StringComparison.Ordinal)
            || normalized.Contains("history", StringComparison.Ordinal)
            || normalized.Contains("recipe", StringComparison.Ordinal);
    }

        private static string BuildChatPayloadJson(
                string model,
                double temperature,
                string systemContent,
                string userContent)
        {
                return string.Create(CultureInfo.InvariantCulture, $$"""
                {
                    "model":"{{EscapeJson(model)}}",
                    "temperature":{{temperature}},
                    "messages":[
                        {
                            "role":"system",
                            "content":"{{EscapeJson(systemContent)}}"
                        },
                        {
                            "role":"user",
                            "content":"{{EscapeJson(userContent)}}"
                        }
                    ]
                }
                """);
        }

        private static string EscapeJson(string value)
        {
                return JavaScriptEncoder.Default.Encode(value);
        }
}
