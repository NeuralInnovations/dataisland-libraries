using Dataisland.MongoDB.Entity;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.Entities;

[BsonIgnoreExtraElements(Inherited = true)]
public class Invite : EntityBase
{
    [BsonElement("organizationId")]
    public ObjectId? OrganizationId { get; set; }

    [BsonElement("userMetadata")]
    public List<string> UserMetadata { get; set; } = [];

    [BsonElement("accessGroupIds")]
    public List<ObjectId> AccessGroupIds { get; set; } = [];

    [BsonElement("expiration")]
    public DateTime Expiration { get; set; }
}
