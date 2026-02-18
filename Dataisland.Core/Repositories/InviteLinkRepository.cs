using Dataisland.Core.Domain.Entities;
using Dataisland.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Core.Repositories;

public interface IInviteLinkRepository : IRepository
{
    Task<InviteLink?> GetByIdAsync(string id);
    Task<InviteLink?> GetByCodeAsync(string code);
    Task<List<InviteLink>> GetByOrganizationIdAsync(ObjectId organizationId);
    Task<InviteLink> CreateAsync(InviteLink link);
    Task DeleteAsync(string id);
}

public class InviteLinkRepository : RepositoryWithIndex<InviteLink>, IInviteLinkRepository
{
    public InviteLinkRepository(IMongoDBProvider provider)
        : base("inviteLinks", provider, new InviteLinkIndexes()) { }

    public async Task<InviteLink?> GetByIdAsync(string id) =>
        await Collection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task<InviteLink?> GetByCodeAsync(string code) =>
        await Collection.Find(x => x.Code == code).FirstOrDefaultAsync();

    public async Task<List<InviteLink>> GetByOrganizationIdAsync(ObjectId organizationId) =>
        await Collection.Find(x => x.OrganizationId == organizationId).ToListAsync();

    public async Task<InviteLink> CreateAsync(InviteLink link)
    {
        await Collection.InsertOneAsync(link);
        return link;
    }

    public async Task DeleteAsync(string id) =>
        await Collection.DeleteOneAsync(x => x.Id == id);
}

file class InviteLinkIndexes : IndexesBuilder<InviteLink>
{
    public InviteLinkIndexes()
    {
        Index(Ascending("code"));
        Index(Ascending("organizationId"));
    }
}
