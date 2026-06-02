using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Application.Features.Cache.Invalidators;

/// <summary>
/// Cache invalidator for <see cref="StreakAdvancedDomainEvent"/> — P4-10 Batch 2.
///
/// On every streak advance (post-commit per ADR 0002 §2):
///   DEL <c>gamification:student:{id}:streak</c> — forces the next read to repopulate from Postgres
///   with the updated <c>CurrentStreak</c> and <c>LongestStreak</c> values.
///
/// Fail-soft: the outer try/catch ensures no exception propagates (ADR 0002 §3).
/// </summary>
internal sealed class StreakAdvancedCacheInvalidator : INotificationHandler<StreakAdvancedDomainEvent>
{
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _opts;
    private readonly ILoggerManager _logger;

    public StreakAdvancedCacheInvalidator(
        IGamificationCache cache,
        IOptions<GamificationCacheOptions> opts,
        ILoggerManager logger)
    {
        _cache  = cache;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task Handle(StreakAdvancedDomainEvent notification, CancellationToken ct)
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
                "P4-10: StreakAdvancedCacheInvalidator failed — Postgres is authoritative, nightly rebuild will reconcile.");
        }
    }
}
