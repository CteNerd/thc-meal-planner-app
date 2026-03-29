namespace ThcMealPlanner.Api.Notifications;

public static class NotificationTypes
{
    public const string MealPlanReady = "meal-plan-ready";
    public const string SecurityAlert = "security-alert";

    public static readonly HashSet<string> All =
    [
        MealPlanReady,
        SecurityAlert
    ];
}

public sealed class SendTestNotificationRequest
{
    public string Type { get; init; } = string.Empty;

    public string? RecipientEmail { get; init; }

    public string? WeekStartDate { get; init; }

    public string? SecurityMessage { get; init; }
}

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public string FromEmail { get; init; } = string.Empty;
}
