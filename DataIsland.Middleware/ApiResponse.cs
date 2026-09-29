using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DataIsland.Middleware;

public record ApiErrorResponse(string Message, string Code, string? TraceId = null);

public record ApiResponse(object? Data, ApiErrorResponse? Error);

public record ApiErrorEnvelope(object? Data, ApiErrorResponse Error);

public record PaginatedApiResponse(object? Data, ApiErrorResponse? Error, PaginationMeta Pagination);

public record PaginationMeta(int Page, int PageSize, long Total, int TotalPages);

public static class ApiErrorResponses
{
    public static ApiErrorEnvelope Create(HttpContext context, int statusCode, object? source = null)
    {
        if (source is ApiErrorEnvelope envelope)
            return envelope;
        if (source is ApiResponse { Error: not null } response)
            return new ApiErrorEnvelope(null, response.Error);

        var error = source as ApiErrorResponse;
        var message = error?.Message ?? ReadStringProperty(source, "message");
        var code = error?.Code ?? ReadStringProperty(source, "code");

        if (source is ValidationProblemDetails validationProblem)
        {
            message ??= validationProblem.Title;
            code ??= "VALIDATION_ERROR";
        }
        else if (source is ProblemDetails problem)
        {
            message ??= problem.Detail ?? problem.Title;
        }
        else if (source is string text)
        {
            message ??= text;
        }
        else
        {
            message ??= ReadStringProperty(source, "error");
        }

        var (defaultMessage, defaultCode) = Defaults(statusCode);
        return new ApiErrorEnvelope(
            null,
            new ApiErrorResponse(
                string.IsNullOrWhiteSpace(message) ? defaultMessage : message,
                string.IsNullOrWhiteSpace(code) ? defaultCode : code,
                error?.TraceId ?? Activity.Current?.Id ?? context.TraceIdentifier));
    }

    public static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        object? source = null,
        CancellationToken cancellationToken = default)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(
            Create(context, statusCode, source),
            cancellationToken: cancellationToken);
    }

    private static string? ReadStringProperty(object? source, string propertyName)
    {
        if (source is null)
            return null;

        if (source is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            var pair = readOnlyDictionary.FirstOrDefault(x =>
                string.Equals(x.Key, propertyName, StringComparison.OrdinalIgnoreCase));
            return pair.Value as string;
        }

        if (source is IDictionary<string, object?> dictionary)
        {
            var pair = dictionary.FirstOrDefault(x =>
                string.Equals(x.Key, propertyName, StringComparison.OrdinalIgnoreCase));
            return pair.Value as string;
        }

        var property = source.GetType().GetProperties()
            .FirstOrDefault(x => string.Equals(x.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        return property?.GetValue(source) as string;
    }

    private static (string Message, string Code) Defaults(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => ("The request is invalid.", "BAD_REQUEST"),
        StatusCodes.Status401Unauthorized => ("Authentication is required.", "UNAUTHORIZED"),
        StatusCodes.Status403Forbidden => ("Access is forbidden.", "FORBIDDEN"),
        StatusCodes.Status404NotFound => ("The requested resource was not found.", "NOT_FOUND"),
        StatusCodes.Status409Conflict => ("The request conflicts with the current state.", "CONFLICT"),
        StatusCodes.Status429TooManyRequests => ("Too many requests.", "TOO_MANY_REQUESTS"),
        StatusCodes.Status500InternalServerError => ("An unexpected error occurred.", "INTERNAL_ERROR"),
        _ => ($"The request failed with status code {statusCode}.", $"HTTP_{statusCode}")
    };
}
