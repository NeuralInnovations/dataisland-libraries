namespace Dataisland.LLM;

/// <summary>
/// High-level LLM service that selects the appropriate provider and model based on tier.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// <paramref name="timeout"/> overrides the tier's configured <c>TimeoutSeconds</c>
    /// for this single call. Use it on hot-path calls whose tail latency matters more than
    /// the tier default (e.g. a file-selection call whose response is 2 strings — a 300s
    /// tier timeout is meaningless there and just multiplies wasted time per retry).
    /// When unset, the tier's configured timeout applies.
    /// </summary>
    /// <para>
    /// <paramref name="cachedContentName"/> references a provider context cache from
    /// <see cref="CreateContextCacheAsync"/>: its cached prefix (system instruction + shared
    /// content) is billed at ~25% and MUST be omitted from <paramref name="messages"/>/
    /// <paramref name="systemPrompt"/>. Only honoured on the primary tier — a Backup fallback
    /// runs a different model and drops the cache automatically.
    /// </para>
    Task<LlmResponse> CompleteAsync(ModelTier tier, IReadOnlyList<LlmMessage> messages,
        string? systemPrompt = null, float? temperature = null, int? maxTokens = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default,
        string? cachedContentName = null);

    /// <summary>
    /// Create a provider context cache for <paramref name="tier"/>'s model holding a shared prefix
    /// reused across many calls in one case. Returns the cache handle to pass back as
    /// <c>cachedContentName</c>, or <c>null</c> when the provider/model can't cache (caller then
    /// sends full prompts). Use the returned handle only with the SAME tier.
    /// </summary>
    Task<string?> CreateContextCacheAsync(ModelTier tier, string? systemInstruction,
        IReadOnlyList<LlmMessage> contents, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Best-effort delete of a cache from <see cref="CreateContextCacheAsync"/>.</summary>
    Task DeleteContextCacheAsync(ModelTier tier, string cacheName, CancellationToken ct = default);

    /// <summary>
    /// Call LLM and parse the response into a typed object.
    /// Uses JSON schema structured output when the provider supports it (OpenAI),
    /// otherwise falls back to JSON mode + prompt-based schema instruction.
    /// See <see cref="CompleteAsync(ModelTier, IReadOnlyList{LlmMessage}, string?, float?, int?, TimeSpan?, CancellationToken)"/>
    /// for the semantics of <paramref name="timeout"/>.
    ///
    /// <paramref name="propertyEnums"/> narrows a field to a per-request allowlist — useful
    /// when the valid values are only known at call time (e.g. the candidate file IDs in
    /// Phase C file-selection). Keys are the camelCase property names of <typeparamref name="T"/>.
    /// On providers that support strict structured outputs (OpenAI, Gemini 2.x) the model
    /// physically cannot emit a value outside the allowlist; on others the restriction is
    /// expressed only via the schema text in the system prompt.
    /// </summary>
    Task<LlmResponse<T>> CompleteAsync<T>(ModelTier tier, IReadOnlyList<LlmMessage> messages,
        string? systemPrompt = null, float? temperature = null, int? maxTokens = null,
        TimeSpan? timeout = null,
        IReadOnlyDictionary<string, string[]>? propertyEnums = null,
        CancellationToken ct = default,
        string? cachedContentName = null) where T : class;

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
