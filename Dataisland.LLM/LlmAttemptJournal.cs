namespace Dataisland.LLM;

/// <summary>
/// Immutable accounting entry for one provider invocation. A logical LLM request keeps the same
/// <see cref="RequestId"/> while retries and tier fallbacks increment <see cref="Attempt"/>.
/// Entries with <see cref="HasTokenUsage"/> false still prove that an invocation was attempted,
/// but are excluded from token/cost projections because the provider returned no usage data.
/// </summary>
public sealed record LlmAttemptUsage(
    string RequestId,
    int Attempt,
    string Model,
    ModelTier Tier,
    string Phase,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int? PromptTokens,
    int? CompletionTokens,
    int? CachedTokens,
    int? ReasoningTokens,
    LlmPriceSnapshot PriceSnapshot,
    decimal? CostUsd,
    string Outcome)
{
    public bool HasTokenUsage => PromptTokens.HasValue && CompletionTokens.HasValue;
}

/// <summary>
/// Per-logical-request append-only collector shared by same-tier retries and Backup fallback.
/// It is deliberately local to one CompleteAsync call, so parallel LLM calls cannot mix entries.
/// </summary>
internal sealed class LlmAttemptJournal
{
    private readonly List<LlmAttemptUsage> _entries = [];
    private int _attempt;

    public string RequestId { get; } = Guid.NewGuid().ToString("N");

    public int NextAttempt() => ++_attempt;

    public void Append(LlmAttemptUsage entry) => _entries.Add(entry);

    public IReadOnlyList<LlmAttemptUsage> Snapshot() => _entries.ToArray();
}

/// <summary>Preserves the attempt journal when the logical request ultimately throws.</summary>
public static class LlmAttemptJournalException
{
    private const string DataKey = "Dataisland.LLM.AttemptJournal";

    internal static void Attach(Exception exception, IReadOnlyList<LlmAttemptUsage> attempts) =>
        exception.Data[DataKey] = attempts;

    public static IReadOnlyList<LlmAttemptUsage> GetAttempts(Exception exception) =>
        exception.Data[DataKey] as IReadOnlyList<LlmAttemptUsage> ?? [];
}
