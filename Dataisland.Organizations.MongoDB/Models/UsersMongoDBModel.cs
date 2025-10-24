using Dataisland.MongoDB;
using Dataisland.Organizations.Models;
using Dataisland.Organizations.MongoDB.Entities;
using Dataisland.Organizations.Users;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Organizations.MongoDB.Models;

public class UsersMongoDBModel(IMongoDBProvider provider, string collectionName)
    : RepositoryWithIndex<UserEntity>(collectionName, provider, new Indexes()),
        IUsersModel
{
    class Indexes : IndexesBuilder<UserEntity>
    {
        public Indexes()
        {
            Index(Ascending(x => x.Id));
        }
    }

    public Task EnsureCreatedAsync() => Task.CompletedTask;

    public async Task<User> CreateAsync()
    {
        var now = DateTime.UtcNow;
        var userId = ObjectId.GenerateNewId();
        var entity = new UserEntity
        {
            Id = userId,
            CreatedAt = now,
            ModifiedAt = now,
            Profile = new UserProfile("New User")
        };
        await Collection.InsertOneAsync(entity);

        return new User(new UserId(userId.ToString()), entity.Profile, entity.CreatedAt, entity.ModifiedAt);
    }

    public async Task<User?> FindAsync(UserId id)
    {
        var objectId = ObjectId.Parse(id.Value);
        var result = await Collection.Find(x => x.Id == objectId).FirstOrDefaultAsync();
        if (result != null)
        {
            return new User(id, result.Profile, result.CreatedAt, result.ModifiedAt);
        }

        return null;
    }
}