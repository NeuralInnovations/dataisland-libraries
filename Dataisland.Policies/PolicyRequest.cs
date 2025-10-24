namespace Dataisland.Policies;

public readonly record struct PolicyRequest(PolicyAction Action, PolicyResource Resource);