using System.Runtime.CompilerServices;
using System.Text.Json;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;

namespace Dataisland.LLM.Providers;

public class GeminiProvider : ILlmProvider
{
    private readonly Client _client;
    private readonly string _model;
    private readonly int _thinkingBudget;
    private readonly ILogger<GeminiProvider> _logger;

    public GeminiProvider(ModelConfig config, ILogger<GeminiProvider> logger)
    {
        _logger = logger;
        _model = config.Model;
        _client = new Client(apiKey: config.ApiKey);
        _thinkingBudget = ResolveThinkingBudget(config.ThinkingBudget, config.Model);

        _logger.LogInformation(
            "Gemini provider: model={Model} thinkingBudget={Budget}", config.Model, _thinkingBudget);

        if (!string.IsNullOrWhiteSpace(config.BaseUrl))
            _logger.LogWarning("BaseUrl is set for Gemini provider but Google.GenAI SDK does not support custom endpoints — it will be ignored");
    }

    /// <summary>
    /// Decide the ThinkingBudget to pass to Google.GenAI.
    /// Default is ALWAYS 0 (disabled) — opting in to thinking is an explicit decision because
    /// it bills at the output rate and routinely dominates per-call cost on Pro. To enable,
    /// set <c>thinkingBudget</c> in the model config JSON (see LlmOptions.ThinkingBudget).
    /// Note: Gemini 2.5 Pro ignores budget=0 and enforces a 128-token mandatory minimum — that's
    /// Google's behaviour, not ours; our "off" is still as off as the API allows.
    /// </summary>
    internal static int ResolveThinkingBudget(int? configured, string model)
        => configured ?? 0;

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var config = BuildConfig(request, _thinkingBudget);
        var contents = BuildContents(request);

        // Use streaming internally — even for "single response" callers — so long generations
        // don't hit Google.GenAI SDK's built-in ~100 s HttpClient timeout.
        // Non-streaming GenerateContentAsync waits for the whole response before returning,
        // so a verbose analytics generation that takes 120 s produces a truncated response
        // with "Raw: {" (just the opening brace) and a JsonException downstream. Streaming
        // keeps the connection alive — chunks arriving every second prevent the HTTP timeout
        // from firing at all. Matches what the old server did on every analytics call.
        //
        // We accumulate chunks into a single Content + use the last chunk's UsageMetadata
        // (which contains the complete tally for the full response), then return as if it
        // were a single response — transparent to every caller in LlmService.
        var contentBuilder = new System.Text.StringBuilder();
        Candidate? finalCandidate = null;
        // Running totals from streamed chunks — the SDK streaming API uses a different usage
        // metadata type than non-streaming, so we extract ints as we go instead of holding
        // the SDK type directly.
        int promptTokens = 0, candidateTokens = 0, thoughtTokens = 0, cachedTokens = 0;
        string? promptBlockReason = null;
        string? promptBlockMessage = null;

        await foreach (var chunk in _client.Models.GenerateContentStreamAsync(
            model: _model,
            contents: contents,
            config: config).WithCancellation(ct))
        {
            if (chunk is null) continue;

            if (chunk.PromptFeedback?.BlockReason is not null)
            {
                promptBlockReason = chunk.PromptFeedback.BlockReason.ToString();
                promptBlockMessage = chunk.PromptFeedback.BlockReasonMessage;
            }

            // Usage metadata on streaming chunks is cumulative-so-far, not the final total on
            // every chunk. Pull from the last non-null one; the streaming API reports final
            // totals on the terminating chunk.
            if (chunk.UsageMetadata is var u and not null)
            {
                promptTokens = u.PromptTokenCount ?? promptTokens;
                candidateTokens = u.CandidatesTokenCount ?? candidateTokens;
                thoughtTokens = u.ThoughtsTokenCount ?? thoughtTokens;
                cachedTokens = u.CachedContentTokenCount ?? cachedTokens;
            }

            var candidate = chunk.Candidates?.FirstOrDefault();
            if (candidate is not null)
                finalCandidate = candidate;

            var chunkText = candidate?.Content?.Parts?.FirstOrDefault()?.Text;
            if (chunkText is not null)
                contentBuilder.Append(chunkText);
        }

        // Prompt-level safety block.
        if (promptBlockReason is not null)
        {
            throw new LlmContentBlockedException(
                $"Gemini prompt blocked: reason={promptBlockReason} message={promptBlockMessage}");
        }

        // Candidate-level content filter.
        var finishReason = finalCandidate?.FinishReason;
        if (finishReason is not null
            && finishReason != FinishReason.STOP
            && finishReason != FinishReason.MAX_TOKENS
            && finishReason != FinishReason.FINISH_REASON_UNSPECIFIED)
        {
            // RECITATION belongs here, not below: the model refused because the output would
            // reproduce memorised text — a decision about CONTENT, not a sick service. Treated as
            // a generic fault it poisoned the shared circuit breaker: OCR of published clinical
            // protocols (МОЗ) trips RECITATION on a few pages, the breaker opens for the whole
            // Vision tier, and every OTHER document then fails instantly with "no text could be
            // recognized". Observed on the Denis protocol upload: 42 RECITATION refusals took 27
            // unrelated files down with them. As a content block it skips retries (re-asking the
            // same model for the same page yields the same refusal) and falls back to Backup,
            // which is a different model and may not have memorised the document.
            if (finishReason is FinishReason.SAFETY
                or FinishReason.PROHIBITED_CONTENT
                or FinishReason.BLOCKLIST
                or FinishReason.SPII
                or FinishReason.IMAGE_SAFETY
                or FinishReason.IMAGE_PROHIBITED_CONTENT
                or FinishReason.RECITATION)
            {
                throw new LlmContentBlockedException(
                    $"Gemini content filtered: finishReason={finishReason}");
            }

            // LANGUAGE, OTHER, MALFORMED_FUNCTION_CALL, UNEXPECTED_TOOL_CALL — these really are
            // faults worth retrying and worth counting against the breaker.
            throw new InvalidOperationException(
                $"Gemini finished without usable output: finishReason={finishReason}");
        }

