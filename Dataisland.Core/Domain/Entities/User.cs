using Dataisland.Core.Domain.ValueObjects;
using Dataisland.MongoDB.Entity;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.Entities;

[BsonIgnoreExtraElements(Inherited = true)]
public class User : EntityCas
{
    [BsonElement("externalIds")]
    public List<string> ExternalIds { get; set; } = [];

    [BsonElement("profile")]
    public UserProfile Profile { get; set; } = new();

    [BsonElement("metadata")]
    public UserMetadata Metadata { get; set; } = new();

    [BsonElement("settings")]
    public UserSettings Settings { get; set; } = new();

    [BsonElement("telegram")]
    public UserTelegramProfile? Telegram { get; set; }

    [BsonElement("anonymous")]
    public AnonymousUserMetadata? Anonymous { get; set; }
}
