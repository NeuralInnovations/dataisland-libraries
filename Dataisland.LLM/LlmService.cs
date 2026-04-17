using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dataisland.LLM.Providers;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Dataisland.LLM;

public class LlmService : ILlmService
{
    private readonly LlmOptions _options;
    private readonly Dictionary<string, ILlmProvider> _providers = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ResiliencePipeline> _circuitBreakers = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<LlmService> _logger;

    public LlmService(LlmOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<LlmService>();
    }

    /// <summary>
    /// Per-provider circuit breaker so that a failing Normal tier
    /// does not block Backup tier (they typically use different providers/models).
    /// </summary>
    private ResiliencePipeline GetCircuitBreaker(ModelConfig config)
    {
        var key = $"{config.Provider}:{config.Model}";
        return _circuitBreakers.GetOrAdd(key, _ => new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(60),
                OnOpened = args =>
                {
                    _logger.LogWarning("LLM circuit breaker OPENED for {Model} ({Duration}s)",
                        config.Model, args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("LLM circuit breaker CLOSED for {Model}", config.Model);
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogInformation("LLM circuit breaker HALF-OPEN for {Model}", config.Model);
                    return ValueTask.CompletedTask;
                }
            })
            .Build());
    }

    public async Task<LlmResponse> CompleteAsync(
        ModelTier tier, IReadOnlyList<LlmMessage> messages,
        string? systemPrompt = null, float? temperature = null, int? maxTokens = null,
        CancellationToken ct = default)
    {
        var config = GetConfig(tier);
        var provider = GetOrCreateProvider(config);

        var request = new LlmRequest(
            Model: config.Model,
            Messages: messages,
            Temperature: temperature ?? config.Temperature,
            MaxTokens: maxTokens ?? config.MaxTokens,
            SystemPrompt: systemPrompt
        );

        var tierName = tier.ToString();
        var sw = Stopwatch.StartNew();

        // Retry with exponential backoff on the PRIMARY tier first, then fall back to Backup
        var retryDelays = new[] { 5, 15, 30 }; // seconds — Gemini quota resets ~47s, need longer waits

        for (var attempt = 0; attempt <= retryDelays.Length; attempt++)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

                var response = await GetCircuitBreaker(config).ExecuteAsync(
                    async token => await provider.CompleteAsync(request, token),
                    timeoutCts.Token);

                sw.Stop();
                RecordMetrics(config, tierName, response, sw.Elapsed);

                if (attempt > 0)
                    _logger.LogInformation("LLM call succeeded for tier {Tier} on retry attempt {Attempt}", tier, attempt);

                return response;
            }
            catch (BrokenCircuitException ex) when (tier != ModelTier.Backup)
            {
                LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "circuit_open").Inc();
                _logger.LogWarning("LLM circuit breaker is open for tier {Tier}, falling back to Backup", tier);
                return await CompleteAsync(ModelTier.Backup, messages, systemPrompt, temperature, maxTokens, ct);
            }
            catch (LlmContentBlockedException ex) when (tier != ModelTier.Backup)
            {
                // Safety/content-filter block on primary — retrying the same model will reproduce the
                // block. Jump straight to Backup (typically a different provider).
                LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "content_blocked").Inc();
                _logger.LogWarning(ex, "LLM content blocked on tier {Tier}, skipping retries and falling back to Backup", tier);
                return await CompleteAsync(ModelTier.Backup, messages, systemPrompt, temperature, maxTokens, ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                sw.Stop();
                LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "timeout").Inc();
                LlmMetrics.RequestDurationSeconds.WithLabels(config.Model, tierName, config.Provider).Observe(sw.Elapsed.TotalSeconds);
                _logger.LogWarning("LLM call timed out after {Timeout}s for tier {Tier} (attempt {Attempt})",
                    config.TimeoutSeconds, tier, attempt + 1);

                // Timeout — no point retrying, go to backup
                if (tier != ModelTier.Backup)
                    return await CompleteAsync(ModelTier.Backup, messages, systemPrompt, temperature, maxTokens, ct);
                throw new TimeoutException($"LLM call timed out after {config.TimeoutSeconds}s (model: {config.Model})");
            }
            catch (Exception ex) when (attempt < retryDelays.Length)
            {
                // Retryable error (rate limit, server error) — wait and retry on SAME tier
                var delay = retryDelays[attempt];
                var category = ClassifyTransientError(ex);
                _logger.LogWarning(ex, "LLM call failed ({Category}) for tier {Tier} (attempt {Attempt}/{Max}), retrying in {Delay}s",
                    category, tier, attempt + 1, retryDelays.Length + 1, delay);
                LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, category).Inc();

                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                sw = Stopwatch.StartNew(); // reset timer for next attempt
            }
            catch (Exception ex) when (tier != ModelTier.Backup)
            {
                // All retries exhausted — fall back to Backup tier
                sw.Stop();
                LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "error").Inc();
                LlmMetrics.RequestDurationSeconds.WithLabels(config.Model, tierName, config.Provider).Observe(sw.Elapsed.TotalSeconds);

                _logger.LogWarning(ex, "LLM call failed for tier {Tier} after {Attempts} attempts, falling back to Backup",
                    tier, retryDelays.Length + 1);
                return await CompleteAsync(ModelTier.Backup, messages, systemPrompt, temperature, maxTokens, ct);
            }
        }

        // Should not reach here, but satisfy compiler
        throw new InvalidOperationException("LLM retry loop exited unexpectedly");
    }

    public async Task<LlmResponse<T>> CompleteAsync<T>(
        ModelTier tier, IReadOnlyList<LlmMessage> messages,
        string? systemPrompt = null, float? temperature = null, int? maxTokens = null,
        CancellationToken ct = default) where T : class
    {
        var config = GetConfig(tier);
        var supportsJsonSchema = SupportsJsonSchema(config);
        var schema = JsonSchemaGenerator.Generate<T>();
        var typeName = typeof(T).Name;

        // Enhance system prompt with JSON instruction
        var enhancedSystemPrompt = systemPrompt ?? "";
        if (!supportsJsonSchema)
        {
            // For providers without native schema support, instruct via prompt
            enhancedSystemPrompt += $"\n\nYou MUST respond with valid JSON matching this schema:\n{schema}\nDo NOT include any text outside the JSON object.";
        }

        var provider = GetOrCreateProvider(config);
        var request = new LlmRequest(
            Model: config.Model,
            Messages: messages,
            Temperature: temperature ?? config.Temperature,
            MaxTokens: maxTokens ?? config.MaxTokens,
            SystemPrompt: enhancedSystemPrompt
        )
        {
            ResponseFormat = supportsJsonSchema ? LlmResponseFormat.JsonSchema : LlmResponseFormat.Json,
            JsonSchema = schema,
            JsonSchemaName = typeName
        };

        var tierName = tier.ToString();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Retry with exponential backoff on the PRIMARY tier first, then fall back to Backup
        var retryDelays = new[] { 5, 15, 30 }; // seconds — Gemini quota resets ~47s, need longer waits

        for (var attempt = 0; attempt <= retryDelays.Length; attempt++)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

                var response = await GetCircuitBreaker(config).ExecuteAsync(
                    async token => await provider.CompleteAsync(request, token),
                    timeoutCts.Token);

                sw.Stop();
                RecordMetrics(config, tierName, response, sw.Elapsed);

                var parsed = JsonResponseParser.TryParse<T>(response.Content, _logger);

                // A null parse means the LLM returned malformed/empty JSON. A single bad
                // response used to silently collapse an entire phase (e.g. relevant-file
                // selection), producing phantom AI-fallbacks. Retry on the same tier before
                // giving up, matching Python's make_llm_request_with_fallback behaviour.
                if (parsed is null && attempt < retryDelays.Length)
                {
                    var delay = retryDelays[attempt];
                    _logger.LogWarning(
                        "LLM returned unparseable JSON for tier {Tier}<{Type}> (attempt {Attempt}/{Max}), retrying in {Delay}s. Raw: {Raw}",
                        tier, typeName, attempt + 1, retryDelays.Length + 1, delay,
                        response.Content.Length > 200 ? response.Content[..200] + "..." : response.Content);
                    LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "parse_retry").Inc();

                    await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                    sw = Stopwatch.StartNew();
                    continue;
                }

                if (attempt > 0)
                    _logger.LogInformation("LLM call succeeded for tier {Tier}<{Type}> on retry attempt {Attempt}",
                        tier, typeName, attempt);

                // After all primary-tier retries exhausted and still null: hand off to Backup
                // so downstream gets a chance at a different provider/prompt interpretation.
                if (parsed is null && tier != ModelTier.Backup)
                {
                    _logger.LogWarning(
                        "LLM returned unparseable JSON for tier {Tier}<{Type}> after {Attempts} attempts, falling back to Backup",
                        tier, typeName, retryDelays.Length + 1);
                    return await CompleteAsync<T>(ModelTier.Backup, messages, systemPrompt, temperature, maxTokens, ct);
                }

                return new LlmResponse<T>(
                    Value: parsed,
                    RawContent: response.Content,
                    PromptTokens: response.PromptTokens,
                    CompletionTokens: response.CompletionTokens,
                    Model: response.Model)
                {
                    CachedTokens = response.CachedTokens
                };
            }
            catch (BrokenCircuitException ex) when (tier != ModelTier.Backup)
            {
                LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "circuit_open").Inc();
                _logger.LogWarning("LLM circuit breaker is open for tier {Tier}<{Type}>, falling back to Backup", tier, typeName);
                return await CompleteAsync<T>(ModelTier.Backup, messages, systemPrompt, temperature, maxTokens, ct);
            }
            catch (LlmContentBlockedException ex) when (tier != ModelTier.Backup)
            {
                LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "content_blocked").Inc();
                _logger.LogWarning(ex, "LLM content blocked on tier {Tier}<{Type}>, skipping retries and falling back to Backup", tier, typeName);
                return await CompleteAsync<T>(ModelTier.Backup, messages, systemPrompt, temperature, maxTokens, ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                sw.Stop();
                LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "timeout").Inc();
                LlmMetrics.RequestDurationSeconds.WithLabels(config.Model, tierName, config.Provider).Observe(sw.Elapsed.TotalSeconds);
                _logger.LogWarning("LLM call timed out after {Timeout}s for tier {Tier}<{Type}> (attempt {Attempt})",
                    config.TimeoutSeconds, tier, typeName, attempt + 1);

                if (tier != ModelTier.Backup)
                    return await CompleteAsync<T>(ModelTier.Backup, messages, systemPrompt, temperature, maxTokens, ct);
                throw new TimeoutException($"LLM call timed out after {config.TimeoutSeconds}s (model: {config.Model})");
            }
            catch (Exception ex) when (attempt < retryDelays.Length)
            {
                var delay = retryDelays[attempt];
                var category = ClassifyTransientError(ex);
                _logger.LogWarning(ex, "LLM call failed ({Category}) for tier {Tier}<{Type}> (attempt {Attempt}/{Max}), retrying in {Delay}s",
                    category, tier, typeName, attempt + 1, retryDelays.Length + 1, delay);
                LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, category).Inc();

                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                sw = Stopwatch.StartNew();
            }
            catch (Exception ex) when (tier != ModelTier.Backup)
            {
                sw.Stop();
                LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "error").Inc();
                LlmMetrics.RequestDurationSeconds.WithLabels(config.Model, tierName, config.Provider).Observe(sw.Elapsed.TotalSeconds);

                _logger.LogWarning(ex, "LLM call failed for tier {Tier}<{Type}> after {Attempts} attempts, falling back to Backup",
                    tier, typeName, retryDelays.Length + 1);
                return await CompleteAsync<T>(ModelTier.Backup, messages, systemPrompt, temperature, maxTokens, ct);
            }
        }

        throw new InvalidOperationException("LLM retry loop exited unexpectedly");
    }

    /// <summary>
    /// Label retryable exceptions so rate-limit/quota errors show up distinctly in
    /// metrics — useful when diagnosing whether Gemini quota or generic errors drove a
    /// fallback wave.
    /// </summary>
    private static string ClassifyTransientError(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException!)
        {
            var msg = e.Message;
            if (string.IsNullOrEmpty(msg)) continue;
            if (msg.Contains("429", StringComparison.Ordinal)
                || msg.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("quota", StringComparison.OrdinalIgnoreCase))
                return "rate_limit";
            if (msg.Contains("503", StringComparison.Ordinal)
                || msg.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("overloaded", StringComparison.OrdinalIgnoreCase))
                return "unavailable";
            if (e.InnerException is null) break;
        }
        return "retry";
    }

    private static bool SupportsJsonSchema(ModelConfig config)
    {
        // OpenAI, Azure OpenAI, and Anthropic via OpenAI-compatible endpoints support structured outputs
        return config.Provider.ToLowerInvariant() is "openai" or "azure" or "gpt" or "anthropic";
    }

    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        ModelTier tier, IReadOnlyList<LlmMessage> messages,
        string? systemPrompt = null, float? temperature = null, int? maxTokens = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var config = GetConfig(tier);
        var provider = GetOrCreateProvider(config);

        var request = new LlmRequest(
            Model: config.Model,
            Messages: messages,
            Temperature: temperature ?? config.Temperature,
            MaxTokens: maxTokens ?? config.MaxTokens,
            SystemPrompt: systemPrompt
        );

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

        await foreach (var token in provider.CompleteStreamingAsync(request, timeoutCts.Token))
        {
            yield return token;
        }
    }

    private static void RecordMetrics(ModelConfig config, string tier, LlmResponse response, TimeSpan elapsed)
    {
        var labels = new[] { response.Model, tier, config.Provider };

        LlmMetrics.PromptTokensTotal.WithLabels(labels).Inc(response.PromptTokens);
        LlmMetrics.CompletionTokensTotal.WithLabels(labels).Inc(response.CompletionTokens);
        if (response.CachedTokens > 0)
            LlmMetrics.CachedTokensTotal.WithLabels(labels).Inc(response.CachedTokens);
        LlmMetrics.RequestsTotal.WithLabels(response.Model, tier, config.Provider, "success").Inc();
        LlmMetrics.RequestDurationSeconds.WithLabels(labels).Observe(elapsed.TotalSeconds);

        if (config.InputTokenCostPer1K > 0 || config.OutputTokenCostPer1K > 0)
        {
            // Cached input is ~25% of normal price on Gemini / ~50% on OpenAI; use 50% as a
            // conservative blended factor for the "billable" input calculation. This keeps the
            // tracked cost in the right ballpark without plumbing per-provider discount factors.
            var billablePromptTokens = response.PromptTokens - (response.CachedTokens / 2);
            var cost = (decimal)billablePromptTokens / 1000m * config.InputTokenCostPer1K
                     + (decimal)response.CompletionTokens / 1000m * config.OutputTokenCostPer1K;
            LlmMetrics.CostDollarsTotal.WithLabels(labels).Inc((double)cost);
        }
    }

    private ModelConfig GetConfig(ModelTier tier) => tier switch
    {
        ModelTier.Simple => _options.Simple,
        ModelTier.Normal => _options.Normal,
        ModelTier.Advanced => _options.Advanced,
        ModelTier.Backup => _options.Backup,
        ModelTier.Vision => _options.Vision ?? _options.Normal,
        _ => _options.Simple
    };

    public decimal EstimateCostUsd(string model, int promptTokens, int completionTokens, int cachedTokens = 0)
    {
        // Lookup is by exact model-name match so the same estimator works regardless of which
        // tier the call was routed to (and across fallbacks between tiers). Match against every
        // configured tier, take the first hit — if the same model is configured on multiple
        // tiers, pricing is assumed identical.
        var config = FindConfigByModel(model);
        if (config is null) return 0m;

        // Mirror of LlmService.RecordMetrics: cached input is billed at roughly half of normal
        // on OpenAI (exact) and a quarter on Gemini (under-reports savings on Gemini). Using
        // /2 as the blended approximation so cost-tracking remains consistent with metrics.
        var billablePrompt = promptTokens - (cachedTokens / 2);
        if (billablePrompt < 0) billablePrompt = 0;

        return (decimal)billablePrompt / 1000m * config.InputTokenCostPer1K
             + (decimal)completionTokens / 1000m * config.OutputTokenCostPer1K;
    }

    private ModelConfig? FindConfigByModel(string model)
    {
        var configs = new[] { _options.Simple, _options.Normal, _options.Advanced, _options.Backup, _options.Vision };
        foreach (var c in configs)
        {
            if (c is not null && !string.IsNullOrEmpty(c.Model)
                && string.Equals(c.Model, model, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    private ILlmProvider GetOrCreateProvider(ModelConfig config)
    {
        var key = $"{config.Provider}:{config.Model}:{config.BaseUrl}";

        if (!_providers.TryGetValue(key, out var provider))
        {
            provider = config.Provider.ToLowerInvariant() switch
            {
                "openai" or "anthropic" or "gpt" or "azure" => new OpenAiProvider(config, _loggerFactory.CreateLogger<OpenAiProvider>()),
                "gemini" or "google" => new GeminiProvider(config, _loggerFactory.CreateLogger<GeminiProvider>()),
                _ => throw new NotSupportedException($"LLM provider '{config.Provider}' not supported")
            };
            _providers[key] = provider;
        }

        return provider;
    }
}

internal class ServiceUnavailableException(string service, Exception? inner = null)
    : Exception($"Service '{service}' is temporarily unavailable", inner);

/// <summary>
/// Thrown by providers when the request or response was blocked by content-safety filters.
/// Retrying the same model will almost certainly produce the same block, so LlmService
/// routes these directly to the Backup tier without retries on the primary.
/// </summary>
public class LlmContentBlockedException(string message, Exception? inner = null)
    : Exception(message, inner);
