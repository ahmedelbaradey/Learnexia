using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Identity.Application.Abstractions;

/// <summary>
/// Identity-scoped post-commit domain-events buffer (P7-07 Security High #1 fix).
///
/// Problem solved: <c>User : IdentityUser&lt;int&gt;</c> cannot extend <c>AggregateRoot</c>, so the
/// existing <c>UnitOfWorkBehavior</c> AggregateRoot scan is unreachable for Identity commands. This
/// buffer provides an equivalent seam: the handler calls <see cref="Add"/> BEFORE returning; the UoW
/// calls <see cref="Drain"/> AFTER a successful commit. On rollback the scoped buffer is GC'd with the
/// request scope — no side-effects fire (satisfies the "drain on success path only" invariant).
///
/// Registered as <c>Scoped</c> so one instance is shared by the handler and the UoW behavior within
/// the same request scope (same DI scope = same DbContext = same transaction boundary).
/// </summary>
public interface IIdentityDomainEventsBuffer
{
    /// <summary>Enqueue a domain event for post-commit dispatch.</summary>
    void Add(IDomainEvent domainEvent);

    /// <summary>
    /// Returns all enqueued events and clears the buffer in one atomic step.
    /// Calling this a second time within the same scope returns an empty list.
    /// </summary>
    IReadOnlyList<IDomainEvent> Drain();
}
