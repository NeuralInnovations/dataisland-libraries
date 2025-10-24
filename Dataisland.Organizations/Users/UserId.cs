namespace Dataisland.Organizations.Users;

[Serializable]
public readonly record struct UserId(string Value)
{
    public override string ToString() => Value;
}