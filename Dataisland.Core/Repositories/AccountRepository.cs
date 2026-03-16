using Dataisland.Core.Domain.Entities;
using Dataisland.MongoDB;
using MongoDB.Driver;

namespace Dataisland.Core.Repositories;

public interface IAccountRepository : IRepository
{
    /// <summary>
    /// Returns existing account or creates a new one for the given provider identity.
    /// UserId is not set on creation — call <see cref="AttachUserAsync"/> after user provisioning.
    /// </summary>
    Task<Account> FindOrCreateAsync(string provider, string providerAccountId);

    Task<Account?> FindByProviderAsync(string provider, string providerAccountId);

    Task<Account?> FindByUserIdAndProviderAsync(string userId, string provider);

    Task<List<Account>> FindByUserIdAsync(string userId);

    /// <summary>
    /// Links an account to an internal user. Throws if the account does not exist.
    /// </summary>
    Task<Account> AttachUserAsync(string accountId, string userId);

    Task<Account> DetachUserAsync(string accountId);

    Task<bool> DeleteByUserIdAsync(string userId);
}

public class AccountRepository : RepositoryWithIndex<Account>, IAccountRepository
{
    public AccountRepository(IMongoDBProvider provider)
        : base("accounts", provider, new AccountIndexes()) { }

    public async Task<Account> FindOrCreateAsync(string provider, string providerAccountId)
    {
        var filter = Builders<Account>.Filter.And(
            Builders<Account>.Filter.Eq(x => x.Provider, provider),
            Builders<Account>.Filter.Eq(x => x.ProviderAccountId, providerAccountId));

        var update = Builders<Account>.Update
            .SetOnInsert(x => x.Provider, provider)
            .SetOnInsert(x => x.ProviderAccountId, providerAccountId)
            .SetOnInsert(x => x.CreatedAt, DateTime.UtcNow)
            .Set(x => x.ModifiedAt, DateTime.UtcNow);

        return await Collection.FindOneAndUpdateAsync(
            filter, update,
            new FindOneAndUpdateOptions<Account>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            });
    }

    public async Task<Account?> FindByProviderAsync(string provider, string providerAccountId) =>
        await Secondary.Find(x => x.Provider == provider && x.ProviderAccountId == providerAccountId)
            .FirstOrDefaultAsync();

    public async Task<Account?> FindByUserIdAndProviderAsync(string userId, string provider) =>
        await Secondary.Find(x => x.UserId == userId && x.Provider == provider)
            .FirstOrDefaultAsync();

    public async Task<List<Account>> FindByUserIdAsync(string userId) =>
        await Secondary.Find(x => x.UserId == userId).ToListAsync();

    public async Task<Account> AttachUserAsync(string accountId, string userId)
    {
        var result = await Collection.FindOneAndUpdateAsync(
            Builders<Account>.Filter.Eq(x => x.Id, accountId),
            Builders<Account>.Update
                .Set(x => x.UserId, userId)
                .Set(x => x.ModifiedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<Account> { ReturnDocument = ReturnDocument.After });

        return result ?? throw new KeyNotFoundException($"Account {accountId} not found");
    }

    public async Task<Account> DetachUserAsync(string accountId)
    {
        var result = await Collection.FindOneAndUpdateAsync(
            Builders<Account>.Filter.Eq(x => x.Id, accountId),
            Builders<Account>.Update
                .Unset(x => x.UserId)
                .Set(x => x.ModifiedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<Account> { ReturnDocument = ReturnDocument.After });

        return result ?? throw new KeyNotFoundException($"Account {accountId} not found");
    }

    public async Task<bool> DeleteByUserIdAsync(string userId)
    {
        var result = await Collection.DeleteManyAsync(x => x.UserId == userId);
        return result.DeletedCount > 0;
    }
}

file class AccountIndexes : IndexesBuilder<Account>
{
    public AccountIndexes()
    {
        Index(Ascending("provider").Ascending("providerAccountId")).Unique();
        Index(Ascending("userId")).Sparse();
    }
}
