using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Identity.Application.Events;

/// <summary>
/// Raised by <see cref="Features.Users.Commands.DeleteAccount.DeleteAccountCommandHandler"/> after all
/// soft-delete mutations are staged (but before the UoW commits). The <c>UnitOfWorkBehavior</c> drains
/// the <see cref="Abstractions.IIdentityDomainEventsBuffer"/> and dispatches this event via
/// <c>IDomainEventDispatcher</c> ONLY on a successful commit — never on rollback.
///
/// P7-07 post-commit buffer fix (Security High #1 / Low #4):
///   Before this fix, Redis session revocation and integration-event publishes ran INSIDE the handler
///   body, before the UoW's CommitAsync. A commit failure would still revoke live sessions, fire
///   downstream delete consumers, and write a phantom audit record. This event defers those side-effects
///   to the post-commit dispatch path so they fire exactly once on success and are discarded on rollback
///   (the scoped buffer is GC'd when the request scope ends).
///
/// Payload carries everything the handler needs:
///   UserId             — the primary account being deleted.
///   DeletedByAdminUserId — the acting admin (for the audit event).
///   AffectedChildIds   — child accounts soft-deleted in the same transaction (cascade case).
/// </summary>
public sealed record AccountDeletedDomainEvent(
    int UserId,
    int DeletedByAdminUserId,
    IReadOnlyList<int> AffectedChildIds,
    string AuditDetails) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
