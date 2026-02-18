using Dataisland.Core.Domain.ValueObjects;
using Dataisland.MongoDB.Entity;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.Entities;

[BsonIgnoreExtraElements(Inherited = true)]
public class Icon : EntityBase
{
    [BsonElement("fileName")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("fileHash")]
    public string FileHash { get; set; } = string.Empty;

    [BsonElement("fileExtension")]
    public string FileExtension { get; set; } = string.Empty;

    [BsonElement("fileSizeKb")]
    public long FileSizeKb { get; set; }

    [BsonElement("metadata")]
    public IconMetadata Metadata { get; set; } = new();
}
