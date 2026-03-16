namespace Dataisland.Core.Domain.Accounts;

public readonly record struct ProviderAccountId(string Id)
{
    public static implicit operator string(ProviderAccountId id) => id.Id;
    public static implicit operator ProviderAccountId(string id) => new(id);

    public override string ToString() => Id;
}
