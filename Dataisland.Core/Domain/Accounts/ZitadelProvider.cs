namespace Dataisland.Core.Domain.Accounts;

public static class ZitadelProvider
{
    public const string Name = "zitadel";
}

public static class ZitadelAccountExtensions
{
    public static ProviderBuilder Zitadel(this ProviderBuilder builder, string subjectId)
    {
        return builder.WithProvider(ZitadelProvider.Name)
            .WithProviderAccountId(new ProviderAccountId(subjectId));
    }
}
