namespace Dataisland.Organizations.Groups;

[Serializable]
public readonly record struct GroupId(string Value)
{
    public override string ToString() => Value;
}