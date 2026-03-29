using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using ThcMealPlanner.Api.Notifications;

namespace ThcMealPlanner.Tests;

public sealed class NotificationEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public NotificationEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostTestNotification_WithValidPayload_ReturnsAccepted()
    {
        var fakeService = new FakeNotificationService();
        var client = CreateAuthenticatedClient(fakeService);

        var response = await client.PostAsJsonAsync("/api/notifications/test", new SendTestNotificationRequest
        {
            Type = NotificationTypes.MealPlanReady,
            WeekStartDate = "2026-04-06"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        fakeService.Calls.Should().ContainSingle();
        fakeService.Calls[0].RecipientEmail.Should().Be("adult1@example.com");
        fakeService.Calls[0].Request.Type.Should().Be(NotificationTypes.MealPlanReady);
    }

    [Fact]
    public async Task PostTestNotification_WithInvalidType_ReturnsBadRequest()
    {
        var fakeService = new FakeNotificationService();
        var client = CreateAuthenticatedClient(fakeService);

        var response = await client.PostAsJsonAsync("/api/notifications/test", new SendTestNotificationRequest
        {
            Type = "unknown"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fakeService.Calls.Should().BeEmpty();
    }

    private HttpClient CreateAuthenticatedClient(FakeNotificationService fakeService)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });

                services.AddSingleton<INotificationService>(fakeService);
            });
        }).CreateClient();
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public List<NotificationCall> Calls { get; } = [];

        public Task SendTestNotificationAsync(
            string familyId,
            string recipientName,
            string recipientEmail,
            SendTestNotificationRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new NotificationCall(familyId, recipientName, recipientEmail, request));
            return Task.CompletedTask;
        }
    }

    private sealed record NotificationCall(
        string FamilyId,
        string RecipientName,
        string RecipientEmail,
        SendTestNotificationRequest Request);
}
