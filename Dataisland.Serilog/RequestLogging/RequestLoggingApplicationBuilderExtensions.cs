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
        var section = configuration.GetSection("Serilog:RequestLogging");
        var cfg = section.Get<RequestLoggingConfig>() ?? new RequestLoggingConfig();

        Log.Information(
            "[Serilog.RequestLogging] Applying config: DefaultLevel={DefaultLevel}, PathRules={RulesCount}, CaptureRequestBody={CaptureRequestBody}, CaptureResponseBody={CaptureResponseBody}",
            cfg.DefaultLevel,
            cfg.PathLevels.Count,
            cfg.CaptureRequestBody,
            cfg.CaptureResponseBody);

        var parsedRules = cfg.PathLevels
            .Where(r => !string.IsNullOrWhiteSpace(r.Path) || !string.IsNullOrWhiteSpace(r.Method))
            .Select(r => new ParsedRule(
                r.Path,
                r.Method,
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
                var method = httpContext.Request.Method;
                
                foreach (var rule in parsedRules)
                {
                    // Check method match if specified
                    if (!string.IsNullOrWhiteSpace(rule.Method) && 
                        !method.Equals(rule.Method, StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    // Check path match if specified
                    if (!string.IsNullOrWhiteSpace(rule.Path))
                    {
                        if (rule.PrefixMatch)
                        {
                            if (!path.StartsWithSegments(rule.Path, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                        else
                        {
                            if (!path.Equals(rule.Path, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                    }
                    
                    return rule.Level;
                }
                return defaultLevel;
            };

            options.EnrichDiagnosticContext = (diagCtx, httpContext) =>
            {
                diagCtx.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip");
                // Ensure StatusCode property exists for templates
                diagCtx.Set("StatusCode", httpContext.Response.StatusCode);
                if (httpContext.Items.TryGetValue("RequestBody", out var req) && req is string reqStr)
                    diagCtx.Set("RequestBody", reqStr);
                if (httpContext.Items.TryGetValue("ResponseBody", out var resp) && resp is string respStr)
                    diagCtx.Set("ResponseBody", respStr);
            };
        });

        // Body capture must run inside Serilog's request middleware so its diagnostic properties
        // are populated before the request completion event is emitted. It is intentionally opt-in:
        // response capture buffers the response and should only be enabled while diagnosing issues.
        if (cfg.CaptureRequestBody || cfg.CaptureResponseBody)
            app.UseMiddleware<SerilogMiddleware>(cfg);

        return app;
    }

    private sealed record ParsedRule(string? Path, string? Method, bool PrefixMatch, LogEventLevel Level);
}
