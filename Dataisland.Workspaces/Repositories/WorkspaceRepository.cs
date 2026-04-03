using Dataisland.MongoDB;
using Dataisland.Workspaces.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Workspaces.Repositories;

public interface IWorkspaceRepository : IRepository
{
    Task<Workspace?> GetByIdAsync(string id);
    Task<List<Workspace>> GetByIdsAsync(IEnumerable<string> ids);
    Task<List<Workspace>> GetByOrganizationIdAsync(ObjectId organizationId);
    Task<PaginatedResult<Workspace>> GetByOrganizationIdPaginatedAsync(ObjectId organizationId, PaginationQuery pagination);
    Task<List<Workspace>> GetSharedByOrganizationIdAsync(ObjectId organizationId);
    Task<List<Workspace>> GetSharedByOrganizationIdsAsync(IEnumerable<ObjectId> organizationIds);
    Task<List<Workspace>> GetAllWithProtocolSyncAsync();
    Task<Workspace> CreateAsync(Workspace workspace);
    Task UpdateAsync(Workspace workspace);
    Task SoftDeleteAsync(string id);
}

public class WorkspaceRepository : RepositoryWithIndex<Workspace>, IWorkspaceRepository
{
    public WorkspaceRepository(IMongoDBProvider provider)
        : base("workspaces", provider, new WorkspaceIndexes()) { }

    public async Task<Workspace?> GetByIdAsync(string id) =>
        await Collection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task<List<Workspace>> GetByIdsAsync(IEnumerable<string> ids) =>
        await Secondary.Find(x => ids.Contains(x.Id)).ToListAsync();

    public async Task<List<Workspace>> GetByOrganizationIdAsync(ObjectId organizationId) =>
        await Secondary.Find(x => x.Metadata.OrganizationId == organizationId && !x.Metadata.IsDeleted)
            .ToListAsync();

    public async Task<PaginatedResult<Workspace>> GetByOrganizationIdPaginatedAsync(ObjectId organizationId, PaginationQuery pagination) =>
        await Secondary.Find(x => x.Metadata.OrganizationId == organizationId && !x.Metadata.IsDeleted)
            .ToPaginatedAsync(pagination);

    public async Task<List<Workspace>> GetSharedByOrganizationIdAsync(ObjectId organizationId) =>
        await Secondary.Find(x => x.Metadata.OrganizationId == organizationId
                                   && x.Profile.IsShared
                                   && !x.Metadata.IsDeleted)
            .ToListAsync();

    public async Task<List<Workspace>> GetSharedByOrganizationIdsAsync(IEnumerable<ObjectId> organizationIds)
    {
        var orgIds = organizationIds.ToList();
        return await Secondary.Find(x => orgIds.Contains(x.Metadata.OrganizationId)
                                          && x.Profile.IsShared
                                          && !x.Metadata.IsDeleted)
            .ToListAsync();
    }

    public async Task<List<Workspace>> GetAllWithProtocolSyncAsync() =>
        await Secondary.Find(x => !x.Metadata.IsDeleted
                                   && x.Metadata.ProtocolSyncSources.Any())
            .ToListAsync();

    public async Task<Workspace> CreateAsync(Workspace workspace)
    {
        await Collection.InsertOneAsync(workspace);
        return workspace;
    }

    public async Task UpdateAsync(Workspace workspace)
    {
        workspace.ModifiedAt = DateTime.UtcNow;
        await Collection.ReplaceOneAsync(x => x.Id == workspace.Id, workspace);
    }

    public async Task SoftDeleteAsync(string id) =>
        await Collection.UpdateOneAsync(
            x => x.Id == id,
            Builders<Workspace>.Update
                .Set(x => x.Metadata.IsDeleted, true)
                .Set(x => x.ModifiedAt, DateTime.UtcNow));
}

file class WorkspaceIndexes : IndexesBuilder<Workspace>
{
    public WorkspaceIndexes()
    {
        Index(Ascending("metadata.organizationId"));
    }
}
