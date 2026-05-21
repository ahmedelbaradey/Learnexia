namespace Learnexia.Shared.Kernel.DomainEvents;

/// <summary>
/// Seam the <c>UnitOfWorkBehavior</c> calls AFTER a successful commit to dispatch the domain events
/// collected from tracked aggregates (ADR 0002 §2 + §5). Dispatch happens only after commit, never on
/// rollback.
///
/// Kept behind this abstraction so a transactional Outbox (Tier 2) can replace the default in-process
/// implementation later WITHOUT changing any caller.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>Dispatch each domain event to its registered handlers.</summary>
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
