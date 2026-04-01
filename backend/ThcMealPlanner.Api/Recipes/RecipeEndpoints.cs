using FluentValidation;
using ThcMealPlanner.Api.Authentication;

namespace ThcMealPlanner.Api.Recipes;

public static class RecipeEndpoints
{
    public static RouteGroupBuilder MapRecipeEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/recipes/import-from-url", ImportRecipeFromUrlAsync);
        group.MapPost("/recipes/{recipeId}/import-from-image", ImportRecipeFromImageAsync);
        group.MapPost("/recipes/{recipeId}/upload-url", CreateUploadUrlAsync);
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

    private static async Task<IResult> ImportRecipeFromUrlAsync(
        HttpContext httpContext,
        ImportRecipeFromUrlRequest request,
        IValidator<ImportRecipeFromUrlRequest> validator,
        IRecipeImportService recipeImportService,
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

        try
        {
            var draft = await recipeImportService.ImportFromUrlAsync(request.Url, cancellationToken);
            return Results.Ok(draft);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Recipe import failed",
                detail: exception.Message);
        }
        catch (HttpRequestException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Recipe import failed",
                detail: exception.Message);
        }
    }

    private static async Task<IResult> CreateUploadUrlAsync(
        HttpContext httpContext,
        string recipeId,
        CreateRecipeUploadUrlRequest request,
        IValidator<CreateRecipeUploadUrlRequest> validator,
        IRecipeService recipeService,
        IRecipeImageUploadService recipeImageUploadService,
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

        var recipe = await recipeService.GetByIdAsync(userContext.FamilyId, recipeId, cancellationToken);
        if (recipe is null)
        {
            return RecipeProblemDetails.RecipeNotFound();
        }

        try
        {
            var response = await recipeImageUploadService.CreateUploadUrlAsync(recipeId, request, cancellationToken);
            return Results.Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Image upload unavailable",
                detail: exception.Message);
        }
    }

    private static async Task<IResult> ImportRecipeFromImageAsync(
        HttpContext httpContext,
        string recipeId,
        ImportRecipeFromImageRequest request,
        IValidator<ImportRecipeFromImageRequest> validator,
        IRecipeService recipeService,
        IRecipeImageUploadService recipeImageUploadService,
        IRecipeImportService recipeImportService,
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

        var recipe = await recipeService.GetByIdAsync(userContext.FamilyId, recipeId, cancellationToken);
        if (recipe is null)
        {
            return RecipeProblemDetails.RecipeNotFound();
        }

        var imageKey = string.IsNullOrWhiteSpace(request.ImageKey)
            ? recipe.ImageKey
            : request.ImageKey;

        if (string.IsNullOrWhiteSpace(imageKey))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Image import failed",
                detail: "Recipe image is missing. Upload a recipe image first.");
        }

        try
        {
            var readUrl = recipeImageUploadService.CreateReadUrl(imageKey);
            var draft = await recipeImportService.ImportFromImageAsync(readUrl, cancellationToken);
            return Results.Ok(draft);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Image import failed",
                detail: exception.Message);
        }
        catch (HttpRequestException exception)
        {
            var statusCode = exception.StatusCode is null
                ? StatusCodes.Status400BadRequest
                : (int)exception.StatusCode.Value;

            return Results.Problem(
                statusCode: statusCode,
                title: "Image import failed",
                detail: exception.Message);
        }
    }

    private static async Task<IResult> ListRecipesAsync(
        HttpContext httpContext,
        IRecipeService recipeService,
        IRecipeImageUploadService recipeImageUploadService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return RecipeProblemDetails.MissingRequiredUserClaims();
        }

        var recipes = await recipeService.ListByFamilyAsync(userContext.FamilyId, cancellationToken);

        return Results.Ok(recipes.Select(recipe => ToResponse(recipe, recipeImageUploadService)).ToList());
    }

    private static async Task<IResult> GetRecipeAsync(
        HttpContext httpContext,
        string recipeId,
        IRecipeService recipeService,
        IRecipeImageUploadService recipeImageUploadService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return RecipeProblemDetails.MissingRequiredUserClaims();
        }

        var recipe = await recipeService.GetByIdAsync(userContext.FamilyId, recipeId, cancellationToken);

        return recipe is null ? RecipeProblemDetails.RecipeNotFound() : Results.Ok(ToResponse(recipe, recipeImageUploadService));
    }

    private static async Task<IResult> CreateRecipeAsync(
        HttpContext httpContext,
        CreateRecipeRequest request,
        IValidator<CreateRecipeRequest> validator,
        IRecipeService recipeService,
        IRecipeImageUploadService recipeImageUploadService,
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

        return Results.Created($"/api/recipes/{created.RecipeId}", ToResponse(created, recipeImageUploadService));
    }

    private static async Task<IResult> UpdateRecipeAsync(
        HttpContext httpContext,
        string recipeId,
        UpdateRecipeRequest request,
        IValidator<UpdateRecipeRequest> validator,
        IRecipeService recipeService,
        IRecipeImageUploadService recipeImageUploadService,
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

        return updated is null ? RecipeProblemDetails.RecipeNotFound() : Results.Ok(ToResponse(updated, recipeImageUploadService));
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

    private static RecipeResponse ToResponse(RecipeDocument recipe, IRecipeImageUploadService recipeImageUploadService)
    {
        return new RecipeResponse
        {
            RecipeId = recipe.RecipeId,
            FamilyId = recipe.FamilyId,
            Name = recipe.Name,
            Description = recipe.Description,
            Category = recipe.Category,
            Cuisine = recipe.Cuisine,
            Servings = recipe.Servings,
            PrepTimeMinutes = recipe.PrepTimeMinutes,
            CookTimeMinutes = recipe.CookTimeMinutes,
            ProteinSource = recipe.ProteinSource,
            CookingMethod = recipe.CookingMethod,
            Difficulty = recipe.Difficulty,
            Tags = recipe.Tags,
            Ingredients = recipe.Ingredients,
            Instructions = recipe.Instructions,
            Nutrition = recipe.Nutrition,
            ImageKey = recipe.ImageKey,
            ImageUrl = string.IsNullOrWhiteSpace(recipe.ImageKey)
                ? null
                : recipeImageUploadService.CreateReadUrl(recipe.ImageKey, TimeSpan.FromHours(1)),
            ThumbnailKey = recipe.ThumbnailKey,
            SourceType = recipe.SourceType,
            SourceUrl = recipe.SourceUrl,
            Variations = recipe.Variations,
            StorageInfo = recipe.StorageInfo,
            CreatedByUserId = recipe.CreatedByUserId,
            CreatedAt = recipe.CreatedAt,
            UpdatedAt = recipe.UpdatedAt
        };
    }
}
