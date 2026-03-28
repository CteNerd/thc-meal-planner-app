namespace ThcMealPlanner.Api.Chat;

public static class ChatConstants
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";
}

public sealed class ChatHistoryMessageDocument
{
    public string FamilyId { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public string ConversationId { get; init; } = string.Empty;

    public string Role { get; init; } = ChatConstants.UserRole;

    public string Content { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public long TTL { get; init; }

    public List<ChatActionDocument> Actions { get; init; } = [];

    public PendingConfirmationDocument? PendingConfirmation { get; init; }
}

public sealed class ChatActionDocument
{
    public string Type { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Result { get; init; }
}

public sealed class PendingConfirmationDocument
{
    public string ActionType { get; init; } = string.Empty;

    public string Prompt { get; init; } = string.Empty;
}

public sealed class ChatMessageRequest
{
    public string Message { get; init; } = string.Empty;

    public string? ConversationId { get; init; }
}

public sealed class ChatMessage
{
    public string Role { get; init; } = ChatConstants.AssistantRole;

    public string Content { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; }

    public bool RequiresConfirmation { get; init; }

    public string? PendingActionType { get; init; }
}

public sealed class ChatMessageResponse
{
    public string ConversationId { get; init; } = string.Empty;

    public ChatMessage AssistantMessage { get; init; } = new();
}

public sealed class ChatHistoryResponse
{
    public string? ConversationId { get; init; }

    public List<ChatMessage> Messages { get; init; } = [];
}
