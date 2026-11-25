using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Authentication;


public static class AuthenticationExtensions
{
    public static IServiceCollection AddAiWorkHubJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings =
            configuration
                .GetSection(JwtSettings.SectionName)
                .Get<JwtSettings>();

        if (settings is null)
        {
            throw new InvalidOperationException(
                "JWT configuration is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.Issuer))
        {
            throw new InvalidOperationException(
                "JWT issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            throw new InvalidOperationException(
                "JWT audience is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.SigningKey))
        {
            throw new InvalidOperationException(
                "JWT signing key is not configured.");
        }

        var signingKey =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(settings.SigningKey))
            {
                KeyId = JwtSettings.KeyId
            };

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = settings.Issuer,

                        ValidateAudience = true,
                        ValidAudience = settings.Audience,

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = signingKey,

                        ValidateLifetime = true,

                        NameClaimType =
                            AiWorkHubClaimTypes.UserId,

                        RoleClaimType =
                            AiWorkHubClaimTypes.Role,

                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
            });

        return services;
    }
}