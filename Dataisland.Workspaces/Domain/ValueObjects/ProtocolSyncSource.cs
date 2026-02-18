using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Workspaces.Domain.ValueObjects;

public class ProtocolSyncSource
{
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
}
