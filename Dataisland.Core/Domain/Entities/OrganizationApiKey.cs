using Dataisland.MongoDB.Entity;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.Entities;

[BsonIgnoreExtraElements(Inherited = true)]
public class OrganizationApiKey : EntityBase
{
    [BsonElement("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [BsonElement("keyName")]
    public string KeyName { get; set; } = string.Empty;

    [BsonElement("organizationId")]
    public ObjectId OrganizationId { get; set; }

    [BsonElement("jwtSecretBase64")]
    public string JwtSecretBase64 { get; set; } = string.Empty;

    [BsonElement("tokensCreated")]
    public int TokensCreated { get; set; }

    [BsonElement("lastTokenCreatedAt")]
    public DateTime? LastTokenCreatedAt { get; set; }

    [BsonElement("accessGroupIds")]
    public List<ObjectId> AccessGroupIds { get; set; } = [];
}
