using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dataisland.Elasticsearch;

public class ElasticsearchHealthCheck(ElasticsearchOptions options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = new ElasticsearchClientSettings(new Uri(options.Url));
            if (!string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password))
                settings = settings.Authentication(
                    new Elastic.Transport.BasicAuthentication(options.Username, options.Password));

            var client = new ElasticsearchClient(settings);
            var response = await client.PingAsync(cancellationToken);

            return response.IsValidResponse
                ? HealthCheckResult.Healthy("Elasticsearch ping succeeded")
                : HealthCheckResult.Unhealthy($"Elasticsearch ping failed: {response.ElasticsearchServerError?.Error?.Reason}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Elasticsearch health check failed", ex);
        }
    }
}

public static class ElasticsearchHealthCheckExtensions
{
    public static IHealthChecksBuilder AddElasticsearch(
        this IHealthChecksBuilder builder, ElasticsearchOptions options)
    {
        return builder.Add(new HealthCheckRegistration(
            "elasticsearch",
            _ => new ElasticsearchHealthCheck(options),
            failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
            tags: ["readiness"]));
    }
}
