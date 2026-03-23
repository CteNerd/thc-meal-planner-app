namespace ThcMealPlanner.Api.Recipes;

internal static class RecipeProblemDetails
{
    public static IResult MissingRequiredUserClaims()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: "Missing required user claims.");
    }

    public static IResult RecipeNotFound()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Recipe not found",
            detail: "No recipe exists for the requested id within this family.");
    }
}
