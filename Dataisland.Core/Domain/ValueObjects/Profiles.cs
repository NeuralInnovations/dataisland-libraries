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

// Forward-compat: tolerate profile fields written by a NEWER service version. Without this, a
// service running an older Core (e.g. flow lagging api) threw on deserialize the moment api
// persisted a new field like `integrations` — an additive change turned into a processing outage.
// The attribute does NOT reach nested types, so every value object hanging off this profile carries
// it too — otherwise the next field added inside one of them repeats the same outage one level down.
[BsonIgnoreExtraElements]
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

    /// <summary>
    /// Approximate per-category clinic prices (admin-configured) used by the business-impact
    /// dashboard to estimate potential revenue lost when a doctor omits a mandatory prescription,
    /// referral/consultation or follow-up. Defaults to a fresh instance (all prices 0 = "not
    /// configured yet" → losses read as 0) so orgs without the field deserialize cleanly.
    /// Surfaced through GET /organizations/{id} for the settings UI.
    /// </summary>
    [BsonElement("tariffs")]
    public OrganizationTariffs Tariffs { get; set; } = new();

    /// <summary>
    /// True when this organisation is a self-serve demo/trial org: it was created through the public
    /// trial signup path (no developer gate) and runs under a case quota (<see cref="TrialCaseQuota"/>)
    /// plus low spend caps. A trial org becomes a paid org ONLY through the server-side, billing-gated
    /// activation path — a trial user (even its own Admin) can NEVER lift its own limits or upgrade
    /// itself. Defaults to false so existing orgs deserialize as non-trial (paid) with no migration.
    /// </summary>
    [BsonElement("isTrial")]
    public bool IsTrial { get; set; }

    /// <summary>
    /// Maximum number of medical cases a trial org may submit (lifetime, not monthly). Enforced at
    /// case intake — once the org's case count reaches this, new submissions are refused and the
    /// frontend shows the paywall. Demo-seed cases (CaseId prefix "DEMO-") do NOT count. Null means
    /// no case quota (paid orgs, or trial orgs with an explicitly unlimited allowance). Only meaningful
    /// when <see cref="IsTrial"/> is true. Canonical trial value: 100.
    /// </summary>
    [BsonElement("trialCaseQuota")]
    public int? TrialCaseQuota { get; set; }

    /// <summary>
    /// UTC timestamp when the trial started (org created via the trial path). Null for non-trial orgs
    /// and for orgs created before this field existed. Used for trial-age metrics and optional
    /// time-based expiry; not enforced by itself.
    /// </summary>
    [BsonElement("trialStartedAt")]
    public DateTime? TrialStartedAt { get; set; }
}

/// <summary>
/// Per-organisation third-party integration toggles. First integration: DocDream — when enabled,
/// the frontend renders the patient id (persisted MedicalData.PatientId) as a deep link into the
/// DocDream desktop app via its custom URL scheme <c>docdream://patient/&lt;id&gt;</c> (the literal
/// token <c>{patientId}</c> in the template is replaced with the URL-encoded id).
/// </summary>
[BsonIgnoreExtraElements]
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

/// <summary>
/// Per-organisation approximate prices for the business-impact dashboard. A missed item's
/// estimated loss = (number of cases it was omitted in) × its price. Pricing is HYBRID: an exact
/// per-service price in <see cref="Services"/> wins; otherwise the item falls back to the average
/// price of its category below. All prices default to 0 ("not configured"); when everything is 0
/// the dashboard shows zero losses and prompts the admin to fill them in. <see cref="Currency"/>
/// is a display code (e.g. "UAH") passed through to the frontend for formatting — no FX conversion.
/// </summary>
[BsonIgnoreExtraElements]
public class OrganizationTariffs
{
    [BsonElement("currency")]
    public string Currency { get; set; } = "UAH";

    /// <summary>Fallback average price of a consultation / specialist referral (missing referral or route).</summary>
    [BsonElement("consultationPrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal ConsultationPrice { get; set; }

    /// <summary>Fallback average price of a diagnostic examination (missing mandatory examination).</summary>
    [BsonElement("examinationPrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal ExaminationPrice { get; set; }

    /// <summary>Fallback average price attributed to a prescribed medication (missing mandatory medication).</summary>
    [BsonElement("medicationPrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal MedicationPrice { get; set; }

    /// <summary>Fallback average price of a repeat/follow-up visit (missing follow-up).</summary>
    [BsonElement("followUpPrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal FollowUpPrice { get; set; }

    /// <summary>
    /// Exact per-service price overrides (admin-configured, discovered from analytics). Each entry
    /// prices one concrete service name within a category; when a missed item's normalised name +
    /// category matches an entry, its price is used instead of the category fallback above. Empty
    /// by default. Matching is case/whitespace-insensitive (see BusinessImpactCalculator).
    /// </summary>
    [BsonElement("services")]
    public List<ServiceTariff> Services { get; set; } = [];
}

/// <summary>
/// One exact per-service price override for the business-impact dashboard. <see cref="Name"/> is
/// the concrete service/drug/examination name as it appears in analytics; <see cref="Category"/>
/// is one of consultation | examination | medication | followUp (which fallback it overrides).
/// </summary>
[BsonIgnoreExtraElements]
public class ServiceTariff
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("price")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Price { get; set; }
}

[BsonIgnoreExtraElements]
public class UserProfile : BasicProfile
{
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("binanceId")]
    public string BinanceId { get; set; } = string.Empty;

    [BsonElement("educationalInstitution")]
    public string EducationalInstitution { get; set; } = string.Empty;
}
