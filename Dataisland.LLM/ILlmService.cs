namespace Dataisland.LLM;

/// <summary>
/// High-level LLM service that selects the appropriate provider and model based on tier.
/// </summary>
public interface ILlmService
{
    Task<LlmResponse> CompleteAsync(ModelTier tier, IReadOnlyList<LlmMessage> messages,
        string? systemPrompt = null, float? temperature = null, int? maxTokens = null,
        CancellationToken ct = default);

    /// <summary>
    /// Call LLM and parse the response into a typed object.
    /// Uses JSON schema structured output when the provider supports it (OpenAI),
    /// otherwise falls back to JSON mode + prompt-based schema instruction.
    /// </summary>
    Task<LlmResponse<T>> CompleteAsync<T>(ModelTier tier, IReadOnlyList<LlmMessage> messages,
        string? systemPrompt = null, float? temperature = null, int? maxTokens = null,
        CancellationToken ct = default) where T : class;

    IAsyncEnumerable<string> CompleteStreamingAsync(ModelTier tier, IReadOnlyList<LlmMessage> messages,
        string? systemPrompt = null, float? temperature = null, int? maxTokens = null,
        CancellationToken ct = default);

    /// <summary>
    /// Estimate the USD cost of a completed request given its model-name and token usage.
    /// Returns 0 when the model is not configured in LlmOptions or its per-1K prices are 0.
    /// Used by downstream spend-tracking (per-organisation cap enforcement, per-case cost
    /// attribution) so callers don't need to re-implement the pricing lookup themselves.
    ///
    /// reasoningTokens should be passed separately from completionTokens — on OpenAI o-series
    /// and gpt-5 models reasoning is billed as output but reported separately in usage, and on
    /// some providers it has its own pricing tier (see ModelConfig.ReasoningTokenCostPer1K).
    /// </summary>
    decimal EstimateCostUsd(string model, int promptTokens, int completionTokens,
        int cachedTokens = 0, int reasoningTokens = 0);
}

public enum ModelTier
{
    Simple,
    Normal,
    Advanced,
    Backup,
    Vision
}
