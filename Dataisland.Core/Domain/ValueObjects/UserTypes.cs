using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.ValueObjects;

public class UserSettings
{
    [BsonElement("activeOrganizationId")]
    public ObjectId ActiveOrganizationId { get; set; }

    [BsonElement("activeWorkspaceId")]
    public ObjectId ActiveWorkspaceId { get; set; }
}

public class UserTelegramProfile
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("authDate")]
    public DateTime AuthDate { get; set; }

    [BsonElement("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [BsonElement("hash")]
    public string Hash { get; set; } = string.Empty;

    [BsonElement("lastName")]
    public string LastName { get; set; } = string.Empty;

    [BsonElement("photoUrl")]
    public string PhotoUrl { get; set; } = string.Empty;

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("chatId")]
    public string ChatId { get; set; } = string.Empty;
}

public class AnonymousUserMetadata
{
    [BsonElement("isEnabled")]
    public bool IsEnabled { get; set; }

    [BsonElement("hash")]
    public string Hash { get; set; } = string.Empty;

    [BsonElement("token")]
    public string Token { get; set; } = string.Empty;
}
