using Dataisland.Core.Domain.Entities;
using Dataisland.Core.Domain.ValueObjects;
using Dataisland.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Core.Repositories;

public interface IOrganizationRepository : IRepository
{
    Task<Organization?> GetByIdAsync(string id);
    Task<List<Organization>> GetByIdsAsync(IEnumerable<string> ids);
    Task<PaginatedResult<Organization>> GetByIdsPaginatedAsync(
        IEnumerable<string> ids, PaginationQuery pagination, CancellationToken ct = default);
    Task<(long Count, DateTime? ModifiedAt)> GetRevisionByIdsAsync(
        IEnumerable<string> ids, CancellationToken ct = default);
    Task<Organization?> GetByMemberIdAsync(ObjectId userId);
    Task<List<Organization>> GetByMemberIdAllAsync(ObjectId userId);
    Task<PaginatedResult<Organization>> GetByMemberIdAllPaginatedAsync(ObjectId userId, PaginationQuery pagination);
    Task<Organization> CreateAsync(Organization org);
    Task UpdateAsync(Organization org);
    Task<Organization?> UpdateQualityThresholdsAsync(
        string id, double? overall, double? critical, Dictionary<string, double>? byAspect,
        List<DepartmentThreshold>? byDepartment, Dictionary<string, int>? categoryWeights,
        bool resetCategoryWeights, CancellationToken ct = default);
    Task SoftDeleteAsync(string id);
    Task<Organization?> GetIfMemberAsync(string orgId, ObjectId memberUserId);
    /// <summary>
    /// Ids of orgs whose processing is currently paused. Used by dispatcher workers to
    /// exclude their cases at the Mongo query level — without this filter, a paused org
    /// with old InQueue rows fills the dispatcher's batch and starves active orgs.
    /// </summary>
    Task<List<ObjectId>> GetPausedOrganizationIdsAsync();
}

public class OrganizationRepository : RepositoryWithIndex<Organization>, IOrganizationRepository
{
    public OrganizationRepository(IMongoDBProvider provider)
        : base("organizations", provider, new OrganizationIndexes()) { }

    public async Task<Organization?> GetByIdAsync(string id) =>
        await Collection.Find(x => x.Id == id && !x.Metadata.IsDeleted).FirstOrDefaultAsync();

    public async Task<List<Organization>> GetByIdsAsync(IEnumerable<string> ids) =>
        await Secondary.Find(x => ids.Contains(x.Id) && !x.Metadata.IsDeleted).ToListAsync();

    public async Task<PaginatedResult<Organization>> GetByIdsPaginatedAsync(
        IEnumerable<string> ids, PaginationQuery pagination, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        return await Secondary.Find(x => idList.Contains(x.Id) && !x.Metadata.IsDeleted)
            .SortBy(x => x.Id)
            .ToPaginatedAsync(pagination, ct);
    }

    public async Task<(long Count, DateTime? ModifiedAt)> GetRevisionByIdsAsync(
        IEnumerable<string> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        var filter = Builders<Organization>.Filter.In(x => x.Id, idList)
            & Builders<Organization>.Filter.Eq(x => x.Metadata.IsDeleted, false);
        var count = await Secondary.CountDocumentsAsync(filter, cancellationToken: ct);
        var modifiedAt = await Secondary.Find(filter)
            .SortByDescending(x => x.ModifiedAt)
            .Project(x => (DateTime?)x.ModifiedAt)
            .FirstOrDefaultAsync(ct);
        return (count, modifiedAt);
    }

    public async Task<Organization?> GetByMemberIdAsync(ObjectId userId) =>
        await Collection.Find(x => x.MemberIds.Contains(userId) && !x.Metadata.IsDeleted).FirstOrDefaultAsync();

    public async Task<List<Organization>> GetByMemberIdAllAsync(ObjectId userId) =>
        await Secondary.Find(x => x.MemberIds.Contains(userId) && !x.Metadata.IsDeleted).ToListAsync();

    public async Task<PaginatedResult<Organization>> GetByMemberIdAllPaginatedAsync(ObjectId userId, PaginationQuery pagination) =>
        await Secondary.Find(x => x.MemberIds.Contains(userId) && !x.Metadata.IsDeleted).ToPaginatedAsync(pagination);

    public async Task<Organization> CreateAsync(Organization org)
    {
        await Collection.InsertOneAsync(org);
        return org;
    }

