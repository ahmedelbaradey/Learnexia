using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Application.Features.Cache.Invalidators;

/// <summary>
/// Cache invalidator for <see cref="StreakFreezeConsumedDomainEvent"/> — P4-11 Batch 1-A.
///
/// When a freeze is consumed to preserve a student's streak (post-commit per ADR 0002 §2):
///   DEL <c>gamification:student:{id}:streak</c> — forces the next dashboard read to repopulate
///   from Postgres with the updated <c>FreezeBalance</c> value. The streak number itself is
///   unchanged by a consume, but the balance field is in the same snapshot — invalidation
///   ensures the cached record does not serve a stale freeze count.
///
/// Fail-soft: the outer try/catch ensures no exception propagates (ADR 0002 §3).
/// </summary>
internal sealed class StreakFreezeConsumedCacheInvalidator
    : INotificationHandler<StreakFreezeConsumedDomainEvent>
{
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _opts;
    private readonly ILoggerManager _logger;

    public StreakFreezeConsumedCacheInvalidator(
        IGamificationCache cache,
        IOptions<GamificationCacheOptions> opts,
        ILoggerManager logger)
    {
        _cache  = cache;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task Handle(StreakFreezeConsumedDomainEvent notification, CancellationToken ct)
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
                "P4-11: StreakFreezeConsumedCacheInvalidator failed — Postgres is authoritative, nightly rebuild will reconcile.");
        }
    }
}
