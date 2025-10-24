using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dataisland.HealthCheck;

/// <summary>
/// A trivial health check that always reports Healthy. Useful for simple liveness checks.
/// </summary>
public sealed class OkHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Always return Healthy. Message kept short for minimal overhead.
        return Task.FromResult(HealthCheckResult.Healthy("ok"));
    }
}

public static class OkHealthCheckBuilderExtensions
{
    /// <summary>
    /// Registers the always-healthy OkHealthCheck. Call after AddHealthChecks():
    /// services.AddHealthChecks().AddOkHealthCheck();
    /// </summary>
    public static IHealthChecksBuilder AddOkHealthCheck(this IHealthChecksBuilder builder, string name = "ok")
    {
        // Tag as liveness so it can be filtered later if readiness checks are added.
        return builder.AddCheck<OkHealthCheck>(name, tags: new[] { "liveness" });
    }
}

