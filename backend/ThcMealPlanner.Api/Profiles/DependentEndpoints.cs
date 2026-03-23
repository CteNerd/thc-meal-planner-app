using FluentValidation;
using ThcMealPlanner.Api.Authentication;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Api.Profiles;

public static class DependentEndpoints
{
    public static RouteGroupBuilder MapDependentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/family/dependents", GetDependentsAsync);
        group.MapPost("/family/dependents", CreateDependentAsync);
        group.MapPut("/family/dependents/{userId}", UpdateDependentAsync);
        group.MapDelete("/family/dependents/{userId}", DeleteDependentAsync);

        return group;
    }

    private static async Task<IResult> GetDependentsAsync(
        HttpContext httpContext,
        IDependentProfileService dependentProfileService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Missing required user claims.");
        }

        if (!userContext.IsHeadOfHousehold)
        {
            return ForbiddenProblem();
        }

        var dependents = await dependentProfileService.ListByFamilyAsync(userContext.FamilyId, cancellationToken);

        return Results.Ok(dependents);
    }

    private static async Task<IResult> CreateDependentAsync(
        HttpContext httpContext,
        CreateDependentRequest request,
        IValidator<CreateDependentRequest> validator,
        IDependentProfileService dependentProfileService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Missing required user claims.");
        }

        if (!userContext.IsHeadOfHousehold)
        {
            return ForbiddenProblem();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var dependent = await dependentProfileService.CreateAsync(userContext.FamilyId, request, cancellationToken);

        return Results.Created($"/api/family/dependents/{dependent.UserId}", dependent);
    }

    private static async Task<IResult> UpdateDependentAsync(
        HttpContext httpContext,
        string userId,
        UpdateDependentRequest request,
        IValidator<UpdateDependentRequest> validator,
        IDependentProfileService dependentProfileService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Missing required user claims.");
        }

        if (!userContext.IsHeadOfHousehold)
        {
            return ForbiddenProblem();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var updated = await dependentProfileService.UpdateAsync(userContext.FamilyId, userId, request, cancellationToken);
        if (updated is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Dependent not found.");
        }

        return Results.Ok(updated);
    }

    private static async Task<IResult> DeleteDependentAsync(
        HttpContext httpContext,
        string userId,
        IDependentProfileService dependentProfileService,
        CancellationToken cancellationToken)
    {
        var userContext = AuthenticatedUserContextResolver.TryResolve(httpContext.User);
        if (userContext is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Missing required user claims.");
        }

        if (!userContext.IsHeadOfHousehold)
        {
            return ForbiddenProblem();
        }

        var deleted = await dependentProfileService.DeleteAsync(userContext.FamilyId, userId, cancellationToken);
        if (!deleted)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Dependent not found.");
        }

        return Results.NoContent();
    }

    private static Dictionary<string, string[]> ToDictionary(this FluentValidation.Results.ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                grouping => grouping.Key,
                grouping => grouping.Select(error => error.ErrorMessage).ToArray());
    }

    private static IResult ForbiddenProblem()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden",
            detail: "This action requires head_of_household role.");
    }
}