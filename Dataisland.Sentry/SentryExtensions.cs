using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Serilog;
using Serilog;
using Serilog.Events;

namespace Dataisland.Sentry;

public static class SentryExtensions
{
    /// <summary>
    /// Wire Sentry into an ASP.NET Core service (WebApplicationBuilder). Sentry's middleware
    /// captures unhandled exceptions from the request pipeline. Also registers Serilog with a
    /// Sentry sink so log events at Error+ are forwarded as Sentry events and Information+ are
    /// attached as breadcrumbs. No-op (with a warning logged at startup) when the <c>Sentry</c>
    /// config section is missing or the DSN is blank — safe for local dev.
    /// </summary>
    public static IWebHostBuilder UseSentry(
        this WebApplicationBuilder builder,
        string serverName,
        Action<SentryAspNetCoreOptions>? configureOptions = null
    )
    {
        var options = builder.Configuration.GetSection("Sentry").Get<SentryOptions>();

        // Register Serilog with the Sentry sink. Sentry.AspNetCore's automatic capture catches
        // the request pipeline; the Serilog sink catches everything else (workers, consumers,
        // background services inside Service.Api) that logs at Error or above.
        builder.Services.AddSerilog((_, cfg) =>
            ConfigureSerilog(cfg, builder.Configuration, options, serverName, alreadyInitialized: true));

        return builder.WebHost.UseSentry(sentry =>
        {
            if (options is not null && !string.IsNullOrWhiteSpace(options.Dsn))
            {
                sentry.Dsn = options.Dsn;
                sentry.Environment = options.Environment;
                sentry.ServerName = serverName;
                sentry.TracesSampleRate = 1;
                sentry.AttachStacktrace = true;
                sentry.Debug = false;
            }

            configureOptions?.Invoke(sentry);
        });
    }

    /// <summary>
    /// Wire Sentry into a worker service (IHostApplicationBuilder — used by
    /// Host.CreateApplicationBuilder). There's no request pipeline here, so Sentry is driven
    /// entirely through the Serilog sink: Error+ logs become Sentry events, Information+
    /// logs become breadcrumbs. Replaces the service's own <c>AddSerilog()</c> call — do not
    /// call both. No-op when the DSN is missing.
    /// </summary>
    public static IHostApplicationBuilder UseSentry(
        this IHostApplicationBuilder builder,
        string serverName)
    {
        var options = builder.Configuration.GetSection("Sentry").Get<SentryOptions>();

        if (options is not null && !string.IsNullOrWhiteSpace(options.Dsn))
        {
            // Initialize the global Sentry client once. The Serilog sink below is configured
            // with InitializeSdk=false so it reuses this client rather than starting a second.
            SentrySdk.Init(sentry =>
            {
                sentry.Dsn = options.Dsn;
                sentry.Environment = options.Environment;
                sentry.ServerName = serverName;
                sentry.TracesSampleRate = 1;
                sentry.AttachStacktrace = true;
                sentry.Debug = false;
            });
        }

        builder.Services.AddSerilog((_, cfg) =>
            ConfigureSerilog(cfg, builder.Configuration, options, serverName, alreadyInitialized: true));

        return builder;
    }

    private static void ConfigureSerilog(
        LoggerConfiguration cfg,
        IConfiguration configuration,
        SentryOptions? sentryOptions,
        string serverName,
        bool alreadyInitialized)
    {
        // ReadFrom.Configuration is what the original `builder.Services.AddSerilog()` relied on
        // to pick up the appsettings.yml Serilog section (MinimumLevel overrides, etc). Keep
        // that behaviour — we only ADD a sink, we don't replace the console/enrichment pipeline.
        cfg.ReadFrom.Configuration(configuration);

        if (sentryOptions is null || string.IsNullOrWhiteSpace(sentryOptions.Dsn))
            return;

        cfg.WriteTo.Sentry(s =>
        {
            // InitializeSdk=false reuses the already-init'd SentrySdk (either from
            // Sentry.AspNetCore in the web app or from SentrySdk.Init in the worker path).
            // Double-init logs a warning and keeps the first client; explicitly setting this
            // skips the noise.
            s.InitializeSdk = !alreadyInitialized;
            s.Dsn = sentryOptions.Dsn;
            s.Environment = sentryOptions.Environment;
            s.ServerName = serverName;
            s.MinimumEventLevel = LogEventLevel.Error;
            s.MinimumBreadcrumbLevel = LogEventLevel.Information;
        });
    }
}
