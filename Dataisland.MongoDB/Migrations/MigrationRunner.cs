using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;

namespace Dataisland.MongoDB.Migrations;

public class MigrationRunner : IMigrationRunner
{
    private readonly IMongoDBProvider _provider;
    private readonly IReadOnlyList<MigrationDescriptor> _migrations;
    private readonly ILogger<MigrationRunner>? _logger;

    private const string CollectionName = "__migrations"; // internal framework collection
    private static readonly TimeSpan StaleRunningThreshold = TimeSpan.FromMinutes(5); // recovery window

    // Direction constants
    private const string DirectionUp = "Up";
    private const string DirectionDown = "Down";

    // Status constants
    private const string StatusRunning = "Running";
    private const string StatusSucceeded = "Succeeded";
    private const string StatusFailed = "Failed";

    // Index name constants
    private const string IndexVersionDirectionStatus = "version_direction_status_idx";
    private const string IndexVersion = "version_idx";
    private const string IndexUniqueUpRunningOrSucceeded = "uniq_up_running_or_succeeded";
    private const string LegacyIndexUniqueUpSucceeded = "uniq_up_succeeded_partial";

    private record MigrationDescriptor(int Version, string Name, string? Description, Migration Instance);

    private class MigrationEntry
    {
        [BsonId] public ObjectId Id { get; set; }
        public int Version { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime AppliedAtUtc { get; set; }
        public long DurationMs { get; set; }
        public string Direction { get; set; } = string.Empty; // Up / Down
        public string Status { get; set; } = string.Empty; // Running / Succeeded / Failed
        public string? Error { get; set; }
    }

    public MigrationRunner(
        IMongoDBProvider provider,
        IEnumerable<Migration> migrations,
        ILogger<MigrationRunner>? logger = null)
    {
        _provider = provider;
        _logger = logger;
        _migrations = migrations
            .Select(m =>
            {
                var attr = m.GetType().GetCustomAttributes(typeof(MigrationAttribute), false)
                    .Cast<MigrationAttribute>()
                    .SingleOrDefault();
                if (attr is null)
                {
                    throw new InvalidOperationException(
                        $"Migration '{m.GetType().FullName}' must be decorated with [Migration(...)] attribute.");
                }

                return new MigrationDescriptor(attr.Version, m.GetType().Name, attr.Description, m);
            })
            .OrderBy(m => m.Version)
            .ToList();

        // Validate uniqueness of versions in provided migration set
        var duplicates = _migrations.GroupBy(m => m.Version).Where(g => g.Count() > 1).ToList();
        if (duplicates.Count != 0)
        {
            var dupText = string.Join(", ", duplicates.Select(d => d.Key));
            throw new InvalidOperationException($"Duplicate migration versions detected: {dupText}");
        }
    }

