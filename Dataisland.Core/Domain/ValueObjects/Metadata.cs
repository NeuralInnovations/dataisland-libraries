using Dataisland.Contracts.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.ValueObjects;

public class BaseMetadata
{
    [BsonElement("isDeleted")]
    public bool IsDeleted { get; set; }

    [BsonElement("lastMigrateAt")]
    public DateTime? LastMigrateAt { get; set; }

    [BsonElement("data")]
    public string Data { get; set; } = string.Empty;
}

public class OrganizationMetadata : BaseMetadata
{
    [BsonElement("creatorId")]
    public ObjectId CreatorId { get; set; }
}

public class UserMetadata : BaseMetadata
{
    [BsonElement("lastLoginAt")]
    public DateTime LastLoginAt { get; set; }

    [BsonElement("invitedBy")]
    public ObjectId InvitedBy { get; set; }

    [BsonElement("items")]
    public List<KeyValueItem> Items { get; set; } = [];
}

public class IconMetadata : BaseMetadata
{
    [BsonElement("organizationId")]
    public ObjectId OrganizationId { get; set; }

    [BsonElement("uploadedBy")]
    public ObjectId UploadedBy { get; set; }

    [BsonElement("resourceId")]
    public ObjectId ResourceId { get; set; }

    [BsonElement("resourceType")]
    public Resource ResourceType { get; set; }
}

public class KeyValueItem
{
    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;

    [BsonElement("value")]
    public string Value { get; set; } = string.Empty;
}
