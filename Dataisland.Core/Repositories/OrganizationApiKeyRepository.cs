using Dataisland.Core.Domain.Entities;
using Dataisland.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Core.Repositories;

public interface IOrganizationApiKeyRepository : IRepository
{
    Task<OrganizationApiKey?> GetByIdAsync(string id);
    Task<OrganizationApiKey?> GetByApiKeyAsync(string apiKey);
    Task<List<OrganizationApiKey>> GetByOrganizationIdAsync(ObjectId organizationId);
    Task<OrganizationApiKey> CreateAsync(OrganizationApiKey key);
    Task UpdateAsync(OrganizationApiKey key);
    Task DeleteAsync(string id);
}

public class OrganizationApiKeyRepository : RepositoryWithIndex<OrganizationApiKey>, IOrganizationApiKeyRepository
{
    public OrganizationApiKeyRepository(IMongoDBProvider provider)
        : base("organizationApiKeys", provider, new OrganizationApiKeyIndexes()) { }

    public async Task<OrganizationApiKey?> GetByIdAsync(string id) =>
        await Collection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task<OrganizationApiKey?> GetByApiKeyAsync(string apiKey) =>
        await Collection.Find(x => x.ApiKey == apiKey).FirstOrDefaultAsync();

    public async Task<List<OrganizationApiKey>> GetByOrganizationIdAsync(ObjectId organizationId) =>
        await Collection.Find(x => x.OrganizationId == organizationId).ToListAsync();

    public async Task<OrganizationApiKey> CreateAsync(OrganizationApiKey key)
    {
        await Collection.InsertOneAsync(key);
        return key;
    }

    public async Task UpdateAsync(OrganizationApiKey key)
    {
        key.ModifiedAt = DateTime.UtcNow;
        await Collection.ReplaceOneAsync(x => x.Id == key.Id, key);
    }

    public async Task DeleteAsync(string id) =>
        await Collection.DeleteOneAsync(x => x.Id == id);
}

file class OrganizationApiKeyIndexes : IndexesBuilder<OrganizationApiKey>
{
    public OrganizationApiKeyIndexes()
    {
        Index(Ascending("apiKey"));
        Index(Ascending("organizationId"));
    }
}
