using Dataisland.Organizations.Users;

namespace Dataisland.Organizations;

[Serializable]
public record OrganizationMember(
    OrganizationId OrganizationId,
    UserId UserId,
    OrganizationMemberStatus Status,
    DateTime JoinedAt,
    DateTime? LeftAt
);