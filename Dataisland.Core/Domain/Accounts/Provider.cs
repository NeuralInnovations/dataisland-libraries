namespace Dataisland.Core.Domain.Accounts;

public readonly record struct Provider(string Id)
{
    public static implicit operator string(Provider id) => id.Id;
    public static implicit operator Provider(string id) => new(id);

    public override string ToString() => Id;
}
