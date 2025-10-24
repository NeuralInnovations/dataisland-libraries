namespace Dataisland.Policies;

public class PolicyEvaluation : IPolicyEvaluation
{
    public static readonly IPolicyEvaluation Default = new PolicyEvaluation();

    private static ReadOnlySpan<char> FastFirstElement(string source, string pattern)
    {
        var index = source.IndexOf(pattern, StringComparison.Ordinal);
        if (index < 0)
        {
            throw new ArgumentException($"Invalid format, pattern '{pattern}' not found in '{source}'");
        }

        return source.AsSpan(..index);
    }

    private static bool MatchAction(PolicyAction policyAction, PolicyAction requestAction)
    {
        return policyAction == requestAction ||
               policyAction.Value == "*" ||
               policyAction.Value == "*:*" ||
               (
                   policyAction.Value.EndsWith('*')
                   &&
                   requestAction.Value.AsSpan().StartsWith(FastFirstElement(policyAction, ":"))
               );
    }

    private static bool MatchResource(
        PolicyResource policyResource,
        PolicyResource requestResource
    )
    {
        return policyResource == requestResource ||
               policyResource.Value == "*" ||
               (
                   policyResource.Value.EndsWith('*')
                   &&
                   requestResource.Value.AsSpan().StartsWith(FastFirstElement(policyResource, "://"))
               );
    }

    public PolicyEffect Evaluate(Policy[] policies, PolicyRequest request)
    {
        var decision = PolicyEffect.Deny;

        foreach (var policy in policies)
        {
            var actionMatches = false;
            foreach (var action in policy.Actions)
            {
                if (MatchAction(action, request.Action))
                {
                    actionMatches = true;
                    break;
                }
            }

            var resourceMatches = false;
            foreach (var resource in policy.Resources)
            {
                if (MatchResource(resource, request.Resource))
                {
                    resourceMatches = true;
                    break;
                }
            }

            if (actionMatches && resourceMatches)
            {
                if (policy.Effect == PolicyEffect.Deny)
                {
                    return PolicyEffect.Deny; // Explicit deny overrides all
                }

                if (policy.Effect == PolicyEffect.Allow)
                {
                    decision = PolicyEffect.Allow;
                }
            }
        }

        return decision;
    }
}