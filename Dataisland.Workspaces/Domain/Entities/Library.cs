using Dataisland.MongoDB.Entity;
using Dataisland.Workspaces.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Workspaces.Domain.Entities;

[BsonIgnoreExtraElements(Inherited = true)]
public class Library : EntityBase
{
    [BsonElement("profile")]
    public LibraryProfile Profile { get; set; } = new();

    [BsonElement("organizationIds")]
    public List<ObjectId> OrganizationIds { get; set; } = [];

    [BsonElement("isDeleted")]
    public bool IsDeleted { get; set; }
}
