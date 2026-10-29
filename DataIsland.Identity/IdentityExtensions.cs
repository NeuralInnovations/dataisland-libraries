using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataIsland.Identity;

public static class IdentityExtensions
{
    /// <param name="debugAuth">
    /// The debug-auth decision, when the caller has already made it — a host knows its environment
    /// name better than this method can guess it, and the policies that list the scheme must be
    /// built from the SAME decision that registers it. Null evaluates it here.
    /// </param>
    public static IServiceCollection AddDataIslandIdentity(
        this IServiceCollection services,
        IConfiguration configuration,
        DebugAuthDecision? debugAuth = null)
    {
        debugAuth ??= DebugAuthGate.Evaluate(configuration);
        services.AddSingleton(debugAuth);

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        authBuilder.AddJwtBearer(options =>
        {
            var authority = configuration["Identity:Authority"];
            if (!string.IsNullOrEmpty(authority))
                options.Authority = authority;

            var audience = configuration["Identity:Audience"];
            if (!string.IsNullOrEmpty(audience))
            {
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidAudience = audience;
            }
            else
            {
                options.TokenValidationParameters.ValidateAudience = false;
            }

            options.TokenValidationParameters.ValidateLifetime = true;
        });

        if (debugAuth.Enabled)
        {
            authBuilder.AddScheme<AuthenticationSchemeOptions, DebugAuthHandler>(
                DebugAuthHandler.SchemeName, _ => { });

            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
                    JwtBearerDefaults.AuthenticationScheme,
                    DebugAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .Build();
            });
        }
        else
        {
            services.AddAuthorization();
        }

        return services;
    }

    public static IServiceCollection AddTokenValidators(this IServiceCollection services)
    {
        return services;
    }
}

