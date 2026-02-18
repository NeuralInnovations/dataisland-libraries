using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Dataisland.Metrics;

public static class TracingExtensions
{
    public static readonly ActivitySource Source = new("Dataisland", "1.0.0");

    /// <summary>
    /// Adds OpenTelemetry distributed tracing with OTLP exporter.
    /// Configure via OTEL_EXPORTER_OTLP_ENDPOINT env var (default: http://localhost:4317).
    /// Set OTEL_TRACING_ENABLED=false to disable.
    /// </summary>
    public static IServiceCollection AddDistributedTracing(
        this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var enabled = configuration.GetValue("OTEL_TRACING_ENABLED", true);
        if (!enabled)
            return services;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource("Dataisland")
                    .AddSource("MassTransit")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return services;
    }
}
