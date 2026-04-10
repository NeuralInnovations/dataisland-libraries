using Dataisland.Core.Domain.Entities;
using Dataisland.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Core.Repositories;

public interface IUserRepository : IRepository
{
    Task<User?> GetByIdAsync(string id);
    Task<User?> GetByExternalIdAsync(string externalId);
    Task<List<User>> GetByIdsAsync(IEnumerable<string> ids);
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(string id);
}

public class UserRepository : RepositoryWithIndex<User>, IUserRepository
{
    public UserRepository(IMongoDBProvider provider)
        : base("users", provider, new UserIndexes()) { }

    public async Task<User?> GetByIdAsync(string id) =>
        await Collection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task<User?> GetByExternalIdAsync(string externalId) =>
        await Collection.Find(x => x.ExternalIds.Contains(externalId)).FirstOrDefaultAsync();

    public async Task<List<User>> GetByIdsAsync(IEnumerable<string> ids) =>
        await Collection.Find(x => ids.Contains(x.Id)).ToListAsync();

    public async Task<User> CreateAsync(User user)
    {
        await Collection.InsertOneAsync(user);
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        user.ModifiedAt = DateTime.UtcNow;
        await Collection.ReplaceOneAsync(x => x.Id == user.Id, user);
    }

    public async Task DeleteAsync(string id) =>
        await Collection.DeleteOneAsync(x => x.Id == id);
}

file class UserIndexes : IndexesBuilder<User>
{
    public UserIndexes()
    {
        Index(Ascending("externalIds"));
    }
}
