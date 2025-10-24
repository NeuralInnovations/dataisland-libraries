using Dataisland.MongoDB;
using Dataisland.Organizations.Groups;
using Dataisland.Organizations.Models;
using Dataisland.Organizations.MongoDB.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Organizations.MongoDB.Models;

public class GroupsMongoDBModel(IMongoDBProvider provider, string collectionName)
    : RepositoryWithIndex<GroupEntity>(collectionName, provider, new Indexes()),
        IGroupsModel
{
    class Indexes : IndexesBuilder<GroupEntity>
    {
        public Indexes()
        {
            Index(Ascending(x => x.Id));
            Index(Ascending(x => x.OrganizationId));
        }
    }

    public async Task<Group> CreateAsync(OrganizationId id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        var now = DateTime.UtcNow;
        var group = new GroupEntity
        {
            OrganizationId = ObjectId.Parse(id.Value),
            Name = name,
            Status = GroupStatus.Active,
            CreatedAt = now,
            ModifiedAt = now,
        };
        await Collection.InsertOneAsync(group);

        return new Group
        {
            Id = new GroupId(group.Id.ToString()),
            OrganizationId = new OrganizationId(group.OrganizationId.ToString()),
            Name = group.Name
        };
    }

    public async Task<IEnumerable<Group>> FindAsync(OrganizationId id)
    {
        var objectId = ObjectId.Parse(id.Value);
        var groups = await Collection
            .Find(x => x.OrganizationId == objectId && x.Status != GroupStatus.Deleted)
            .ToListAsync();

        return groups.Select(group => new Group
        {
            Id = new GroupId(group.Id.ToString()),
            OrganizationId = new OrganizationId(group.OrganizationId.ToString()),
            Name = group.Name
        });
    }

    public async Task<Group?> FindAsync(GroupId id)
    {
        var objectId = ObjectId.Parse(id.Value);
        var group = await Collection
            .Find(x => x.Id == objectId && x.Status != GroupStatus.Deleted)
            .FirstOrDefaultAsync();
        if (group != null)
        {
            return new Group
            {
                Id = new GroupId(group.Id.ToString()),
                OrganizationId = new OrganizationId(group.OrganizationId.ToString()),
                Name = group.Name
            };
        }

        return null;
    }

    public async Task<bool> DeleteAsync(GroupId id)
    {
        var objectId = ObjectId.Parse(id.Value);
        var result = await Collection.UpdateOneAsync(
            filter: this.Filter().Eq(x => x.Id, objectId),
            update: this.Update().Set(x => x.Status, GroupStatus.Deleted)
        );
        return result.ModifiedCount != 0;
    }
    
    public Task EnsureCreatedAsync() => Task.CompletedTask;
}