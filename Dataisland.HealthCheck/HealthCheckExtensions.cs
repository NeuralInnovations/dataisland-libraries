using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Dataisland.HealthCheck;

public static class HealthCheckExtensions
{
    /// <summary>
    /// Maps three probes, because liveness and readiness answer different questions and used to
    /// share one endpoint that checked Mongo, Redis, RabbitMQ and Elasticsearch. A blip in any of
    /// them failed liveness too, and the kubelet restarted a process that was perfectly healthy and
    /// would have recovered on its own (governance audit 2026-09-24, G14).
    ///
    ///   /health/live  — the process is up and serving. No dependency is consulted: nothing a
    ///                   restart could fix is being asked about.
    ///   /health/ready — the dependencies tagged "readiness". Failing this takes the pod out of the
    ///                   service's endpoints and puts it back when the dependency returns.
    ///   /health       — everything, unchanged. Other clusters and dashboards still probe it.
    ///
    /// Order matters: UseHealthChecks branches on a path PREFIX, so a "/health" registered first
    /// would swallow both of the others.
    /// </summary>
    public static IApplicationBuilder UseHealthChecks(this IApplicationBuilder app)
    {
        app.UseHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        app.UseHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadinessTag)
        });

        return app.UseHealthChecks("/health");
    }

    /// <summary>The tag a check carries when its failure should stop traffic but not the process.</summary>
    public const string ReadinessTag = "readiness";
}
