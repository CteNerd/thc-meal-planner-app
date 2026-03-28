using Microsoft.AspNetCore.Mvc;

namespace ThcMealPlanner.Api.MealPlans;

internal static class MealPlanProblemDetails
{
    public static IResult MissingRequiredUserClaims() =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Authentication required",
            detail: "Required user claims are missing.");

    public static IResult PlanNotFound(string weekStartDate) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Meal plan not found",
            detail: $"No meal plan exists for week starting {weekStartDate}.");

    public static IResult NoPlanExists() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "No active meal plan",
            detail: "The family does not have an active meal plan.");

    public static IResult PlanAlreadyExists(string weekStartDate) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Meal plan already exists",
            detail: $"A meal plan already exists for week starting {weekStartDate}. Use PUT to update it, or set replaceExisting to true when generating.");
}
