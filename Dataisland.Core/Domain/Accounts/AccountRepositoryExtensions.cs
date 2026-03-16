using Dataisland.Core.Domain.Entities;
using Dataisland.Core.Repositories;

namespace Dataisland.Core.Domain.Accounts;

public static class AccountRepositoryExtensions
{
    public static Task<Account> FindOrCreateAsync(
        this IAccountRepository repository, Action<ProviderBuilder> action)
    {
        var (provider, providerAccountId) = BuildAndValidate(action);
        return repository.FindOrCreateAsync(provider, providerAccountId);
    }

    public static Task<Account?> FindByProviderAsync(
        this IAccountRepository repository, Action<ProviderBuilder> action)
    {
        var (provider, providerAccountId) = BuildAndValidate(action);
        return repository.FindByProviderAsync(provider, providerAccountId);
    }

    private static (string provider, string providerAccountId) BuildAndValidate(Action<ProviderBuilder> action)
    {
        var builder = new ProviderBuilder();
        action(builder);

        string provider = builder.Provider;
        string providerAccountId = builder.ProviderAccountId;

        if (string.IsNullOrEmpty(provider))
            throw new ArgumentException("Provider must be set via builder (e.g. b => b.Zitadel(id))");
        if (string.IsNullOrEmpty(providerAccountId))
            throw new ArgumentException("ProviderAccountId must be set via builder");

        return (provider, providerAccountId);
    }
}
