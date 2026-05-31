namespace Learnexia.Modules.Gamification.Domain.Services;

/// <summary>
/// Pure static helper — computes how many hearts have regenerated since the last refill tick
/// (P4-04 lazy pull-model refill, D5 / D12).
///
/// Rules:
///   - If <paramref name="currentHearts"/> &gt;= <paramref name="cap"/> → clock idle; return at-cap with null timestamp.
///   - If <paramref name="lastRefillAt"/> is null → clock not running; no refill.
///   - Otherwise: tick count = floor(elapsed minutes / interval). Each tick restores 1 heart up to cap.
///     Advance <see cref="RefillResult.NewLastRefillAt"/> by exactly the consumed ticks (NOT to now),
///     preserving the partial-interval remainder so a 31-min gap awards exactly 1 heart
///     and the next refill is due 29 min later — not 30 min from now.
///   - When hearts reach cap after refill: <see cref="RefillResult.NewLastRefillAt"/> = null (clock idle).
///
/// Zero side-effects. All parameters are value types. Fully unit-testable.
/// Mirrors the shape of <see cref="StreakDayCalculator"/> (P4-03).
/// </summary>
public static class LazyHeartRefiller
{
    /// <summary>Result returned by <see cref="Compute"/>.</summary>
    /// <param name="NewHearts">Hearts count after applying the refill (clamped to cap).</param>
    /// <param name="NewLastRefillAt">
    /// Updated refill clock. Null when hearts are at cap (clock idles). Otherwise advanced
    /// by exactly the number of consumed ticks × interval to preserve the partial-tick remainder.
    /// </param>
    /// <param name="Regenerated">How many hearts were regenerated (0 when nothing changed).</param>
    public readonly record struct RefillResult(int NewHearts, DateTime? NewLastRefillAt, int Regenerated);

    /// <summary>
    /// Compute lazy time-based refill. Pure static (no DI, no DB).
    /// </summary>
    /// <param name="currentHearts">Current Hearts value (0..cap).</param>
    /// <param name="cap">Max hearts (e.g., 5).</param>
    /// <param name="lastRefillAt">Last refill clock timestamp; null means clock is idle (full hearts or never lost).</param>
    /// <param name="nowUtc">Current UTC time (from ISystemClock or the event's OccurredAtUtc).</param>
    /// <param name="intervalMin">Refill interval in minutes (e.g., 30).</param>
    public static RefillResult Compute(
        int currentHearts,
        int cap,
        DateTime? lastRefillAt,
        DateTime nowUtc,
        int intervalMin)
    {
        // Already at or above cap — clear the clock and return unchanged.
        if (currentHearts >= cap)
            return new RefillResult(cap, null, 0);

        // Clock not running — student may be in an impossible state; return unchanged (no refill).
        if (lastRefillAt is null)
            return new RefillResult(currentHearts, null, 0);

        // Normalize both timestamps to UTC before computing elapsed time.
        // Npgsql's legacy timestamp behavior (EnableLegacyTimestampBehavior=true) returns
        // `timestamp with time zone` columns as DateTime with Kind=Local, while callers pass
        // nowUtc as Kind=Utc.  A mixed-Kind subtraction in .NET does NOT auto-convert —
        // it computes on raw Ticks, producing a negative elapsed time on UTC+ machines.
        // ToUniversalTime() normalises both to Utc Kind so the difference is always correct.
        double elapsedMinutes = (nowUtc.ToUniversalTime() - lastRefillAt.Value.ToUniversalTime()).TotalMinutes;
        int ticksToRegen = (int)(elapsedMinutes / intervalMin);

        // No full interval has elapsed (or clock skew produced a negative value) — no change.
        if (ticksToRegen <= 0)
            return new RefillResult(currentHearts, lastRefillAt, 0);

        // Restore hearts up to cap, clamped.
        int regenerated = Math.Min(ticksToRegen, cap - currentHearts);
        int newHearts = currentHearts + regenerated;

        // Advance the clock by exactly the consumed ticks (preserves partial-interval remainder).
        // If now at cap, clear the clock entirely — no pending refill.
        DateTime? newLastRefillAt = newHearts >= cap
            ? null
            : lastRefillAt.Value.AddMinutes(regenerated * intervalMin);

        return new RefillResult(newHearts, newLastRefillAt, regenerated);
    }
}
