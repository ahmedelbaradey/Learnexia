namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Raised when a student's streak is broken by the nightly <c>StreakSweepJob</c>.
/// Re-published synthetically by the job (capture-before-bulk pattern, P4-09 D6) so
/// Notifications can dispatch a StreakAtRisk nudge without referencing Gamification.Domain
/// (module isolation, rule 1).
///
/// <para><c>BrokenAtDate</c> is the calendar day (UTC) on which the streak was reset, carried as
/// <see cref="DateOnly"/> so consumers can use it as the dedupe key date without clock-skew
/// sensitivity.</para>
///
/// Payload carries opaque int IDs only — NO PII.
/// Mirrors <see cref="Learning.LessonCompletedIntegrationEvent"/> shape (FR-GM-8 / P4-09).
/// </summary>
public sealed record StreakBrokenIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int PreviousStreak,
    DateOnly BrokenAtDate) : IIntegrationEvent;
