using MongoDB.Bson;

namespace Dataisland.Organizations.MongoDB.Entities;

[Serializable]
public class OrganizationMemberEntity : EntityId
{
    public ObjectId OrganizationId { get; set; }
    public ObjectId UserId { get; set; }
    public OrganizationMemberStatus Status { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
}