using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Application.Features.Cache.Invalidators;

/// <summary>
/// Cache invalidator for <see cref="StudentLeveledUpDomainEvent"/> — P4-10 Batch 2.
///
/// On every level transition (post-commit per ADR 0002 §2):
///   DEL <c>gamification:student:{id}:xp</c> — the XP snapshot embeds the student's current level;
///   invalidating it forces the next read to repopulate from Postgres with the new level.
///
/// This is idempotent with <see cref="XpAwardedCacheInvalidator"/>: both delete the same key.
/// The double-delete is a no-op on Redis (idempotent) and the next read repopulates correctly
/// regardless of which handler fires first.
///
/// Fail-soft: the outer try/catch ensures no exception propagates (ADR 0002 §3).
/// </summary>
internal sealed class StudentLeveledUpCacheInvalidator : INotificationHandler<StudentLeveledUpDomainEvent>
{
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _opts;
    private readonly ILoggerManager _logger;

    public StudentLeveledUpCacheInvalidator(
        IGamificationCache cache,
        IOptions<GamificationCacheOptions> opts,
        ILoggerManager logger)
    {
        _cache  = cache;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task Handle(StudentLeveledUpDomainEvent notification, CancellationToken ct)
    {
        try
        {
            if (!_opts.Enabled) return;

            await _cache.DeleteAsync(
                GamificationCacheKeys.Xp(notification.StudentId), ct);
        }
        catch (Exception)
        {
            // Fail-soft: Postgres remains authoritative. Do NOT log ex.Message (F-09).
            _logger.LogWarn(
                "P4-10: StudentLeveledUpCacheInvalidator failed — Postgres is authoritative, nightly rebuild will reconcile.");
        }
    }
}
