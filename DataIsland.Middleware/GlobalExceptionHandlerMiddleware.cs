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

            if (context.Response.StatusCode >= StatusCodes.Status400BadRequest
                && !HasResponseBody(context.Response))
            {
                await ApiErrorResponses.WriteAsync(
                    context,
                    context.Response.StatusCode,
                    cancellationToken: context.RequestAborted);
            }
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "API error {ErrorCode}: {Message}", ex.ErrorCode, ex.Message);
            await ApiErrorResponses.WriteAsync(
                context,
                ex.StatusCode,
                new ApiErrorResponse(ex.Message, ex.ErrorCode),
                context.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            await ApiErrorResponses.WriteAsync(
                context,
                StatusCodes.Status401Unauthorized,
                new ApiErrorResponse("Unauthorized", "UNAUTHORIZED"),
                context.RequestAborted);
        }
        catch (FormatException ex)
        {
            logger.LogWarning(ex, "Invalid format: {Message}", ex.Message);
            await ApiErrorResponses.WriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                new ApiErrorResponse("Invalid request format", "INVALID_FORMAT"),
                context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request cancelled by client");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await ApiErrorResponses.WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                new ApiErrorResponse("An unexpected error occurred", "INTERNAL_ERROR"),
                context.RequestAborted);
        }
    }

    private static bool HasResponseBody(HttpResponse response)
    {
        if (response.HasStarted || response.ContentLength is > 0)
            return true;
        return response.Body.CanSeek && response.Body.Length > 0;
    }
}
