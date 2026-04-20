using Dataisland.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Core.Services;

/// <summary>
/// Mongo-backed singleton lease. Service.Api runs with <c>replicas: 2</c> so both pods
/// execute the queue workers on their own 5s ticks — claim is atomic at the document level,
/// but the double-polling still wastes Mongo reads and widens the window where
/// StuckCaseRecoveryWorker and the dispatcher race on the same case. Rather than invent a
/// distributed coordinator, we use a TTL-style lease doc: each pod tries to extend ownership
/// on a fixed interval; whoever wins runs the tick, the other skips.
/// </summary>
public interface IDispatcherLeaseService
{
    /// <summary>
    /// Attempt to acquire or extend a named lease. Returns true when this pod holds the lease
    /// for at least <paramref name="leaseDuration"/> starting now. The lease is owned by a
    /// caller-supplied ownerId (typically the pod name); any other pod calling with a
    /// different ownerId while the lease is active returns false.
    /// </summary>
    Task<bool> TryAcquireAsync(
        string leaseName,
        string ownerId,
        TimeSpan leaseDuration,
        CancellationToken ct = default);
}

public class DispatcherLeaseService : IDispatcherLeaseService
{
    private readonly IMongoCollection<BsonDocument> _collection;

    public DispatcherLeaseService(IMongoDBProvider provider)
    {
        _collection = provider.Database.GetCollection<BsonDocument>("dispatcherLeases");
    }

    public async Task<bool> TryAcquireAsync(
        string leaseName,
        string ownerId,
        TimeSpan leaseDuration,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now + leaseDuration;

        // Filter: lease doc doesn't exist yet, OR it's expired, OR we already own it.
        // Combined with an upsert, this is the standard "acquire-or-extend" pattern — a
        // competing pod whose lease is still valid sees no match and gets DuplicateKey
        // from the upsert, which we treat as "someone else has it".
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", leaseName),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Lte("expiresAt", now),
                Builders<BsonDocument>.Filter.Eq("owner", ownerId)));

        var update = Builders<BsonDocument>.Update
            .Set("owner", ownerId)
            .Set("acquiredAt", now)
            .Set("expiresAt", expiresAt)
            .SetOnInsert("_id", leaseName);

        try
        {
            var result = await _collection.UpdateOneAsync(
                filter,
                update,
                new UpdateOptions { IsUpsert = true },
                cancellationToken: ct);

            // If we inserted or modified the doc, we own the lease. ModifiedCount can be 0 on
            // a rewrite with identical values, so we check both MatchedCount (we found our own)
            // and UpsertedId (we inserted new).
            return result.UpsertedId is not null || result.MatchedCount > 0;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another pod's lease is still valid and we hit the unique _id constraint.
            return false;
        }
    }
}
