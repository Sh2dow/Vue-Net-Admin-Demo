using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace backend.ServiceDefaults;

/// <summary>
/// Adds standard JWT Bearer authentication against an OpenID Connect provider (OpenIddict).
/// </summary>
public static class JwtBearerExtensions
{
    public static AuthenticationBuilder AddJwtBearerAuthentication(
        this IServiceCollection services,
        string authority,
        string? audience = null)
    {
        var normalizedAuthority = authority.TrimEnd('/');

        return services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = false;
                options.MapInboundClaims = false;
                options.MetadataAddress = $"{normalizedAuthority}/.well-known/openid-configuration";

                // Accept self-signed/dev certificates for metadata discovery
                options.BackchannelHttpHandler = new System.Net.Http.HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        (_, _, _, _) => true
                };

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    ValidateIssuer = true,
                    ValidIssuer = normalizedAuthority,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = "sub",
                    RoleClaimType = ClaimTypes.Role,
                };

                if (!string.IsNullOrWhiteSpace(audience))
                {
                    options.TokenValidationParameters.ValidAudiences = [audience];
                }

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = ctx =>
                    {
                        var logger = ctx.HttpContext.RequestServices
                            .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                            .CreateLogger("JwtBearer");
                        logger.LogWarning(ctx.Exception, "JWT authentication failed for {Path}", ctx.Request.Path);
                        return Task.CompletedTask;
                    }
                };
            });
    }
}
