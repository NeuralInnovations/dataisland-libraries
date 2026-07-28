namespace Dataisland.LLM;

/// <summary>
/// Ambient (per-async-flow) tag naming the logical operation — the "phase" — driving the current
/// LLM call, so the per-phase LLM cost/request metrics can attribute spend to a specific step.
/// Case sub-steps use "case:&lt;MethodName&gt;" (protocol search, per-diagnosis recommendations,
/// summary, scoring…); the different analytics flows use their own tags ("periodic_analytics",
/// "general_analytics", "anamnesis"). Unset reads as "unknown".
///
/// Set it with a <c>using (LlmPhaseContext.Begin("…"))</c> scope around the call site; it flows
/// through async/await into LlmService.RecordMetrics without threading a parameter everywhere.
/// </summary>
public static class LlmPhaseContext
{
    private static readonly AsyncLocal<string?> _current = new();

    public static string Current => _current.Value ?? "unknown";

    public static IDisposable Begin(string phase)
    {
        var previous = _current.Value;
        _current.Value = string.IsNullOrWhiteSpace(phase) ? "unknown" : phase;
        return new Scope(previous);
    }

    private sealed class Scope(string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _current.Value = previous;
        }
    }
}
