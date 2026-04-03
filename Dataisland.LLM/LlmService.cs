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
    private readonly Dictionary<string, ResiliencePipeline> _circuitBreakers = new();
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
        if (!_circuitBreakers.TryGetValue(key, out var cb))
        {
            cb = new ResiliencePipelineBuilder()
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
                .Build();
            _circuitBreakers[key] = cb;
        }
        return cb;
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
                _logger.LogWarning(ex, "LLM call failed for tier {Tier} (attempt {Attempt}/{Max}), retrying in {Delay}s",
                    tier, attempt + 1, retryDelays.Length + 1, delay);
                LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "retry").Inc();

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

                if (attempt > 0)
                    _logger.LogInformation("LLM call succeeded for tier {Tier}<{Type}> on retry attempt {Attempt}",
                        tier, typeName, attempt);

                var parsed = JsonResponseParser.TryParse<T>(response.Content, _logger);

                return new LlmResponse<T>(
                    Value: parsed,
                    RawContent: response.Content,
                    PromptTokens: response.PromptTokens,
                    CompletionTokens: response.CompletionTokens,
                    Model: response.Model);
            }
            catch (BrokenCircuitException ex) when (tier != ModelTier.Backup)
            {
                LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "circuit_open").Inc();
                _logger.LogWarning("LLM circuit breaker is open for tier {Tier}<{Type}>, falling back to Backup", tier, typeName);
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
                _logger.LogWarning(ex, "LLM call failed for tier {Tier}<{Type}> (attempt {Attempt}/{Max}), retrying in {Delay}s",
                    tier, typeName, attempt + 1, retryDelays.Length + 1, delay);
                LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "retry").Inc();

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
        LlmMetrics.RequestsTotal.WithLabels(response.Model, tier, config.Provider, "success").Inc();
        LlmMetrics.RequestDurationSeconds.WithLabels(labels).Observe(elapsed.TotalSeconds);

        if (config.InputTokenCostPer1K > 0 || config.OutputTokenCostPer1K > 0)
        {
            var cost = (decimal)response.PromptTokens / 1000m * config.InputTokenCostPer1K
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
