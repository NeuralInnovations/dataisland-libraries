using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Dataisland.Serilog.RequestLogging;

public class SerilogMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var diag = ctx.RequestServices.GetService<global::Serilog.IDiagnosticContext>();

        const int reqLimit = 2048;
        const int respLimit = 4096;

        // Request body (JSON only)
        try
        {
            if (ctx.Request.ContentLength > 0 && (ctx.Request.ContentType?.Contains("application/json") ?? false))
            {
                ctx.Request.EnableBuffering();
                using var reader = new StreamReader(
                    ctx.Request.Body,
                    System.Text.Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024,
                    leaveOpen: true);

                var body = await reader.ReadToEndAsync();
                ctx.Request.Body.Position = 0;

                if (body.Length > reqLimit) body = body.Substring(0, reqLimit) + "...";
                diag?.Set("RequestBody", body);
            }
        }
        catch { /* ignore body read errors */ }

        // Response body
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
                responseBody = await respReader.ReadToEndAsync();
                if (responseBody.Length > respLimit) responseBody = responseBody.Substring(0, respLimit) + "...";
            }
            catch { /* ignore body read errors */ }

            diag?.Set("ResponseBody", responseBody);
            diag?.Set("StatusCode", ctx.Response.StatusCode);

            mem.Position = 0;
            await mem.CopyToAsync(originalBody);
            ctx.Response.Body = originalBody;
        }
    }
}