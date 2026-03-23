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
    public static AuthenticatedUserContext? TryResolve(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub");
        var email = user.FindFirstValue("email");
        var name = user.FindFirstValue("name");
        var familyId = user.FindFirstValue("custom:familyId") ?? user.FindFirstValue("familyId");
        var role = user.FindFirstValue("custom:role") ?? user.FindFirstValue("role") ?? "member";

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
}