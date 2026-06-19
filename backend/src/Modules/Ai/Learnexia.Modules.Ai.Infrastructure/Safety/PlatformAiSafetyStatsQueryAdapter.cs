using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Ai;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Ai.Infrastructure.Safety;

/// <summary>
/// Implements <see cref="IPlatformAiSafetyStatsQuery"/> by querying <see cref="AiDbContext"/>
/// for windowed platform-wide AI safety event statistics required by the P7-10 analytics dashboard.
///
/// <para>Cross-module isolation: the Identity façade injects <see cref="IPlatformAiSafetyStatsQuery"/>
/// from <c>Shared.Contracts</c> — it never references this adapter or any Ai project
/// directly. This adapter lives in <c>Ai.Infrastructure</c> and is registered in
/// <c>AddAiInfrastructure</c>.</para>
///
/// <para>The <c>SafetyEvent.ActionTaken</c> field is a plain string stored from the domain
/// (<c>"Blocked"</c>, <c>"Regenerated"</c>, <c>"FallbackReturned"</c>). We treat "Blocked"
/// as the hard-block category and everything else as "flagged but delivered".</para>
///
/// <para>AI request volume is now REAL — counted from the <c>ai.AiUsageLogs</c> table (built in the
/// P7-11 tutor-cost sub-batch). Counts non-streaming completions (streamed responses are a documented
/// capture gap). <see cref="PlatformAiSafetyStats.AiRequestVolumeNaReason"/> is therefore null.</para>
///
/// <para>All results are sentinel-safe: an empty window returns zeroed stats — never null, never throws.</para>
/// </summary>
public sealed class PlatformAiSafetyStatsQueryAdapter : IPlatformAiSafetyStatsQuery
{
    private readonly AiDbContext _db;

    // Hard-block action value — stored verbatim by the SafetyLayer.
    private const string BlockedAction = "Blocked";

    public PlatformAiSafetyStatsQueryAdapter(AiDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<PlatformAiSafetyStats> GetPlatformAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        // Aggregate over SafetyEvents in the window.
        // SafetyEvent.OccurredAtUtc is indexed (SafetyEventConfig) — efficient range query.
        var events = await _db.SafetyEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc)
            .Select(e => e.ActionTaken)
            .ToListAsync(ct);

        int total   = events.Count;
        int blocked = events.Count(a => a == BlockedAction);
        int flagged = total - blocked;

        // AI request volume — real data from ai.AiUsageLogs (P7-11 tutor-cost). OccurredAtUtc is
        // indexed. Counts non-streaming completions (streamed responses are a documented capture gap).
        int requestVolume = await _db.AiUsageLogs
            .AsNoTracking()
            .CountAsync(u => u.OccurredAtUtc >= fromUtc && u.OccurredAtUtc < toUtc, ct);

        return new PlatformAiSafetyStats(
            TotalSafetyEvents:        total,
            BlockedCount:             blocked,
            FlaggedCount:             flagged,
            AiRequestVolume:          requestVolume,
            AiRequestVolumeNaReason:  null);
    }
}
