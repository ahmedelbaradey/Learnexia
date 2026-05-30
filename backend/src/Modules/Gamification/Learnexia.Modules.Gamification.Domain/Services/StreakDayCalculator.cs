using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Gamification.Domain.Services;

/// <summary>
/// Pure static date-bucket helper for the streak engine (FR-GM-2).
/// All streak-day classification logic lives here — no DI, no DB, fully unit-testable.
/// Mirrors the shape of <see cref="LevelCurve"/> and <c>LearningPathEngine</c>.
/// </summary>
public static class StreakDayCalculator
{
    /// <summary>
    /// Classifies the relationship between a student's last activity date and today, driving
    /// the streak-advance/reset decision in <c>AdvanceStreakCommandHandler</c>.
    /// </summary>
    public enum Transition
    {
        /// <summary>Same calendar day as last activity — do nothing.</summary>
        NoOp,

        /// <summary>No prior activity — this is the student's first ever qualifying lesson.</summary>
        FirstActivity,

        /// <summary>Activity is exactly one day after last activity — streak continues.</summary>
        Advance,

        /// <summary>Activity is more than one day after last activity — streak breaks and restarts.</summary>
        Reset,

        /// <summary>
        /// The activity date is earlier than the stored last-activity date — out-of-order / stale event.
        /// The caller should log a warning and return Success without writing streak state.
        /// </summary>
        OutOfOrder,
    }

    /// <summary>
    /// Classifies how <paramref name="today"/> relates to <paramref name="lastActivityDateUtc"/>.
    /// </summary>
    /// <param name="lastActivityDateUtc">
    /// The <c>LastActivityDateUtc</c> stored on the student's <c>StudentXpProfile</c>.
    /// <c>null</c> means the student has never had a qualifying activity.
    /// </param>
    /// <param name="today">The current UTC calendar day (or configured-timezone day).</param>
    /// <returns>The appropriate <see cref="Transition"/>.</returns>
    public static Transition Classify(DateOnly? lastActivityDateUtc, DateOnly today)
    {
        if (lastActivityDateUtc is null)
            return Transition.FirstActivity;

        var last = lastActivityDateUtc.Value;

        if (last == today)
            return Transition.NoOp;

        if (last == today.AddDays(-1))
            return Transition.Advance;

        if (last < today.AddDays(-1))
            return Transition.Reset;

        // last > today — out-of-order / stale event (e.g. delayed re-delivery, clock skew).
        // Return OutOfOrder so the handler can log and soft-skip without a try/catch.
        return Transition.OutOfOrder;
    }

    /// <summary>
    /// Converts a UTC <see cref="DateTime"/> to a <see cref="DateOnly"/> in the configured timezone.
    /// Use <c>"UTC"</c> as the default (Q2 decision — per-user timezone deferred to a future story).
    /// </summary>
    /// <param name="utc">The UTC instant to convert.</param>
    /// <param name="timeZoneId">A Windows/IANA timezone ID, e.g. <c>"UTC"</c> or <c>"Africa/Cairo"</c>.</param>
    public static DateOnly DayOf(DateTime utc, string timeZoneId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        return DateOnly.FromDateTime(local);
    }

    /// <summary>
    /// Returns today's date in the configured timezone via the injected <see cref="ISystemClock"/>.
    /// </summary>
    /// <param name="timeZoneId">A Windows/IANA timezone ID.</param>
    /// <param name="clock">The clock seam — never use <see cref="DateTime.UtcNow"/> directly in testable code.</param>
    public static DateOnly Today(string timeZoneId, ISystemClock clock)
        => DayOf(clock.UtcNow, timeZoneId);
}
