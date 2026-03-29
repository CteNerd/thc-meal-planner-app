using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Options;

namespace ThcMealPlanner.Api.Notifications;

public interface INotificationService
{
    Task SendTestNotificationAsync(
        string familyId,
        string recipientName,
        string recipientEmail,
        SendTestNotificationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class SesNotificationService : INotificationService
{
    private readonly IAmazonSimpleEmailServiceV2 _sesClient;
    private readonly NotificationOptions _options;

    public SesNotificationService(
        IAmazonSimpleEmailServiceV2 sesClient,
        IOptions<NotificationOptions> options)
    {
        _sesClient = sesClient;
        _options = options.Value;
    }

    public async Task SendTestNotificationAsync(
        string familyId,
        string recipientName,
        string recipientEmail,
        SendTestNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("Notifications:FromEmail must be configured for SES sends.");
        }

        var (subject, body) = BuildMessage(familyId, recipientName, request);

        var response = await _sesClient.SendEmailAsync(new SendEmailRequest
        {
            FromEmailAddress = _options.FromEmail,
            Destination = new Destination
            {
                ToAddresses = [recipientEmail]
            },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content
                    {
                        Charset = "UTF-8",
                        Data = subject
                    },
                    Body = new Body
                    {
                        Text = new Content
                        {
                            Charset = "UTF-8",
                            Data = body
                        }
                    }
                }
            }
        }, cancellationToken);

        if (response.HttpStatusCode is < System.Net.HttpStatusCode.OK or >= System.Net.HttpStatusCode.MultipleChoices)
        {
            throw new InvalidOperationException($"SES send failed with status code {(int)response.HttpStatusCode}.");
        }
    }

    private static (string Subject, string Body) BuildMessage(
        string familyId,
        string recipientName,
        SendTestNotificationRequest request)
    {
        if (string.Equals(request.Type, NotificationTypes.MealPlanReady, StringComparison.Ordinal))
        {
            var weekStartDate = string.IsNullOrWhiteSpace(request.WeekStartDate) ? "this week" : request.WeekStartDate;
            return (
                Subject: "THC Meal Planner: Meal plan ready",
                Body: $"Hi {DisplayName(recipientName)}, your meal plan for {weekStartDate} is ready for family {familyId}."
            );
        }

        var message = string.IsNullOrWhiteSpace(request.SecurityMessage)
            ? "A security event was detected in your account."
            : request.SecurityMessage;

        return (
            Subject: "THC Meal Planner: Security alert",
            Body: $"Hi {DisplayName(recipientName)}, {message}"
        );
    }

    private static string DisplayName(string recipientName)
    {
        return string.IsNullOrWhiteSpace(recipientName) ? "there" : recipientName;
    }
}
