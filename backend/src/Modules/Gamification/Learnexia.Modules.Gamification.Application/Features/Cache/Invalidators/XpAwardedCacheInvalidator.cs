using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Modules.Gamification.Application.Leagues.Caching;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Application.Features.Cache.Invalidators;

/// <summary>
/// Cache invalidator for <see cref="XpAwardedDomainEvent"/> — P4-10 Batch 2.
///
/// On every XP award (post-commit per ADR 0002 §2):
///   1. DEL <c>gamification:student:{id}:xp</c> — forces the next read to repopulate from Postgres.
///   2. DEL <c>gamification:student:{id}:league:{weeklyKey}</c> — the league snapshot embeds
///      <c>WeeklyXp</c>; it must be invalidated so the stale snapshot is not served.
///   3. ZADD CH <c>gamification:league:{leagueId}:standings</c> — mirrors the post-commit
///      <c>WeeklyXp</c> into the sorted-set for O(log n) rank reads.
///
/// This is the post-commit authoritative sorted-set update. A pre-commit leading write also exists
/// in <see cref="IncrementLeagueXpCommandHandler"/> (Batch 1-B) — both calls are <c>ZADD CH</c>
/// which is idempotent at the score level, so two writes produce the same outcome. The post-commit
/// write here is authoritative because it reads the committed <c>WeeklyXp</c> from Postgres (the
/// Batch 1-B write uses the in-memory value before SaveChanges flushes). Both being present is safe
/// and provides a defence-in-depth guarantee.
///
/// Fail-soft: the outer try/catch ensures no exception propagates back to the upstream UoW or the
/// <c>IsolatedNotificationPublisher</c> dispatch chain (ADR 0002 §3).
///
/// Repository-free per §7 CONVENTIONS — reads are delegated to <see cref="IGamificationQueryService"/>.
/// </summary>
internal sealed class XpAwardedCacheInvalidator : INotificationHandler<XpAwardedDomainEvent>
{
    private readonly IGamificationCache _cache;
    private readonly ILeagueLeaderboard _leaderboard;
    private readonly IGamificationQueryService _queryService;
    private readonly ISystemClock _clock;
    private readonly GamificationCacheOptions _opts;
    private readonly ILoggerManager _logger;

    public XpAwardedCacheInvalidator(
        IGamificationCache cache,
        ILeagueLeaderboard leaderboard,
        IGamificationQueryService queryService,
        ISystemClock clock,
        IOptions<GamificationCacheOptions> opts,
        ILoggerManager logger)
    {
        _cache        = cache;
        _leaderboard  = leaderboard;
        _queryService = queryService;
        _clock        = clock;
        _opts         = opts.Value;
        _logger       = logger;
    }

    public async Task Handle(XpAwardedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            if (!_opts.Enabled) return;

            // 1. Invalidate the XP snapshot key.
            await _cache.DeleteAsync(
                GamificationCacheKeys.Xp(notification.StudentId), ct);

            // 2. Invalidate the league snapshot key (embeds WeeklyXp + CurrentRank).
            var nowUtc = _clock.UtcNow.ToUniversalTime();
            var (weeklyKey, _, _) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, nowUtc);
            await _cache.DeleteAsync(
                GamificationCacheKeys.LeagueSnapshot(notification.StudentId, weeklyKey), ct);

            // 3. Update the sorted-set with the authoritative post-commit WeeklyXp from Postgres.
            //    Reads profile → membership → ZADD CH. Both steps are optional (no membership = no-op).
            var profile = await _queryService.GetProfileEntityForCacheAsync(notification.StudentId, ct);
            if (profile is null) return;

            // Use event timestamp for period key (same logic as IncrementLeagueXpCommandHandler)
            // so a delayed dispatch at a week boundary credits the correct week's membership.
            var (eventWeeklyKey, _, _) = MissionPeriodCalculator.GetCurrentPeriod(
                MissionType.Weekly, notification.OccurredAtUtc);

            var membership = await _queryService.GetMembershipForCacheAsync(
                profile.Id, eventWeeklyKey, ct);
            if (membership is null)
            {
                // Student has no active membership for this week (lazy placement not yet triggered).
                // No sorted-set entry to update — log at debug level only.
                return;
            }

            await _leaderboard.UpsertMembershipAsync(
                leagueId:     membership.LeagueId,
                membershipId: membership.Id,
                weeklyXp:     membership.WeeklyXp,
                joinOrder:    membership.JoinOrder,
                ct:           ct);
        }
        catch (Exception)
        {
            // Fail-soft: Postgres remains authoritative. The nightly GamificationCacheRebuildJob
            // will reconcile any Redis drift. Do NOT log ex.Message (F-09 — no infra detail leaks).
            _logger.LogWarn(
                "P4-10: XpAwardedCacheInvalidator failed — Postgres is authoritative, nightly rebuild will reconcile.");
        }
    }
}
