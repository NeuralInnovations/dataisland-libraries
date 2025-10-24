using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Sentry.AspNetCore;

namespace Dataisland.Sentry;

public static class SentryExtensions
{
    public static IWebHostBuilder UseSentry(
        this WebApplicationBuilder builder,
        string serverName,
        Action<SentryAspNetCoreOptions>? configureOptions = null
    )
    {
        var options = builder.Configuration.GetSection("Sentry").Get<SentryOptions>()!;
        return builder.WebHost.UseSentry(sentry =>
        {
            sentry.Dsn = options.Dsn;
            sentry.TracesSampleRate = 1;
            sentry.ServerName = serverName;
            sentry.AttachStacktrace = true;
            sentry.Environment = options.Environment;
            sentry.Debug = false;

            configureOptions?.Invoke(sentry);
        });
    }
}