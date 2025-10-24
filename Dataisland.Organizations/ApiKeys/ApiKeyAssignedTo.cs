namespace Dataisland.Organizations.ApiKeys;

/// <summary>
/// ApiKey Assigned to entity.
/// Example: User:{id} | Organization:{id}
/// </summary>
/// <param name="Value"></param>
public readonly record struct ApiKeyAssignedTo(string Value)
{
    public static implicit operator string(ApiKeyAssignedTo id) => id.Value;
    public static implicit operator ApiKeyAssignedTo(string id) => new() { Value = id };
    public override string ToString() => Value;
}