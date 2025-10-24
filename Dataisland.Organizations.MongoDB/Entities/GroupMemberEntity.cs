using Dataisland.Organizations.Users;
using MongoDB.Bson;

namespace Dataisland.Organizations.MongoDB.Entities;

[Serializable]
public class GroupMemberEntity : EntityId
{
    public ObjectId GroupId { get; set; }
    public UserId UserId { get; set; }
}