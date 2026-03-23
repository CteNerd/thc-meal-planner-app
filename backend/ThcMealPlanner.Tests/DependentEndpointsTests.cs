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

public sealed class DependentEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DependentEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDependents_ReturnsFamilyScopedDependentsOnly()
    {
        var repository = new InMemoryDependentRepository();
        await repository.PutAsync(
            new DynamoDbKey("USER#dep_abc123", "PROFILE"),
            new DependentProfileDocument
            {
                UserId = "dep_abc123",
                Name = "Child 1",
                FamilyId = "FAM#test-family",
                Role = "dependent",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        await repository.PutAsync(
            new DynamoDbKey("USER#dep_other", "PROFILE"),
            new DependentProfileDocument
            {
                UserId = "dep_other",
                Name = "Child X",
                FamilyId = "FAM#other",
                Role = "dependent",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var client = CreateAuthenticatedClient(repository);

        var response = await client.GetAsync("/api/family/dependents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<DependentProfileDocument>>();
        items.Should().NotBeNull();
        items!.Should().HaveCount(1);
        items[0].UserId.Should().Be("dep_abc123");
    }

    [Fact]
    public async Task PostDependent_CreatesDependent()
    {
        var repository = new InMemoryDependentRepository();
        var client = CreateAuthenticatedClient(repository);

        var response = await client.PostAsJsonAsync(
            "/api/family/dependents",
            new CreateDependentRequest
            {
                Name = "Child 2",
                AgeGroup = "elementary"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<DependentProfileDocument>();
        created.Should().NotBeNull();
        created!.UserId.Should().StartWith("dep_");
        created.FamilyId.Should().Be("FAM#test-family");
    }

    [Fact]
    public async Task PostDependent_WithInvalidPayload_ReturnsBadRequestValidationProblem()
    {
        var repository = new InMemoryDependentRepository();
        var client = CreateAuthenticatedClient(repository);

        var response = await client.PostAsJsonAsync(
            "/api/family/dependents",
            new CreateDependentRequest
            {
                Name = string.Empty
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Title.Should().Be("One or more validation errors occurred.");
        problem.Errors.Should().NotBeNull();
        problem.Errors!.Should().ContainKey("Name");
        problem.Errors["Name"].Should().Contain(message => message.Contains("must not be empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostDependent_WithInvalidNestedPayload_ReturnsNestedValidationErrors()
    {
        var repository = new InMemoryDependentRepository();
        var client = CreateAuthenticatedClient(repository);

        var response = await client.PostAsJsonAsync(
            "/api/family/dependents",
            new CreateDependentRequest
            {
                Name = "Child Nested",
                Allergies =
                [
                    new AllergyModel
                    {
                        Allergen = string.Empty,
                        Severity = string.Empty
                    }
                ],
                MacroTargets = new MacroTargetsModel
                {
                    Calories = -1
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Title.Should().Be("One or more validation errors occurred.");
        problem.Errors.Should().NotBeNull();
        problem.Errors!.Should().ContainKey("Allergies[0].Allergen");
        problem.Errors.Should().ContainKey("Allergies[0].Severity");
        problem.Errors.Should().ContainKey("MacroTargets.Calories");
    }

    [Fact]
    public async Task PutDependent_OutsideFamily_ReturnsNotFound()
    {
        var repository = new InMemoryDependentRepository();
        await repository.PutAsync(
            new DynamoDbKey("USER#dep_foreign", "PROFILE"),
            new DependentProfileDocument
            {
                UserId = "dep_foreign",
                Name = "Child Foreign",
                FamilyId = "FAM#other",
                Role = "dependent",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var client = CreateAuthenticatedClient(repository);

        var response = await client.PutAsJsonAsync(
            "/api/family/dependents/dep_foreign",
            new UpdateDependentRequest { Name = "Updated" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        problem.Title.Should().Be("Dependent not found");
        problem.Detail.Should().Be("No dependent exists for the requested user id within this family.");
    }

    [Fact]
    public async Task PutDependent_WithInvalidPayload_ReturnsBadRequestValidationProblem()
    {
        var repository = new InMemoryDependentRepository();
        await repository.PutAsync(
            new DynamoDbKey("USER#dep_invalid", "PROFILE"),
            new DependentProfileDocument
            {
                UserId = "dep_invalid",
                Name = "Child Invalid",
                FamilyId = "FAM#test-family",
                Role = "dependent",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var client = CreateAuthenticatedClient(repository);

        var response = await client.PutAsJsonAsync(
            "/api/family/dependents/dep_invalid",
            new UpdateDependentRequest
            {
                Name = new string('x', 101)
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Title.Should().Be("One or more validation errors occurred.");
        problem.Errors.Should().NotBeNull();
        problem.Errors!.Should().ContainKey("Name");
        problem.Errors["Name"].Should().Contain(message => message.Contains("100", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeleteDependent_RemovesRecord()
    {
        var repository = new InMemoryDependentRepository();
        await repository.PutAsync(
            new DynamoDbKey("USER#dep_del", "PROFILE"),
            new DependentProfileDocument
            {
                UserId = "dep_del",
                Name = "Child Delete",
                FamilyId = "FAM#test-family",
                Role = "dependent",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var client = CreateAuthenticatedClient(repository);

        var response = await client.DeleteAsync("/api/family/dependents/dep_del");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var existing = await repository.GetAsync(new DynamoDbKey("USER#dep_del", "PROFILE"));
        existing.Should().BeNull();
    }

    [Fact]
    public async Task GetDependents_WithMemberRole_ReturnsForbidden()
    {
        var repository = new InMemoryDependentRepository();
        var client = CreateMemberClient(repository);

        var response = await client.GetAsync("/api/family/dependents");
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
        problem.Title.Should().Be("Forbidden");
        problem.Detail.Should().Be("This action requires head_of_household role.");
    }

    [Fact]
    public async Task PostDependent_WithMemberRole_ReturnsForbidden()
    {
        var repository = new InMemoryDependentRepository();
        var client = CreateMemberClient(repository);

        var response = await client.PostAsJsonAsync(
            "/api/family/dependents",
            new CreateDependentRequest { Name = "Child 3" });
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
        problem.Title.Should().Be("Forbidden");
        problem.Detail.Should().Be("This action requires head_of_household role.");
    }

    [Fact]
    public async Task PutDependent_WithMemberRole_ReturnsForbidden()
    {
        var repository = new InMemoryDependentRepository();
        var client = CreateMemberClient(repository);

        var response = await client.PutAsJsonAsync(
            "/api/family/dependents/dep_abc123",
            new UpdateDependentRequest { Name = "Updated" });
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
        problem.Title.Should().Be("Forbidden");
        problem.Detail.Should().Be("This action requires head_of_household role.");
    }

    [Fact]
    public async Task DeleteDependent_WithMemberRole_ReturnsForbidden()
    {
        var repository = new InMemoryDependentRepository();
        var client = CreateMemberClient(repository);

        var response = await client.DeleteAsync("/api/family/dependents/dep_abc123");
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
        problem.Title.Should().Be("Forbidden");
        problem.Detail.Should().Be("This action requires head_of_household role.");
    }

    [Fact]
    public async Task GetDependents_WhenMissingRequiredClaims_ReturnsUnauthorizedProblemDetails()
    {
        var repository = new InMemoryDependentRepository();
        var client = CreateMissingClaimsClient(repository);

        var response = await client.GetAsync("/api/family/dependents");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Title.Should().Be("Unauthorized");
        problem.Detail.Should().Be("Missing required user claims.");
    }

    [Fact]
    public async Task PutDependent_WhenMissingRequiredClaims_ReturnsUnauthorizedProblemDetails()
    {
        var repository = new InMemoryDependentRepository();
        var client = CreateMissingClaimsClient(repository);

        var response = await client.PutAsJsonAsync(
            "/api/family/dependents/dep_any",
            new UpdateDependentRequest
            {
                Name = "Updated"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Title.Should().Be("Unauthorized");
        problem.Detail.Should().Be("Missing required user claims.");
    }

    [Fact]
    public async Task PostDependent_WhenMissingRequiredClaims_ReturnsUnauthorizedProblemDetails()
    {
        var repository = new InMemoryDependentRepository();
        var client = CreateMissingClaimsClient(repository);

        var response = await client.PostAsJsonAsync(
            "/api/family/dependents",
            new CreateDependentRequest
            {
                Name = "Child"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Title.Should().Be("Unauthorized");
        problem.Detail.Should().Be("Missing required user claims.");
    }

    [Fact]
    public async Task DeleteDependent_WhenMissingRequiredClaims_ReturnsUnauthorizedProblemDetails()
    {
        var repository = new InMemoryDependentRepository();
        var client = CreateMissingClaimsClient(repository);

        var response = await client.DeleteAsync("/api/family/dependents/dep_any");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Title.Should().Be("Unauthorized");
        problem.Detail.Should().Be("Missing required user claims.");
    }

    private HttpClient CreateAuthenticatedClient(InMemoryDependentRepository repository)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });

                services.AddSingleton<IDynamoDbRepository<DependentProfileDocument>>(repository);
                services.AddScoped<IValidator<CreateDependentRequest>, CreateDependentRequestValidator>();
                services.AddScoped<IValidator<UpdateDependentRequest>, UpdateDependentRequestValidator>();
            });
        }).CreateClient();
    }

    private HttpClient CreateMemberClient(InMemoryDependentRepository repository)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(MemberAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, MemberAuthHandler>(
                        MemberAuthHandler.SchemeName,
                        _ => { });

                services.AddSingleton<IDynamoDbRepository<DependentProfileDocument>>(repository);
                services.AddScoped<IValidator<CreateDependentRequest>, CreateDependentRequestValidator>();
                services.AddScoped<IValidator<UpdateDependentRequest>, UpdateDependentRequestValidator>();
            });
        }).CreateClient();
    }

    private HttpClient CreateMissingClaimsClient(InMemoryDependentRepository repository)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(MissingClaimsAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, MissingClaimsAuthHandler>(
                        MissingClaimsAuthHandler.SchemeName,
                        _ => { });

                services.AddSingleton<IDynamoDbRepository<DependentProfileDocument>>(repository);
                services.AddScoped<IValidator<CreateDependentRequest>, CreateDependentRequestValidator>();
                services.AddScoped<IValidator<UpdateDependentRequest>, UpdateDependentRequestValidator>();
            });
        }).CreateClient();
    }

    private sealed class InMemoryDependentRepository : IDynamoDbRepository<DependentProfileDocument>
    {
        private readonly Dictionary<string, DependentProfileDocument> _store = new(StringComparer.Ordinal);

        public Task<DependentProfileDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(ToMapKey(key), out var document);
            return Task.FromResult(document);
        }

        public Task PutAsync(DynamoDbKey key, DependentProfileDocument document, CancellationToken cancellationToken = default)
        {
            _store[ToMapKey(key)] = document;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.Remove(ToMapKey(key));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DependentProfileDocument>> QueryByPartitionKeyAsync(
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

            return Task.FromResult<IReadOnlyList<DependentProfileDocument>>(items);
        }

        public Task<IReadOnlyList<DependentProfileDocument>> QueryByIndexPartitionKeyAsync(
            string indexName,
            string partitionKeyName,
            string partitionKeyValue,
            IReadOnlyDictionary<string, string>? equalsFilters = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var items = _store.Values
                .Where(item => string.Equals(item.FamilyId, partitionKeyValue, StringComparison.Ordinal))
                .ToList();

            if (equalsFilters is not null &&
                equalsFilters.TryGetValue("role", out var roleFilter))
            {
                items = items
                    .Where(item => string.Equals(item.Role, roleFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (limit.HasValue)
            {
                items = items.Take(limit.Value).ToList();
            }

            return Task.FromResult<IReadOnlyList<DependentProfileDocument>>(items);
        }

        private static string ToMapKey(DynamoDbKey key)
            => $"{key.PartitionKey}|{key.SortKey}";
    }
}