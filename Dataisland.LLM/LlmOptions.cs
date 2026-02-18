using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Dataisland.LLM;

public class LlmOptions
{
    public required ModelConfig Simple { get; set; }
    public required ModelConfig Normal { get; set; }
    public required ModelConfig Advanced { get; set; }
    public required ModelConfig Backup { get; set; }
    public ModelConfig? Whisper { get; set; }
    public ModelConfig? ImageGen { get; set; }
    public ModelConfig? Vision { get; set; }
}

public class ModelConfig
{
    public required string Provider { get; set; }
    public required string Model { get; set; }
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public float Temperature { get; set; } = 0.7f;
    public int? MaxTokens { get; set; }
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Cost per 1K input (prompt) tokens in dollars. Used for cost tracking metrics.</summary>
    public decimal InputTokenCostPer1K { get; set; }

    /// <summary>Cost per 1K output (completion) tokens in dollars. Used for cost tracking metrics.</summary>
    public decimal OutputTokenCostPer1K { get; set; }

    /// <summary>
    /// Parse a JSON model config string (query_flow format) into ModelConfig.
    /// JSON format: {"apiVersion":"...","apiKey":"...","apiBase":"...","modelName":"...","modelFamily":"...","inputTokenCost":0.01,"outputTokenCost":0.03}
    /// </summary>
    public static ModelConfig FromJson(string json)
    {
        var doc = JsonSerializer.Deserialize<JsonModelConfig>(json)
            ?? throw new InvalidOperationException($"Failed to parse model config JSON: {json}");

        return new ModelConfig
        {
            Provider = doc.ModelFamily ?? doc.ApiVersion ?? "openai",
            Model = doc.ModelName ?? throw new InvalidOperationException("modelName is required"),
            ApiKey = doc.ApiKey,
            BaseUrl = doc.ApiBase,
            InputTokenCostPer1K = doc.InputTokenCost ?? 0,
            OutputTokenCostPer1K = doc.OutputTokenCost ?? 0,
        };
    }

    private sealed class JsonModelConfig
    {
        [JsonPropertyName("apiVersion")] public string? ApiVersion { get; set; }
        [JsonPropertyName("apiKey")] public string? ApiKey { get; set; }
        [JsonPropertyName("apiBase")] public string? ApiBase { get; set; }
        [JsonPropertyName("modelName")] public string? ModelName { get; set; }
        [JsonPropertyName("modelFamily")] public string? ModelFamily { get; set; }
        [JsonPropertyName("inputTokenCost")] public decimal? InputTokenCost { get; set; }
        [JsonPropertyName("outputTokenCost")] public decimal? OutputTokenCost { get; set; }
    }
}

public static class LlmOptionsExtensions
{
    /// <summary>
    /// Load LLM options from JSON config env vars.
    /// SIMPLE_MODEL_CONFIG is required; NORMAL, ADVANCED, BACKUP fall back to SIMPLE if not set.
    /// JSON format: {"modelName":"…","modelFamily":"…","apiKey":"…","apiBase":"…","apiVersion":"…"}
    /// </summary>
    public static LlmOptions GetLlmOptions(this IConfiguration configuration)
    {
        var simple = configuration["SIMPLE_MODEL_CONFIG"]
            ?? throw new InvalidOperationException("SIMPLE_MODEL_CONFIG env var is required");
        var normal = configuration["NORMAL_MODEL_CONFIG"];
        var advanced = configuration["ADVANCED_MODEL_CONFIG"];
        var backup = configuration["BACKUP_MODEL_CONFIG"];
        var vision = configuration["VISION_MODEL_CONFIG"];

        return new LlmOptions
        {
            Simple = ModelConfig.FromJson(simple),
            Normal = !string.IsNullOrWhiteSpace(normal) ? ModelConfig.FromJson(normal) : ModelConfig.FromJson(simple),
            Advanced = !string.IsNullOrWhiteSpace(advanced) ? ModelConfig.FromJson(advanced) : ModelConfig.FromJson(simple),
            Backup = !string.IsNullOrWhiteSpace(backup) ? ModelConfig.FromJson(backup) : ModelConfig.FromJson(simple),
            Vision = !string.IsNullOrWhiteSpace(vision) ? ModelConfig.FromJson(vision) : null,
        };
    }
}
