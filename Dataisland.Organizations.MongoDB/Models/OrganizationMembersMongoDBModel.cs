using Dataisland.MongoDB;
using Dataisland.Organizations.Models;
using Dataisland.Organizations.MongoDB.Entities;
using Dataisland.Organizations.Users;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Organizations.MongoDB.Models;

public class OrganizationMembersMongoDBModel(IMongoDBProvider provider, string collectionName)
    : RepositoryWithIndex<OrganizationMemberEntity>(collectionName, provider, new Indexes()),
        IOrganizationMembersModel
{
    class Indexes : IndexesBuilder<OrganizationMemberEntity>
    {
        public Indexes()
        {
            Index(
                Ascending(x => x.OrganizationId)
                    .Ascending(x => x.UserId)
            );
            Index(Ascending(x => x.UserId));
        }
    }

    public async Task<int> RemoveAsync(
        OrganizationId id,
        IEnumerable<UserId> userIds
    )
    {
        var objectId = ObjectId.Parse(id.Value);
        var users = userIds.DistinctBy(x => x.Value).Select(x => ObjectId.Parse(x.Value)).ToArray();
        if (users.Length != 0)
        {
            var filter =
                    this.Filter().Eq(x => x.OrganizationId, objectId)
                    & this.Filter().In(x => x.UserId, users)
                ;
            var result = await Collection.DeleteManyAsync(filter);
            return (int)result.DeletedCount;
        }

        return 0;
    }

    public async Task<IEnumerable<OrganizationMember>> AddAsync(
        OrganizationId id,
        IEnumerable<UserId> userIds
    )
    {
        var now = DateTime.UtcNow;
        var objectId = ObjectId.Parse(id.Value);
        var entities = userIds.DistinctBy(x => x.Value).Select(userId => new OrganizationMemberEntity
        {
            OrganizationId = objectId,
            UserId = ObjectId.Parse(userId.Value),
            JoinedAt = now,
            LeftAt = null,
            Status = OrganizationMemberStatus.Active
        }).ToArray();
        if (entities.Length != 0)
        {
            await Collection.InsertManyAsync(entities);

            return entities.Select(m
                => new OrganizationMember(id, new UserId(m.UserId.ToString()), m.Status, m.JoinedAt, m.LeftAt)
            ).ToArray();
        }

        return [];
    }

    public async Task<int> ChangeStatusAsync(
        OrganizationId id,
        IEnumerable<UserId> userIds,
        OrganizationMemberStatus status
    )
    {
        var objectId = ObjectId.Parse(id.Value);
        var users = userIds.DistinctBy(x => x.Value).Select(x => ObjectId.Parse(x.Value)).ToArray();
        var filter =
                this.Filter().Eq(x => x.OrganizationId, objectId)
                & this.Filter().In(x => x.UserId, users)
            ;
        var update = this.Update().Set(x => x.Status, status);
        var updated = await Collection.UpdateManyAsync(filter, update);
        return (int)updated.ModifiedCount;
    }

    public async Task<IEnumerable<OrganizationMember>> FindAsync(OrganizationId id)
    {
        var objectId = ObjectId.Parse(id.Value);
        var members = await Collection
            .Find(x => x.OrganizationId == objectId)
            .ToListAsync();

        return members.Select(g =>
                new OrganizationMember(id, new UserId(g.UserId.ToString()), g.Status, g.JoinedAt, g.LeftAt)
            )
            .ToArray();
    }

    public async Task<IEnumerable<OrganizationMember>> FindAsync(UserId userId)
    {
        var objectId = ObjectId.Parse(userId.Value);
        var members = await Collection
            .Find(x => x.UserId == objectId)
            .ToListAsync();

        return members.Select(g =>
                new OrganizationMember(
                    new OrganizationId(g.OrganizationId.ToString()),
                    new UserId(g.UserId.ToString()),
                    g.Status,
                    g.JoinedAt,
                    g.LeftAt
                )
            )
            .ToArray();
    }

    public async Task<OrganizationMember?> FindAsync(OrganizationId id, UserId userId)
    {
        var orgId = ObjectId.Parse(id.Value);
        var usrId = ObjectId.Parse(userId.Value);
        var member = await Collection
            .Find(x => x.OrganizationId == orgId && x.UserId == usrId)
            .FirstOrDefaultAsync();

        if (member != null)
        {
            return new OrganizationMember(
                id,
                new UserId(member.UserId.ToString()),
                member.Status,
                member.JoinedAt,
                member.LeftAt
            );
        }

        return null;
    }

    public Task EnsureCreatedAsync() => Task.CompletedTask;
}