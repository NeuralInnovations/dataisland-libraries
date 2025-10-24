namespace Dataisland.Organizations.ApiKeys;

public readonly record struct ApiKeyId(string Value)
{
    public override string ToString() => Value;
}