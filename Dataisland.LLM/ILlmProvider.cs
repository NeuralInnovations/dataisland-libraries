namespace Dataisland.LLM;

public interface ILlmProvider
{
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);
    IAsyncEnumerable<string> CompleteStreamingAsync(LlmRequest request, CancellationToken ct = default);
}

public record LlmRequest(
    string Model,
    IReadOnlyList<LlmMessage> Messages,
    float Temperature = 0.7f,
    int? MaxTokens = null,
    string? SystemPrompt = null
)
{
    /// <summary>Response format hint for providers that support structured output.</summary>
    public LlmResponseFormat ResponseFormat { get; init; } = LlmResponseFormat.Text;

    /// <summary>JSON schema string for JsonSchema response format. Used by OpenAI structured outputs.</summary>
    public string? JsonSchema { get; init; }

    /// <summary>Schema name for providers that require it (e.g. OpenAI json_schema format).</summary>
    public string? JsonSchemaName { get; init; }
}

public record LlmMessage(string Role, string Content)
{
    /// <summary>Optional image content for vision-capable models.</summary>
    public IReadOnlyList<LlmImageContent>? Images { get; init; }
}

public record LlmImageContent(byte[] Data, string MimeType);

public record LlmResponse(
    string Content,
    int PromptTokens,
    int CompletionTokens,
    string Model
)
{
    /// <summary>
    /// Tokens served from provider-side prompt cache (0 if no hit). Gemini reports this as
    /// usageMetadata.cachedContentTokenCount; OpenAI as usage.prompt_tokens_details.cached_tokens.
    /// Used to verify prompt caching is actually firing — otherwise we pay full price for the
    /// big static prompts (search_relevant_protocol, final_recommendations_guide).
    /// </summary>
    public int CachedTokens { get; init; }
}

/// <summary>Typed LLM response wrapping a parsed object and token usage.</summary>
public record LlmResponse<T>(
    T? Value,
    string RawContent,
    int PromptTokens,
    int CompletionTokens,
    string Model
)
{
    public int CachedTokens { get; init; }
}

public enum LlmResponseFormat
{
    /// <summary>Free-form text (default).</summary>
    Text,
    /// <summary>Provider uses JSON mode — guarantees valid JSON but no schema enforcement.</summary>
    Json,
    /// <summary>Provider uses JSON schema mode — guarantees output matches the schema (OpenAI structured outputs).</summary>
    JsonSchema
}
