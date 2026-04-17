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

        // Prompt-level safety block: the whole request was filtered before any generation.
        // Retrying the same model produces the same block — surface as a specific exception
        // so LlmService can fall straight through to the Backup tier.
        var promptBlock = response.PromptFeedback?.BlockReason;
        if (promptBlock is not null)
        {
            throw new LlmContentBlockedException(
                $"Gemini prompt blocked: reason={promptBlock} message={response.PromptFeedback?.BlockReasonMessage}");
        }

        // Candidate-level content filter: generation started but was stopped for safety/recitation/etc.
        // Any non-STOP, non-MAX_TOKENS finish reason means the response is not usable.
        var candidate = response.Candidates?.FirstOrDefault();
        var finishReason = candidate?.FinishReason;
        if (finishReason is not null
            && finishReason != FinishReason.STOP
            && finishReason != FinishReason.MAX_TOKENS
            && finishReason != FinishReason.FINISH_REASON_UNSPECIFIED)
        {
            if (finishReason is FinishReason.SAFETY
                or FinishReason.PROHIBITED_CONTENT
                or FinishReason.BLOCKLIST
                or FinishReason.SPII
                or FinishReason.IMAGE_SAFETY
                or FinishReason.IMAGE_PROHIBITED_CONTENT)
            {
                throw new LlmContentBlockedException(
                    $"Gemini content filtered: finishReason={finishReason}");
            }

            // RECITATION, LANGUAGE, OTHER, MALFORMED_FUNCTION_CALL, UNEXPECTED_TOOL_CALL
            throw new InvalidOperationException(
                $"Gemini finished without usable output: finishReason={finishReason}");
        }

        var text = ExtractText(response);
        var usage = response.UsageMetadata;

        // Empty completion tokens with no explicit block: transient failure — raise so the
        // LlmService retry loop (or backup fallthrough) can retry instead of returning
        // an empty string that downstream will treat as a bad JSON parse.
        if (usage is not null && (usage.CandidatesTokenCount ?? 0) == 0 && string.IsNullOrEmpty(text))
        {
            throw new InvalidOperationException(
                $"Gemini returned empty completion (prompt_tokens={usage.PromptTokenCount}, candidates=0)");
        }

        // Gemini 2.5 reports "thought" tokens separately from visible output but bills them at
        // the same rate as output. They are non-zero even with ThinkingBudget=0 on 2.5 Pro
        // (mandatory 128-token minimum) and on any model where budget=0 is ignored. Fold them
        // into CompletionTokens so our cost/token metrics match Google's invoice.
        var candidateTokens = usage?.CandidatesTokenCount ?? 0;
        var thoughtTokens = usage?.ThoughtsTokenCount ?? 0;

        return new LlmResponse(
            Content: text,
            PromptTokens: usage?.PromptTokenCount ?? 0,
            CompletionTokens: candidateTokens + thoughtTokens,
            Model: request.Model)
        {
            CachedTokens = usage?.CachedContentTokenCount ?? 0
        };
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
            // Disable Gemini 2.5 Flash "dynamic thinking" — these prompts are deterministic
            // classifiers, query generators, and filters; thinking adds no quality but is billed
            // as output tokens. Note: Gemini 2.5 Pro ignores budget=0 (mandatory thinking).
            ThinkingConfig = new ThinkingConfig
            {
                ThinkingBudget = 0,
                IncludeThoughts = false
            }
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
