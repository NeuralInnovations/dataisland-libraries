using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.ValueObjects;

public class InviteValidationParameters
{
    [BsonElement("domain")]
    public string Domain { get; set; } = string.Empty;
}
