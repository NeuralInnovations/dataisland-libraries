using Dataisland.Contracts.Shared;
using Dataisland.MongoDB;
using Dataisland.Workspaces.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Workspaces.Repositories;

public interface IAccessGroupRepository : IRepository
{
    Task<AccessGroup?> GetByIdAsync(string id);
    Task<List<AccessGroup>> GetByOrganizationIdAsync(ObjectId organizationId);
    Task<PaginatedResult<AccessGroup>> GetByOrganizationIdPaginatedAsync(ObjectId organizationId, PaginationQuery pagination);
    Task<List<AccessGroup>> GetByMemberIdAsync(ObjectId userId);
    Task<List<AccessGroup>> GetByMemberAndOrganizationAsync(ObjectId userId, ObjectId organizationId);
    Task<AccessGroup> CreateAsync(AccessGroup group);
    Task UpdateAsync(AccessGroup group);
    Task DeleteAsync(string id);
    Task<List<string>> FindUserWorkspaceIdsAsync(ObjectId userId);
    Task<List<ObjectId>> FindUserOrganizationIdsAsync(ObjectId userId);
    Task<bool> IsAdminAsync(ObjectId userId, ObjectId organizationId);
}

public class AccessGroupRepository : RepositoryWithIndex<AccessGroup>, IAccessGroupRepository
{
    public AccessGroupRepository(IMongoDBProvider provider)
        : base("accessGroups", provider, new AccessGroupIndexes()) { }

    public async Task<AccessGroup?> GetByIdAsync(string id) =>
        await Collection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task<List<AccessGroup>> GetByOrganizationIdAsync(ObjectId organizationId) =>
        await Collection.Find(x => x.OrganizationId == organizationId && !x.Metadata.IsDeleted)
            .ToListAsync();

    public async Task<PaginatedResult<AccessGroup>> GetByOrganizationIdPaginatedAsync(ObjectId organizationId, PaginationQuery pagination) =>
        await Collection.Find(x => x.OrganizationId == organizationId && !x.Metadata.IsDeleted)
            .ToPaginatedAsync(pagination);

    public async Task<List<AccessGroup>> GetByMemberIdAsync(ObjectId userId) =>
        await Collection.Find(x => x.MemberIds.Contains(userId) && !x.Metadata.IsDeleted)
            .ToListAsync();

    public async Task<List<AccessGroup>> GetByMemberAndOrganizationAsync(ObjectId userId, ObjectId organizationId) =>
        await Collection.Find(x => x.MemberIds.Contains(userId)
                                   && x.OrganizationId == organizationId
                                   && !x.Metadata.IsDeleted)
            .ToListAsync();

    public async Task<AccessGroup> CreateAsync(AccessGroup group)
    {
        await Collection.InsertOneAsync(group);
        return group;
    }

    public async Task UpdateAsync(AccessGroup group)
    {
        group.ModifiedAt = DateTime.UtcNow;
        await Collection.ReplaceOneAsync(x => x.Id == group.Id, group);
    }

    public async Task DeleteAsync(string id) =>
        await Collection.UpdateOneAsync(
            x => x.Id == id,
            Builders<AccessGroup>.Update.Set(x => x.Metadata.IsDeleted, true));

    public async Task<List<string>> FindUserWorkspaceIdsAsync(ObjectId userId)
    {
        var groups = await GetByMemberIdAsync(userId);
        return groups
            .SelectMany(g => g.Regulation.RegulateWorkspaceIds)
            .Distinct()
            .Select(id => id.ToString())
            .ToList();
    }

    public async Task<List<ObjectId>> FindUserOrganizationIdsAsync(ObjectId userId)
    {
        var groups = await GetByMemberIdAsync(userId);
        return groups.Select(g => g.OrganizationId).Distinct().ToList();
    }

    public async Task<bool> IsAdminAsync(ObjectId userId, ObjectId organizationId)
    {
        var groups = await GetByMemberAndOrganizationAsync(userId, organizationId);
        return groups.Any(g => g.Permits.IsAdmin && g.Regulation.IsRegulateOrganization);
    }
}

file class AccessGroupIndexes : IndexesBuilder<AccessGroup>
{
    public AccessGroupIndexes()
    {
        Index(Ascending("organizationId"));
        Index(Ascending("memberIds"));
    }
}
