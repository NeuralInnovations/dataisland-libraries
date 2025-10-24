namespace Dataisland.Organizations;

[Serializable]
public record Organization(
    OrganizationId Id,
    OrganizationProfile Profile,
    OrganizationStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);