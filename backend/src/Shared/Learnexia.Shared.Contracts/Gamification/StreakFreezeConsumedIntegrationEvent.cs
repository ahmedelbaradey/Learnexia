namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Published when a student's streak freeze is consumed to preserve their streak.
/// Re-published synthetically by <c>StreakFreezeConsumedRepublisher</c> (P4-11) so the
/// Notifications module can dispatch a "freeze saved your streak — keep going!" nudge
/// without referencing Gamification.Domain (module isolation rule 1).
///
/// <para><c>RemainingFreezeBalance</c> is the count AFTER consumption (0 or 1 remaining).
/// FE and Notifications use this to decide whether to show a "low freeze" warning.</para>
///
/// Payload carries opaque int IDs only — NO PII.
/// Mirrors <see cref="StreakBrokenIntegrationEvent"/> shape (P4-09 / P4-11).
/// </summary>
public sealed record StreakFreezeConsumedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int CurrentStreak,
    int RemainingFreezeBalance) : IIntegrationEvent;
