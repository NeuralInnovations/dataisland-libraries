using Dataisland.Core.Domain.Entities;
using Dataisland.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Core.Repositories;

public interface IOrganizationRepository : IRepository
{
    Task<Organization?> GetByIdAsync(string id);
    Task<List<Organization>> GetByIdsAsync(IEnumerable<string> ids);
    Task<Organization?> GetByMemberIdAsync(ObjectId userId);
    Task<List<Organization>> GetByMemberIdAllAsync(ObjectId userId);
    Task<PaginatedResult<Organization>> GetByMemberIdAllPaginatedAsync(ObjectId userId, PaginationQuery pagination);
    Task<Organization> CreateAsync(Organization org);
    Task UpdateAsync(Organization org);
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
        await Collection.ReplaceOneAsync(x => x.Id == org.Id, org);
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
