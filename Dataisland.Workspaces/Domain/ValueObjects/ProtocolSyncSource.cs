using Dataisland.Contracts.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Workspaces.Domain.ValueObjects;

public class ProtocolSyncSource
{
    [BsonElement("sourceType")]
    [BsonRepresentation(BsonType.String)]
    public ProtocolSourceType SourceType { get; set; } = ProtocolSourceType.Generic;

    [BsonElement("seedUrl")]
    public string SeedUrl { get; set; } = string.Empty;

    [BsonElement("linkPattern")]
    public string LinkPattern { get; set; } = string.Empty;

    [BsonElement("enabled")]
    public bool Enabled { get; set; } = true;

    [BsonElement("syncIntervalHours")]
    public int SyncIntervalHours { get; set; } = 24;

    [BsonElement("lastSyncAt")]
    public DateTime? LastSyncAt { get; set; }

    [BsonElement("maxConcurrentDownloads")]
    public int MaxConcurrentDownloads { get; set; } = 5;

    [BsonElement("credentials")]
    public Dictionary<string, string>? Credentials { get; set; }
}
