using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ThcMealPlanner.Api.Authentication;

public static class CognitoAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddCognitoAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CognitoAuthenticationOptions>(
            configuration.GetSection(CognitoAuthenticationOptions.SectionName));

        var options = configuration.GetSection(CognitoAuthenticationOptions.SectionName)
            .Get<CognitoAuthenticationOptions>() ?? new CognitoAuthenticationOptions();

        var authority = options.GetAuthority();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(optionsBuilder =>
            {
                optionsBuilder.Authority = authority;
                optionsBuilder.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = true,
                    ValidAudience = options.ClientId,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "name"
                };
            });

        return services;
    }
}