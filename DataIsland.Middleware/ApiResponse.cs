namespace DataIsland.Middleware;

public record ApiErrorResponse(string Message, string Code, string? TraceId = null);

public record PaginationMeta(int Page, int PageSize, long Total, int TotalPages);
