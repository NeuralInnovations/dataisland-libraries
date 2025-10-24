namespace Dataisland.Policies;

/// <summary>
/// resourceType://resourceId
/// Examples: organization://12345
/// </summary>
/// <param name="value">resourceType://resourceId</param>
[Serializable]
public readonly record struct PolicyResource(string Value) :
    IEquatable<string>,
    IComparable<string>,
    IComparable<PolicyResource>
{
    public static implicit operator string(PolicyResource resource) => resource.Value;
    public static implicit operator PolicyResource(string resource) => new(resource);
    public override string ToString() => Value;
    public override int GetHashCode() => Value.GetHashCode();
    public bool Equals(string? other) => Value.Equals(other);
    public bool Equals(PolicyResource other) => Value == other.Value;
    public int CompareTo(string? other) => String.Compare(Value, other, StringComparison.Ordinal);
    public int CompareTo(PolicyResource other) => String.Compare(Value, other.Value, StringComparison.Ordinal);
}