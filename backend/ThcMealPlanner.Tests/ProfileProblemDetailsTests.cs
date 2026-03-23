using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ThcMealPlanner.Api.Profiles;

namespace ThcMealPlanner.Tests;

public sealed class ProfileProblemDetailsTests
{
    [Fact]
    public async Task MissingRequiredUserClaims_ReturnsUnauthorizedProblemDetails()
    {
        var result = ProfileProblemDetails.MissingRequiredUserClaims();

        var (statusCode, problem) = await ExecuteAndReadProblemAsync(result);

        statusCode.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Should().NotBeNull();
    }

    [Fact]
    public async Task ProfileNotFound_ReturnsNotFoundProblemDetails()
    {
        var result = ProfileProblemDetails.ProfileNotFound();

        var (statusCode, problem) = await ExecuteAndReadProblemAsync(result);

        statusCode.Should().Be(StatusCodes.Status404NotFound);
        problem.Should().NotBeNull();
    }

    [Fact]
    public async Task DependentNotFound_ReturnsNotFoundProblemDetails()
    {
        var result = ProfileProblemDetails.DependentNotFound();

        var (statusCode, problem) = await ExecuteAndReadProblemAsync(result);

        statusCode.Should().Be(StatusCodes.Status404NotFound);
        problem.Should().NotBeNull();
    }

    [Fact]
    public async Task HeadOfHouseholdRequired_ReturnsForbiddenProblemDetails()
    {
        var result = ProfileProblemDetails.HeadOfHouseholdRequired();

        var (statusCode, problem) = await ExecuteAndReadProblemAsync(result);

        statusCode.Should().Be(StatusCodes.Status403Forbidden);
        problem.Should().NotBeNull();
    }

    private static async Task<(int StatusCode, ApiProblemDetails Problem)> ExecuteAndReadProblemAsync(IResult result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.RequestServices = services.BuildServiceProvider();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ApiProblemDetails>(httpContext.Response.Body);
        problem.Should().NotBeNull();

        return (httpContext.Response.StatusCode, problem!);
    }
}
