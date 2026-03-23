using FluentValidation;
using System.Security.Claims;
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
        var sub = httpContext.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Missing user subject claim.");
        }

        var key = BuildProfileKey(sub);
        var profile = await repository.GetAsync(key, cancellationToken);

        return profile is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Profile not found.")
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

        var user = httpContext.User;
        var sub = user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Missing user subject claim.");
        }

        var key = BuildProfileKey(sub);
        var existing = await repository.GetAsync(key, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var merged = Merge(existing, request, user, sub, now);

        await repository.PutAsync(key, merged, cancellationToken);

        return Results.Ok(merged);
    }

    private static UserProfileDocument Merge(
        UserProfileDocument? existing,
        UpdateProfileRequest request,
        ClaimsPrincipal user,
        string sub,
        DateTimeOffset now)
    {
        return new UserProfileDocument
        {
            UserId = existing?.UserId ?? sub,
            Name = request.Name ?? existing?.Name ?? user.FindFirstValue("name") ?? string.Empty,
            Email = request.Email ?? existing?.Email ?? user.FindFirstValue("email") ?? string.Empty,
            FamilyId = existing?.FamilyId ?? user.FindFirstValue("custom:familyId") ?? user.FindFirstValue("familyId") ?? $"FAM#{sub}",
            Role = request.Role ?? existing?.Role ?? "member",
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