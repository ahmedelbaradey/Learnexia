namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Cross-module read seam: Gamification implements; student dashboard + P9-12 (future consumer) inject.
/// Returns a student's current participation snapshot(s) for active timed events.
///
/// Registered Scoped in <c>AddGamificationInfrastructure()</c>.
/// Mirrors <see cref="IStudentMissionsQuery"/> shape — opaque ids, never-null sentinel.
/// </summary>
public interface IStudentTimedEventParticipationQuery
{
    /// <summary>
    /// Returns all current <see cref="TimedEventParticipationSnapshot"/> rows for
    /// <paramref name="studentId"/> whose event window is still active at the time of the call.
    /// Returns an empty list (never null) for brand-new students or when no active events exist.
    /// </summary>
    Task<IReadOnlyList<TimedEventParticipationSnapshot>> GetActiveByStudentIdAsync(
        int studentId,
        CancellationToken ct = default);
}

/// <summary>
/// Lightweight participation projection crossing the module boundary (P4-12).
/// Contains only the fields that consumers need — no navigation properties, no PII.
/// </summary>
/// <param name="TimedEventId">Opaque int FK to the <c>TimedEvent</c> catalog row.</param>
/// <param name="Code">Stable event code (e.g. "DOUBLE_XP_WEEKEND") — for deep-link by P9-12.</param>
/// <param name="Progress">Current progress count (in-window qualifying actions).</param>
/// <param name="Target">Denormalized target snapshot set at participation creation.</param>
/// <param name="Status">Participation lifecycle state.</param>
/// <param name="JoinedUtc">UTC timestamp when the student first joined this event.</param>
/// <param name="CompletedUtc">UTC when the participation was completed, or null if not yet completed.</param>
/// <param name="EventEndUtc">UTC when the event window closes.</param>
public sealed record TimedEventParticipationSnapshot(
    int TimedEventId,
    string Code,
    int Progress,
    int Target,
    TimedEventParticipationStatusDto Status,
    DateTime JoinedUtc,
    DateTime? CompletedUtc,
    DateTime EventEndUtc);

/// <summary>
/// Mirror of <c>Learnexia.Modules.Gamification.Domain.Enums.TimedEventParticipationStatus</c>
/// for cross-module use. Integer values MUST remain in sync with the Domain enum.
/// A compile-time assertion in the integration tests pins both to the same integers.
/// </summary>
public enum TimedEventParticipationStatusDto
{
    NotStarted = 0,
    InProgress = 1,
    Completed  = 2,
}
