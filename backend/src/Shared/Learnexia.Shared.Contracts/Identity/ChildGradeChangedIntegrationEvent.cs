namespace Learnexia.Shared.Contracts.Identity;

/// <summary>
/// Published by the Identity module after an admin overrides a child's grade
/// (P7-08 <c>OverrideChildGradeCommand</c>). Post-commit, best-effort.
///
/// The override is NON-DESTRUCTIVE: consumers must NOT delete XP, badges, streaks,
/// or mastery data. The event signals that the child's curriculum scope should be
/// re-evaluated for the new grade; existing learning history is preserved.
///
/// Consumers:
///   Learning module — check whether any persisted grade-scoped state needs rewriting.
///     If all reads key off <c>User.Grade</c> at query time the consumer is a no-op stub.
///
/// Field notes:
///   <c>ChildUserId</c> — the student whose grade changed (opaque id, no PII).
///   <c>OldGrade</c>    — the grade value before the override (1–6).
///   <c>NewGrade</c>    — the grade value after the override (1–6).
///   <c>ChangedByAdminUserId</c> — the acting admin (opaque id).
///   <c>OccurredOnUtc</c> — timestamp of the committed change.
/// </summary>
public sealed record ChildGradeChangedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int ChildUserId,
    int OldGrade,
    int NewGrade,
    int ChangedByAdminUserId) : IIntegrationEvent;
