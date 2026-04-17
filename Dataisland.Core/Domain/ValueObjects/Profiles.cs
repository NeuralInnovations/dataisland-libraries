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

    /// <summary>
    /// Monthly cumulative spend cap (USD) for LLM API usage attributed to this organisation.
    /// When the current month's recorded spend reaches this cap, new case-processing requests
    /// for the organisation are refused until the next calendar month. Defaults to $500 as a
    /// conservative safety net for new organisations that have not explicitly configured one.
    /// Set to 0 to disable the cap entirely (unlimited spend).
    /// </summary>
    [BsonElement("apiSpendCapUsd")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal ApiSpendCapUsd { get; set; } = 500m;
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
