namespace Dataisland.Core.Domain.Accounts;

public static class InternalJwtProvider
{
    public const string Name = "internal_jwt";
}

public static class InternalJwtAccountExtensions
{
    public static ProviderBuilder InternalJwt(this ProviderBuilder builder, string accountId)
    {
        return builder.WithProvider(InternalJwtProvider.Name)
            .WithProviderAccountId(new ProviderAccountId(accountId));
    }
}
