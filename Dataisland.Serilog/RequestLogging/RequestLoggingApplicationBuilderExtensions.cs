using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace Dataisland.Serilog.RequestLogging;

public static class RequestLoggingApplicationBuilderExtensions
{
    public static IApplicationBuilder UseConfiguredSerilogRequestLogging(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        app.UseMiddleware<SerilogMiddleware>();
        
        var section = configuration.GetSection("Serilog:RequestLogging");
        var cfg = section.Get<RequestLoggingConfig>() ?? new RequestLoggingConfig();

        Log.Information("[Serilog.RequestLogging] Applying config: DefaultLevel={DefaultLevel}, PathRules={RulesCount}", cfg.DefaultLevel, cfg.PathLevels.Count);

        var parsedRules = cfg.PathLevels
            .Where(r => !string.IsNullOrWhiteSpace(r.Path))
            .Select(r => new ParsedRule(
                r.Path!,
                r.PrefixMatch,
                Enum.TryParse<LogEventLevel>(r.Level, true, out var lvl) ? lvl : LogEventLevel.Information))
            .ToList();

        var defaultLevel = Enum.TryParse<LogEventLevel>(cfg.DefaultLevel, true, out var d)
            ? d
            : LogEventLevel.Information;

        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                var path = httpContext.Request.Path;
                foreach (var rule in parsedRules)
                {
                    if (rule.PrefixMatch)
                    {
                        if (path.StartsWithSegments(rule.Path, StringComparison.OrdinalIgnoreCase))
                            return rule.Level;
                    }
                    else
                    {
                        if (path.Equals(rule.Path, StringComparison.OrdinalIgnoreCase))
                            return rule.Level;
                    }
                }
                return defaultLevel;
            };

            options.EnrichDiagnosticContext = (diagCtx, httpContext) =>
            {
                diagCtx.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip");
                // Ensure StatusCode property exists for templates
                diagCtx.Set("StatusCode", httpContext.Response?.StatusCode);
                if (httpContext.Items.TryGetValue("RequestBody", out var req) && req is string reqStr)
                    diagCtx.Set("RequestBody", reqStr);
                if (httpContext.Items.TryGetValue("ResponseBody", out var resp) && resp is string respStr)
                    diagCtx.Set("ResponseBody", respStr);
            };
        });

        return app;
    }

    private sealed record ParsedRule(string Path, bool PrefixMatch, LogEventLevel Level);
}
