using Dataisland.Core.Domain.Entities;
using Dataisland.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Core.Repositories;

public interface IInviteRepository : IRepository
{
    Task<Invite?> GetByIdAsync(string id);
    Task<List<Invite>> GetByOrganizationIdAsync(ObjectId organizationId);
    Task<PaginatedResult<Invite>> GetByOrganizationIdPaginatedAsync(ObjectId organizationId, PaginationQuery pagination);
    Task<Invite?> GetByUserMetadataAsync(string userMetadata, ObjectId organizationId);
    Task<Invite> CreateAsync(Invite invite);
    Task DeleteAsync(string id);
}

public class InviteRepository : RepositoryWithIndex<Invite>, IInviteRepository
{
    public InviteRepository(IMongoDBProvider provider)
        : base("invites", provider, new InviteIndexes()) { }

    public async Task<Invite?> GetByIdAsync(string id) =>
        await Collection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task<List<Invite>> GetByOrganizationIdAsync(ObjectId organizationId) =>
        await Collection.Find(x => x.OrganizationId == organizationId).ToListAsync();

    public async Task<PaginatedResult<Invite>> GetByOrganizationIdPaginatedAsync(ObjectId organizationId, PaginationQuery pagination) =>
        await Collection.Find(x => x.OrganizationId == organizationId).ToPaginatedAsync(pagination);

    public async Task<Invite?> GetByUserMetadataAsync(string userMetadata, ObjectId organizationId) =>
        await Collection.Find(x => x.UserMetadata.Contains(userMetadata)
                                   && x.OrganizationId == organizationId)
            .FirstOrDefaultAsync();

    public async Task<Invite> CreateAsync(Invite invite)
    {
        await Collection.InsertOneAsync(invite);
        return invite;
    }

    public async Task DeleteAsync(string id) =>
        await Collection.DeleteOneAsync(x => x.Id == id);
}

file class InviteIndexes : IndexesBuilder<Invite>
{
    public InviteIndexes()
    {
        Index(Ascending("organizationId"));
    }
}
