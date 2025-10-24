using Dataisland.Organizations.Users;

namespace Dataisland.Organizations.MongoDB.Entities;

[Serializable]
public class UserEntity : EntityId
{
    public UserProfile Profile { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}