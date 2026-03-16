namespace Dataisland.Core.Domain.Accounts;

public class ProviderBuilder
{
    internal Provider Provider;
    internal ProviderAccountId ProviderAccountId;

    public ProviderBuilder WithProvider(Provider provider)
    {
        Provider = provider;
        return this;
    }

    public ProviderBuilder WithProviderAccountId(ProviderAccountId providerAccountId)
    {
        ProviderAccountId = providerAccountId;
        return this;
    }
}
