using FluentValidation;
using ThcMealPlanner.Api.Authentication;
using ThcMealPlanner.Api.GroceryLists;
using ThcMealPlanner.Api.Profiles;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Api.MealPlans;

public static class MealPlanEndpoints
{
    public static RouteGroupBuilder MapMealPlanEndpoints(this RouteGroupBuilder group)
    {
        // Register specific paths before parameterized to avoid shadowing
        group.MapGet("/meal-plans/current", GetCurrentMealPlanAsync);
        group.MapGet("/meal-plans/history", GetMealPlanHistoryAsync);
        group.MapGet("/meal-plans/{weekStartDate}/swap-options", GetSwapOptionsAsync);
        group.MapPost("/meal-plans/generate", GenerateMealPlanAsync);
        group.MapGet("/meal-plans/{weekStartDate}", GetMealPlanByWeekAsync);
        group.MapPost("/meal-plans", CreateMealPlanAsync);
        group.MapPut("/meal-plans/{weekStartDate}", UpdateMealPlanAsync);
        group.MapDelete("/meal-plans/{weekStartDate}", DeleteMealPlanAsync);

        return group;
    }

    private static async Task<IResult> GetCurrentMealPlanAsync(
        HttpContext httpContext,
        IMealPlanService mealPlanService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return MealPlanProblemDetails.MissingRequiredUserClaims();
        }

        var plan = await mealPlanService.GetCurrentAsync(userContext.FamilyId, cancellationToken);

