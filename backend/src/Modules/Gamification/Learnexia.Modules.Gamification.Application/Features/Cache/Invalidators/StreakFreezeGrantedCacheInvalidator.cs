using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Application.Features.Cache.Invalidators;

/// <summary>
/// Cache invalidator for <see cref="StreakFreezeGrantedDomainEvent"/> — P4-11 security fix (Medium #2).
///
/// When a freeze is granted on a 7-day streak milestone (post-commit per ADR 0002 §2):
///   DEL <c>gamification:student:{id}:streak</c> — forces the next dashboard read to repopulate
///   from Postgres with the updated <c>FreezeBalance</c> value. Without this invalidator the
///   dashboard can serve a stale <c>FreezeBalance=0</c> for up to 60s after the grant.
///
/// Mirrors <see cref="StreakFreezeConsumedCacheInvalidator"/> exactly — same key, same fail-soft pattern.
///
/// Fail-soft: the outer try/catch ensures no exception propagates (ADR 0002 §3).
/// </summary>
internal sealed class StreakFreezeGrantedCacheInvalidator
    : INotificationHandler<StreakFreezeGrantedDomainEvent>
{
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _opts;
    private readonly ILoggerManager _logger;

    public StreakFreezeGrantedCacheInvalidator(
        IGamificationCache cache,
        IOptions<GamificationCacheOptions> opts,
        ILoggerManager logger)
    {
        _cache  = cache;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task Handle(StreakFreezeGrantedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            if (!_opts.Enabled) return;

            await _cache.DeleteAsync(
                GamificationCacheKeys.Streak(notification.StudentId), ct);
        }
        catch (Exception)
        {
            // Fail-soft: Postgres remains authoritative. Do NOT log ex.Message (F-09).
            _logger.LogWarn(
                "P4-11: StreakFreezeGrantedCacheInvalidator failed — Postgres is authoritative, nightly rebuild will reconcile.");
        }
    }
}
