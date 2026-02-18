using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.ValueObjects;

public class BasicProfile
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;
}

public class DescriptionProfile : BasicProfile
{
    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;
}

public class OrganizationProfile : DescriptionProfile
{
    [BsonElement("limitSegmentKey")]
    public string LimitSegmentKey { get; set; } = string.Empty;

    [BsonElement("iconId")]
    public ObjectId IconId { get; set; }
}

public class UserProfile : BasicProfile
{
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("binanceId")]
    public string BinanceId { get; set; } = string.Empty;

    [BsonElement("educationalInstitution")]
    public string EducationalInstitution { get; set; } = string.Empty;
}
