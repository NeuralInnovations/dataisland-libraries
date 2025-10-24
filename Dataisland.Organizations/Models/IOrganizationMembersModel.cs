using Dataisland.Organizations.Users;

namespace Dataisland.Organizations.Models;

public interface IOrganizationMembersModel : IEnsureCreated
{
    Task<IEnumerable<OrganizationMember>> AddAsync(OrganizationId id, IEnumerable<UserId> userIds);
    Task<int> RemoveAsync(OrganizationId id, IEnumerable<UserId> userIds);

    Task<int> ChangeStatusAsync(
        OrganizationId id,
        IEnumerable<UserId> userIds,
        OrganizationMemberStatus status
    );

    Task<IEnumerable<OrganizationMember>> FindAsync(OrganizationId id);
    Task<IEnumerable<OrganizationMember>> FindAsync(UserId userId);
    Task<OrganizationMember?> FindAsync(OrganizationId id, UserId userId);
}