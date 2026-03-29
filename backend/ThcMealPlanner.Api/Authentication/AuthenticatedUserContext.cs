using System.Security.Claims;

namespace ThcMealPlanner.Api.Authentication;

public sealed record AuthenticatedUserContext(
    string Sub,
    string Email,
    string Name,
    string FamilyId,
    string Role)
{
    public bool IsHeadOfHousehold =>
        string.Equals(Role, "head_of_household", StringComparison.OrdinalIgnoreCase);
}

public static class AuthenticatedUserContextResolver
{
    private static readonly string[] SubClaimTypes =
    [
        "sub",
        ClaimTypes.NameIdentifier,
        "nameidentifier",
        "cognito:username"
    ];

    private static readonly string[] FamilyIdClaimTypes =
    [
        "custom:familyId",
        "custom:familyid",
        "custom:family_id",
        "familyId",
        "familyid",
        "family_id"
    ];

    private static readonly string[] RoleClaimTypes =
    [
        "custom:role",
        "role"
    ];

    public static AuthenticatedUserContext? TryResolve(ClaimsPrincipal user)
    {
        var sub = FindFirstValue(user, SubClaimTypes);
        var email = FindFirstValue(user, "email");
        var name = FindFirstValue(user, "name");
        var familyId = FindFirstValue(user, FamilyIdClaimTypes);
        var role = FindFirstValue(user, RoleClaimTypes) ?? "member";

        if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(familyId))
        {
            return null;
        }

        return new AuthenticatedUserContext(
            sub,
            email ?? string.Empty,
            name ?? string.Empty,
            familyId,
            role);
    }

    public static string? FindFamilyId(ClaimsPrincipal user)
    {
        return FindFirstValue(user, FamilyIdClaimTypes);
    }

    public static string? FindSub(ClaimsPrincipal user)
    {
        return FindFirstValue(user, SubClaimTypes);
    }

    public static string? FindRole(ClaimsPrincipal user)
    {
        return FindFirstValue(user, RoleClaimTypes);
    }

    private static string? FindFirstValue(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var match = user.Claims.FirstOrDefault(claim =>
                string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(match?.Value))
            {
                return match.Value;
            }
        }

        return null;
    }
}