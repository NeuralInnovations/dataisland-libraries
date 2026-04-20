using Dataisland.Contracts.Shared;
using Dataisland.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dataisland.Core.Services;

/// <summary>
/// Snapshot of the fields a downstream worker needs to decide whether the message it's
/// holding is still the current dispatch or a phantom. Intentionally a tiny projection —
/// we don't want to pull the full MedicalData shape into <c>Dataisland.Core</c> just for
/// a two-field check.
/// </summary>
public record MedicalDataDispatchState(
    string Id,
    MedicalDataStatus Status,
    string? CurrentDispatchId);

/// <summary>
/// Cross-service lookup so Service.MedicalFlow (which doesn't own the medical-data repository)
/// can cheaply verify a case's current status and dispatch id before doing work. The full
/// repository lives in Service.Api's infrastructure layer — duplicating it across libraries
/// would pull too much; this lookup reads the minimum projection directly from Mongo.
/// </summary>
public interface IMedicalDataDispatchLookup
{
    Task<MedicalDataDispatchState?> GetAsync(string caseId, CancellationToken ct = default);

    /// <summary>
    /// Bump the case's ModifiedAt so StuckCaseRecoveryWorker doesn't treat a legitimately
    /// long-running case as a dead one. Only writes if status is still InProgress AND the
    /// dispatch id matches — if either changed (paused, stolen by recovery, rerun), it's a
    /// no-op so we don't accidentally revive an abandoned dispatch. Returns true when the
    /// write actually landed.
    /// </summary>
    Task<bool> HeartbeatAsync(string caseId, string dispatchId, CancellationToken ct = default);
}

/// <summary>
/// No-op fallback used when MongoDB isn't wired into the host service (e.g. local
/// Service.MedicalFlow without a connection string). Always returns null — callers treat
/// null as "cannot verify, let the message through" so processing still works, it just
/// loses the phantom-detection defence until MongoDB is configured.
/// </summary>
public class NullMedicalDataDispatchLookup : IMedicalDataDispatchLookup
{
    public Task<MedicalDataDispatchState?> GetAsync(string caseId, CancellationToken ct = default)
        => Task.FromResult<MedicalDataDispatchState?>(null);

    public Task<bool> HeartbeatAsync(string caseId, string dispatchId, CancellationToken ct = default)
        => Task.FromResult(false);
}

public class MedicalDataDispatchLookup : IMedicalDataDispatchLookup
{
    private readonly IMongoCollection<BsonDocument> _collection;

    public MedicalDataDispatchLookup(IMongoDBProvider provider)
    {
        // The collection name matches the one declared in Service.Api's MedicalDataRepository
        // (base("medicalData", ...)). Hardcoding is the least bad option — a shared constant
        // would need to be imported from Service.Api, and we're deliberately keeping this
        // library free of that dependency.
        _collection = provider.Database.GetCollection<BsonDocument>("medicalData");
    }

    public async Task<MedicalDataDispatchState?> GetAsync(string caseId, CancellationToken ct = default)
    {
        // ObjectId is how BsonId is persisted; the string representation is what callers carry.
        if (!ObjectId.TryParse(caseId, out var oid))
            return null;

        var projection = Builders<BsonDocument>.Projection
            .Include("status")
            .Include("currentDispatchId");

        var doc = await _collection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", oid))
            .Project(projection)
            .FirstOrDefaultAsync(ct);

        if (doc is null) return null;

        var status = doc.Contains("status")
            ? (MedicalDataStatus)doc["status"].ToInt32()
            : MedicalDataStatus.InQueue;

        var dispatchId = doc.Contains("currentDispatchId") && !doc["currentDispatchId"].IsBsonNull
            ? doc["currentDispatchId"].AsString
            : null;

        return new MedicalDataDispatchState(caseId, status, dispatchId);
    }

    public async Task<bool> HeartbeatAsync(string caseId, string dispatchId, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(caseId, out var oid))
            return false;

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", oid),
            Builders<BsonDocument>.Filter.Eq("status", (int)MedicalDataStatus.InProgress),
            Builders<BsonDocument>.Filter.Eq("currentDispatchId", dispatchId));

        var update = Builders<BsonDocument>.Update.Set("_modified", DateTime.UtcNow);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}
