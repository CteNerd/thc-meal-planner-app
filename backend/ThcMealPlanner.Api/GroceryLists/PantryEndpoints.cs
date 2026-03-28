using FluentValidation;
using ThcMealPlanner.Api.Authentication;

namespace ThcMealPlanner.Api.GroceryLists;

public static class PantryEndpoints
{
    public static RouteGroupBuilder MapPantryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/pantry/staples", GetPantryStaplesAsync);
        group.MapPut("/pantry/staples", ReplacePantryStaplesAsync);
        group.MapPost("/pantry/staples/items", AddPantryStapleAsync);
        group.MapDelete("/pantry/staples/items/{name}", DeletePantryStapleAsync);

        return group;
    }

    private static async Task<IResult> GetPantryStaplesAsync(
        HttpContext httpContext,
        IGroceryListService groceryListService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return GroceryListProblemDetails.MissingRequiredUserClaims();
        }

        var pantry = await groceryListService.GetPantryStaplesAsync(userContext.FamilyId, cancellationToken);
        return Results.Ok(pantry);
    }

    private static async Task<IResult> ReplacePantryStaplesAsync(
        HttpContext httpContext,
        ReplacePantryStaplesRequest request,
        IValidator<ReplacePantryStaplesRequest> validator,
        IGroceryListService groceryListService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return GroceryListProblemDetails.MissingRequiredUserClaims();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var pantry = await groceryListService.ReplacePantryStaplesAsync(userContext.FamilyId, request, cancellationToken);
        return Results.Ok(pantry);
    }

    private static async Task<IResult> AddPantryStapleAsync(
        HttpContext httpContext,
        AddPantryStapleItemRequest request,
        IValidator<AddPantryStapleItemRequest> validator,
        IGroceryListService groceryListService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return GroceryListProblemDetails.MissingRequiredUserClaims();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var pantry = await groceryListService.AddPantryStapleAsync(userContext.FamilyId, request, cancellationToken);
        return Results.Created("/api/pantry/staples", pantry);
    }

    private static async Task<IResult> DeletePantryStapleAsync(
        HttpContext httpContext,
        string name,
        IGroceryListService groceryListService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return GroceryListProblemDetails.MissingRequiredUserClaims();
        }

        var deleted = await groceryListService.DeletePantryStapleAsync(userContext.FamilyId, name, cancellationToken);

        return deleted
            ? Results.NoContent()
            : GroceryListProblemDetails.PantryItemNotFound(name);
    }
}
