namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Published by the Gamification <c>LapseWinBackJob</c> (cron "0 12 * * 0" UTC) on the exact
/// day a student crosses the lapse threshold (default: 3 days with no activity).
/// One-shot semantics: fired once per lapse transition day, not every day until return.
/// Consumed by Notifications to dispatch a LapseWinBack nudge ("We miss you — your hearts are
/// full and missions await") without referencing Gamification.Domain (module isolation, rule 1).
///
/// Payload carries opaque int IDs only — NO PII.
/// Mirrors <see cref="Learning.LessonCompletedIntegrationEvent"/> shape (FR-GM-8 / P4-09).
/// </summary>
public sealed record LapseWinBackIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int DaysSinceLastActivity) : IIntegrationEvent;
