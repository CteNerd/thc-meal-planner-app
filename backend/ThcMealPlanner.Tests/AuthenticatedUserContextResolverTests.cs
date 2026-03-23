using System.Security.Claims;
using FluentAssertions;
using ThcMealPlanner.Api.Authentication;

namespace ThcMealPlanner.Tests;

public sealed class AuthenticatedUserContextResolverTests
{
    [Fact]
    public void TryResolve_WithRequiredClaims_ReturnsContext()
    {
        var principal = BuildPrincipal(
            new Claim("sub", "user-123"),
            new Claim("email", "user@example.com"),
            new Claim("name", "User Name"),
            new Claim("custom:familyId", "FAM#abc"),
            new Claim("custom:role", "head_of_household"));

        var context = AuthenticatedUserContextResolver.TryResolve(principal);

        context.Should().NotBeNull();
        context!.Sub.Should().Be("user-123");
        context.Email.Should().Be("user@example.com");
        context.Name.Should().Be("User Name");
        context.FamilyId.Should().Be("FAM#abc");
        context.Role.Should().Be("head_of_household");
        context.IsHeadOfHousehold.Should().BeTrue();
    }

    [Fact]
    public void TryResolve_WhenCustomClaimsMissing_UsesFallbackClaimsAndDefaultRole()
    {
        var principal = BuildPrincipal(
            new Claim("sub", "user-123"),
            new Claim("familyId", "FAM#fallback"));

        var context = AuthenticatedUserContextResolver.TryResolve(principal);

        context.Should().NotBeNull();
        context!.FamilyId.Should().Be("FAM#fallback");
        context.Role.Should().Be("member");
        context.Email.Should().BeEmpty();
        context.Name.Should().BeEmpty();
        context.IsHeadOfHousehold.Should().BeFalse();
    }

    [Fact]
    public void TryResolve_WhenSubMissing_ReturnsNull()
    {
        var principal = BuildPrincipal(new Claim("custom:familyId", "FAM#abc"));

        var context = AuthenticatedUserContextResolver.TryResolve(principal);

        context.Should().BeNull();
    }

    [Fact]
    public void TryResolve_WhenFamilyIdMissing_ReturnsNull()
    {
        var principal = BuildPrincipal(new Claim("sub", "user-123"));

        var context = AuthenticatedUserContextResolver.TryResolve(principal);

        context.Should().BeNull();
    }

    [Fact]
    public void IsHeadOfHousehold_IsCaseInsensitive()
    {
        var context = new AuthenticatedUserContext("user-123", string.Empty, string.Empty, "FAM#abc", "HEAD_OF_HOUSEHOLD");

        context.IsHeadOfHousehold.Should().BeTrue();
    }

    private static ClaimsPrincipal BuildPrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}