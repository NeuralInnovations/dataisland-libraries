using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dataisland.Elasticsearch;

public class ElasticsearchHealthCheck(IElasticClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var isAlive = await client.PingAsync(cancellationToken);

            return isAlive
                ? HealthCheckResult.Healthy("Elasticsearch ping succeeded")
                : HealthCheckResult.Unhealthy("Elasticsearch ping failed");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Elasticsearch health check failed", ex);
        }
    }
}

public static class ElasticsearchHealthCheckExtensions
{
    public static IHealthChecksBuilder AddElasticsearch(this IHealthChecksBuilder builder)
    {
        return builder.Add(new HealthCheckRegistration(
            "elasticsearch",
            sp => new ElasticsearchHealthCheck(sp.GetRequiredService<IElasticClient>()),
            failureStatus: HealthStatus.Unhealthy,
            tags: ["readiness"]));
    }
}
