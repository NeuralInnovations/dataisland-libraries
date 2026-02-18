using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataIsland.Identity;

public static class IdentityExtensions
{
    public static IServiceCollection AddDataIslandIdentity(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication()
            .AddJwtBearer(options =>
            {
                var authority = configuration["Identity:Authority"];
                if (!string.IsNullOrEmpty(authority))
                    options.Authority = authority;

                options.TokenValidationParameters.ValidateAudience = false;
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddTokenValidators(this IServiceCollection services)
    {
        return services;
    }
}
