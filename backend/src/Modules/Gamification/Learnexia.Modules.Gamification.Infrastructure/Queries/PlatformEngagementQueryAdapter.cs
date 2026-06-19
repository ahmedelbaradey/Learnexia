using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries;

/// <summary>
/// Implements <see cref="IPlatformEngagementQuery"/> by querying <see cref="GamificationDbContext"/>
/// for windowed platform-wide engagement statistics required by the P7-10 analytics dashboard.
///
/// <para>Cross-module isolation: the Identity façade injects <see cref="IPlatformEngagementQuery"/>
/// from <c>Shared.Contracts</c> — it never references this adapter or any Gamification project
/// directly. This adapter lives in <c>Gamification.Infrastructure</c> and is registered in
/// <c>AddGamificationInfrastructure</c>.</para>
///
/// <para>OQ-1 (brief confirmed): Gamification persists an <c>XpAward</c> append-only ledger.
/// Each row carries <c>OccurredAtUtc</c> (the causally-correct event timestamp, NOT the DB insert time).
/// XP-earned-in-window is therefore derivable by summing <c>XpAward.XpAmount</c> where
/// <c>OccurredAtUtc ∈ [fromUtc, toUtc)</c>.</para>
///
/// <para>Missions-completed: derived from <c>StudentMission.CompletedAtUtc</c> (set when status
/// transitions to <see cref="MissionStatus.Completed"/>).</para>
///
/// <para>All results are sentinel-safe: an empty window returns zeroed stats — never null, never throws.</para>
/// </summary>
public sealed class PlatformEngagementQueryAdapter : IPlatformEngagementQuery
{
    private readonly GamificationDbContext _db;

    public PlatformEngagementQueryAdapter(GamificationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<PlatformEngagementStats> GetPlatformAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        // ── Missions completed in window ──────────────────────────────────────────────────────────
        var missionsCompleted = await _db.StudentMissions
            .AsNoTracking()
            .CountAsync(m => m.Status == MissionStatus.Completed
                          && m.CompletedAtUtc.HasValue
                          && m.CompletedAtUtc.Value >= fromUtc
                          && m.CompletedAtUtc.Value < toUtc,
                         ct);

        // ── XP earned in window (from append-only ledger) ────────────────────────────────────────
        // Sum XpAmount for all awards whose causally-correct event timestamp falls in the window.
        // Cast to long in the projection to avoid int overflow on large platforms.
        var xpEarned = await _db.XpAwards
            .AsNoTracking()
            .Where(a => a.OccurredAtUtc >= fromUtc && a.OccurredAtUtc < toUtc)
            .SumAsync(a => (long)a.XpAmount, ct);

        return new PlatformEngagementStats(
            MissionsCompleted: missionsCompleted,
            XpEarnedInWindow:  xpEarned);
    }
}
