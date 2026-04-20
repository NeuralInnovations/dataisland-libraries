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
    /// captures unhandled exceptions from the request pipeline. Also reconfigures the static
    /// <c>Log.Logger</c> so <c>builder.Host.UseSerilog()</c> (which uses the static logger)
    /// forwards Error+ events to Sentry too. No-op on the Sentry side when the DSN is blank;
    /// logging stays intact either way.
    /// </summary>
    public static IWebHostBuilder UseSentry(
        this WebApplicationBuilder builder,
        string serverName,
        Action<SentryAspNetCoreOptions>? configureOptions = null
    )
    {
        var options = builder.Configuration.GetSection("Sentry").Get<SentryOptions>();
        var hasDsn = options is not null && !string.IsNullOrWhiteSpace(options.Dsn);

        ReconfigureStaticLogger(builder.Configuration, options, hasDsn, serverName);

        return builder.WebHost.UseSentry(sentry =>
        {
            if (hasDsn)
            {
                sentry.Dsn = options!.Dsn;
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
    /// Host.CreateApplicationBuilder). Calls <c>SentrySdk.Init</c> when a DSN is configured and
    /// reconfigures the static <c>Log.Logger</c> to include a Sentry sink. The caller should
    /// still invoke <c>builder.Services.AddSerilog()</c> afterwards to register the logger with
    /// the DI container — this method calls it for you, so don't call it twice.
    /// </summary>
    public static IHostApplicationBuilder UseSentry(
        this IHostApplicationBuilder builder,
        string serverName)
    {
        var options = builder.Configuration.GetSection("Sentry").Get<SentryOptions>();
        var hasDsn = options is not null && !string.IsNullOrWhiteSpace(options.Dsn);

        if (hasDsn)
        {
            // Initialize the global Sentry client once. The Serilog sink below is configured
            // with InitializeSdk=false so it reuses this client rather than starting a second.
            SentrySdk.Init(sentry =>
            {
                sentry.Dsn = options!.Dsn;
                sentry.Environment = options.Environment;
                sentry.ServerName = serverName;
                sentry.TracesSampleRate = 1;
                sentry.AttachStacktrace = true;
                sentry.Debug = false;
            });
        }

        ReconfigureStaticLogger(builder.Configuration, options, hasDsn, serverName);

        // Register the static Log.Logger with DI — matches the pre-Sentry pattern
        // (services used to call this directly with no args).
        builder.Services.AddSerilog();

        return builder;
    }

    /// <summary>
    /// Rebuild the static <c>Log.Logger</c> so Serilog sees appsettings' <c>Serilog</c> section
    /// plus a guaranteed Console sink (the worker appsettings' Serilog sections define only
    /// MinimumLevel — if we relied solely on ReadFrom.Configuration we'd end up with a
    /// sink-less logger and silent startup). When a DSN is present, also append the Sentry sink.
    /// </summary>
    private static void ReconfigureStaticLogger(
        IConfiguration configuration,
        SentryOptions? sentryOptions,
        bool hasDsn,
        string serverName)
    {
        var cfg = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

        if (hasDsn)
        {
            cfg.WriteTo.Sentry(s =>
            {
                // InitializeSdk=false reuses the already-init'd SentrySdk (either from
                // Sentry.AspNetCore in the web app or from SentrySdk.Init in the worker path).
                s.InitializeSdk = false;
                s.Dsn = sentryOptions!.Dsn;
                s.Environment = sentryOptions.Environment;
                s.ServerName = serverName;
                s.MinimumEventLevel = LogEventLevel.Error;
                s.MinimumBreadcrumbLevel = LogEventLevel.Information;
            });
        }

        Log.Logger = cfg.CreateLogger();
    }
}
