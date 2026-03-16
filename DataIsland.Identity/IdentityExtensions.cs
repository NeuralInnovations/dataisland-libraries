using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataIsland.Identity;

public static class IdentityExtensions
{
    public static IServiceCollection AddDataIslandIdentity(
        this IServiceCollection services, IConfiguration configuration)
    {
        var isDebugAuth = configuration.GetValue<bool>("Auth:Debug");
        var debugSecret = configuration["DataIslandAuth:DebugSecret"];

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

        var environment = configuration["ASPNETCORE_ENVIRONMENT"]
                         ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                         ?? "Production";
        var isDevelopment = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);

        if (isDebugAuth && !isDevelopment)
        {
            // Silently ignore debug auth in non-Development environments
            isDebugAuth = false;
        }

        if (isDebugAuth && !string.IsNullOrEmpty(debugSecret))
        {
            authBuilder.AddScheme<AuthenticationSchemeOptions, DebugAuthHandler>(
                DebugAuthHandler.SchemeName, _ => { });

            services.AddSingleton(new DebugAuthSecret(debugSecret));

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

public record DebugAuthSecret(string Secret);
