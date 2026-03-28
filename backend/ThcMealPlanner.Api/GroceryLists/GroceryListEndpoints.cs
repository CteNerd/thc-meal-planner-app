using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ThcMealPlanner.Api.Authentication;

namespace ThcMealPlanner.Api.GroceryLists;

public static class GroceryListEndpoints
{
    public static RouteGroupBuilder MapGroceryListEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/grocery-lists/current", GetCurrentAsync);
        group.MapPost("/grocery-lists/generate", GenerateAsync);
        group.MapPut("/grocery-lists/items/{itemId}/toggle", ToggleItemAsync);
        group.MapPost("/grocery-lists/items", AddItemAsync);
        group.MapDelete("/grocery-lists/items/{itemId}", RemoveItemAsync);
        group.MapPut("/grocery-lists/items/{itemId}/in-stock", SetInStockAsync);
        group.MapGet("/grocery-lists/poll", PollAsync);

        return group;
    }

    private static async Task<IResult> GetCurrentAsync(
        HttpContext httpContext,
        IGroceryListService groceryListService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return GroceryListProblemDetails.MissingRequiredUserClaims();
        }

        var list = await groceryListService.GetCurrentAsync(userContext.FamilyId, cancellationToken);
        return list is null ? GroceryListProblemDetails.ActiveListNotFound() : Results.Ok(list);
    }

    private static async Task<IResult> GenerateAsync(
        HttpContext httpContext,
        GenerateGroceryListRequest request,
        IValidator<GenerateGroceryListRequest> validator,
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

        var list = await groceryListService.GenerateAsync(
            userContext.FamilyId,
            userContext.Sub,
            userContext.Name,
            request,
            cancellationToken);

        return Results.Created("/api/grocery-lists/current", list);
    }

    private static async Task<IResult> ToggleItemAsync(
        HttpContext httpContext,
        string itemId,
        ToggleGroceryItemRequest request,
        IValidator<ToggleGroceryItemRequest> validator,
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

        var result = await groceryListService.ToggleItemAsync(
            userContext.FamilyId,
            itemId,
            userContext.Sub,
            userContext.Name,
            request,
            cancellationToken);

        return result.Status switch
        {
            GroceryItemMutationStatus.NotFoundList => GroceryListProblemDetails.ActiveListNotFound(),
            GroceryItemMutationStatus.NotFoundItem => GroceryListProblemDetails.ItemNotFound(itemId),
            GroceryItemMutationStatus.Conflict => GroceryListProblemDetails.VersionConflict(),
            _ => Results.Ok(new GroceryItemMutationResponse
            {
                Item = result.Item!,
                Version = result.List!.Version,
                UpdatedAt = result.List.UpdatedAt,
                Progress = result.List.Progress
            })
        };
    }

    private static async Task<IResult> AddItemAsync(
        HttpContext httpContext,
        AddGroceryItemRequest request,
        IValidator<AddGroceryItemRequest> validator,
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

        var result = await groceryListService.AddItemAsync(userContext.FamilyId, request, cancellationToken);

        return result.Status switch
        {
            GroceryItemMutationStatus.NotFoundList => GroceryListProblemDetails.ActiveListNotFound(),
            GroceryItemMutationStatus.Conflict => GroceryListProblemDetails.VersionConflict(),
            _ => Results.Created($"/api/grocery-lists/items/{result.Item!.Id}", new GroceryItemMutationResponse
            {
                Item = result.Item,
                Version = result.List!.Version,
                UpdatedAt = result.List.UpdatedAt,
                Progress = result.List.Progress
            })
        };
    }

    private static async Task<IResult> SetInStockAsync(
        HttpContext httpContext,
        string itemId,
        SetInStockRequest request,
        IValidator<SetInStockRequest> validator,
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

        var result = await groceryListService.SetInStockAsync(userContext.FamilyId, itemId, request, cancellationToken);

        return result.Status switch
        {
            GroceryItemMutationStatus.NotFoundList => GroceryListProblemDetails.ActiveListNotFound(),
            GroceryItemMutationStatus.NotFoundItem => GroceryListProblemDetails.ItemNotFound(itemId),
            GroceryItemMutationStatus.Conflict => GroceryListProblemDetails.VersionConflict(),
            _ => Results.Ok(new GroceryItemMutationResponse
            {
                Item = result.Item!,
                Version = result.List!.Version,
                UpdatedAt = result.List.UpdatedAt,
                Progress = result.List.Progress
            })
        };
    }

    private static async Task<IResult> RemoveItemAsync(
        HttpContext httpContext,
        string itemId,
        [AsParameters] RemoveGroceryItemRequest request,
        IValidator<RemoveGroceryItemRequest> validator,
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

        var result = await groceryListService.RemoveItemAsync(userContext.FamilyId, itemId, request, cancellationToken);

        return result.Status switch
        {
            GroceryItemMutationStatus.NotFoundList => GroceryListProblemDetails.ActiveListNotFound(),
            GroceryItemMutationStatus.NotFoundItem => GroceryListProblemDetails.ItemNotFound(itemId),
            GroceryItemMutationStatus.Conflict => GroceryListProblemDetails.VersionConflict(),
            _ => Results.NoContent()
        };
    }

    private static async Task<IResult> PollAsync(
        HttpContext httpContext,
        DateTimeOffset? since,
        IGroceryListService groceryListService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return GroceryListProblemDetails.MissingRequiredUserClaims();
        }

        var result = await groceryListService.PollAsync(userContext.FamilyId, since, cancellationToken);

        return result.Status switch
        {
            GroceryListPollStatus.NotFound => GroceryListProblemDetails.ActiveListNotFound(),
            GroceryListPollStatus.NoChanges => Results.StatusCode(StatusCodes.Status304NotModified),
            _ => Results.Ok(result.Response!)
        };
    }
}
