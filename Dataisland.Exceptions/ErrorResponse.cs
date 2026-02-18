namespace Dataisland.Exceptions;

public record ErrorResponse(string Error, string Code, string? TraceId = null);
