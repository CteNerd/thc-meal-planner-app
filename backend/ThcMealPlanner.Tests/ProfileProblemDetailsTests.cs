using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ThcMealPlanner.Api.Profiles;

namespace ThcMealPlanner.Tests;

public sealed class ProfileProblemDetailsTests
{
    [Fact]
    public async Task MissingRequiredUserClaims_ReturnsUnauthorizedProblemDetails()
    {
        var result = ProfileProblemDetails.MissingRequiredUserClaims();

        var problem = await ExecuteAndReadProblemAsync(result);

        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Title.Should().Be("Unauthorized");
        problem.Detail.Should().Be("Missing required user claims.");
    }

    [Fact]
    public async Task ProfileNotFound_ReturnsNotFoundProblemDetails()
    {
        var result = ProfileProblemDetails.ProfileNotFound();

        var problem = await ExecuteAndReadProblemAsync(result);

        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Profile not found");
        problem.Detail.Should().Be("No profile exists for the current user.");
    }

    [Fact]
    public async Task DependentNotFound_ReturnsNotFoundProblemDetails()
    {
        var result = ProfileProblemDetails.DependentNotFound();

        var problem = await ExecuteAndReadProblemAsync(result);

        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Dependent not found");
        problem.Detail.Should().Be("No dependent exists for the requested user id within this family.");
    }

    [Fact]
    public async Task HeadOfHouseholdRequired_ReturnsForbiddenProblemDetails()
    {
        var result = ProfileProblemDetails.HeadOfHouseholdRequired();

        var problem = await ExecuteAndReadProblemAsync(result);

        problem.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden");
        problem.Detail.Should().Be("This action requires head_of_household role.");
    }

    private static async Task<ApiProblemDetails> ExecuteAndReadProblemAsync(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ApiProblemDetails>(httpContext.Response.Body);
        problem.Should().NotBeNull();

        return problem!;
    }
}
