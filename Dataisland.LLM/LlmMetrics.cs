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

    public static readonly Counter CostDollarsTotal = Metrics.CreateCounter(
        "llm_cost_dollars_total",
        "Estimated total cost in dollars for LLM calls",
        Labels);

    public static readonly Counter RequestsTotal = Metrics.CreateCounter(
        "llm_requests_total",
        "Total LLM requests by outcome",
        ["model", "tier", "provider", "status"]);

    public static readonly Histogram RequestDurationSeconds = Metrics.CreateHistogram(
        "llm_request_duration_seconds",
        "Duration of LLM requests in seconds",
        new HistogramConfiguration
        {
            LabelNames = Labels,
            Buckets = [0.5, 1, 2, 5, 10, 20, 30, 60, 120]
        });
}
