namespace Dataisland.Policies;

/// <summary>
/// Group/Id | User/Id | Api/Id
/// </summary>
/// <param name="Value"></param>
public readonly record struct AssignedTo(string Value)
{
    public static implicit operator string(AssignedTo id) => id.Value;
    public static implicit operator AssignedTo(string id) => new() { Value = id };
    public override string ToString() => Value;
}