using MongoDB.Driver;

namespace Dataisland.MongoDB;

public record PaginationQuery(int Page = 1, int PageSize = 20)
{
    public int Skip => (ValidatedPage - 1) * ValidatedPageSize;
    public int ValidatedPage => Math.Max(Page, 1);
    public int ValidatedPageSize => Math.Clamp(PageSize, 1, 100);
}

public record PaginatedResult<T>(List<T> Items, int Page, int PageSize, long Total)
{
    public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
}

public static class PaginationExtensions
{
    public static async Task<PaginatedResult<T>> ToPaginatedAsync<T>(
        this IFindFluent<T, T> query, PaginationQuery pagination, CancellationToken ct = default)
    {
        var total = await query.CountDocumentsAsync(ct);
        var items = await query
            .Skip(pagination.Skip)
            .Limit(pagination.ValidatedPageSize)
            .ToListAsync(ct);
        return new PaginatedResult<T>(items, pagination.ValidatedPage, pagination.ValidatedPageSize, total);
    }
}
