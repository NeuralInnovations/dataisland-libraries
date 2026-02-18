using Dataisland.Core.Domain.ValueObjects;
using Dataisland.MongoDB.Entity;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.Entities;

[BsonIgnoreExtraElements(Inherited = true)]
public class Organization : EntityCas
{
    [BsonElement("profile")]
    public OrganizationProfile Profile { get; set; } = new();

    [BsonElement("metadata")]
    public OrganizationMetadata Metadata { get; set; } = new();

    [BsonElement("memberIds")]
    public List<ObjectId> MemberIds { get; set; } = [];

    [BsonElement("deletedMemberIds")]
    public List<ObjectId> DeletedMemberIds { get; set; } = [];
}
