namespace Dataisland.Organizations.Users;

[Serializable]
public record User(UserId Id, UserProfile Profile, DateTime CreatedAt, DateTime ModifiedAt);