        return plan is null
            ? MealPlanProblemDetails.NoPlanExists()
            : Results.Ok(plan);
    }

    private static async Task<IResult> GetMealPlanHistoryAsync(
        HttpContext httpContext,
        IMealPlanService mealPlanService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return MealPlanProblemDetails.MissingRequiredUserClaims();
        }

        var history = await mealPlanService.GetHistoryAsync(userContext.FamilyId, cancellationToken: cancellationToken);

        return Results.Ok(history);
    }

    private static async Task<IResult> GetSwapOptionsAsync(
        HttpContext httpContext,
        string weekStartDate,
        string? day,
        string? mealType,
        int? limit,
        IMealPlanService mealPlanService,
        IDependentProfileService dependentProfileService,
        IDynamoDbRepository<UserProfileDocument> profileRepository,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return MealPlanProblemDetails.MissingRequiredUserClaims();
        }

        if (string.IsNullOrWhiteSpace(day) || string.IsNullOrWhiteSpace(mealType))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["day"] = string.IsNullOrWhiteSpace(day) ? ["day query parameter is required."] : [],
                ["mealType"] = string.IsNullOrWhiteSpace(mealType) ? ["mealType query parameter is required."] : []
            }.Where(kvp => kvp.Value.Length > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        var profileContext = await BuildFamilyProfileContextAsync(
            userContext.Sub, userContext.FamilyId, profileRepository, dependentProfileService, cancellationToken);

        var suggestions = await mealPlanService.SuggestSwapOptionsAsync(
            userContext.FamilyId,
            weekStartDate,
            day,
            mealType,
            limit ?? 5,
            cancellationToken,
            profileContext: profileContext);

        return Results.Ok(suggestions);
    }

    private static async Task<string?> BuildFamilyProfileContextAsync(
        string userId,
        string familyId,
        IDynamoDbRepository<UserProfileDocument> profileRepository,
        IDependentProfileService dependentProfileService,
        CancellationToken cancellationToken)
    {
        try
        {
            var profileTask = profileRepository.GetAsync(new DynamoDbKey($"USER#{userId}", "PROFILE"), cancellationToken);
            var dependentsTask = dependentProfileService.ListByFamilyAsync(familyId, cancellationToken);
            await Task.WhenAll(profileTask, dependentsTask);

            var profile = await profileTask;
            var dependents = await dependentsTask;
            var parts = new List<string>();

            if (profile is not null)
            {
                if (profile.DietaryPrefs.Count > 0)
                    parts.Add($"Adult dietary prefs: {string.Join(", ", profile.DietaryPrefs)}");
                if (profile.Allergies.Count > 0)
                    parts.Add($"Adult allergies: {string.Join(", ", profile.Allergies.Select(a => a.Allergen))}");
                if (profile.ExcludedIngredients.Count > 0)
                    parts.Add($"Excluded ingredients: {string.Join(", ", profile.ExcludedIngredients)}");
            }

            foreach (var dep in dependents)
            {
                var depParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(dep.AgeGroup)) depParts.Add($"age {dep.AgeGroup}");
                if (dep.Allergies.Count > 0) depParts.Add($"allergies: {string.Join(", ", dep.Allergies.Select(a => a.Allergen))}");
                if (dep.AvoidedFoods.Count > 0) depParts.Add($"avoids: {string.Join(", ", dep.AvoidedFoods)}");
                if (dep.PreferredFoods.Count > 0) depParts.Add($"prefers: {string.Join(", ", dep.PreferredFoods)}");
                if (!string.IsNullOrWhiteSpace(dep.EatingStyle)) depParts.Add(dep.EatingStyle);
                if (depParts.Count > 0)
                    parts.Add($"{dep.Name} ({string.Join("; ", depParts)})");
            }

            return parts.Count > 0 ? string.Join(". ", parts) : null;
        }
        catch
        {
            // Profile context is best-effort; do not fail the suggestions call.
            return null;
        }
    }

    private static async Task<IResult> GetMealPlanByWeekAsync(
        HttpContext httpContext,
        string weekStartDate,
        IMealPlanService mealPlanService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return MealPlanProblemDetails.MissingRequiredUserClaims();
        }

        var plan = await mealPlanService.GetByWeekAsync(userContext.FamilyId, weekStartDate, cancellationToken);

        return plan is null
            ? MealPlanProblemDetails.PlanNotFound(weekStartDate)
            : Results.Ok(plan);
    }

    private static async Task<IResult> CreateMealPlanAsync(
        HttpContext httpContext,
        CreateMealPlanRequest request,
        IValidator<CreateMealPlanRequest> validator,
        IMealPlanService mealPlanService,
        IGroceryListService groceryListService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return MealPlanProblemDetails.MissingRequiredUserClaims();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var existing = await mealPlanService.GetByWeekAsync(userContext.FamilyId, request.WeekStartDate, cancellationToken);
        if (existing is not null)
        {
            return MealPlanProblemDetails.PlanAlreadyExists(request.WeekStartDate);
        }

        var plan = await mealPlanService.CreateAsync(userContext.FamilyId, userContext.Sub, request, cancellationToken);
        await groceryListService.GenerateAsync(
            userContext.FamilyId,
            userContext.Sub,
            userContext.Name,
            new GenerateGroceryListRequest { WeekStartDate = request.WeekStartDate, ClearExisting = false },
            cancellationToken);

        return Results.Created($"/api/meal-plans/{plan.WeekStartDate}", plan);
    }

    private static async Task<IResult> GenerateMealPlanAsync(
        HttpContext httpContext,
        GenerateMealPlanRequest request,
        IValidator<GenerateMealPlanRequest> validator,
        IMealPlanService mealPlanService,
        IGroceryListService groceryListService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return MealPlanProblemDetails.MissingRequiredUserClaims();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        if (!request.ReplaceExisting)
        {
            var existing = await mealPlanService.GetByWeekAsync(userContext.FamilyId, request.WeekStartDate, cancellationToken);
            if (existing is not null)
            {
                return MealPlanProblemDetails.PlanAlreadyExists(request.WeekStartDate);
            }
        }

        var plan = await mealPlanService.GenerateAsync(userContext.FamilyId, userContext.Sub, request, cancellationToken);
        await groceryListService.GenerateAsync(
            userContext.FamilyId,
            userContext.Sub,
            userContext.Name,
            new GenerateGroceryListRequest { WeekStartDate = request.WeekStartDate, ClearExisting = false },
            cancellationToken);

        return Results.Created($"/api/meal-plans/{plan.WeekStartDate}", plan);
    }

    private static async Task<IResult> UpdateMealPlanAsync(
        HttpContext httpContext,
        string weekStartDate,
        UpdateMealPlanRequest request,
        IValidator<UpdateMealPlanRequest> validator,
        IMealPlanService mealPlanService,
        IGroceryListService groceryListService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return MealPlanProblemDetails.MissingRequiredUserClaims();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var updated = await mealPlanService.UpdateAsync(userContext.FamilyId, weekStartDate, request, cancellationToken);

        if (updated is not null)
        {
            await groceryListService.GenerateAsync(
                userContext.FamilyId,
                userContext.Sub,
                userContext.Name,
                new GenerateGroceryListRequest { WeekStartDate = weekStartDate, ClearExisting = false },
                cancellationToken);
        }

        return updated is null
            ? MealPlanProblemDetails.PlanNotFound(weekStartDate)
            : Results.Ok(updated);
    }

    private static async Task<IResult> DeleteMealPlanAsync(
        HttpContext httpContext,
        string weekStartDate,
        IMealPlanService mealPlanService,
        IGroceryListService groceryListService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return MealPlanProblemDetails.MissingRequiredUserClaims();
        }

        var deleted = await mealPlanService.DeleteAsync(userContext.FamilyId, weekStartDate, cancellationToken);

        if (deleted)
        {
            await groceryListService.GenerateAsync(
                userContext.FamilyId,
                userContext.Sub,
                userContext.Name,
                new GenerateGroceryListRequest { ClearExisting = false },
                cancellationToken);
        }

        return deleted ? Results.NoContent() : MealPlanProblemDetails.PlanNotFound(weekStartDate);
    }
}
