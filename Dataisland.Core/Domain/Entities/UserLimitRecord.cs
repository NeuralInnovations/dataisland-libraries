using Dataisland.Core.Domain.Enums;
using Dataisland.MongoDB.Entity;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.Entities;

[BsonIgnoreExtraElements(Inherited = true)]
public class UserLimitRecord : EntityCas
{
    [BsonElement("userId")]
    public ObjectId UserId { get; set; }

    [BsonElement("restoreData")]
    public string RestoreData { get; set; } = string.Empty;

    [BsonElement("action")]
    public LimitActionType Action { get; set; }

    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;

    [BsonElement("records")]
    public List<LimitDaysRecord> Records { get; set; } = [];

    [BsonElement("lastRefreshAt")]
    public DateTime LastRefreshAt { get; set; }
}

public class LimitDaysRecord
{
    [BsonElement("daysCount")]
    public int DaysCount { get; set; }

    [BsonElement("countLimit")]
    public int? CountLimit { get; set; }

    [BsonElement("tokenLimit")]
    public long? TokenLimit { get; set; }

    [BsonElement("activeTill")]
    public DateTime ActiveTill { get; set; }
}
