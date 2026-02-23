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
        var isDevEnvironment = configuration.GetValue<bool>("Service:IsDevEnvironment");
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

            options.TokenValidationParameters.ValidateAudience = false;
        });

        if (isDevEnvironment && !string.IsNullOrEmpty(debugSecret))
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
