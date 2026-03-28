using System.Text.RegularExpressions;
using FluentValidation;
using ThcMealPlanner.Api.Authentication;

namespace ThcMealPlanner.Api.Chat;

public static partial class ChatEndpoints
{
    public static RouteGroupBuilder MapChatEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/chat/message", PostMessageAsync);
        group.MapGet("/chat/history", GetHistoryAsync);

        return group;
    }

    private static async Task<IResult> PostMessageAsync(
        HttpContext httpContext,
        ChatMessageRequest request,
        IValidator<ChatMessageRequest> validator,
        IChatService chatService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return ChatProblemDetails.MissingRequiredUserClaims();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var sanitizedRequest = new ChatMessageRequest
        {
            Message = SanitizeMessage(request.Message),
            ConversationId = request.ConversationId
        };

        var response = await chatService.SendMessageAsync(
            userContext.FamilyId,
            userContext.Sub,
            userContext.Name,
            sanitizedRequest,
            cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetHistoryAsync(
        HttpContext httpContext,
        string? conversationId,
        int? limit,
        IChatService chatService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return ChatProblemDetails.MissingRequiredUserClaims();
        }

        var boundedLimit = Math.Clamp(limit ?? 50, 1, 200);
        var messages = await chatService.GetHistoryAsync(userContext.Sub, conversationId, boundedLimit, cancellationToken);

        return Results.Ok(new ChatHistoryResponse
        {
            ConversationId = conversationId,
            Messages = messages.ToList()
        });
    }

    private static string SanitizeMessage(string raw)
    {
        var trimmed = raw.Trim();
        var withoutControlChars = UnsafeControlCharacterRegex().Replace(trimmed, string.Empty);
        return withoutControlChars.Length == 0 ? string.Empty : withoutControlChars;
    }

    private static Dictionary<string, string[]> ToDictionary(this FluentValidation.Results.ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                grouping => grouping.Key,
                grouping => grouping.Select(error => error.ErrorMessage).ToArray());
    }

    [GeneratedRegex("[\\u0000-\\u0008\\u000B\\u000C\\u000E-\\u001F]")]
    private static partial Regex UnsafeControlCharacterRegex();
}
