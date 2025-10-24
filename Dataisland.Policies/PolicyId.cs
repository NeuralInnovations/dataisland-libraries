namespace Dataisland.Policies;

public readonly record struct PolicyId(string Value)
{
    public static implicit operator string(PolicyId id) => id.Value;
    public static implicit operator PolicyId(string id) => new() { Value = id };
    public override string ToString() => Value;
}