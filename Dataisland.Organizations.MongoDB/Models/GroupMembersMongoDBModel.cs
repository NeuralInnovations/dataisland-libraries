using Dataisland.MongoDB;
using Dataisland.Organizations.Groups;
using Dataisland.Organizations.Models;
using Dataisland.Organizations.MongoDB.Entities;
using Dataisland.Organizations.Users;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Organizations.MongoDB.Models;

public class GroupMembersMongoDBModel(IMongoDBProvider provider, string collectionName)
    : RepositoryWithIndex<GroupMemberEntity>(collectionName, provider, new Indexes()),
        IGroupMembersModel
{
    class Indexes : IndexesBuilder<GroupMemberEntity>
    {
        public Indexes()
        {
            Index(
                Ascending(x => x.GroupId)
                    .Ascending(x => x.UserId)
            );
        }
    }

    public async Task<int> AddAsync(GroupId groupId, IEnumerable<UserId> userIds)
    {
        var objectId = ObjectId.Parse(groupId.Value);
        var entities = userIds.DistinctBy(x => x.Value).Select(userId => new GroupMemberEntity
        {
            GroupId = objectId,
            UserId = userId
        }).ToArray();
        if (entities.Length != 0)
        {
            await Collection.InsertManyAsync(entities);
            return entities.Length;
        }

        return 0;
    }

    public async Task<int> RemoveAsync(GroupId groupId, IEnumerable<UserId> userIds)
    {
        var ids = userIds.DistinctBy(x => x.Value).ToArray();
        if (ids.Length != 0)
        {
            var objectId = ObjectId.Parse(groupId.Value);
            var filter = this.Filter().Eq(x => x.GroupId, objectId) & this.Filter().In(x => x.UserId, ids);
            var result = await Collection.DeleteManyAsync(filter);
            return (int)result.DeletedCount;
        }

        return 0;
    }

    public async Task<IEnumerable<UserId>> FindAsync(GroupId groupId)
    {
        var objectId = ObjectId.Parse(groupId.Value);
        var members = await Collection
            .Find(x => x.GroupId == objectId)
            .ToListAsync();

        return members.Select(m => m.UserId);
    }

    public Task EnsureCreatedAsync() => Task.CompletedTask;
}