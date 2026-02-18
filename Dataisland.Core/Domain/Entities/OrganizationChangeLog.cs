using Dataisland.Contracts.Shared;
using Dataisland.MongoDB.Entity;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.Entities;

[BsonIgnoreExtraElements(Inherited = true)]
public class OrganizationChangeLog : EntityBase
{
    [BsonElement("organizationId")]
    public ObjectId OrganizationId { get; set; }

    [BsonElement("organizationName")]
    public string OrganizationName { get; set; } = string.Empty;

    [BsonElement("recordCount")]
    public int RecordCount { get; set; }

    [BsonElement("changes")]
    public List<ChangeLogRecord> Changes { get; set; } = [];
}

public class ChangeLogRecord
{
    [BsonElement("modifiedAt")]
    public DateTime ModifiedAt { get; set; }

    [BsonElement("userId")]
    public ObjectId UserId { get; set; }

    [BsonElement("externalUserId")]
    public string ExternalUserId { get; set; } = string.Empty;

    [BsonElement("resource")]
    public AccessResource Resource { get; set; }

    [BsonElement("action")]
    public AccessAction Action { get; set; }

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;
}
