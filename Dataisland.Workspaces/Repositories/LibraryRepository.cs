using Dataisland.Contracts.Shared;
using Dataisland.MongoDB;
using Dataisland.Workspaces.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Workspaces.Repositories;

public interface ILibraryRepository : IRepository
{
    Task<Library?> GetByIdAsync(string id);
    Task<List<Library>> GetByOrganizationIdAsync(ObjectId organizationId);
    Task<List<Library>> GetLocalLibrariesAsync();
    Task<Library> CreateAsync(Library library);
    Task UpdateAsync(Library library);
    Task SoftDeleteAsync(string id);
    Task RemoveOrganizationFromAllAsync(ObjectId organizationId);
}

public class LibraryRepository : RepositoryWithIndex<Library>, ILibraryRepository
{
    public LibraryRepository(IMongoDBProvider provider)
        : base("libraries", provider, new LibraryIndexes()) { }

    public async Task<Library?> GetByIdAsync(string id) =>
        await Collection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task<List<Library>> GetByOrganizationIdAsync(ObjectId organizationId) =>
        await Collection.Find(x => x.OrganizationIds.Contains(organizationId) && !x.IsDeleted).ToListAsync();

    public async Task<List<Library>> GetLocalLibrariesAsync() =>
        await Collection.Find(x => x.Profile.Type == LibraryType.Local && !x.IsDeleted).ToListAsync();

    public async Task<Library> CreateAsync(Library library)
    {
        await Collection.InsertOneAsync(library);
        return library;
    }

    public async Task UpdateAsync(Library library)
    {
        library.ModifiedAt = DateTime.UtcNow;
        await Collection.ReplaceOneAsync(x => x.Id == library.Id, library);
    }

    public async Task SoftDeleteAsync(string id) =>
        await Collection.UpdateOneAsync(
            x => x.Id == id,
            Builders<Library>.Update
                .Set(x => x.IsDeleted, true)
                .Set(x => x.ModifiedAt, DateTime.UtcNow));

    public async Task RemoveOrganizationFromAllAsync(ObjectId organizationId) =>
        await Collection.UpdateManyAsync(
            x => x.OrganizationIds.Contains(organizationId),
            Builders<Library>.Update.Pull(x => x.OrganizationIds, organizationId));
}

file class LibraryIndexes : IndexesBuilder<Library>
{
    public LibraryIndexes()
    {
        Index(Ascending("organizationIds"));
    }
}
