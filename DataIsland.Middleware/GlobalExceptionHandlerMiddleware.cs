using System.Diagnostics;
using Dataisland.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DataIsland.Middleware;

public class GlobalExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "API error {ErrorCode}: {Message}", ex.ErrorCode, ex.Message);
            await WriteErrorResponse(context, ex.StatusCode, ex.Message, ex.ErrorCode);
        }
        catch (UnauthorizedAccessException)
        {
            await WriteErrorResponse(context, 401, "Unauthorized", "UNAUTHORIZED");
        }
        catch (FormatException ex)
        {
            logger.LogWarning(ex, "Invalid format: {Message}", ex.Message);
            await WriteErrorResponse(context, 400, "Invalid request format", "INVALID_FORMAT");
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request cancelled by client");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteErrorResponse(context, 500, "An unexpected error occurred", "INTERNAL_ERROR");
        }
    }

    private static async Task WriteErrorResponse(
        HttpContext context, int statusCode, string message, string code)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var response = new
        {
            Data = (object?)null,
            Error = new ApiErrorResponse(message, code, traceId)
        };
        await context.Response.WriteAsJsonAsync(response);
    }
}
