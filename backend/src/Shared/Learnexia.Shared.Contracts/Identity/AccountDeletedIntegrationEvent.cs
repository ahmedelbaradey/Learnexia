namespace Learnexia.Shared.Contracts.Identity;

/// <summary>
/// Published by the Identity module after an admin successfully soft-deletes a user account
/// (P7-07 <c>DeleteAccountCommand</c>). This is a terminal transition — the account cannot
/// be reactivated after deletion. Post-commit, best-effort.
///
/// <c>AffectedChildIds</c>: populated when the delete was a cascade (parent deletion with
/// <c>CascadeChildren=true</c>); empty for non-cascade deletes. Lets downstream consumers
/// (Gamification, Learning) clean up per-child data in one event rather than N separate events.
///
/// Consumers:
///   Gamification module — clean up XP/streak/league for deleted accounts.
///   Parent module — update family linkage view.
///   Learning module — thin no-op stub for now (data-purge semantics deferred per Q-B1).
///
/// Carries only opaque identifiers; no PII in the payload.
/// The deletion reason belongs in the (separate) audit event, not here — Finding #10.
/// </summary>
public sealed record AccountDeletedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int UserId,
    int DeletedByAdminUserId,
    IReadOnlyList<int> AffectedChildIds) : IIntegrationEvent;
