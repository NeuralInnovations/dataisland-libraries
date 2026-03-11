using System.Runtime.CompilerServices;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;

namespace Dataisland.LLM.Providers;

public class GeminiProvider : ILlmProvider
{
    private readonly Client _client;
    private readonly string _model;
    private readonly ILogger<GeminiProvider> _logger;

    public GeminiProvider(ModelConfig config, ILogger<GeminiProvider> logger)
    {
        _logger = logger;
        _model = config.Model;
        _client = new Client(apiKey: config.ApiKey);

        if (!string.IsNullOrWhiteSpace(config.BaseUrl))
            _logger.LogWarning("BaseUrl is set for Gemini provider but Google.GenAI SDK does not support custom endpoints — it will be ignored");
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var config = BuildConfig(request);
        var contents = BuildContents(request);

        var response = await _client.Models.GenerateContentAsync(
            model: _model,
            contents: contents,
            config: config);

        var text = ExtractText(response);
        var usage = response.UsageMetadata;

        return new LlmResponse(
            Content: text,
            PromptTokens: usage?.PromptTokenCount ?? 0,
            CompletionTokens: usage?.CandidatesTokenCount ?? 0,
            Model: request.Model);
    }

    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        LlmRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var config = BuildConfig(request);
        var contents = BuildContents(request);

        await foreach (var chunk in _client.Models.GenerateContentStreamAsync(
            model: _model,
            contents: contents,
            config: config).WithCancellation(ct))
        {
            var text = chunk?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (text is not null)
                yield return text;
        }
    }

    internal static GenerateContentConfig BuildConfig(LlmRequest request)
    {
        var config = new GenerateContentConfig
        {
            Temperature = request.Temperature,
        };

        if (request.MaxTokens.HasValue)
            config.MaxOutputTokens = request.MaxTokens.Value;

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            config.SystemInstruction = new Content
            {
                Parts = [new Part { Text = request.SystemPrompt }]
            };
        }

        // Set JSON response mode for structured output
        if (request.ResponseFormat is LlmResponseFormat.Json or LlmResponseFormat.JsonSchema)
        {
            config.ResponseMimeType = "application/json";
        }

        return config;
    }

    internal static List<Content> BuildContents(LlmRequest request)
    {
        var contents = new List<Content>();

        foreach (var msg in request.Messages)
        {
            var role = msg.Role.ToLowerInvariant() switch
            {
                "user" => "user",
                "assistant" => "model",
                // Gemini doesn't support system role in contents;
                // actual system prompt is handled via GenerateContentConfig.SystemInstruction
                "system" => "user",
                _ => "user"
            };

            var parts = new List<Part>();

            if (!string.IsNullOrEmpty(msg.Content))
                parts.Add(new Part { Text = msg.Content });

            if (msg.Images is { Count: > 0 })
            {
                foreach (var img in msg.Images)
                {
                    parts.Add(new Part
                    {
                        InlineData = new Blob
                        {
                            Data = img.Data,
                            MimeType = img.MimeType
                        }
                    });
                }
            }

            contents.Add(new Content { Role = role, Parts = parts });
        }

        return contents;
    }

    private string ExtractText(GenerateContentResponse response)
    {
        var candidate = response.Candidates?.FirstOrDefault();
        if (candidate is null)
        {
            _logger.LogWarning("Gemini returned no candidates");
            return string.Empty;
        }

        var text = candidate.Content?.Parts?.FirstOrDefault()?.Text;
        if (text is null)
        {
            _logger.LogWarning("Gemini candidate has no text content. FinishReason: {FinishReason}",
                candidate.FinishReason);
            return string.Empty;
        }

        return text;
    }
}
