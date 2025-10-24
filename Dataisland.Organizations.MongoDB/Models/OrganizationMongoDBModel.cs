using Dataisland.MongoDB;
using Dataisland.Organizations.Models;
using Dataisland.Organizations.MongoDB.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Organizations.MongoDB.Models;

public class OrganizationMongoDBModel(IMongoDBProvider provider, string collectionName)
    : RepositoryWithIndex<OrganizationEntity>(collectionName, provider, new Indexes()),
        IOrganizationsModel
{
    class Indexes : IndexesBuilder<OrganizationEntity>
    {
        public Indexes()
        {
            Index(Ascending(x => x.Id));
        }
    }

    public async Task<Organization?> FindAsync(OrganizationId id)
    {
        var objectId = ObjectId.Parse(id.Value);
        var org = await Collection
            .Find(x => x.Id == objectId && x.Status != OrganizationStatus.Deleted)
            .FirstOrDefaultAsync();
        if (org != null)
        {
            return new Organization(
                new OrganizationId(org.Id.ToString()),
                org.Profile,
                org.Status,
                org.CreatedAt,
                org.ModifiedAt
            );
        }

        return null;
    }

    public async Task<Organization> CreateAsync(string name)
    {
        var now = DateTime.UtcNow;
        var org = new OrganizationEntity
        {
            Profile = new OrganizationProfile
            {
                Name = name
            },
            CreatedAt = now,
            ModifiedAt = now,
            Status = OrganizationStatus.Active,
        };
        await Collection.InsertOneAsync(org);

        return new Organization(
            new OrganizationId(org.Id.ToString()),
            org.Profile,
            org.Status,
            org.CreatedAt,
            org.ModifiedAt
        );
    }

    public async Task<bool> ExistsAsync(OrganizationId id)
    {
        var objectId = ObjectId.Parse(id.Value);
        return await Collection
            .Find(x => x.Id == objectId && x.Status != OrganizationStatus.Deleted)
            .AnyAsync();
    }

    public async Task<bool> DeleteAsync(OrganizationId id)
    {
        var objectId = ObjectId.Parse(id.Value);
        var result = await Collection.UpdateOneAsync(
            filter: this.Filter().Eq(x => x.Id, objectId),
            update: this.Update().Set(x => x.Status, OrganizationStatus.Deleted)
                .Set(x => x.ModifiedAt, DateTime.UtcNow)
        );
        return result.ModifiedCount != 0;
    }
    
    public Task EnsureCreatedAsync() => Task.CompletedTask;
}