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

    /// <summary>
    /// Per-case hard-stop cost cap (USD). If a single case's accumulated LLM spend exceeds
    /// this value during processing, remaining LLM calls are aborted and the case is returned
    /// with an error. Guards against runaway single-case costs — e.g., a pathological input
    /// that keeps retrying or a model loop that burns through tokens. Defaults to 0 (disabled)
    /// because a reasonable value depends on expected case complexity; developers should set
    /// this to ~5-10× the observed average per-case cost to catch outliers without throttling
    /// legitimate long cases.
    /// </summary>
    [BsonElement("perCaseCostCapUsd")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal PerCaseCostCapUsd { get; set; } = 0m;
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