        var completionText = contentBuilder.ToString();

        // Empty output with no explicit block: transient failure — raise so the LlmService
        // retry/fallback chain engages instead of passing an empty string downstream.
        if (candidateTokens == 0 && string.IsNullOrEmpty(completionText))
        {
            throw new InvalidOperationException(
                $"Gemini returned empty completion (prompt_tokens={promptTokens}, candidates=0)");
        }

        return new LlmResponse(
            Content: completionText,
            PromptTokens: promptTokens,
            CompletionTokens: candidateTokens,
            Model: request.Model)
        {
            CachedTokens = cachedTokens,
            ReasoningTokens = thoughtTokens
        };
    }

    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        LlmRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var config = BuildConfig(request, _thinkingBudget);
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

    internal static GenerateContentConfig BuildConfig(LlmRequest request, int thinkingBudget)
    {
        var config = new GenerateContentConfig
        {
            Temperature = request.Temperature,
            // Default: 0 (thinking disabled). Explicit opt-in via ModelConfig.ThinkingBudget.
            // Pro ignores 0 and enforces a 128-token mandatory minimum — that's Google's API,
            // not ours. If you want more thinking on Pro for a specific tier, set thinkingBudget
            // in that tier's secret JSON (e.g. 1024 for clinical reasoning, 2048 for ambiguous).
            ThinkingConfig = new ThinkingConfig
            {
                ThinkingBudget = thinkingBudget,
                IncludeThoughts = false
            }
        };

        if (request.MaxTokens.HasValue)
            config.MaxOutputTokens = request.MaxTokens.Value;

        // A context cache already carries the system instruction + shared prefix; Gemini rejects
        // a per-request SystemInstruction alongside a cache, so the two are mutually exclusive.
        if (!string.IsNullOrWhiteSpace(request.CachedContentName))
        {
            config.CachedContent = request.CachedContentName;
        }
        else if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
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

        // Gemini 2.x supports a Draft-2020-12 JSON Schema subset via ResponseJsonSchema. When
        // the caller provided a schema, pass it straight through — this is strict structured
        // output, so the model's response parses on first try (no retry loop, no prompt
        // "please emit JSON" hack in the system message). Deserialize to JsonElement so the
        // SDK's own serializer round-trips the schema JSON cleanly to the API.
        if (request.ResponseFormat == LlmResponseFormat.JsonSchema
            && !string.IsNullOrWhiteSpace(request.JsonSchema))
        {
            config.ResponseJsonSchema = JsonSerializer.Deserialize<JsonElement>(request.JsonSchema);
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

    public async Task<string?> CreateContextCacheAsync(
        string? systemInstruction, IReadOnlyList<LlmMessage> contents, TimeSpan ttl,
        CancellationToken ct = default)
    {
        try
        {
            var config = new CreateCachedContentConfig
            {
                // Reuse the same content-shaping as a normal request so the cached prefix is
                // byte-identical to what an uncached call would send.
                Contents = BuildContents(new LlmRequest(_model, contents)),
                Ttl = $"{(int)ttl.TotalSeconds}s"
            };

            if (!string.IsNullOrWhiteSpace(systemInstruction))
                config.SystemInstruction = new Content { Parts = [new Part { Text = systemInstruction }] };

            var cache = await _client.Caches.CreateAsync(model: _model, config: config);

            _logger.LogInformation(
                "Gemini context cache created: name={Name} model={Model} ttl={Ttl}s cachedTokens={Tokens}",
                cache?.Name, _model, (int)ttl.TotalSeconds, cache?.UsageMetadata?.TotalTokenCount);
            return cache?.Name;
        }
        catch (Exception ex)
        {
            // Caching is a pure cost optimisation. A failure here (content below the model's
            // minimum cacheable size, quota, transient API error) must NEVER break the case —
            // return null so the caller falls back to sending the full prompt uncached.
            _logger.LogWarning(ex, "Gemini context cache create failed — using uncached prompts for this case");
            return null;
        }
    }

    public async Task DeleteContextCacheAsync(string cacheName, CancellationToken ct = default)
    {
        try
        {
            await _client.Caches.DeleteAsync(name: cacheName);
        }
        catch (Exception ex)
        {
            // Non-fatal: caches self-expire at their TTL, so a failed delete only wastes storage
            // for a few minutes. Debug-level so it never adds noise to the case log.
            _logger.LogDebug(ex, "Gemini context cache delete failed (self-expires at TTL): {Name}", cacheName);
        }
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