    public async Task RunAsync(MigrationVersion toVersion, CancellationToken cancellationToken)
    {
        var collection = _provider.Database.GetCollection<MigrationEntry>(CollectionName);
        await EnsureIndexesAsync(collection, cancellationToken);

        // Load applied (Succeeded Up) migrations to determine current version
        var applied = await collection.Find(
                Builders<MigrationEntry>.Filter.Where(e => e.Direction == DirectionUp && e.Status == StatusSucceeded))
            .Project(e => new { e.Version })
            .ToListAsync(cancellationToken);

        var appliedVersions = applied.Select(a => a.Version).ToHashSet();

        var currentVersion = appliedVersions.Count == 0 ? 0 : appliedVersions.Max();
        var highestKnownVersion = _migrations.Count == 0 ? 0 : _migrations.Max(m => m.Version);

        int targetVersion = toVersion.Version == int.MaxValue
            ? highestKnownVersion
            : toVersion.Version;

        if (targetVersion > highestKnownVersion)
        {
            _logger?.LogWarning(
                "Requested target version {Requested} exceeds highest known migration {Highest}. Falling back to highest.",
                targetVersion, highestKnownVersion);
            targetVersion = highestKnownVersion;
        }

        if (targetVersion == currentVersion)
        {
            _logger?.LogInformation("MongoDB migrations: database already at target version {Version}", currentVersion);
            return;
        }

        if (targetVersion > currentVersion)
        {
            // Upgrade path
            var toApply = _migrations
                .Where(m => m.Version > currentVersion && m.Version <= targetVersion)
                .OrderBy(m => m.Version)
                .ToList();

            _logger?.LogInformation("Applying {Count} MongoDB migrations UP from {Current} to {Target}: {Versions}",
                toApply.Count, currentVersion, targetVersion, string.Join(",", toApply.Select(m => m.Version)));

            foreach (var descriptor in toApply)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteMigrationAsync(descriptor, isUp: true, collection, cancellationToken);
            }
        }
        else
        {
            // Downgrade path
            var toRevert = _migrations
                .Where(m => m.Version > targetVersion && m.Version <= currentVersion)
                .OrderByDescending(m => m.Version)
                .ToList();

            _logger?.LogInformation("Reverting {Count} MongoDB migrations DOWN from {Current} to {Target}: {Versions}",
                toRevert.Count, currentVersion, targetVersion, string.Join(",", toRevert.Select(m => m.Version)));

            foreach (var descriptor in toRevert)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Ensure we only DOWN ones that were actually applied
                if (!appliedVersions.Contains(descriptor.Version))
                {
                    _logger?.LogInformation(
                        "Skipping migration {Version} ({Name}) Down because Up was never successfully applied",
                        descriptor.Version, descriptor.Name);
                    continue;
                }

                await ExecuteMigrationAsync(descriptor, isUp: false, collection, cancellationToken);
            }
        }
    }

    private async Task ExecuteMigrationAsync(
        MigrationDescriptor descriptor,
        bool isUp,
        IMongoCollection<MigrationEntry> collection,
        CancellationToken cancellationToken)
    {
        var migration = descriptor.Instance;
        migration.InternalClient = _provider.Client;
        migration.InternalDatabase = _provider.Database;

        if (isUp)
        {
            // Fast skip if already succeeded
            var existsSucceeded = await collection.Find(x =>
                    x.Version == descriptor.Version && x.Direction == DirectionUp && x.Status == StatusSucceeded)
                .Project(x => x.Id)
                .Limit(1)
                .AnyAsync(cancellationToken);
            if (existsSucceeded)
            {
                _logger?.LogInformation(
                    "Skipping migration {Version} ({Name}) Up because it's already marked Succeeded.",
                    descriptor.Version, descriptor.Name);
                return;
            }

            // Stale RUNNING recovery: find existing running record, if older than threshold -> mark Failed so we can retry
            var runningRecord = await collection.Find(x =>
                    x.Version == descriptor.Version && x.Direction == DirectionUp && x.Status == StatusRunning)
                .Sort(Builders<MigrationEntry>.Sort.Ascending(x => x.AppliedAtUtc))
                .FirstOrDefaultAsync(cancellationToken);
            if (runningRecord != null)
            {
                var age = DateTime.UtcNow - runningRecord.AppliedAtUtc;
                if (age > StaleRunningThreshold)
                {
                    _logger?.LogWarning(
                        "Detected stale RUNNING migration {Version} ({Name}) older than {AgeMinutes:F1} minutes. Marking as Failed and retrying.",
                        descriptor.Version, descriptor.Name, age.TotalMinutes);
                    var update = Builders<MigrationEntry>.Update
                        .Set(x => x.Status, StatusFailed)
                        .Set(x => x.Error, $"Recovered stale RUNNING (age {age}).")
                        .Set(x => x.DurationMs, (long)age.TotalMilliseconds);
                    try
                    {
                        await collection.UpdateOneAsync(x => x.Id == runningRecord.Id, update,
                            cancellationToken: cancellationToken);
                    }
                    catch
                    {
                        _logger?.LogError($"Can't update migration {runningRecord.Id} with failed recovered RUNNING status.");
                    }
                }
                else
                {
                    _logger?.LogInformation(
                        "Another instance appears to be running migration {Version} ({Name}) (age {AgeSeconds:F1}s). Skipping.",
                        descriptor.Version, descriptor.Name, age.TotalSeconds);
                    return; // still within threshold -> assume active
                }
            }
        }

        if (isUp && !migration.ShouldUp())
        {
            _logger?.LogInformation("Skipping migration {Version} ({Name}) Up due to ShouldUp=false",
                descriptor.Version, descriptor.Name);
            return;
        }

        if (!isUp && !migration.ShouldDown())
        {
            _logger?.LogInformation("Skipping migration {Version} ({Name}) Down due to ShouldDown=false",
                descriptor.Version, descriptor.Name);
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        ObjectId insertedId = ObjectId.Empty;
        MigrationEntry? entry = null;

        if (isUp)
        {
            // Insert RUNNING marker under partial unique index (Up + Status in [Running, Succeeded])
            entry = new MigrationEntry
            {
                Id = ObjectId.GenerateNewId(),
                Version = descriptor.Version,
                Name = descriptor.Name,
                Description = descriptor.Description,
                AppliedAtUtc = DateTime.UtcNow,
                Direction = DirectionUp,
                Status = StatusRunning,
                DurationMs = 0
            };
            try
            {
                await collection.InsertOneAsync(entry, cancellationToken: cancellationToken);
                insertedId = entry.Id;
            }
            catch (MongoWriteException mwx) when (mwx.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                _logger?.LogInformation(
                    "Concurrent attempt detected for migration {Version} ({Name}). Another instance inserted RUNNING/SUCCEEDED. Skipping.",
                    descriptor.Version, descriptor.Name);
                return;
            }
        }
        else
        {
            entry = new MigrationEntry
            {
                Id = ObjectId.GenerateNewId(),
                Version = descriptor.Version,
                Name = descriptor.Name,
                Description = descriptor.Description,
                AppliedAtUtc = DateTime.UtcNow,
                Direction = DirectionDown,
                Status = StatusRunning,
                DurationMs = 0
            };
        }

        try
        {
            if (isUp)
            {
                await migration.UpAsync();
            }
            else
            {
                await migration.DownAsync();
            }

            sw.Stop();
            var duration = sw.ElapsedMilliseconds;

            if (isUp)
            {
                var update = Builders<MigrationEntry>.Update
                    .Set(x => x.Status, StatusSucceeded)
                    .Set(x => x.DurationMs, duration)
                    .Set(x => x.Error, null)
                    .Set(x => x.AppliedAtUtc, entry!.AppliedAtUtc)
                    .Set(x => x.Description, entry.Description);
                await collection.UpdateOneAsync(x => x.Id == insertedId, update, cancellationToken: cancellationToken);
            }
            else
            {
                entry!.Status = StatusSucceeded;
                entry.DurationMs = duration;
                try
                {
                    await collection.InsertOneAsync(entry, cancellationToken: cancellationToken);
                }
                catch
                {
                    _logger?.LogError($"Can't insert migration {entry.Id} with succeeded status.");
                }
            }

            _logger?.LogInformation("MongoDB migration {Direction} {Version} ({Name}) succeeded in {Ms} ms",
                isUp ? DirectionUp : DirectionDown, descriptor.Version, descriptor.Name, duration);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var duration = sw.ElapsedMilliseconds;
            if (isUp && insertedId != ObjectId.Empty)
            {
                var updateFail = Builders<MigrationEntry>.Update
                    .Set(x => x.Status, StatusFailed)
                    .Set(x => x.Error, ex.ToString())
                    .Set(x => x.DurationMs, duration);
                try
                {
                    await collection.UpdateOneAsync(x => x.Id == insertedId, updateFail,
                        cancellationToken: cancellationToken);
                }
                catch
                {
                    _logger?.LogError($"Can't update migration {insertedId} with failed status.");
                }
            }
            else if (!isUp && entry != null)
            {
                entry.Status = StatusFailed;
                entry.Error = ex.ToString();
                entry.DurationMs = duration;
                try
                {
                    await collection.InsertOneAsync(entry, cancellationToken: cancellationToken);
                }
                catch
                {
                    _logger?.LogError($"Can't insert migration {entry.Id} with failed status.");
                }
            }

            _logger?.LogError(ex, "MongoDB migration {Direction} {Version} ({Name}) failed after {Ms} ms",
                isUp ? DirectionUp : DirectionDown, descriptor.Version, descriptor.Name, duration);
            throw;
        }
    }

    private static async Task EnsureIndexesAsync(IMongoCollection<MigrationEntry> collection, CancellationToken ct)
    {
        // 1. Cleanup duplicates of successful Up (keep earliest AppliedAtUtc)
        var succeededUp = await collection
            .Find(Builders<MigrationEntry>.Filter.Where(e => e.Direction == DirectionUp && e.Status == StatusSucceeded))
            .Project(x => new { x.Id, x.Version, x.AppliedAtUtc })
            .ToListAsync(ct);
        var duplicates = succeededUp.GroupBy(x => x.Version).Where(g => g.Count() > 1).ToList();
        if (duplicates.Count > 0)
        {
            var toRemove = duplicates.SelectMany(g => g.OrderBy(x => x.AppliedAtUtc).Skip(1).Select(x => x.Id))
                .ToList();
            if (toRemove.Count > 0)
            {
                await collection.DeleteManyAsync(Builders<MigrationEntry>.Filter.In(e => e.Id, toRemove), ct);
            }
        }

        // 2. Drop legacy partial unique index if present (name: uniq_up_succeeded_partial)
        try
        {
            await collection.Indexes.DropOneAsync(LegacyIndexUniqueUpSucceeded, ct);
        }
        catch
        {
        }

        // 3. Create indexes
        var indexKeys = Builders<MigrationEntry>.IndexKeys
            .Ascending(e => e.Version)
            .Ascending(e => e.Direction)
            .Ascending(e => e.Status);
        var indexModel =
            new CreateIndexModel<MigrationEntry>(indexKeys,
                new CreateIndexOptions { Name = IndexVersionDirectionStatus });

        var singleVersionIndex = new CreateIndexModel<MigrationEntry>(
            Builders<MigrationEntry>.IndexKeys.Ascending(e => e.Version),
            new CreateIndexOptions { Name = IndexVersion, Unique = false });

        var uniqueUpRunningOrSucceeded = new CreateIndexModel<MigrationEntry>(
            Builders<MigrationEntry>.IndexKeys.Ascending(e => e.Version),
            new CreateIndexOptions<MigrationEntry>
            {
                Name = IndexUniqueUpRunningOrSucceeded,
                Unique = true,
                PartialFilterExpression = Builders<MigrationEntry>.Filter.And(
                    Builders<MigrationEntry>.Filter.Eq(e => e.Direction, DirectionUp),
                    Builders<MigrationEntry>.Filter.In(e => e.Status, new[] { StatusRunning, StatusSucceeded }))
            });

        await collection.Indexes.CreateManyAsync(new[] { indexModel, singleVersionIndex, uniqueUpRunningOrSucceeded },
            ct);
    }
}