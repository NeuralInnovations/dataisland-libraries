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
}

public enum ModelTier
{
    Simple,
    Normal,
    Advanced,
    Backup,
    Vision
}
