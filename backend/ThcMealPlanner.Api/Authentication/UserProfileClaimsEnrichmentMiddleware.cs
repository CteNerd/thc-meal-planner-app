using System.Security.Claims;
using ThcMealPlanner.Api.Profiles;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Api.Authentication;

public sealed class UserProfileClaimsEnrichmentMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserProfileClaimsEnrichmentMiddleware> _logger;

    public UserProfileClaimsEnrichmentMiddleware(
        RequestDelegate next,
        ILogger<UserProfileClaimsEnrichmentMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var sub = AuthenticatedUserContextResolver.FindSub(user);
            var familyId = AuthenticatedUserContextResolver.FindFamilyId(user);

            if (!string.IsNullOrWhiteSpace(sub) && string.IsNullOrWhiteSpace(familyId))
            {
                try
                {
                    var repository = context.RequestServices.GetService<IDynamoDbRepository<UserProfileDocument>>();

                    if (repository is null)
                    {
                        await _next(context);
                        return;
                    }

                    var profile = await repository.GetAsync(new DynamoDbKey($"USER#{sub}", "PROFILE"), context.RequestAborted);

                    if (profile is not null && !string.IsNullOrWhiteSpace(profile.FamilyId) && user.Identity is ClaimsIdentity identity)
                    {
                        identity.AddClaim(new Claim("familyId", profile.FamilyId));

                        if (string.IsNullOrWhiteSpace(AuthenticatedUserContextResolver.FindRole(user)) && !string.IsNullOrWhiteSpace(profile.Role))
                        {
                            identity.AddClaim(new Claim("role", profile.Role));
                        }
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Unable to enrich authenticated user claims from profile for sub {Sub}.",
                        sub);
                }
            }
        }

        await _next(context);
    }
}
