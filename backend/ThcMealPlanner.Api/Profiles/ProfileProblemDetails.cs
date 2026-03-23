namespace ThcMealPlanner.Api.Profiles;

internal static class ProfileProblemDetails
{
    public static IResult MissingRequiredUserClaims()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: "Missing required user claims.");
    }

    public static IResult ProfileNotFound()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Profile not found",
            detail: "No profile exists for the current user.");
    }

    public static IResult DependentNotFound()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Dependent not found",
            detail: "No dependent exists for the requested user id within this family.");
    }

    public static IResult HeadOfHouseholdRequired()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden",
            detail: "This action requires head_of_household role.");
    }
}