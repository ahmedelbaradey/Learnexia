namespace Learnexia.Shared.Contracts.Identity;

/// <summary>
/// Published by the Identity module after an admin successfully suspends a user account
/// (P7-07 <c>SuspendAccountCommand</c>). Post-commit, best-effort: publish failure never
/// rolls back the primary status change.
///
/// Consumers:
///   Gamification module — freeze the student's active streak/league on suspension.
///   Parent module — update family linkage view.
///   Learning module — thin no-op stub for now (data-purge semantics deferred per Q-B1).
///
/// Carries only opaque identifiers; no PII in the payload.
/// The suspension reason belongs in the (separate) audit event, not here — Finding #10.
/// </summary>
public sealed record AccountSuspendedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int UserId,
    int SuspendedByAdminUserId) : IIntegrationEvent;
