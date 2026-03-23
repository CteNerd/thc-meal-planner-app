using FluentValidation;
using ThcMealPlanner.Api.Authentication;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Api.Profiles;

public static class ProfileEndpoints
{
    public static RouteGroupBuilder MapProfileEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/profile", GetProfileAsync);
        group.MapPut("/profile", UpdateProfileAsync);

        return group;
    }

    private static async Task<IResult> GetProfileAsync(
        HttpContext httpContext,
        IDynamoDbRepository<UserProfileDocument> repository,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return ProfileProblemDetails.MissingRequiredUserClaims();
        }

        var key = BuildProfileKey(userContext.Sub);
        var profile = await repository.GetAsync(key, cancellationToken);

        return profile is null
            ? ProfileProblemDetails.ProfileNotFound()
            : Results.Ok(profile);
    }

    private static async Task<IResult> UpdateProfileAsync(
        HttpContext httpContext,
        UpdateProfileRequest request,
        IValidator<UpdateProfileRequest> validator,
        IDynamoDbRepository<UserProfileDocument> repository,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return ProfileProblemDetails.MissingRequiredUserClaims();
        }

        var key = BuildProfileKey(userContext.Sub);
        var existing = await repository.GetAsync(key, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var merged = Merge(existing, request, userContext, now);

        await repository.PutAsync(key, merged, cancellationToken);

        return Results.Ok(merged);
    }

    private static UserProfileDocument Merge(
        UserProfileDocument? existing,
        UpdateProfileRequest request,
        AuthenticatedUserContext userContext,
        DateTimeOffset now)
    {
        return new UserProfileDocument
        {
            UserId = existing?.UserId ?? userContext.Sub,
            Name = request.Name ?? existing?.Name ?? userContext.Name,
            Email = request.Email ?? existing?.Email ?? userContext.Email,
            FamilyId = existing?.FamilyId ?? userContext.FamilyId,
            Role = existing?.Role ?? userContext.Role,
            DietaryPrefs = request.DietaryPrefs ?? existing?.DietaryPrefs ?? [],
            Allergies = request.Allergies ?? existing?.Allergies ?? [],
            ExcludedIngredients = request.ExcludedIngredients ?? existing?.ExcludedIngredients ?? [],
            MacroTargets = request.MacroTargets ?? existing?.MacroTargets,
            CuisinePreferences = request.CuisinePreferences ?? existing?.CuisinePreferences ?? [],
            CookingConstraints = request.CookingConstraints ?? existing?.CookingConstraints,
            FlavorPreferences = request.FlavorPreferences ?? existing?.FlavorPreferences,
            DefaultServings = request.DefaultServings ?? existing?.DefaultServings,
            FamilyMembers = request.FamilyMembers ?? existing?.FamilyMembers ?? [],
            DoctorNotes = request.DoctorNotes ?? existing?.DoctorNotes ?? [],
            NotificationPrefs = request.NotificationPrefs ?? existing?.NotificationPrefs,
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now
        };
    }

    private static DynamoDbKey BuildProfileKey(string sub)
    {
        return new DynamoDbKey($"USER#{sub}", "PROFILE");
    }

    private static Dictionary<string, string[]> ToDictionary(this FluentValidation.Results.ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                grouping => grouping.Key,
                grouping => grouping.Select(error => error.ErrorMessage).ToArray());
    }
}