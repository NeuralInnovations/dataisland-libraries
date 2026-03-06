using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Dataisland.Metrics;

/// <summary>
/// Collects <see cref="HealthCheckRegistration"/> entries added via
/// <see cref="AddWorkerHealthChecks"/> so the worker web host can re-register them.
/// </summary>
public sealed class WorkerHealthCheckRegistry
{
    internal List<Action<IHealthChecksBuilder>> Configurators { get; } = [];
}

public static class WorkerMetricsExtensions
{
    /// <summary>
    /// Returns an <see cref="IHealthChecksBuilder"/> that stores registrations for the
    /// worker metrics/health web host. Use just like <c>services.AddHealthChecks()</c>.
    /// </summary>
    public static IHealthChecksBuilder AddWorkerHealthChecks(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(WorkerHealthCheckRegistry)))
            services.AddSingleton<WorkerHealthCheckRegistry>();

        var registry = new WorkerHealthCheckRegistry();
        services.AddSingleton(registry);

        // Return a real builder so callers can chain .AddRabbitMq() etc.
        var hcBuilder = services.AddHealthChecks();
        return new DelegatingHealthChecksBuilder(hcBuilder, registry);
    }

    /// <summary>
    /// Starts a lightweight Kestrel HTTP server exposing /health and /metrics.
    /// Configure the port via Metrics:Port (env: Metrics__Port, default 9090).
    /// Set to 0 to disable.
    /// </summary>
    public static IServiceCollection AddWorkerMetricsServer(
        this IServiceCollection services, IConfiguration configuration)
    {
        var port = configuration.GetValue("Metrics:Port", 9090);
        if (port > 0)
            services.AddSingleton<IHostedService>(sp => new WorkerWebHostService(sp, port));
        return services;
    }

    /// <summary>
    /// Wraps <see cref="IHealthChecksBuilder"/> to capture registrations in the registry.
    /// </summary>
    private sealed class DelegatingHealthChecksBuilder(
        IHealthChecksBuilder inner,
        WorkerHealthCheckRegistry registry) : IHealthChecksBuilder
    {
        public IServiceCollection Services => inner.Services;

        public IHealthChecksBuilder Add(HealthCheckRegistration registration)
        {
            registry.Configurators.Add(b => b.Add(registration));
            inner.Add(registration);
            return this;
        }
    }

    private sealed class WorkerWebHostService(IServiceProvider rootProvider, int port)
        : IHostedService, IAsyncDisposable
    {
        private WebApplication? _app;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls($"http://+:{port}");
            builder.Logging.ClearProviders();

            // Copy health check registrations from the root host
            var registries = rootProvider.GetServices<WorkerHealthCheckRegistry>();
            var hcBuilder = builder.Services.AddHealthChecks();
            foreach (var reg in registries)
            foreach (var configurator in reg.Configurators)
                configurator(hcBuilder);

            // If no health checks were registered, add a basic OK check
            hcBuilder.AddCheck("self", () => HealthCheckResult.Healthy("ok"));

            _app = builder.Build();

            _app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = WriteHealthResponse
            });
            _app.MapMetrics("/metrics");

            await _app.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_app is not null)
                await _app.StopAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (_app is not null)
                await _app.DisposeAsync();
        }

        private static Task WriteHealthResponse(HttpContext ctx, HealthReport report)
        {
            ctx.Response.ContentType = "application/json";
            var status = report.Status switch
            {
                HealthStatus.Healthy => "healthy",
                HealthStatus.Degraded => "degraded",
                _ => "unhealthy"
            };

            var entries = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString().ToLowerInvariant(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            });

            var result = System.Text.Json.JsonSerializer.Serialize(new { status, entries });
            return ctx.Response.WriteAsync(result);
        }
    }
}
