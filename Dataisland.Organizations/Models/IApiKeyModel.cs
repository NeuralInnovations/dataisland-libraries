using Dataisland.Organizations.ApiKeys;

namespace Dataisland.Organizations.Models;

public interface IApiKeyModel : IEnsureCreated
{
    Task<ApiKey> CreateAsync(ApiKeyAssignedTo assignedTo, string key);
    Task<ApiKey?> UpdateAsync(ApiKeyId id, string key);
    Task<ApiKey?> FindAsync(ApiKeyId keyId);
    Task<IEnumerable<ApiKey>> FindAsync(ApiKeyAssignedTo assignedTo);
    Task<int> DeleteAsync(ApiKeyId keyId);
}