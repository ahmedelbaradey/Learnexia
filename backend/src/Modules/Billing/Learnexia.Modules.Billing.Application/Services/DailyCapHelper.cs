using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Billing.Application.Services;

/// <summary>
/// Pure, stateless helper that computes the child's local "today" date string (<c>yyyy-MM-dd</c>)
/// from a timezone id + the platform clock.
///
/// <para>Mirrors <c>StreakDayCalculator</c> in the Gamification module: no I/O, fully deterministic,
/// unit-testable by passing a fixed <see cref="ISystemClock"/>.</para>
///
/// <para>Used by:
/// <list type="bullet">
///   <item><c>SpendCreditCommandHandler</c> — lazy daily-usage reset inside the atomic debit txn.</item>
///   <item><c>EnergyStatusQueryHandler</c> — lazy daily-usage reset before computing the status DTO.</item>
/// </list>
/// </para>
/// </summary>
public static class DailyCapHelper
{
    /// <summary>
    /// Returns the child's local "today" as a <c>yyyy-MM-dd</c> string.
    /// Falls back to UTC if <paramref name="childTimeZoneId"/> is absent or invalid.
    /// </summary>
    public static string Today(string? childTimeZoneId, ISystemClock clock)
    {
        var tzId = string.IsNullOrWhiteSpace(childTimeZoneId) ? "UTC" : childTimeZoneId;

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch
        {
            tz = TimeZoneInfo.Utc;
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, tz);
        return localNow.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="storedDateLocal"/> is stale relative to the
    /// child's local "today" — i.e. the daily counter should be reset to zero.
    /// </summary>
    public static bool IsStale(string? storedDateLocal, string? childTimeZoneId, ISystemClock clock)
        => storedDateLocal != Today(childTimeZoneId, clock);
}