    public async Task UpdateAsync(Organization org)
    {
        org.ModifiedAt = DateTime.UtcNow;
        var replacement = org.ToBsonDocument();
        var profile = replacement.GetValue("profile", BsonNull.Value) as BsonDocument ?? new BsonDocument();
        profile.Remove("qualityThresholds");
        replacement.Remove("profile");

        // Quality thresholds have a dedicated update path. Preserve the current database value
        // when another settings screen saves an organization it read before that update.
        var replacementProfile = new BsonDocument("$mergeObjects", new BsonArray
        {
            new BsonDocument("$literal", profile),
            new BsonDocument("qualityThresholds", "$profile.qualityThresholds")
        });
        var stage = new BsonDocument("$replaceWith", new BsonDocument("$mergeObjects", new BsonArray
        {
            new BsonDocument("$literal", replacement),
            new BsonDocument("profile", replacementProfile)
        }));
        var update = Builders<Organization>.Update.Pipeline(
            PipelineDefinition<Organization, Organization>.Create(new[] { stage }));
        await Collection.UpdateOneAsync(x => x.Id == org.Id, update);
    }

    public async Task<Organization?> UpdateQualityThresholdsAsync(
        string id, double? overall, double? critical, Dictionary<string, double>? byAspect,
        List<DepartmentThreshold>? byDepartment, Dictionary<string, int>? categoryWeights,
        bool resetCategoryWeights, CancellationToken ct = default)
    {
        const string path = "profile.qualityThresholds";
        var settings = new QualityThresholds
        {
            Configured = true,
            ByAspect = byAspect ?? [],
            ByDepartment = byDepartment ?? [],
            CategoryWeights = categoryWeights
        };
        if (overall.HasValue) settings.Overall = overall.Value;
        if (critical.HasValue) settings.Critical = critical.Value;

        var changes = settings.ToBsonDocument();
        if (!overall.HasValue) changes.Remove("overall");
        if (!critical.HasValue) changes.Remove("critical");
        if (byAspect is null) changes.Remove("byAspect");
        if (byDepartment is null) changes.Remove("byDepartment");
        if ((categoryWeights is not null || resetCategoryWeights) &&
            !overall.HasValue && !critical.HasValue && byAspect is null && byDepartment is null)
            changes.Remove("configured");

        // Merge only explicitly supplied fields, including when a legacy document has no
        // thresholds object. Treat submitted dictionaries as literal data in the pipeline.
        var stages = new List<BsonDocument>
        {
            new("$set", new BsonDocument
            {
                { path, new BsonDocument("$mergeObjects", new BsonArray
                    {
                        "$" + path,
                        new BsonDocument("$literal", changes)
                    }) },
                { "_modified", new BsonDateTime(DateTime.UtcNow) }
            })
        };
        if (resetCategoryWeights)
            stages.Add(new BsonDocument("$unset", path + ".categoryWeights"));

        var update = Builders<Organization>.Update.Pipeline(PipelineDefinition<Organization, Organization>.Create(stages));
        return await Collection.FindOneAndUpdateAsync(
            x => x.Id == id && !x.Metadata.IsDeleted, update,
            new FindOneAndUpdateOptions<Organization> { ReturnDocument = ReturnDocument.After }, ct);
    }

    public async Task<Organization?> GetIfMemberAsync(string orgId, ObjectId memberUserId)
    {
        // GetByIdAsync already filters IsDeleted
        var org = await GetByIdAsync(orgId);
        if (org is null || !org.MemberIds.Contains(memberUserId)) return null;
        return org;
    }

    public async Task SoftDeleteAsync(string id) =>
        await Collection.UpdateOneAsync(
            x => x.Id == id,
            Builders<Organization>.Update
                .Set(x => x.Metadata.IsDeleted, true)
                .Set(x => x.ModifiedAt, DateTime.UtcNow));

    public async Task<List<ObjectId>> GetPausedOrganizationIdsAsync()
    {
        var filter = Builders<Organization>.Filter.And(
            Builders<Organization>.Filter.Ne("profile.processingPausedAt", BsonNull.Value),
            Builders<Organization>.Filter.Eq(x => x.Metadata.IsDeleted, false));
        var projection = Builders<Organization>.Projection.Expression(x => x.Id);
        var ids = await Secondary.Find(filter).Project(projection).ToListAsync();
        return ids.Where(id => ObjectId.TryParse(id, out _))
            .Select(ObjectId.Parse)
            .ToList();
    }
}

file class OrganizationIndexes : IndexesBuilder<Organization>
{
    public OrganizationIndexes()
    {
        Index(Ascending("memberIds"));
    }
}
