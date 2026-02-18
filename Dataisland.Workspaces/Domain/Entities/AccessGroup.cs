using Dataisland.Contracts.Shared;
using Dataisland.Core.Domain.ValueObjects;
using Dataisland.MongoDB.Entity;
using Dataisland.Workspaces.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Workspaces.Domain.Entities;

[BsonIgnoreExtraElements(Inherited = true)]
public class AccessGroup : EntityBase
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("type")]
    public AccessGroupType Type { get; set; } = AccessGroupType.ManuallyCreated;

    [BsonElement("organizationId")]
    public ObjectId OrganizationId { get; set; }

    [BsonElement("permits")]
    public AccessGroupPermits Permits { get; set; } = new();

    [BsonElement("regulation")]
    public AccessGroupRegulation Regulation { get; set; } = new();

    [BsonElement("memberIds")]
    public List<ObjectId> MemberIds { get; set; } = [];

    [BsonElement("metadata")]
    public BaseMetadata Metadata { get; set; } = new();
}
