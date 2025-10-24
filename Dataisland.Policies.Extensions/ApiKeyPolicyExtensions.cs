using Dataisland.Organizations;
using Dataisland.Organizations.ApiKeys;
using Dataisland.Policies;

namespace Dataisland.Users.Policies;

public static class ApiKeyPolicyExtensions
{
    public static Task<IEnumerable<Policy>> FindByApiKeyAsync(
        this IPoliciesModel model,
        OrganizationId orgId, ApiKeyId keyId
    )
    {
        return model.FindAsync(q => q.ApiKey(orgId, keyId));
    }

    public static PolicyQuery ApiKey(this PolicyQuery query, OrganizationId orgId, ApiKeyId keyId)
    {
        query.Principal = $"Organization:{orgId}";
        query.AssignedTo = $"ApiKey:{keyId}";
        return query;
    }
}