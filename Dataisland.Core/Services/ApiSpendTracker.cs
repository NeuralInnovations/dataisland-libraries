using Dataisland.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Dataisland.Core.Services;

public record SpendGateResult(bool Allowed, decimal CurrentSpendUsd, decimal CapUsd, string? Reason);

public interface IApiSpendTracker
{
    /// <summary>
    /// Check whether the organisation is permitted to start a new LLM-consuming operation
    /// given its current-month cumulative spend and configured cap. A cap of 0 means
    /// unlimited. Returns a non-Allowed result with Reason set when the cap would be exceeded
    /// or when the organisation cannot be found.
    /// </summary>
    Task<SpendGateResult> CheckAsync(string organizationId, CancellationToken ct = default);

    /// <summary>
    /// Record post-operation spend for the organisation, atomically incrementing the current
    /// month's cumulative cost and token counters. Safe to call from parallel consumers.
    /// </summary>
    Task<decimal> RecordAsync(
        string organizationId,
        decimal costUsd,
        long promptTokens,
        long completionTokens,
        CancellationToken ct = default);
}

/// <summary>
/// No-op fallback used when MongoDB is not wired into the host service — e.g. Service.MedicalFlow
/// deployments where the per-org spend cap feature is not yet enabled. Always permits processing
/// and silently drops spend-record calls, so existing deployments that don't provide a MongoDB
/// connection string still start cleanly without losing case-processing functionality.
/// </summary>
public class NullApiSpendTracker(ILogger<NullApiSpendTracker> logger) : IApiSpendTracker
{
    private bool _warned;

    public Task<SpendGateResult> CheckAsync(string organizationId, CancellationToken ct = default)
    {
        if (!_warned)
        {
            logger.LogWarning(
                "NullApiSpendTracker in use — per-organisation API spend caps are DISABLED. " +
                "Set MongoDB__ConnectionString on the service to enable enforcement.");
            _warned = true;
        }
        return Task.FromResult(new SpendGateResult(true, 0m, 0m, null));
    }

    public Task<decimal> RecordAsync(string organizationId, decimal costUsd, long promptTokens, long completionTokens, CancellationToken ct = default)
        => Task.FromResult(0m);
}

public class ApiSpendTracker(
    IOrganizationRepository organizations,
    IApiSpendRepository spendRepo,
    ILogger<ApiSpendTracker> logger) : IApiSpendTracker
{
    public async Task<SpendGateResult> CheckAsync(string organizationId, CancellationToken ct = default)
    {
        var org = await organizations.GetByIdAsync(organizationId);
        if (org is null)
        {
            // Unknown org: refuse rather than silently bypass. If the caller routes requests
            // through this tracker, routing at all implies they expect gating.
            logger.LogWarning("ApiSpendTracker: organisation {OrgId} not found; refusing", organizationId);
            return new SpendGateResult(false, 0m, 0m, $"Organisation {organizationId} not found");
        }

        var cap = org.Profile.ApiSpendCapUsd;
        var yearMonth = CurrentYearMonth();
        var current = await spendRepo.GetCurrentSpendAsync(organizationId, yearMonth, ct);

        // Cap of 0 is explicit "no cap" — bypass the check.
        if (cap <= 0m)
            return new SpendGateResult(true, current, 0m, null);

        if (current >= cap)
        {
            logger.LogWarning(
                "ApiSpendTracker: organisation {OrgId} reached monthly cap — current=${Current:F2} cap=${Cap:F2} month={YearMonth}",
                organizationId, current, cap, yearMonth);
            return new SpendGateResult(false, current, cap,
                $"Monthly API spend cap of ${cap:F2} reached (current ${current:F2}). Increase the cap or wait until next calendar month.");
        }

        return new SpendGateResult(true, current, cap, null);
    }

    public async Task<decimal> RecordAsync(
        string organizationId,
        decimal costUsd,
        long promptTokens,
        long completionTokens,
        CancellationToken ct = default)
    {
        // Cost of 0 still worth recording so the case-count and token counters advance —
        // useful for "case ran for free because all calls were cached" visibility.
        var yearMonth = CurrentYearMonth();
        var newTotal = await spendRepo.IncrementAsync(
            organizationId, yearMonth, costUsd, promptTokens, completionTokens, ct);

        logger.LogInformation(
            "ApiSpendTracker: org={OrgId} month={YearMonth} +${Delta:F4} -> ${Total:F2} (tokens +{Prompt}p +{Completion}c)",
            organizationId, yearMonth, costUsd, newTotal, promptTokens, completionTokens);

        return newTotal;
    }

    private static string CurrentYearMonth() => DateTime.UtcNow.ToString("yyyy-MM");
}
