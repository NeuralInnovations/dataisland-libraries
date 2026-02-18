using Dataisland.Contracts.Shared;
using Dataisland.Core.Domain.ValueObjects;
using Dataisland.Workspaces.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Workspaces.Domain.ValueObjects;

public class ChunkingConfig
{
    [BsonElement("chunkSize")]
    public int ChunkSize { get; set; } = 512;

    [BsonElement("overlapSize")]
    public int OverlapSize { get; set; } = 50;
}

public class WorkspaceProfile : DescriptionProfile
{
    [BsonElement("isPublic")]
    public bool IsPublic { get; set; }

    [BsonElement("isShared")]
    public bool IsShared { get; set; }

    [BsonElement("chunkingConfig")]
    public ChunkingConfig ChunkingConfig { get; set; } = new();
}

public class WorkspaceMetadata : BaseMetadata
{
    [BsonElement("organizationId")]
    public ObjectId OrganizationId { get; set; }

    [BsonElement("protocolSyncSources")]
    public List<ProtocolSyncSource> ProtocolSyncSources { get; set; } = [];
}

public class LibraryProfile : DescriptionProfile
{
    [BsonElement("region")]
    public LibraryRegion Region { get; set; } = LibraryRegion.Any;

    [BsonElement("type")]
    public LibraryType Type { get; set; } = LibraryType.Local;

    [BsonElement("libraryUrl")]
    public string LibraryUrl { get; set; } = string.Empty;

    [BsonElement("remoteLibraryId")]
    public string RemoteLibraryId { get; set; } = string.Empty;

    [BsonElement("isPublic")]
    public bool IsPublic { get; set; }
}
