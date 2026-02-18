using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DataIsland.Middleware;

public static class MiddlewareExtensions
{
    public static IServiceCollection AddLoggingFilters(this IServiceCollection services)
    {
        // Logging filters for request/response processing
        return services;
    }

    public static IServiceCollection AddAuditLogging(this IServiceCollection services)
    {
        services.AddSingleton<IAuditLogger, AuditLogger>();
        return services;
    }

    public static WebApplication AddHttpMiddleware(this WebApplication app)
    {
        app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
        return app;
    }
}
