using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using FluentAssertions;
using Microsoft.Extensions.Options;
using ThcMealPlanner.Api.Notifications;

namespace ThcMealPlanner.Tests;

public sealed class SesNotificationServiceTests
{
    [Fact]
    public async Task SendTestNotificationAsync_WhenFromEmailMissing_Throws()
    {
        var fakeClient = new FakeSesClient();
        var service = CreateService(fakeClient, fromEmail: "");

        var act = async () => await service.SendTestNotificationAsync(
            "FAM#test-family",
            "Adult 1",
            "adult1@example.com",
            new SendTestNotificationRequest { Type = NotificationTypes.MealPlanReady });

        await act.Should().ThrowAsync<InvalidOperationException>();
        fakeClient.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task SendTestNotificationAsync_WhenMealPlanReady_SendsExpectedPayload()
    {
        var fakeClient = new FakeSesClient();
        var service = CreateService(fakeClient, fromEmail: "sender@example.com");

        await service.SendTestNotificationAsync(
            "FAM#test-family",
            "Adult 1",
            "adult1@example.com",
            new SendTestNotificationRequest
            {
                Type = NotificationTypes.MealPlanReady,
                WeekStartDate = "2026-04-06"
            });

        var call = fakeClient.Calls.Should().ContainSingle().Subject;
        call.FromEmailAddress.Should().Be("sender@example.com");
        call.Destination.ToAddresses.Should().ContainSingle("adult1@example.com");
        call.Content.Simple.Subject.Data.Should().Be("THC Meal Planner: Meal plan ready");
        call.Content.Simple.Body.Text.Data.Should().Contain("2026-04-06");
    }

    [Fact]
    public async Task SendTestNotificationAsync_WhenSecurityAlertWithoutMessage_UsesDefaultBody()
    {
        var fakeClient = new FakeSesClient();
        var service = CreateService(fakeClient, fromEmail: "sender@example.com");

        await service.SendTestNotificationAsync(
            "FAM#test-family",
            "",
            "adult1@example.com",
            new SendTestNotificationRequest
            {
                Type = NotificationTypes.SecurityAlert,
                SecurityMessage = null
            });

        var call = fakeClient.Calls.Should().ContainSingle().Subject;
        call.Content.Simple.Subject.Data.Should().Be("THC Meal Planner: Security alert");
        call.Content.Simple.Body.Text.Data.Should().Contain("A security event was detected in your account.");
        call.Content.Simple.Body.Text.Data.Should().Contain("Hi there");
    }

    [Fact]
    public async Task SendTestNotificationAsync_WhenSesStatusNotSuccess_Throws()
    {
        var fakeClient = new FakeSesClient
        {
            ResponseStatusCode = System.Net.HttpStatusCode.BadRequest
        };

        var service = CreateService(fakeClient, fromEmail: "sender@example.com");

        var act = async () => await service.SendTestNotificationAsync(
            "FAM#test-family",
            "Adult 1",
            "adult1@example.com",
            new SendTestNotificationRequest { Type = NotificationTypes.MealPlanReady });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SES send failed with status code*");
    }

    private static SesNotificationService CreateService(IAmazonSimpleEmailServiceV2 client, string fromEmail)
    {
        return new SesNotificationService(client, Options.Create(new NotificationOptions
        {
            FromEmail = fromEmail
        }));
    }

    private sealed class FakeSesClient : AmazonSimpleEmailServiceV2Client
    {
        public List<SendEmailRequest> Calls { get; } = [];

        public System.Net.HttpStatusCode ResponseStatusCode { get; init; } = System.Net.HttpStatusCode.OK;

        public FakeSesClient()
            : base(new BasicAWSCredentials("test", "test"), new AmazonSimpleEmailServiceV2Config { RegionEndpoint = RegionEndpoint.USEast1 })
        {
        }

        public override Task<SendEmailResponse> SendEmailAsync(SendEmailRequest request, CancellationToken cancellationToken = default)
        {
            Calls.Add(request);

            return Task.FromResult(new SendEmailResponse
            {
                HttpStatusCode = ResponseStatusCode,
                MessageId = Guid.NewGuid().ToString("N")
            });
        }
    }
}
