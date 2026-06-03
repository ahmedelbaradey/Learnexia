using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries.Cached;

/// <summary>
/// Cache decorator for <see cref="IActiveTimedEventsQuery"/> — P4-11-B1-B.
/// Wraps <see cref="PostgresActiveTimedEventsQuery"/> with a Redis read-through layer.
/// Registered as the resolved <see cref="IActiveTimedEventsQuery"/> at DI composition root;
/// consumers (Learning dashboard, IXpBoostCalculator) see no change.
///
/// Cache key: <c>gamification:timed_events:active</c> — global (not per-student; events are global).
/// TTL: <c>GamificationCacheOptions.TimedEventsTtlSeconds</c> (default 30s).
///
/// Empty-list result IS cached (all-quiet state is the common case between events — caching it
/// avoids repeated Postgres queries during the typical no-event window). Null is never returned
/// by the inner query so the null-skip pattern from student-scoped decorators does not apply.
///
/// Invalidated by <c>TimedEventStartedCacheInvalidator</c> and <c>TimedEventEndedCacheInvalidator</c>.
/// </summary>
internal sealed class CachedActiveTimedEventsQuery : IActiveTimedEventsQuery
{
    private readonly IActiveTimedEventsQuery _inner;
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _opts;
    private readonly ILoggerManager _logger;

    public CachedActiveTimedEventsQuery(
        IActiveTimedEventsQuery inner,
        IGamificationCache cache,
        IOptions<GamificationCacheOptions> opts,
        ILoggerManager logger)
    {
        _inner  = inner;
        _cache  = cache;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ActiveTimedEventSnapshot>> GetActiveAtAsync(
        DateTime atUtc, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return await _inner.GetActiveAtAsync(atUtc, ct);

        var key = GamificationCacheKeys.ActiveTimedEvents();

        var cached = await _cache.TryGetAsync<List<ActiveTimedEventSnapshot>>(key, ct);
        if (cached is not null)
        {
            _logger.LogInfo("cache.gamification.timed_events.hit");
            return cached;
        }

        _logger.LogInfo("cache.gamification.timed_events.miss");
        var fresh = await _inner.GetActiveAtAsync(atUtc, ct);

        // Cache even an empty list — the no-event window is the common case.
        var asList = fresh as List<ActiveTimedEventSnapshot> ?? fresh.ToList();
        await _cache.SetAsync(
            key, asList, TimeSpan.FromSeconds(_opts.TimedEventsTtlSeconds), ct);

        return fresh;
    }
}
