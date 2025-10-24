using Dataisland.Organizations.Groups;
using MongoDB.Bson;

namespace Dataisland.Organizations.MongoDB.Entities;

[Serializable]
public class GroupEntity : EntityId
{
    public ObjectId OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public GroupStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}
