using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

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
                optionsBuilder.MapInboundClaims = false;
                optionsBuilder.Authority = authority;
                optionsBuilder.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = true,
                    ValidAudience = options.ClientId,
                    AudienceValidator = (audiences, securityToken, _) =>
                    {
                        if (audiences.Any(audience =>
                            string.Equals(audience, options.ClientId, StringComparison.Ordinal)))
                        {
                            return true;
                        }

                        if (securityToken is JwtSecurityToken jwt)
                        {
                            var clientId = jwt.Claims
                                .FirstOrDefault(claim => string.Equals(claim.Type, "client_id", StringComparison.OrdinalIgnoreCase))
                                ?.Value;

                            return string.Equals(clientId, options.ClientId, StringComparison.Ordinal);
                        }

                        return false;
                    },
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "name"
                };
            });

        return services;
    }
}