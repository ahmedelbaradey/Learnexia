namespace Learnexia.Shared.Contracts.Identity;

/// <summary>
/// Published by the Identity module after an admin successfully reactivates a previously
/// suspended user account (P7-07 <c>ReactivateAccountCommand</c>). Post-commit, best-effort.
///
/// Consumers:
///   Gamification module — unfreeze the student's active streak/league on reactivation.
///   Parent module — update family linkage view.
///
/// Carries only opaque identifiers; no PII in the payload.
/// The reactivation reason belongs in the (separate) audit event, not here — Finding #10.
/// </summary>
public sealed record AccountReactivatedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int UserId,
    int ReactivatedByAdminUserId) : IIntegrationEvent;
