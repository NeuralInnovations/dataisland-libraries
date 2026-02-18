using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Dataisland.MQ;

public class RabbitMqHealthCheck(RabbitMqOptions options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = options.Host,
                VirtualHost = options.VirtualHost,
                UserName = options.Username,
                Password = options.Password,
            };

            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ connection is open")
                : HealthCheckResult.Unhealthy("RabbitMQ connection is not open");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ health check failed", ex);
        }
    }
}

public static class RabbitMqHealthCheckExtensions
{
    public static IHealthChecksBuilder AddRabbitMq(
        this IHealthChecksBuilder builder, RabbitMqOptions options)
    {
        return builder.Add(new HealthCheckRegistration(
            "rabbitmq",
            _ => new RabbitMqHealthCheck(options),
            failureStatus: HealthStatus.Unhealthy,
            tags: ["readiness"]));
    }
}
