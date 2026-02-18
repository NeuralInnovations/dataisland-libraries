using Dataisland.MongoDB.Entity;
using Dataisland.Workspaces.Domain.ValueObjects;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Workspaces.Domain.Entities;

[BsonIgnoreExtraElements(Inherited = true)]
public class Workspace : EntityCas
{
    [BsonElement("profile")]
    public WorkspaceProfile Profile { get; set; } = new();

    [BsonElement("metadata")]
    public WorkspaceMetadata Metadata { get; set; } = new();
}
