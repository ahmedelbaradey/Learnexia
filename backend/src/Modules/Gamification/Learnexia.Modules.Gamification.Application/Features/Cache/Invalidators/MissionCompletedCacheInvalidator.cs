using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Application.Features.Cache.Invalidators;

/// <summary>
/// Cache invalidator for <see cref="MissionCompletedDomainEvent"/> — P4-10 Batch 2.
///
/// On every mission completion (post-commit per ADR 0002 §2):
///   DEL <c>gamification:student:{id}:missions:{dailyKey}</c>
///   DEL <c>gamification:student:{id}:missions:{weeklyKey}</c>
///
/// Both the daily and weekly mission cache keys are removed (Q16 recommendation). The daily key
/// is the primary key used by <c>CachedStudentMissionsQuery</c>; the weekly key is removed
/// defensively to cover any future weekly-cadence cache entries. A Redis DEL on a non-existent
/// key is a no-op.
///
/// The period keys are derived from <see cref="MissionCompletedDomainEvent.CompletedAtUtc"/> (the
/// event timestamp) rather than wall-clock, preserving correctness at day/week boundaries.
///
/// Fail-soft: the outer try/catch ensures no exception propagates (ADR 0002 §3).
/// </summary>
internal sealed class MissionCompletedCacheInvalidator : INotificationHandler<MissionCompletedDomainEvent>
{
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _opts;
    private readonly ILoggerManager _logger;

    public MissionCompletedCacheInvalidator(
        IGamificationCache cache,
        IOptions<GamificationCacheOptions> opts,
        ILoggerManager logger)
    {
        _cache  = cache;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task Handle(MissionCompletedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            if (!_opts.Enabled) return;

            // Derive period keys from event timestamp (not wall-clock) to correctly target the
            // cache key that was populated when the mission was first read this period.
            var (dailyKey, _, _)  = MissionPeriodCalculator.GetCurrentPeriod(
                MissionType.Daily,  notification.CompletedAtUtc);
            var (weeklyKey, _, _) = MissionPeriodCalculator.GetCurrentPeriod(
                MissionType.Weekly, notification.CompletedAtUtc);

            // Remove both keys: the decorator only writes daily keys today, but we also clear
            // the weekly key defensively (Q16). DEL on a missing key is a no-op in Redis.
            await _cache.DeleteAsync(
                GamificationCacheKeys.Missions(notification.StudentId, dailyKey), ct);
            await _cache.DeleteAsync(
                GamificationCacheKeys.Missions(notification.StudentId, weeklyKey), ct);
        }
        catch (Exception)
        {
            // Fail-soft: Postgres remains authoritative. Do NOT log ex.Message (F-09).
            _logger.LogWarn(
                "P4-10: MissionCompletedCacheInvalidator failed — Postgres is authoritative, nightly rebuild will reconcile.");
        }
    }
}
