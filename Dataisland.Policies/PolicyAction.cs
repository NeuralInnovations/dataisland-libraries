namespace Dataisland.Policies;

/// <summary>
/// Value can be a wildcard '*' or '*:*'
/// Examples: 'resource:action' | 'resource:*' | '*:action' | 
/// </summary>
/// <param name="Value">* | *:*</param>
[Serializable]
public readonly record struct PolicyAction(string Value) :
    IEquatable<string>,
    IComparable<string>,
    IComparable<PolicyAction>
{
    public static implicit operator string(PolicyAction resource) => resource.Value;
    public static implicit operator PolicyAction(string resource) => new(resource);
    public override string ToString() => Value;
    public override int GetHashCode() => Value.GetHashCode();
    public bool Equals(string? other) => Value.Equals(other);
    public bool Equals(PolicyAction other) => Value == other.Value;
    public int CompareTo(string? other) => String.Compare(Value, other, StringComparison.Ordinal);
    public int CompareTo(PolicyAction other) => String.Compare(Value, other.Value, StringComparison.Ordinal);
}