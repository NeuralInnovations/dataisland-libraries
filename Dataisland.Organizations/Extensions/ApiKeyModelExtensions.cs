using Dataisland.Organizations.ApiKeys;
using Dataisland.Organizations.Models;
using Dataisland.Organizations.Users;

namespace Dataisland.Organizations.Extensions;

public static class ApiKeyModelExtensions
{
    public static Task<ApiKey> CreateAsync(
        this IApiKeyModel model,
        Func<ApiKeyAssignedTo, ApiKeyAssignedTo> assignedTo,
        string key
    )
    {
        var result = assignedTo(new ApiKeyAssignedTo(""));
        return model.CreateAsync(result, key);
    }

    public static Task<ApiKey> CreateAssignedToOrganizationAsync(
        this IApiKeyModel model,
        OrganizationId organizationId,
        string key
    )
    {
        return model.CreateAsync(x => x.Organization(organizationId), key);
    }

    public static Task<ApiKey> CreateAssignedToUserAsync(
        this IApiKeyModel model,
        OrganizationId organizationId,
        UserId userId,
        string key
    )
    {
        return model.CreateAsync(x => x.User(organizationId, userId), key);
    }

    public static Task<IEnumerable<ApiKey>> FindAsync(
        this IApiKeyModel model,
        Func<ApiKeyAssignedTo, ApiKeyAssignedTo> builder
    )
    {
        var result = builder(new ApiKeyAssignedTo(""));
        return model.FindAsync(result);
    }

    public static Task<IEnumerable<ApiKey>> FindAsync(
        this IApiKeyModel model,
        OrganizationId organizationId
    )
    {
        return model.FindAsync(x => x.Organization(organizationId));
    }

    public static Task<IEnumerable<ApiKey>> FindAsync(
        this IApiKeyModel model,
        OrganizationId organizationId,
        UserId userId
    )
    {
        return model.FindAsync(x => x.User(organizationId, userId));
    }

    public static ApiKeyAssignedTo Organization(
        this ApiKeyAssignedTo key,
        OrganizationId id
    )
    {
        return new ApiKeyAssignedTo($"Organization:{id.Value}");
    }

    public static ApiKeyAssignedTo User(
        this ApiKeyAssignedTo key,
        OrganizationId id,
        UserId userId
    )
    {
        return new ApiKeyAssignedTo($"Organization:{id.Value}#User:{userId.Value}");
    }
}