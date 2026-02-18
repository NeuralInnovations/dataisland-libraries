using Dataisland.Core.Domain.Entities;
using Dataisland.Core.Domain.Enums;
using Dataisland.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Core.Repositories;

public interface IUserLimitRecordRepository : IRepository
{
    Task<UserLimitRecord?> GetByUserAndActionAsync(ObjectId userId, LimitActionType action);
    Task<UserLimitRecord> CreateAsync(UserLimitRecord record);
    Task UpdateAsync(UserLimitRecord record);
}

public class UserLimitRecordRepository : RepositoryWithIndex<UserLimitRecord>, IUserLimitRecordRepository
{
    public UserLimitRecordRepository(IMongoDBProvider provider)
        : base("userLimitRecords", provider, new UserLimitRecordIndexes()) { }

    public async Task<UserLimitRecord?> GetByUserAndActionAsync(ObjectId userId, LimitActionType action) =>
        await Collection.Find(x => x.UserId == userId && x.Action == action).FirstOrDefaultAsync();

    public async Task<UserLimitRecord> CreateAsync(UserLimitRecord record)
    {
        await Collection.InsertOneAsync(record);
        return record;
    }

    public async Task UpdateAsync(UserLimitRecord record)
    {
        record.ModifiedAt = DateTime.UtcNow;
        await Collection.ReplaceOneAsync(x => x.Id == record.Id, record);
    }
}

file class UserLimitRecordIndexes : IndexesBuilder<UserLimitRecord>
{
    public UserLimitRecordIndexes()
    {
        Index(Ascending("userId"));
        Index(Ascending("action"));
    }
}
