namespace Dataisland.Core.Domain.Accounts;

public static class DebugProvider
{
    public const string Name = "debug";
}

public static class DebugAccountExtensions
{
    public static ProviderBuilder Debug(this ProviderBuilder builder, string accountId)
    {
        return builder.WithProvider(DebugProvider.Name)
            .WithProviderAccountId(new ProviderAccountId(accountId));
    }
}
