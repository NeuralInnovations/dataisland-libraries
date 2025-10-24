using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Organizations.MongoDB.Entities;

public abstract class EntityId : IEntityId
{
    [BsonId]
    public ObjectId Id { get; set; }
}

public interface IEntityId
{
    ObjectId Id { get; set; }
}