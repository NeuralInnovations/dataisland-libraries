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

    /// <summary>
    /// When set, dispatching of queued medical cases / anamnesis / analytics for this organisation
    /// is paused — the dispatcher workers skip the org on each tick. Already-in-flight MQ messages
    /// complete normally; new submissions still land in the queue (Status=InQueue) and flush on
    /// resume. Null = running. Used as the admin "stop processing" kill switch.
    /// </summary>
    [BsonElement("processingPausedAt")]
    public DateTime? ProcessingPausedAt { get; set; }

    /// <summary>
    /// Third-party integration settings for this organisation (admin-configured). Defaults to a
    /// fresh instance so orgs without the field in Mongo deserialize cleanly and integrations
    /// read as "off". Surfaced through GET /organizations/{id} for the frontend to react to.
    /// </summary>
    [BsonElement("integrations")]
    public OrganizationIntegrations Integrations { get; set; } = new();
}

/// <summary>
/// Per-organisation third-party integration toggles. First integration: DocDream — when enabled,
/// the frontend renders the patient id (persisted MedicalData.PatientId) as a deep link into the
/// DocDream desktop app via its custom URL scheme <c>docdream://patient/&lt;id&gt;</c> (the literal
/// token <c>{patientId}</c> in the template is replaced with the URL-encoded id).
/// </summary>
public class OrganizationIntegrations
{
    [BsonElement("docDreamEnabled")]
    public bool DocDreamEnabled { get; set; }

    /// <summary>
    /// Optional override for the DocDream patient deep-link template. Default (when null/empty)
    /// is the fixed scheme <c>docdream://patient/{patientId}</c> applied by the frontend. Kept
    /// per-org as an escape hatch; the admin UI only exposes the on/off toggle.
    /// </summary>
    [BsonElement("docDreamPatientUrlTemplate")]
    public string? DocDreamPatientUrlTemplate { get; set; }
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
