using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;

namespace Dataisland.Metrics;

public static class WorkerMetricsExtensions
{
    /// <summary>
    /// Starts a lightweight Kestrel HTTP server exposing /metrics for Prometheus scraping.
    /// Configure the port via Metrics:Port (env: Metrics__Port). Set to 0 to disable.
    /// </summary>
    public static IServiceCollection AddWorkerMetricsServer(
        this IServiceCollection services, IConfiguration configuration)
    {
        var port = configuration.GetValue<int>("Metrics:Port", 0);
        if (port > 0)
            services.AddHostedService(_ => new MetricServerHostedService(port));
        return services;
    }

    private sealed class MetricServerHostedService(int port) : IHostedService, IDisposable
    {
        private KestrelMetricServer? _server;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _server = new KestrelMetricServer(port: port);
            _server.Start();
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_server is not null)
                await _server.StopAsync();
        }

        public void Dispose() => _server?.Dispose();
    }
}
