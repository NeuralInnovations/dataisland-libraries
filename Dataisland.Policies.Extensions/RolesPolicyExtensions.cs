using Dataisland.Organizations;
using Dataisland.Organizations.Roles;
using Dataisland.Policies;

namespace Dataisland.Users.Policies;

public static class RolesPolicyExtensions
{
    public static Task<IEnumerable<Policy>> FindByRoleAsync(
        this IPoliciesModel model,
        OrganizationId orgId,
        Role id
    )
    {
        return model.FindAsync(q => q.Role(orgId, id));
    }

    public static PolicyQuery Role(this PolicyQuery query, Role id)
    {
        query.Principal = $"Global";
        query.AssignedTo = $"Role:{id}";
        return query;
    }

    public static PolicyQuery Role(this PolicyQuery query, OrganizationId orgId, Role id)
    {
        query.Principal = $"Organization:{orgId}";
        query.AssignedTo = $"Role:{id}";
        return query;
    }
}