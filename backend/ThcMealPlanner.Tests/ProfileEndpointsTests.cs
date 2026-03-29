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
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        problem.Title.Should().Be("Profile not found");
        problem.Detail.Should().Be("No profile exists for the current user.");
    }

    [Fact]
    public async Task GetProfile_WhenMissingRequiredClaims_ReturnsUnauthorizedProblemDetails()
    {
        var client = CreateMissingClaimsClient(new InMemoryProfileRepository());

        var response = await client.GetAsync("/api/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Title.Should().Be("Unauthorized");
        problem.Detail.Should().Be("Missing required user claims.");
    }

    [Fact]
    public async Task PutProfile_WhenMissingRequiredClaims_ReturnsUnauthorizedProblemDetails()
    {
        var client = CreateMissingClaimsClient(new InMemoryProfileRepository());

        var response = await client.PutAsJsonAsync(
            "/api/profile",
            new UpdateProfileRequest
            {
                Name = "Adult 1"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Title.Should().Be("Unauthorized");
        problem.Detail.Should().Be("Missing required user claims.");
    }

    [Fact]
    public async Task GetProfile_WhenFamilyClaimMissing_ResolvesClaimsFromStoredProfile()
    {
        var repository = new InMemoryProfileRepository();
        await repository.PutAsync(
            new DynamoDbKey("USER#test-user-123", "PROFILE"),
            new UserProfileDocument
            {
                UserId = "test-user-123",
                Name = "Adult 1",
                Email = "adult1@example.com",
                FamilyId = "FAM#test-family",
                Role = "head_of_household"
            });

        var client = CreateMissingClaimsClient(repository);

        var response = await client.GetAsync("/api/profile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDocument>();
        profile.Should().NotBeNull();
        profile!.FamilyId.Should().Be("FAM#test-family");
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
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Title.Should().Be("One or more validation errors occurred.");
        problem.Errors.Should().NotBeNull();
        problem.Errors!.Should().ContainKey("MacroTargets.Calories");
        problem.Errors["MacroTargets.Calories"]
            .Should()
            .Contain(message => message.Contains("greater than '0'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PutProfile_WhenRoleProvided_ReturnsBadRequest()
    {
        var client = CreateAuthenticatedClient(new InMemoryProfileRepository());

        var response = await client.PutAsJsonAsync(
            "/api/profile",
            new UpdateProfileRequest
            {
                Role = "head_of_household"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Title.Should().Be("One or more validation errors occurred.");
        problem.Errors.Should().NotBeNull();
        problem.Errors!.Should().ContainKey("Role");
        problem.Errors["Role"].Should().ContainSingle();
        problem.Errors["Role"][0].Should().Contain("Role cannot be updated");
    }

    [Fact]
    public async Task PutProfile_WithInvalidAllergy_ReturnsNestedValidationError()
    {
        var client = CreateAuthenticatedClient(new InMemoryProfileRepository());

        var response = await client.PutAsJsonAsync(
            "/api/profile",
            new UpdateProfileRequest
            {
                Allergies =
                [
                    new AllergyModel
                    {
                        Allergen = string.Empty,
                        Severity = "moderate"
                    }
                ]
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Title.Should().Be("One or more validation errors occurred.");
        problem.Errors.Should().NotBeNull();
        problem.Errors!.Should().ContainKey("Allergies[0].Allergen");
        problem.Errors["Allergies[0].Allergen"]
            .Should()
            .Contain(message => message.Contains("must not be empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PutProfile_WithPartialPayload_PreservesExistingFields()
    {
        var repository = new InMemoryProfileRepository();
        await repository.PutAsync(
            new DynamoDbKey("USER#test-user-123", "PROFILE"),
            new UserProfileDocument
            {
                UserId = "test-user-123",
                Name = "Adult 1",
                Email = "adult1@example.com",
                FamilyId = "FAM#existing",
                Role = "member",
                DietaryPrefs = ["vegetarian"],
                ExcludedIngredients = ["mushrooms"],
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
            });

        var client = CreateAuthenticatedClient(repository);

        var response = await client.PutAsJsonAsync(
            "/api/profile",
            new UpdateProfileRequest
            {
                DefaultServings = 3
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<UserProfileDocument>();
        updated.Should().NotBeNull();
        updated!.DietaryPrefs.Should().ContainSingle().Which.Should().Be("vegetarian");
        updated.ExcludedIngredients.Should().ContainSingle().Which.Should().Be("mushrooms");
        updated.DefaultServings.Should().Be(3);
    }

    [Fact]
    public async Task PutProfile_WhenCreatingNewProfile_UsesFamilyIdClaim()
    {
        var client = CreateAuthenticatedClient(new InMemoryProfileRepository());

        var response = await client.PutAsJsonAsync(
            "/api/profile",
            new UpdateProfileRequest
            {
                Name = "Adult 1"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<UserProfileDocument>();
        profile.Should().NotBeNull();
        profile!.FamilyId.Should().Be("FAM#test-family");
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

    private HttpClient CreateMissingClaimsClient(InMemoryProfileRepository repository)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(MissingClaimsAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, MissingClaimsAuthHandler>(
                        MissingClaimsAuthHandler.SchemeName,
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

        public Task<IReadOnlyList<UserProfileDocument>> QueryByIndexPartitionKeyAsync(
            string indexName,
            string partitionKeyName,
            string partitionKeyValue,
            IReadOnlyDictionary<string, string>? equalsFilters = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var items = _store.Values.ToList();

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