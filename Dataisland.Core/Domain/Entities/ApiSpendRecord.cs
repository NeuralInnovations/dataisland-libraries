using Dataisland.MongoDB.Entity;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.Entities;

/// <summary>
/// Monthly aggregate of LLM API spend attributed to one organisation. One document per
/// (OrganizationId, YearMonth). Updated atomically via $inc as cases complete so concurrent
/// case consumers don't race. Used by IApiSpendTracker to gate new case processing when the
/// organisation's configured ApiSpendCapUsd is reached in the current calendar month.
/// </summary>
[BsonIgnoreExtraElements(Inherited = true)]
public class ApiSpendRecord : EntityBase
{
    [BsonElement("organizationId")]
    public string OrganizationId { get; set; } = string.Empty;

    /// <summary>Calendar month bucket, formatted as "YYYY-MM" (UTC).</summary>
    [BsonElement("yearMonth")]
    public string YearMonth { get; set; } = string.Empty;

    [BsonElement("cumulativeCostUsd")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal CumulativeCostUsd { get; set; }

    [BsonElement("promptTokens")]
    public long PromptTokens { get; set; }

    [BsonElement("completionTokens")]
    public long CompletionTokens { get; set; }

    [BsonElement("caseCount")]
    public long CaseCount { get; set; }

    [BsonElement("lastIncrementedAt")]
    public DateTime LastIncrementedAt { get; set; } = DateTime.UtcNow;
}
