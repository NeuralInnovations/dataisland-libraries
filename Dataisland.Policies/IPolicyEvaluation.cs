namespace Dataisland.Policies;

public interface IPolicyEvaluation
{
    PolicyEffect Evaluate(Policy[] policies, PolicyRequest request);
}