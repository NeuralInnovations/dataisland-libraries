using Dataisland.Organizations;
using Dataisland.Organizations.Groups;
using Dataisland.Policies;

namespace Dataisland.Users.Policies;

public static class GroupPolicyExtensions
{
    public static Task<IEnumerable<Policy>> FindByGroupAsync(
        this IPoliciesModel model,
        OrganizationId orgId, GroupId groupId
    )
    {
        return model.FindAsync(q => q.Group(orgId, groupId));
    }

    public static PolicyQuery Group(this PolicyQuery query, OrganizationId orgId, GroupId groupId)
    {
        query.Principal = $"Organization:{orgId}";
        query.AssignedTo = $"Group:{groupId}";
        return query;
    }
}