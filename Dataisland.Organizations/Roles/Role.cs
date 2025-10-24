namespace Dataisland.Organizations.Roles;

public readonly record struct Role(string Value)
{
    public override string ToString() => Value;
}