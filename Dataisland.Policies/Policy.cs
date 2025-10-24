namespace Dataisland.Policies;

[Serializable]
public record Policy(
    string Name,
    PolicyEffect Effect,
    PolicyAction[] Actions,
    PolicyResource[] Resources
)
{
    public PolicyId Id { get; set; }
}