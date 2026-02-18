namespace Dataisland.LLM;

/// <summary>
/// High-level LLM service that selects the appropriate provider and model based on tier.
/// </summary>
public interface ILlmService
{
    Task<LlmResponse> CompleteAsync(ModelTier tier, IReadOnlyList<LlmMessage> messages,
        string? systemPrompt = null, float? temperature = null, int? maxTokens = null,
        CancellationToken ct = default);

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
