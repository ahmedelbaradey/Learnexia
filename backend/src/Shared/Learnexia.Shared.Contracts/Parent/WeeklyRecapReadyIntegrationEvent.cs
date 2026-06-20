namespace Learnexia.Shared.Contracts.Parent;

/// <summary>
/// Raised by the Parent weekly-report job after upserting a child's weekly report;
/// consumed by P9-06 Notifications — WeeklyReport category, code WEEKLY_RECAP.
///
/// <para>Emitted once per active child per week from
/// <c>Parent.Infrastructure.Jobs.WeeklyReportJob</c> (Mondays 03:00 UTC), immediately
/// after the <c>WeeklyReport</c> row upsert succeeds. Zero-activity weeks
/// (<c>XpEarned == 0 &amp;&amp; SkillsImproved == 0</c>) are suppressed at the producer —
/// never-shaming per FR-GM-8.</para>
///
/// Payload carries opaque int ids + numeric summary scalars only — NO PII.
/// </summary>
public sealed record WeeklyRecapReadyIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int XpEarned,
    int SkillsImproved,
    DateTime WeekStartUtc) : global::Learnexia.Shared.Contracts.IIntegrationEvent;
