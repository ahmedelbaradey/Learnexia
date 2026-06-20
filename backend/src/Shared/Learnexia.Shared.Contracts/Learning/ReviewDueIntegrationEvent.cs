namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Raised once per student per daily spaced-repetition sweep when ≥1 skill is due for review.
/// Emitted by <c>SpacedRepetitionSweepJob</c> (Learning.Infrastructure) after the per-row SR
/// column writes have committed.
///
/// <para>Consumed by:</para>
/// <list type="bullet">
///   <item>P9-09 (Notifications) — emits a "وقت مراجعة سريعة" push nudge governed by the
///         P9-07 global push budget + per-(child, category, day) Redis dedup + arbitration.
///         The consumer owns cadence policy; this event fires once per student per sweep.</item>
///   <item>P4-06 (Missions) — future consumer, not in scope for this slice.</item>
/// </list>
///
/// <para>Per-student digest: one event per sweep carries <see cref="DueCount"/> (total due skills)
/// and <see cref="TopSkills"/> (cap 3, most-urgent first). <see cref="TopSkills"/>[0] is the
/// headline skill for nudge copy (name + estimated minutes).</para>
///
/// <para>Payload carries opaque int IDs + curriculum display strings only — <b>NO PII</b>
/// (no student name, email, DOB, or any child-data fields). <see cref="DueSkillSnapshot.SkillName"/>
/// is Learning-owned curriculum content, not identity data.</para>
///
/// Mirrors the shape of <see cref="Gamification.BadgeEarnedIntegrationEvent"/> (opaque ids only,
/// one digest per meaningful event, post-commit publish, fail-soft).
/// </summary>
public sealed record ReviewDueIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int DueCount,
    IReadOnlyList<DueSkillSnapshot> TopSkills) : IIntegrationEvent;

/// <summary>
/// One due skill in the <see cref="ReviewDueIntegrationEvent"/> digest.
/// <para><see cref="SkillName"/> is the single <c>Skill.Name</c> from the Learning curriculum
/// (not bilingual — Learning curriculum carries a single <c>Name</c> field; the notification
/// consumer localises the surrounding <em>template</em> and injects this as a placeholder).</para>
/// <para><see cref="EstimatedTimeMinutes"/> comes from <c>Skill.EstimatedTimeMinutes</c> —
/// enables copy such as "~5 دقائق" without a back-reference to Learning.</para>
/// </summary>
public sealed record DueSkillSnapshot(
    int SkillId,
    string SkillName,
    int EstimatedTimeMinutes);
