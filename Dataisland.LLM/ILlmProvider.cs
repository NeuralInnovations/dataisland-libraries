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
);

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
);
