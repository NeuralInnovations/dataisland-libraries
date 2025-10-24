using Dataisland.MongoDB;
using Dataisland.Organizations.ApiKeys;
using Dataisland.Organizations.Models;
using Dataisland.Organizations.MongoDB.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Organizations.MongoDB.Models;

public class ApiKeyMongoDBModel(IMongoDBProvider provider, string collectionName)
    : RepositoryWithIndex<ApiKeyEntity>(collectionName, provider, new Indexes()),
        IApiKeyModel
{
    class Indexes : IndexesBuilder<ApiKeyEntity>
    {
        public Indexes()
        {
            Index(
                Ascending(x => x.Id)
            );
            Index(Ascending(x => x.AssignedTo));
            Index(Ascending(x => x.Key));
        }
    }

    public async Task<ApiKey> CreateAsync(ApiKeyAssignedTo assignedTo, string key)
    {
        var now = DateTime.UtcNow;
        var entity = new ApiKeyEntity
        {
            Id = ObjectId.GenerateNewId(),
            AssignedTo = assignedTo,
            Key = key,
            CreatedAt = now,
            ModifiedAt = now
        };
        await Collection.InsertOneAsync(entity);

        return new ApiKey(
            new ApiKeyId(entity.Id.ToString()),
            entity.AssignedTo,
            entity.Key,
            entity.CreatedAt,
            entity.ModifiedAt
        );
    }

    public async Task<ApiKey?> UpdateAsync(ApiKeyId id, string key)
    {
        var now = DateTime.UtcNow;
        var obj = ObjectId.Parse(id.Value);
        var result = await Collection
            .FindOneAndUpdateAsync(
                this.Filter().Eq(x => x.Id, obj),
                this.Update()
                    .Set(x => x.Key, key)
                    .Set(x => x.ModifiedAt, now)
            );
        if (result != null)
        {
            return new ApiKey(id, result.AssignedTo, result.Key, result.CreatedAt, result.ModifiedAt);
        }

        return null;
    }

    public async Task<ApiKey?> FindAsync(ApiKeyId keyId)
    {
        var result = await Collection
            .Find(this.Filter().Eq(x => x.Id, ObjectId.Parse(keyId.Value)))
            .FirstOrDefaultAsync();

        if (result != null)
        {
            return new ApiKey(new ApiKeyId(result.Id.ToString()), result.AssignedTo, result.Key, result.CreatedAt,
                result.ModifiedAt);
        }

        return null;
    }

    public async Task<IEnumerable<ApiKey>> FindAsync(ApiKeyAssignedTo assignedTo)
    {
        var results = await Collection
            .Find(this.Filter().Eq(x => x.AssignedTo, assignedTo))
            .ToListAsync();

        return results
            .Select(x => new ApiKey(new ApiKeyId(x.Id.ToString()), x.AssignedTo, x.Key, x.CreatedAt, x.ModifiedAt))
            .ToArray();
    }

    public async Task<int> DeleteAsync(ApiKeyId keyId)
    {
        var objId = ObjectId.Parse(keyId.Value);
        var result = await Collection.DeleteOneAsync(this.Filter().Eq(x => x.Id, objId));
        return (int)result.DeletedCount;
    }

    public Task EnsureCreatedAsync() => Task.CompletedTask;
}