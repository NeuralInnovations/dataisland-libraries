namespace Dataisland.Policies;

public readonly record struct Principal(string Value)
{
    public static implicit operator string(Principal id) => id.Value;
    public static implicit operator Principal(string id) => new() { Value = id };
    public override string ToString() => Value;
}