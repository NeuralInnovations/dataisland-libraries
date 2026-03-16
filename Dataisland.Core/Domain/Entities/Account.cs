using Dataisland.MongoDB.Entity;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.Entities;

/// <summary>
/// Links an external identity provider account to an internal user.
/// Unique constraint on (Provider, ProviderAccountId) ensures one account per provider identity.
/// Multiple accounts can point to the same UserId, enabling multi-provider auth.
/// </summary>
[BsonIgnoreExtraElements(Inherited = true)]
public class Account : EntityBase
{
    [BsonElement("provider")]
    public string Provider { get; set; } = string.Empty;

    [BsonElement("providerAccountId")]
    public string ProviderAccountId { get; set; } = string.Empty;

    [BsonElement("userId")]
    [BsonIgnoreIfNull]
    public string? UserId { get; set; }

    [BsonIgnore]
    public bool HasUser => UserId is not null;
}
