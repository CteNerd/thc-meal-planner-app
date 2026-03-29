using FluentValidation;
using ThcMealPlanner.Api.Authentication;

namespace ThcMealPlanner.Api.Notifications;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/notifications/test", SendTestNotificationAsync);

        return group;
    }

    private static async Task<IResult> SendTestNotificationAsync(
        HttpContext httpContext,
        SendTestNotificationRequest request,
        IValidator<SendTestNotificationRequest> validator,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return Results.Unauthorized();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var recipientEmail = string.IsNullOrWhiteSpace(request.RecipientEmail)
            ? userContext.Email
            : request.RecipientEmail;

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["recipientEmail"] = ["Recipient email is required."]
            });
        }

        await notificationService.SendTestNotificationAsync(
            userContext.FamilyId,
            userContext.Name,
            recipientEmail,
            request,
            cancellationToken);

        return Results.Accepted();
    }
}
