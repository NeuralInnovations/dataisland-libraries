namespace Dataisland.Organizations;

[Serializable]
public readonly record struct OrganizationId(string Value)
{
    public override string ToString() => Value;
}