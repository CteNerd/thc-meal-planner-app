using FluentValidation;
using ThcMealPlanner.Api.Authentication;

namespace ThcMealPlanner.Api.Recipes;

public static class RecipeEndpoints
{
    public static RouteGroupBuilder MapRecipeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/recipes/favorites", ListFavoritesAsync);
        group.MapPost("/recipes/{recipeId}/favorite", AddFavoriteAsync);
        group.MapDelete("/recipes/{recipeId}/favorite", RemoveFavoriteAsync);

        group.MapGet("/recipes", ListRecipesAsync);
        group.MapGet("/recipes/{recipeId}", GetRecipeAsync);
        group.MapPost("/recipes", CreateRecipeAsync);
        group.MapPut("/recipes/{recipeId}", UpdateRecipeAsync);
        group.MapDelete("/recipes/{recipeId}", DeleteRecipeAsync);

        return group;
    }

    private static async Task<IResult> ListRecipesAsync(
        HttpContext httpContext,
        IRecipeService recipeService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return RecipeProblemDetails.MissingRequiredUserClaims();
        }

        var recipes = await recipeService.ListByFamilyAsync(userContext.FamilyId, cancellationToken);

        return Results.Ok(recipes);
    }

    private static async Task<IResult> GetRecipeAsync(
        HttpContext httpContext,
        string recipeId,
        IRecipeService recipeService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return RecipeProblemDetails.MissingRequiredUserClaims();
        }

        var recipe = await recipeService.GetByIdAsync(userContext.FamilyId, recipeId, cancellationToken);

        return recipe is null ? RecipeProblemDetails.RecipeNotFound() : Results.Ok(recipe);
    }

    private static async Task<IResult> CreateRecipeAsync(
        HttpContext httpContext,
        CreateRecipeRequest request,
        IValidator<CreateRecipeRequest> validator,
        IRecipeService recipeService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return RecipeProblemDetails.MissingRequiredUserClaims();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var created = await recipeService.CreateAsync(userContext.FamilyId, userContext.Sub, request, cancellationToken);

        return Results.Created($"/api/recipes/{created.RecipeId}", created);
    }

    private static async Task<IResult> UpdateRecipeAsync(
        HttpContext httpContext,
        string recipeId,
        UpdateRecipeRequest request,
        IValidator<UpdateRecipeRequest> validator,
        IRecipeService recipeService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return RecipeProblemDetails.MissingRequiredUserClaims();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var updated = await recipeService.UpdateAsync(userContext.FamilyId, recipeId, request, cancellationToken);

        return updated is null ? RecipeProblemDetails.RecipeNotFound() : Results.Ok(updated);
    }

    private static async Task<IResult> DeleteRecipeAsync(
        HttpContext httpContext,
        string recipeId,
        IRecipeService recipeService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return RecipeProblemDetails.MissingRequiredUserClaims();
        }

        var deleted = await recipeService.DeleteAsync(userContext.FamilyId, recipeId, cancellationToken);

        return deleted ? Results.NoContent() : RecipeProblemDetails.RecipeNotFound();
    }

    private static async Task<IResult> AddFavoriteAsync(
        HttpContext httpContext,
        string recipeId,
        FavoriteRecipeRequest request,
        IValidator<FavoriteRecipeRequest> validator,
        IRecipeService recipeService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return RecipeProblemDetails.MissingRequiredUserClaims();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var favorite = await recipeService.AddFavoriteAsync(
            userContext.FamilyId,
            userContext.Sub,
            recipeId,
            request,
            cancellationToken);

        return favorite is null ? RecipeProblemDetails.RecipeNotFound() : Results.Ok(favorite);
    }

    private static async Task<IResult> RemoveFavoriteAsync(
        HttpContext httpContext,
        string recipeId,
        IRecipeService recipeService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return RecipeProblemDetails.MissingRequiredUserClaims();
        }

        await recipeService.RemoveFavoriteAsync(userContext.Sub, recipeId, cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> ListFavoritesAsync(
        HttpContext httpContext,
        string? category,
        IRecipeService recipeService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return RecipeProblemDetails.MissingRequiredUserClaims();
        }

        var favorites = await recipeService.ListFavoritesAsync(userContext.Sub, category, cancellationToken);

        return Results.Ok(favorites);
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
