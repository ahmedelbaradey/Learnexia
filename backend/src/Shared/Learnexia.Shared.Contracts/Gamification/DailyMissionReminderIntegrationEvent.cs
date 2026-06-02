namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Published by the Gamification <c>StreakAtRiskJob</c> (second pass, cron "0 18 * * *" UTC) when a
/// student has an active daily mission that expires within the configured lookahead window
/// (default: 6 hours). At most one event is published per student per run regardless of how many
/// missions are pending.
/// Consumed by Notifications to dispatch a DailyMissionReminder nudge ("Your daily mission ends
/// soon — finish it for +30 XP") without referencing Gamification.Domain (module isolation, rule 1).
///
/// Payload carries opaque int IDs only — NO PII.
/// Mirrors <see cref="Learning.LessonCompletedIntegrationEvent"/> shape (FR-GM-8 / P4-09).
/// </summary>
public sealed record DailyMissionReminderIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int MinutesRemaining) : IIntegrationEvent;
