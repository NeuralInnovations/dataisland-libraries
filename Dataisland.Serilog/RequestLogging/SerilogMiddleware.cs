using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Dataisland.Serilog.RequestLogging;

public class SerilogMiddleware(RequestDelegate next, RequestLoggingConfig config)
{
    private const int MaximumBodyLimit = 64 * 1024;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var diag = ctx.RequestServices.GetService<global::Serilog.IDiagnosticContext>();

        // Request body (JSON only)
        if (config.CaptureRequestBody)
        {
            try
            {
                if (ctx.Request.ContentLength > 0 && IsJson(ctx.Request.ContentType))
                {
                    ctx.Request.EnableBuffering();
                    using var reader = new StreamReader(
                        ctx.Request.Body,
                        System.Text.Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: false,
                        bufferSize: 1024,
                        leaveOpen: true);

                    var body = await ReadLimitedAsync(reader, config.RequestBodyLimit);
                    ctx.Request.Body.Position = 0;

                    diag?.Set("RequestBody", body);
                }
            }
            catch
            {
                // Body capture is diagnostic only and must never break request handling.
                if (ctx.Request.Body.CanSeek)
                    ctx.Request.Body.Position = 0;
            }
        }

        if (!config.CaptureResponseBody)
        {
            await next(ctx);
            return;
        }

        var originalBody = ctx.Response.Body;
        await using var mem = new MemoryStream();
        ctx.Response.Body = mem;

        try
        {
            await next(ctx);
        }
        finally
        {
            mem.Position = 0;
            string responseBody = string.Empty;
            try
            {
                using var respReader = new StreamReader(mem, System.Text.Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                responseBody = await ReadLimitedAsync(respReader, config.ResponseBodyLimit);
            }
            catch
            {
                // Body capture is diagnostic only and must never replace the application response.
            }

            diag?.Set("ResponseBody", responseBody);
            diag?.Set("StatusCode", ctx.Response.StatusCode);

            mem.Position = 0;
            ctx.Response.Body = originalBody;
            await mem.CopyToAsync(originalBody);
        }
    }

    private static bool IsJson(string? contentType) =>
        contentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true ||
        contentType?.Contains("+json", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task<string> ReadLimitedAsync(StreamReader reader, int configuredLimit)
    {
        var limit = Math.Clamp(configuredLimit, 1, MaximumBodyLimit);
        var buffer = new char[limit + 1];
        var charsRead = 0;

        while (charsRead < buffer.Length)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(charsRead, buffer.Length - charsRead));
            if (read == 0)
                break;

            charsRead += read;
        }

        return charsRead > limit
            ? new string(buffer, 0, limit) + "..."
            : new string(buffer, 0, charsRead);
    }
}
