using Dataisland.Contracts.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.ValueObjects;

[BsonIgnoreExtraElements]
public class OrganizationMemberRole
{
    [BsonElement("userId")]
    public ObjectId UserId { get; set; }

    [BsonElement("role")]
    public OrganizationRole Role { get; set; } = OrganizationRole.Admin;

    [BsonElement("doctorId")]
    public string? DoctorId { get; set; }
}
