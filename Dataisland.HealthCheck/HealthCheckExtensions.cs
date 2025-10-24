using Microsoft.AspNetCore.Builder;

namespace Dataisland.HealthCheck;

public static class HealthCheckExtensions
{
    public static IApplicationBuilder UseHealthChecks(this IApplicationBuilder app)
    {
        return app.UseHealthChecks("/health");
    }
}