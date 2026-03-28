namespace ThcMealPlanner.Api.Chat;

internal static class ChatProblemDetails
{
    public static IResult MissingRequiredUserClaims() =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Authentication required",
            detail: "Required user claims are missing.");
}
