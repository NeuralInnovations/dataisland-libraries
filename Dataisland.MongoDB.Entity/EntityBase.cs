using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.MongoDB.Entity;

public abstract class EntityBase
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("_created")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("_modified")]
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Entity with Compare-and-Swap versioning for optimistic concurrency.
/// </summary>
public abstract class EntityCas : EntityBase
{
    [BsonElement("_cas")]
    public long Cas { get; set; }
}
