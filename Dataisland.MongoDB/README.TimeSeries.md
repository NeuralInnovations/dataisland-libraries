Time-series repository quickstart

This library now includes an abstract TimeSeriesRepository<TDocument> to work with MongoDB time-series collections in a type-safe, convenient way.

Highlights
- Ensures a time-series collection exists (created on first use) with:
  - time field
  - optional meta field
  - optional granularity
  - optional expireAfter (TTL)
- Ready-to-use helpers:
  - InsertAsync / InsertManyAsync
  - GetRangeAsync(start, end, extraFilter?, projection?, sort?, limit?, readFromSecondary?)
  - GetRangeByMetaAsync(...)
  - GetLatestAsync(filter?) / GetLatestByMetaAsync(meta, extraFilter?)
  - CountAsync(...) / CountByMetaAsync(...)
  - DeleteOlderThanAsync(threshold) / DeleteOlderThanByMetaAsync(threshold, meta)
- Works with Primary/Secondary read preferences for read-heavy workloads

Create a repository

1) With expressions

public sealed class MetricsRepository(IMongoDBProvider provider)
  : TimeSeriesRepository<MetricDocument>(
      collectionName: "metrics",
      provider: provider,
      timeField: x => x.Timestamp,
      metaField: x => x.Tags.DeviceId,
      granularity: TimeSeriesGranularity.Minutes,
      expireAfter: TimeSpan.FromDays(30)
    )
{
    // Optionally customize collection create options
    protected override void ConfigureCreateCollectionOptions(CreateCollectionOptions options)
    {
        // e.g., options.Capped = true; options.MaxSize = ...;
    }
}

2) With field names

public sealed class LogsRepository(IMongoDBProvider provider)
  : TimeSeriesRepository<LogEntry>(
      collectionName: "logs",
      provider: provider,
      timeFieldName: "ts",
      metaFieldName: "meta.host",
      granularity: TimeSeriesGranularity.Hours,
      expireAfter: TimeSpan.FromDays(7)
    )
{ }

Use in DI

services.AddMongoDB(mongoOptions);
services.AddRepository<MetricsRepository>();

// or map to an API interface
services.AddRepository<IMetricsRepository, MetricsRepository>();

Example document

public sealed class MetricDocument
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public MetricTags Tags { get; set; } = new();
}

public sealed class MetricTags
{
    public string DeviceId { get; set; } = string.Empty;
}

Queries

var repo = provider.GetRequiredService<MetricsRepository>();

// Insert
await repo.InsertAsync(new MetricDocument { Timestamp = DateTime.UtcNow, Value = 42, Tags = new() { DeviceId = "A1" } });

// Range
var lastHour = await repo.GetRangeAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, limit: 1000);

// Range by meta
var devA = await repo.GetRangeByMetaAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, meta: "A1");

// Latest point overall
var latest = await repo.GetLatestAsync();

// Latest for a device
var latestA = await repo.GetLatestByMetaAsync("A1");

// Count
var count = await repo.CountAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

// Delete old data
var deleted = await repo.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-30));

Notes
- If the collection already exists with different time-series options, MongoDB cannot alter them later. Adjust in code and create a new collection if needed.
- ExpireAfter sets the collection-level TTL for time-series measurements. You generally should not create a separate TTL index for time-series collections.
- Meta field is optional. If omitted, Meta* helpers simply no-op the meta predicate.

