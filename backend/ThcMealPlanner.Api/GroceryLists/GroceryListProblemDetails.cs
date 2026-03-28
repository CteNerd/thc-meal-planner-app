namespace ThcMealPlanner.Api.GroceryLists;

internal static class GroceryListProblemDetails
{
    public static IResult MissingRequiredUserClaims() =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Authentication required",
            detail: "Required user claims are missing.");

    public static IResult ActiveListNotFound() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Grocery list not found",
            detail: "No active grocery list exists for this family.");

    public static IResult ItemNotFound(string itemId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Grocery item not found",
            detail: $"No grocery item exists with id '{itemId}'.");

    public static IResult VersionConflict() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Version conflict",
            detail: "The grocery list has changed. Re-fetch the latest list and retry your action.");

    public static IResult PantryItemNotFound(string name) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Pantry staple not found",
            detail: $"No pantry staple exists with name '{name}'.");
}
