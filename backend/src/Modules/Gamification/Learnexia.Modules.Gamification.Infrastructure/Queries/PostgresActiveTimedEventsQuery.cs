using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Contracts.Gamification;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries;

/// <summary>
/// Postgres implementation of <see cref="IActiveTimedEventsQuery"/>. Wrapped by
/// <c>CachedActiveTimedEventsQuery</c> (P4-11-B1-B decorator) at DI composition root — registered
/// directly as concrete type, not as <see cref="IActiveTimedEventsQuery"/>.
///
/// Source-of-truth filter: <c>IsActive = true AND StartUtc &lt;= atUtc AND EndUtc &gt; atUtc</c>.
/// Using the clock comparison (not just <c>IsActive</c>) ensures the query is correct even if the
/// sweep job has not yet fired for a just-started event. <c>IsActive</c> acts as the sweep-job
/// idempotency latch — the query uses both predicates for defence-in-depth.
///
/// Read-only: <c>AsNoTracking</c>. Returns an empty list (never null) when no events are active.
/// </summary>
internal sealed class PostgresActiveTimedEventsQuery : IActiveTimedEventsQuery
{
    private readonly IGamificationRepository _repo;

    public PostgresActiveTimedEventsQuery(IGamificationRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<ActiveTimedEventSnapshot>> GetActiveAtAsync(
        DateTime atUtc, CancellationToken ct = default)
    {
        var rows = await _repo.GetActiveTimedEventsAsync(atUtc, ct);

        // Under Npgsql.EnableLegacyTimestampBehavior=true, reading a timestamptz column returns
        // DateTime with Kind=Local. Consumers (XpBoostCalculator, Learning dashboard) compare
        // StartUtc/EndUtc against a Kind=Utc parameter. A raw Kind=Local vs Kind=Utc comparison
        // in C# uses raw ticks without tz conversion — incorrect on machines with UTC offset != 0.
        // ToUniversalTime() converts Kind=Local → Kind=Utc using the local offset, making the
        // comparison correct regardless of the host timezone.
        return rows
            .Select(e => new ActiveTimedEventSnapshot(
                Id:         e.Id,
                Code:       e.Code,
                NameEn:     e.NameEn,
                NameAr:     e.NameAr,
                Multiplier: e.Multiplier,
                Scope:      (TimedEventScopeDto)(int)e.Scope,
                StartUtc:   e.StartUtc.ToUniversalTime(),
                EndUtc:     e.EndUtc.ToUniversalTime()))
            .ToList();
    }
}
