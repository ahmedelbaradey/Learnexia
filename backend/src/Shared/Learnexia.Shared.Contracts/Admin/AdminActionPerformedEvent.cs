namespace Learnexia.Shared.Contracts.Admin;

/// <summary>
/// Published by every admin mutation handler in the P7-01..P7-05 curriculum-management wave,
/// immediately after a successful commit (best-effort — a publish failure must never fail the
/// primary mutation).
///
/// The consumer (P7-12 audit-log wave) is deferred. This event carries only opaque identifiers;
/// no PII. Mirrors the shape of <see cref="Identity.LearningLanguageChangedIntegrationEvent"/>.
///
/// Fields:
///   AdminUserId        — the authenticated admin who performed the action (from JWT).
///   Action             — human-readable action string, e.g. "Subject.Created", "Unit.Reordered"
///                        (uses the constants in <see cref="AdminActions"/>; not an enum so new
///                        actions can be added per story without touching Shared.Contracts).
///   TargetEntityType   — e.g. "Subject", "Unit".
///   TargetEntityId     — the primary key of the affected row (0 for multi-row batch actions).
///   Details            — optional summary string (e.g. "IsActive=false", "SequenceOrders updated: 3 items").
/// </summary>
public sealed record AdminActionPerformedEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    int AdminUserId,
    string Action,
    string TargetEntityType,
    int TargetEntityId,
    string? Details) : IIntegrationEvent
{
    DateTime IIntegrationEvent.OccurredOnUtc => OccurredAtUtc;
}
