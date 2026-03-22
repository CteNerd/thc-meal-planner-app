namespace ThcMealPlanner.Api.Authentication;

public sealed class CognitoAuthenticationOptions
{
    public const string SectionName = "Authentication:Cognito";

    public string Region { get; init; } = "us-east-1";

    public string UserPoolId { get; init; } = "placeholder-user-pool-id";

    public string ClientId { get; init; } = "placeholder-client-id";

    public string GetAuthority()
    {
        return $"https://cognito-idp.{Region}.amazonaws.com/{UserPoolId}";
    }
}