using Prometheus;

namespace Dataisland.LLM;

public static class LlmMetrics
{
    private static readonly string[] Labels = ["model", "tier", "provider"];

    public static readonly Counter PromptTokensTotal = Metrics.CreateCounter(
        "llm_prompt_tokens_total",
        "Total input (prompt) tokens consumed by LLM calls",
        Labels);

    public static readonly Counter CompletionTokensTotal = Metrics.CreateCounter(
        "llm_completion_tokens_total",
        "Total output (completion) tokens consumed by LLM calls",
        Labels);

    public static readonly Counter CachedTokensTotal = Metrics.CreateCounter(
        "llm_cached_tokens_total",
        "Input tokens served from provider-side prompt cache (subset of prompt tokens)",
        Labels);

    public static readonly Counter ReasoningTokensTotal = Metrics.CreateCounter(
        "llm_reasoning_tokens_total",
        "Hidden reasoning/thinking tokens billed as output (OpenAI o-series, gpt-5) — " +
        "excluded from llm_completion_tokens_total but folded into llm_cost_dollars_total",
        Labels);

    public static readonly Counter CostDollarsTotal = Metrics.CreateCounter(
        "llm_cost_dollars_total",
        "Estimated total cost in dollars for LLM calls (sum of input + output + reasoning)",
        Labels);

    // Per-token-type cost counters. Sum of the three equals CostDollarsTotal. Kept separate so
    // dashboards can show the real split without reconstructing it from a proportional ratio —
    // the ratio approach is incorrect when input dominates the bill (common on RAG-heavy
    // workloads where prompt tokens are 70–90% of the invoice).
    public static readonly Counter InputCostDollarsTotal = Metrics.CreateCounter(
        "llm_input_cost_dollars_total",
        "Estimated cost in dollars attributable to prompt/input tokens (with cached-token discount applied)",
        Labels);

    public static readonly Counter OutputCostDollarsTotal = Metrics.CreateCounter(
        "llm_output_cost_dollars_total",
        "Estimated cost in dollars attributable to visible output tokens (completion)",
        Labels);

    public static readonly Counter ReasoningCostDollarsTotal = Metrics.CreateCounter(
        "llm_reasoning_cost_dollars_total",
        "Estimated cost in dollars attributable to reasoning/thinking tokens " +
        "(Gemini 2.5 Pro thoughts, OpenAI o-series/gpt-5 reasoning)",
        Labels);

    public static readonly Counter RequestsTotal = Metrics.CreateCounter(
        "llm_requests_total",
        "Total LLM requests by outcome",
        ["model", "tier", "provider", "status"]);

    // Per-phase breakdown (additive to the existing model/tier/provider counters — separate metric
    // so existing dashboards are untouched). "phase" is the logical operation: case sub-steps
    // ("case:GeneratePerDiagnosisRecommendationsAsync", "case:SearchProtocolForDiagnosisAsync", …)
    // and the analytics flows ("periodic_analytics", "general_analytics", "anamnesis"). Answers
    // "which phase / which analytics kind eats the money" at a glance.
    private static readonly string[] PhaseLabels = ["phase", "model", "tier"];

    public static readonly Counter PhaseCostDollarsTotal = Metrics.CreateCounter(
        "llm_phase_cost_dollars_total",
        "Estimated LLM cost in dollars attributed to the logical phase driving the call.",
        PhaseLabels);

    public static readonly Counter PhaseRequestsTotal = Metrics.CreateCounter(
        "llm_phase_requests_total",
        "LLM request count attributed to the logical phase driving the call.",
        PhaseLabels);

    public static readonly Histogram RequestDurationSeconds = Metrics.CreateHistogram(
        "llm_request_duration_seconds",
        "Duration of LLM requests in seconds",
        new HistogramConfiguration
        {
            LabelNames = Labels,
            Buckets = [0.5, 1, 2, 5, 10, 20, 30, 60, 120]
        });
}
