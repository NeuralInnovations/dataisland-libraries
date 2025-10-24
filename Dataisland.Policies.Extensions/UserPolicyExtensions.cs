using Dataisland.Organizations;
using Dataisland.Organizations.Users;
using Dataisland.Policies;

namespace Dataisland.Users.Policies;

public static class UserPolicyExtensions
{
    public static Task<IEnumerable<Policy>> FindByUserAsync(
        this IPoliciesModel model,
        OrganizationId orgId,
        UserId id
    )
    {
        return model.FindAsync(q => q.UserId(orgId, id));
    }

    public static PolicyQuery UnOrganizationUserId(this PolicyQuery query, UserId id)
    {
        query.Principal = $"Global";
        query.AssignedTo = $"User:{id}";
        return query;
    }

    public static PolicyQuery UserId(this PolicyQuery query, OrganizationId orgId, UserId id)
    {
        query.Principal = $"Organization:{orgId}";
        query.AssignedTo = $"User:{id}";
        return query;
    }
}