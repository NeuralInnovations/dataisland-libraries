using Dataisland.Core.Domain.Entities;
using Dataisland.MongoDB;
using MongoDB.Driver;

namespace Dataisland.Core.Repositories;

public interface IApiSpendRepository : IRepository
{
    /// <summary>
    /// Read the current cumulative spend for an organisation in the given month bucket.
    /// Returns 0 if no record exists yet.
    /// </summary>
    Task<decimal> GetCurrentSpendAsync(string organizationId, string yearMonth, CancellationToken ct = default);

    /// <summary>
    /// Atomic upsert-and-increment of an organisation's monthly spend record. Returns the
    /// cumulative cost after the increment (post-value). Uses $inc semantics so concurrent
    /// writes from parallel case consumers do not race.
    /// </summary>
    Task<decimal> IncrementAsync(
        string organizationId,
        string yearMonth,
        decimal additionalCostUsd,
        long additionalPromptTokens,
        long additionalCompletionTokens,
        CancellationToken ct = default);

    Task<ApiSpendRecord?> GetRecordAsync(string organizationId, string yearMonth, CancellationToken ct = default);
}

public class ApiSpendRepository : RepositoryWithIndex<ApiSpendRecord>, IApiSpendRepository
{
    public ApiSpendRepository(IMongoDBProvider provider)
        : base("apiSpendRecords", provider, new ApiSpendRecordIndexes()) { }

    public async Task<decimal> GetCurrentSpendAsync(string organizationId, string yearMonth, CancellationToken ct = default)
    {
        var record = await Secondary
            .Find(x => x.OrganizationId == organizationId && x.YearMonth == yearMonth)
            .FirstOrDefaultAsync(ct);
        return record?.CumulativeCostUsd ?? 0m;
    }

    public async Task<ApiSpendRecord?> GetRecordAsync(string organizationId, string yearMonth, CancellationToken ct = default) =>
        await Secondary
            .Find(x => x.OrganizationId == organizationId && x.YearMonth == yearMonth)
            .FirstOrDefaultAsync(ct);

    public async Task<decimal> IncrementAsync(
        string organizationId,
        string yearMonth,
        decimal additionalCostUsd,
        long additionalPromptTokens,
        long additionalCompletionTokens,
        CancellationToken ct = default)
    {
        var filter = Builders<ApiSpendRecord>.Filter.And(
            Builders<ApiSpendRecord>.Filter.Eq(x => x.OrganizationId, organizationId),
            Builders<ApiSpendRecord>.Filter.Eq(x => x.YearMonth, yearMonth));

        var now = DateTime.UtcNow;
        var update = Builders<ApiSpendRecord>.Update
            .Inc(x => x.CumulativeCostUsd, additionalCostUsd)
            .Inc(x => x.PromptTokens, additionalPromptTokens)
            .Inc(x => x.CompletionTokens, additionalCompletionTokens)
            .Inc(x => x.CaseCount, 1)
            .Set(x => x.LastIncrementedAt, now)
            .Set(x => x.ModifiedAt, now)
            .SetOnInsert(x => x.OrganizationId, organizationId)
            .SetOnInsert(x => x.YearMonth, yearMonth)
            .SetOnInsert(x => x.CreatedAt, now);

        var options = new FindOneAndUpdateOptions<ApiSpendRecord, ApiSpendRecord>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var updated = await Collection.FindOneAndUpdateAsync(filter, update, options, ct);
        return updated?.CumulativeCostUsd ?? 0m;
    }
}

file class ApiSpendRecordIndexes : IndexesBuilder<ApiSpendRecord>
{
    public ApiSpendRecordIndexes()
    {
        // Compound unique index: one row per (org, month). Drives both the $inc upsert
        // path in IncrementAsync and the GetCurrentSpendAsync lookup.
        Index(Builders<ApiSpendRecord>.IndexKeys
                .Ascending(x => x.OrganizationId)
                .Ascending(x => x.YearMonth))
            .Unique();
    }
}
