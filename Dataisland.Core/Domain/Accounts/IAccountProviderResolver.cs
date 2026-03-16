using System.Security.Claims;

namespace Dataisland.Core.Domain.Accounts;

/// <summary>
/// Resolves the current authenticated principal to a provider identity.
/// Implementation maps auth scheme/issuer → (Provider, ProviderAccountId).
/// </summary>
public interface IAccountProviderResolver
{
    (string Provider, string ProviderAccountId) Resolve(ClaimsPrincipal principal);
}
