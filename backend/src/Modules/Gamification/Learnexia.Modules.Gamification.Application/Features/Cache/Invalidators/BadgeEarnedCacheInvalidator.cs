using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Application.Features.Cache.Invalidators;

/// <summary>
/// Cache invalidator for <see cref="BadgeEarnedDomainEvent"/> — P4-10 Batch 2.
///
/// On every badge award (post-commit per ADR 0002 §2):
///   DEL <c>gamification:student:{id}:badges:3</c> — the dashboard call hard-codes <c>Take(3)</c>,
///   so only the <c>:3</c> variant is cached. Forcing a miss here ensures the next dashboard fetch
///   returns the updated badge list including the newly awarded badge.
///
/// Fail-soft: the outer try/catch ensures no exception propagates (ADR 0002 §3).
/// </summary>
internal sealed class BadgeEarnedCacheInvalidator : INotificationHandler<BadgeEarnedDomainEvent>
{
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _opts;
    private readonly ILoggerManager _logger;

    public BadgeEarnedCacheInvalidator(
        IGamificationCache cache,
        IOptions<GamificationCacheOptions> opts,
        ILoggerManager logger)
    {
        _cache  = cache;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task Handle(BadgeEarnedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            if (!_opts.Enabled) return;

            // Only the dashboard-variant key (recentTake = 3) is cached — per Batch 1-A design.
            await _cache.DeleteAsync(
                GamificationCacheKeys.Badges(notification.StudentId, take: 3), ct);
        }
        catch (Exception)
        {
            // Fail-soft: Postgres remains authoritative. Do NOT log ex.Message (F-09).
            _logger.LogWarn(
                "P4-10: BadgeEarnedCacheInvalidator failed — Postgres is authoritative, nightly rebuild will reconcile.");
        }
    }
}
