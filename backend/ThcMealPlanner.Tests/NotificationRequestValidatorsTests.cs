using FluentAssertions;
using ThcMealPlanner.Api.Notifications;

namespace ThcMealPlanner.Tests;

public sealed class NotificationRequestValidatorsTests
{
    private readonly SendTestNotificationRequestValidator _validator = new();

    [Fact]
    public async Task Validate_WhenTypeInvalid_ReturnsError()
    {
        var result = await _validator.ValidateAsync(new SendTestNotificationRequest
        {
            Type = "unknown"
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Type");
    }

    [Fact]
    public async Task Validate_WhenMealPlanDateInvalid_ReturnsError()
    {
        var result = await _validator.ValidateAsync(new SendTestNotificationRequest
        {
            Type = NotificationTypes.MealPlanReady,
            WeekStartDate = "not-a-date"
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "WeekStartDate");
    }

    [Fact]
    public async Task Validate_WhenRecipientEmailInvalid_ReturnsError()
    {
        var result = await _validator.ValidateAsync(new SendTestNotificationRequest
        {
            Type = NotificationTypes.SecurityAlert,
            RecipientEmail = "not-an-email"
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RecipientEmail");
    }

    [Fact]
    public async Task Validate_WhenSecurityMessageTooLong_ReturnsError()
    {
        var result = await _validator.ValidateAsync(new SendTestNotificationRequest
        {
            Type = NotificationTypes.SecurityAlert,
            SecurityMessage = new string('x', 501)
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SecurityMessage");
    }

    [Fact]
    public async Task Validate_WhenValidMealPlanReadyRequest_Passes()
    {
        var result = await _validator.ValidateAsync(new SendTestNotificationRequest
        {
            Type = NotificationTypes.MealPlanReady,
            RecipientEmail = "adult1@example.com",
            WeekStartDate = "2026-04-06"
        });

        result.IsValid.Should().BeTrue();
    }
}
