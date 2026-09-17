namespace Dataisland.LLM;

/// <summary>
/// One effective-dated standard text-token tariff. Rates use the units published by providers:
/// USD per one million tokens, and USD per one million cached tokens per hour for storage.
/// </summary>
public sealed record LlmModelPrice(
    DateTimeOffset EffectiveFrom,
    string Provider,
    string Model,
    decimal InputUsdPerMillionTokens,
    decimal OutputUsdPerMillionTokens,
    decimal CacheReadUsdPerMillionTokens,
    decimal CacheStorageUsdPerMillionTokenHour,
    decimal? ReasoningUsdPerMillionTokens = null);

/// <summary>
/// The exact tariff selected for a cost calculation. Unknown models deliberately carry null
/// rates: uncertainty must never be silently converted to zero or another model's tariff.
/// </summary>
public sealed record LlmPriceSnapshot(
    string Model,
    DateTimeOffset PricedAt,
    DateTimeOffset? EffectiveFrom,
    string? Provider,
    decimal? InputUsdPerMillionTokens,
    decimal? OutputUsdPerMillionTokens,
    decimal? CacheReadUsdPerMillionTokens,
    decimal? CacheStorageUsdPerMillionTokenHour,
    decimal? ReasoningUsdPerMillionTokens,
    string Status)
{
    public bool IsKnown => string.Equals(Status, KnownStatus, StringComparison.Ordinal);

    public const string KnownStatus = "known";
    public const string UnknownModelStatus = "unknown_model";
    public const string NoEffectivePriceStatus = "no_effective_price";
}

/// <summary>Cost components for one model. <see cref="TotalUsd"/> is null when pricing is uncertain.</summary>
public sealed record LlmCostEstimate(
    LlmPriceSnapshot Price,
    decimal? InputUsd,
    decimal? CacheReadUsd,
    decimal? OutputUsd,
    decimal? ReasoningUsd,
    decimal? CacheStorageUsd)
{
    public bool IsKnown => Price.IsKnown;

    public decimal? TotalUsd => IsKnown
        ? InputUsd.GetValueOrDefault()
          + CacheReadUsd.GetValueOrDefault()
          + OutputUsd.GetValueOrDefault()
          + ReasoningUsd.GetValueOrDefault()
          + CacheStorageUsd.GetValueOrDefault()
        : null;
}

/// <summary>
/// Canonical model tariff registry shared by LLM metrics, MedicalFlow spend accounting and API
/// reporting. Model routing tiers are intentionally absent: a model has one tariff at a point in
/// time regardless of whether it was reached as Simple, Normal or Backup.
/// </summary>
public sealed class LlmPricingRegistry
{
    private readonly IReadOnlyList<LlmModelPrice> _prices;

    public LlmPricingRegistry(IEnumerable<LlmModelPrice>? prices = null)
    {
        _prices = (prices ?? BuiltInPrices)
            .OrderBy(price => price.EffectiveFrom)
            .ToArray();
    }

    /// <summary>
    /// Standard synchronous text tariffs. Add a new effective-dated row when a provider changes
    /// a price; do not edit an old row, because event snapshots must remain auditable.
    /// </summary>
    public static IReadOnlyList<LlmModelPrice> BuiltInPrices { get; } =
    [
        // OpenAI API pricing, model release 2024-07-18. Prompt caching has no separately billed storage.
        new(new DateTimeOffset(2024, 7, 18, 0, 0, 0, TimeSpan.Zero), "openai", "gpt-4o-mini",
            0.15m, 0.60m, 0.075m, 0m),

        // Gemini 2.5 standard paid-tier text/image/video pricing.
        new(new DateTimeOffset(2025, 6, 17, 0, 0, 0, TimeSpan.Zero), "gemini", "gemini-2.5-pro",
            1.25m, 10m, 0.125m, 4.50m),
        new(new DateTimeOffset(2025, 6, 17, 0, 0, 0, TimeSpan.Zero), "gemini", "gemini-2.5-flash",
            0.30m, 2.50m, 0.03m, 1m),
        new(new DateTimeOffset(2025, 7, 22, 0, 0, 0, TimeSpan.Zero), "gemini", "gemini-2.5-flash-lite",
            0.10m, 0.40m, 0.01m, 1m)
    ];

    public LlmPriceSnapshot GetSnapshot(string model, DateTimeOffset? at = null)
    {
        var pricedAt = at ?? DateTimeOffset.UtcNow;
        var modelPrices = _prices
            .Where(price => string.Equals(price.Model, model, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (modelPrices.Length == 0)
            return Unknown(model, pricedAt, LlmPriceSnapshot.UnknownModelStatus);

        var selected = modelPrices.LastOrDefault(price => price.EffectiveFrom <= pricedAt);
        if (selected is null)
            return Unknown(model, pricedAt, LlmPriceSnapshot.NoEffectivePriceStatus);

        return new LlmPriceSnapshot(
            selected.Model,
            pricedAt,
            selected.EffectiveFrom,
            selected.Provider,
            selected.InputUsdPerMillionTokens,
            selected.OutputUsdPerMillionTokens,
            selected.CacheReadUsdPerMillionTokens,
            selected.CacheStorageUsdPerMillionTokenHour,
            selected.ReasoningUsdPerMillionTokens,
            LlmPriceSnapshot.KnownStatus);
    }

    public LlmCostEstimate Estimate(
        string model,
        long promptTokens,
        long completionTokens,
        long cachedTokens = 0,
        long reasoningTokens = 0,
        decimal cacheStorageTokenHours = 0m,
        DateTimeOffset? at = null)
    {
        var price = GetSnapshot(model, at);
        if (!price.IsKnown)
            return new LlmCostEstimate(price, null, null, null, null, null);

        var normalizedPrompt = Math.Max(0, promptTokens);
        var normalizedCached = Math.Clamp(cachedTokens, 0, normalizedPrompt);
        var uncachedTokens = normalizedPrompt - normalizedCached;
        var reasoningRate = price.ReasoningUsdPerMillionTokens ?? price.OutputUsdPerMillionTokens!.Value;

        return new LlmCostEstimate(
            price,
            uncachedTokens / 1_000_000m * price.InputUsdPerMillionTokens!.Value,
            normalizedCached / 1_000_000m * price.CacheReadUsdPerMillionTokens!.Value,
            Math.Max(0, completionTokens) / 1_000_000m * price.OutputUsdPerMillionTokens!.Value,
            Math.Max(0, reasoningTokens) / 1_000_000m * reasoningRate,
            Math.Max(0m, cacheStorageTokenHours) / 1_000_000m
                * price.CacheStorageUsdPerMillionTokenHour!.Value);
    }

    private static LlmPriceSnapshot Unknown(string model, DateTimeOffset pricedAt, string status) =>
        new(model, pricedAt, null, null, null, null, null, null, null, status);
}
