using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Dataisland.LLM;

public class LlmOptions
{
    public ModelConfig Simple { get; set; } = new();
    public ModelConfig Normal { get; set; } = new();
    public ModelConfig Advanced { get; set; } = new();
    public ModelConfig Backup { get; set; } = new();
    public ModelConfig? Whisper { get; set; }
    public ModelConfig? ImageGen { get; set; }
    public ModelConfig? Vision { get; set; }
}

public class ModelConfig
{
    public string Provider { get; set; } = "openai";
    public string Model { get; set; } = "";
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public float Temperature { get; set; } = 0.7f;
    public int? MaxTokens { get; set; }
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Cost per 1K input (prompt) tokens in dollars. Used for cost tracking metrics.</summary>
    public decimal InputTokenCostPer1K { get; set; }

    /// <summary>Cost per 1K output (completion) tokens in dollars. Used for cost tracking metrics.</summary>
    public decimal OutputTokenCostPer1K { get; set; }

    // Aliases for Infisical JSON format: {"modelName":"...", "modelFamily":"..."}
    [JsonPropertyName("modelName")]
    public string? ModelName { set => Model = value ?? Model; get => null; }

    [JsonPropertyName("modelFamily")]
    public string? ModelFamily { set => Provider = value ?? Provider; get => null; }

    [JsonPropertyName("apiBase")]
    public string? ApiBase { set => BaseUrl = value ?? BaseUrl; get => null; }

    [JsonPropertyName("inputTokenCost")]
    public decimal? InputTokenCost { set { if (value.HasValue) InputTokenCostPer1K = value.Value; } get => null; }

    [JsonPropertyName("outputTokenCost")]
    public decimal? OutputTokenCost { set { if (value.HasValue) OutputTokenCostPer1K = value.Value; } get => null; }

    internal bool IsConfigured => !string.IsNullOrEmpty(Model);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static ModelConfig FromJson(string json) =>
        JsonSerializer.Deserialize<ModelConfig>(json, JsonOptions)
        ?? throw new InvalidOperationException($"Failed to parse model config JSON: {json}");
}

public static class LlmOptionsExtensions
{
    /// <summary>
    /// Load LLM options from JSON env vars.
    /// LLM__SimpleModelConfig is required; Normal, Advanced, Backup fall back to Simple if not set.
    /// Accepts both formats: {"model":"...","provider":"..."} and {"modelName":"...","modelFamily":"..."}
    /// </summary>
    public static LlmOptions GetLlmOptions(this IConfiguration configuration)
    {
        // .NET EnvironmentVariablesConfigurationProvider converts __ to : in env var names,
        // so LLM__SimpleModelConfig env var becomes LLM:SimpleModelConfig config key.
        // Try all known key formats for compatibility.
        var simple = configuration["LLM:SimpleModelConfig"] ?? configuration["LLM__SimpleModelConfig"]
            ?? configuration["SIMPLE_MODEL_CONFIG"]
            ?? throw new InvalidOperationException("LLM__SimpleModelConfig env var is required");
        var normal = configuration["LLM:NormalModelConfig"] ?? configuration["LLM__NormalModelConfig"]
            ?? configuration["NORMAL_MODEL_CONFIG"];
        var advanced = configuration["LLM:AdvancedModelConfig"] ?? configuration["LLM__AdvancedModelConfig"]
            ?? configuration["ADVANCED_MODEL_CONFIG"];
        var backup = configuration["LLM:BackupModelConfig"] ?? configuration["LLM__BackupModelConfig"]
            ?? configuration["BACKUP_MODEL_CONFIG"];
        var vision = configuration["LLM:VisionModelConfig"] ?? configuration["LLM__VisionModelConfig"]
            ?? configuration["VISION_MODEL_CONFIG"];

        var simpleConfig = ModelConfig.FromJson(simple);
        return new LlmOptions
        {
            Simple = simpleConfig,
            Normal = !string.IsNullOrWhiteSpace(normal) ? ModelConfig.FromJson(normal) : simpleConfig,
            Advanced = !string.IsNullOrWhiteSpace(advanced) ? ModelConfig.FromJson(advanced) : simpleConfig,
            Backup = !string.IsNullOrWhiteSpace(backup) ? ModelConfig.FromJson(backup) : simpleConfig,
            Vision = !string.IsNullOrWhiteSpace(vision) ? ModelConfig.FromJson(vision) : null,
        };
    }
}
