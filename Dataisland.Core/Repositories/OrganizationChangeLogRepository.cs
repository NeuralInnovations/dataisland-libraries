using Dataisland.Core.Domain.Entities;
using Dataisland.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Core.Repositories;

public interface IOrganizationChangeLogRepository : IRepository
{
    Task<OrganizationChangeLog?> GetByOrganizationIdAsync(ObjectId organizationId);
    Task<OrganizationChangeLog> CreateAsync(OrganizationChangeLog log);
    Task AddChangeAsync(ObjectId organizationId, ChangeLogRecord record);
}

public class OrganizationChangeLogRepository : RepositoryWithIndex<OrganizationChangeLog>, IOrganizationChangeLogRepository
{
    public OrganizationChangeLogRepository(IMongoDBProvider provider)
        : base("organizationChangeLogs", provider, new ChangeLogIndexes()) { }

    public async Task<OrganizationChangeLog?> GetByOrganizationIdAsync(ObjectId organizationId) =>
        await Collection.Find(x => x.OrganizationId == organizationId).FirstOrDefaultAsync();

    public async Task<OrganizationChangeLog> CreateAsync(OrganizationChangeLog log)
    {
        await Collection.InsertOneAsync(log);
        return log;
    }

    public async Task AddChangeAsync(ObjectId organizationId, ChangeLogRecord record) =>
        await Collection.UpdateOneAsync(
            x => x.OrganizationId == organizationId,
            Builders<OrganizationChangeLog>.Update
                .Push(x => x.Changes, record)
                .Inc(x => x.RecordCount, 1)
                .Set(x => x.ModifiedAt, DateTime.UtcNow),
            new UpdateOptions { IsUpsert = false });
}

file class ChangeLogIndexes : IndexesBuilder<OrganizationChangeLog>
{
    public ChangeLogIndexes()
    {
        Index(Ascending("organizationId"));
    }
}
