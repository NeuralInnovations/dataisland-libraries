namespace Dataisland.Exceptions;

public class ApiException(
    string message,
    int statusCode = 500,
    string errorCode = "INTERNAL_ERROR",
    Exception? innerException = null
) : Exception(message, innerException)
{
    public int StatusCode { get; } = statusCode;
    public string ErrorCode { get; } = errorCode;
}

public class NotFoundException(string entity, string id)
    : ApiException($"{entity} '{id}' not found", 404, "NOT_FOUND");

public class ValidationException(string message)
    : ApiException(message, 400, "VALIDATION_ERROR");

public class ServiceUnavailableException(string service, Exception? inner = null)
    : ApiException($"Service '{service}' is temporarily unavailable", 503, "SERVICE_UNAVAILABLE", inner);
