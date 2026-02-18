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
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<LlmService> _logger;
    private readonly ResiliencePipeline _circuitBreaker;

    public LlmService(LlmOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<LlmService>();

        _circuitBreaker = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(60),
                OnOpened = args =>
                {
                    _logger.LogWarning("LLM circuit breaker OPENED for {Duration}s", args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("LLM circuit breaker CLOSED");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogInformation("LLM circuit breaker HALF-OPEN");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
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

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

            var response = await _circuitBreaker.ExecuteAsync(
                async token => await provider.CompleteAsync(request, token),
                timeoutCts.Token);

            sw.Stop();
            RecordMetrics(config, tierName, response, sw.Elapsed);

            return response;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "timeout").Inc();
            LlmMetrics.RequestDurationSeconds.WithLabels(config.Model, tierName, config.Provider).Observe(sw.Elapsed.TotalSeconds);

            _logger.LogWarning("LLM call timed out after {Timeout}s for tier {Tier}, model {Model}",
                config.TimeoutSeconds, tier, config.Model);

            if (tier != ModelTier.Backup)
                return await CompleteAsync(ModelTier.Backup, messages, systemPrompt, temperature, maxTokens, ct);

            throw new TimeoutException($"LLM call timed out after {config.TimeoutSeconds}s (model: {config.Model})");
        }
        catch (BrokenCircuitException ex)
        {
            LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "circuit_open").Inc();

            _logger.LogWarning("LLM circuit breaker is open for tier {Tier}, falling back", tier);

            if (tier != ModelTier.Backup)
                return await CompleteAsync(ModelTier.Backup, messages, systemPrompt, temperature, maxTokens, ct);

            throw new ServiceUnavailableException("LLM", ex);
        }
        catch (Exception ex) when (tier != ModelTier.Backup)
        {
            sw.Stop();
            LlmMetrics.RequestsTotal.WithLabels(config.Model, tierName, config.Provider, "error").Inc();
            LlmMetrics.RequestDurationSeconds.WithLabels(config.Model, tierName, config.Provider).Observe(sw.Elapsed.TotalSeconds);

            _logger.LogWarning(ex, "LLM call failed for tier {Tier}, falling back to Backup", tier);
            return await CompleteAsync(ModelTier.Backup, messages, systemPrompt, temperature, maxTokens, ct);
        }
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
                "openai" or "anthropic" => new OpenAiProvider(config, _loggerFactory.CreateLogger<OpenAiProvider>()),
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
