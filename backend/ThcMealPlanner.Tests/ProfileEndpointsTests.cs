using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using System.Net;
using System.Net.Http.Json;
using ThcMealPlanner.Api.Profiles;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Tests;

public sealed class ProfileEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProfileEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProfile_WhenMissing_ReturnsNotFound()
    {
        var client = CreateAuthenticatedClient(new InMemoryProfileRepository());

        var response = await client.GetAsync("/api/profile");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutProfile_WithValidPayload_UpsertsAndReturnsProfile()
    {
        var client = CreateAuthenticatedClient(new InMemoryProfileRepository());

        var request = new UpdateProfileRequest
        {
            DietaryPrefs = ["vegetarian"],
            Allergies =
            [
                new AllergyModel
                {
                    Allergen = "tree nuts",
                    Severity = "severe",
                    CrossContamination = true
                }
            ],
            DefaultServings = 4
        };

        var putResponse = await client.PutAsJsonAsync("/api/profile", request);
        var getResponse = await client.GetAsync("/api/profile");

        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await getResponse.Content.ReadFromJsonAsync<UserProfileDocument>();
        profile.Should().NotBeNull();
        profile!.UserId.Should().Be("test-user-123");
        profile.DietaryPrefs.Should().ContainSingle().Which.Should().Be("vegetarian");
        profile.Allergies.Should().ContainSingle().Which.Allergen.Should().Be("tree nuts");
        profile.DefaultServings.Should().Be(4);
    }

    [Fact]
    public async Task PutProfile_WithInvalidPayload_ReturnsBadRequestWithValidationProblem()
    {
        var client = CreateAuthenticatedClient(new InMemoryProfileRepository());

        var request = new UpdateProfileRequest
        {
            MacroTargets = new MacroTargetsModel
            {
                Calories = -50
            }
        };

        var response = await client.PutAsJsonAsync("/api/profile", request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("MacroTargets.Calories");
    }

    private HttpClient CreateAuthenticatedClient(InMemoryProfileRepository repository)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });

                services.AddSingleton<IDynamoDbRepository<UserProfileDocument>>(repository);
                services.AddScoped<IValidator<UpdateProfileRequest>, UpdateProfileRequestValidator>();
            });
        }).CreateClient();
    }

    private sealed class InMemoryProfileRepository : IDynamoDbRepository<UserProfileDocument>
    {
        private readonly Dictionary<string, UserProfileDocument> _store = new(StringComparer.Ordinal);

        public Task<UserProfileDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(ToMapKey(key), out var document);

            return Task.FromResult(document);
        }

        public Task PutAsync(DynamoDbKey key, UserProfileDocument document, CancellationToken cancellationToken = default)
        {
            _store[ToMapKey(key)] = document;

            return Task.CompletedTask;
        }

        public Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.Remove(ToMapKey(key));

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UserProfileDocument>> QueryByPartitionKeyAsync(
            string partitionKey,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var items = _store
                .Where(entry => entry.Key.StartsWith(partitionKey + "|", StringComparison.Ordinal))
                .Select(entry => entry.Value)
                .ToList();

            if (limit.HasValue)
            {
                items = items.Take(limit.Value).ToList();
            }

            return Task.FromResult<IReadOnlyList<UserProfileDocument>>(items);
        }

        private static string ToMapKey(DynamoDbKey key)
        {
            return $"{key.PartitionKey}|{key.SortKey}";
        }
    }
}