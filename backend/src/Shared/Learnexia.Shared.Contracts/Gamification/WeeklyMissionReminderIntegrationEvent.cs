namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Published by the Gamification <c>StreakAtRiskJob</c> (third pass, daily cron "0 18 * * *" UTC)
/// when a student has an active WEEKLY mission expiring within the configured lookahead window
/// (default: 48 hours, <c>Gamification:Reengagement:WeeklyMissionReminderLookaheadHours</c>) and
/// still incomplete (Status = NotStarted or InProgress). At most one event is published per student
/// per run — when a student has multiple weekly missions in window, the most-urgent (earliest
/// <c>PeriodEndUtc</c>) is selected.
///
/// <para>Note: the job runs daily, but the per-(student, period) dedupe in the Notifications
/// consumer (<c>nudge-tier:{studentId}:WEEKLY_CHALLENGE:{periodKey}</c>, TTL ≈ lookahead + margin)
/// ensures at most one weekly reminder per student per weekly period, regardless of how many daily
/// runs fall within the 48-hour window.</para>
///
/// Consumed by Notifications → WeeklyChallenge nudge (P9-06).
/// Payload carries opaque int IDs and a period key only — NO PII.
/// Mirrors <see cref="DailyMissionReminderIntegrationEvent"/> (FR-GM-8 / P9-06).
/// </summary>
public sealed record WeeklyMissionReminderIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int Progress,             // current progress toward Target at scan time
    int Target,              // weekly mission target (denormalized from StudentMission)
    int MinutesRemaining,    // (most-urgent PeriodEndUtc - OccurredOnUtc).TotalMinutes — mission-relative
    string PeriodKey) : IIntegrationEvent;  // e.g. "W:2026-22" — stable per-period key for Option-A dedupe
