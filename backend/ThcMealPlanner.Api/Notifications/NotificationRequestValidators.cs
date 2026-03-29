using FluentValidation;

namespace ThcMealPlanner.Api.Notifications;

public sealed class SendTestNotificationRequestValidator : AbstractValidator<SendTestNotificationRequest>
{
    public SendTestNotificationRequestValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(type => NotificationTypes.All.Contains(type))
            .WithMessage("type must be one of: meal-plan-ready, security-alert.");

        RuleFor(x => x.RecipientEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.RecipientEmail));

        RuleFor(x => x.WeekStartDate)
            .Must(BeAValidIsoDate)
            .When(x =>
                string.Equals(x.Type, NotificationTypes.MealPlanReady, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(x.WeekStartDate))
            .WithMessage("weekStartDate must be a valid ISO date (yyyy-MM-dd).");

        RuleFor(x => x.SecurityMessage)
            .MaximumLength(500)
            .When(x =>
                string.Equals(x.Type, NotificationTypes.SecurityAlert, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(x.SecurityMessage));
    }

    private static bool BeAValidIsoDate(string? value)
    {
        return DateOnly.TryParse(value, out _);
    }
}
