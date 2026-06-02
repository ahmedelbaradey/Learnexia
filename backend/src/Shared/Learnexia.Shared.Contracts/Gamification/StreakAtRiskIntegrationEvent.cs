namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Published by the Gamification <c>StreakAtRiskJob</c> (cron "0 18 * * *" UTC) when a student
/// has an active streak but has not yet completed any qualifying activity today.
/// Consumed by Notifications to dispatch a StreakAtRisk nudge ("Your X-day streak ends in 6 hours
/// — one lesson keeps it alive") without referencing Gamification.Domain (module isolation, rule 1).
///
/// <para><c>LastActivityDateUtc</c> is the student's last recorded activity date as a nullable
/// <see cref="DateOnly"/> so consumers can use it for dedupe-key and analytics without
/// wall-clock sensitivity.</para>
///
/// Payload carries opaque int IDs only — NO PII.
/// Mirrors <see cref="Learning.LessonCompletedIntegrationEvent"/> shape (FR-GM-8 / P4-09).
/// </summary>
public sealed record StreakAtRiskIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int CurrentStreak,
    DateOnly? LastActivityDateUtc) : IIntegrationEvent;
