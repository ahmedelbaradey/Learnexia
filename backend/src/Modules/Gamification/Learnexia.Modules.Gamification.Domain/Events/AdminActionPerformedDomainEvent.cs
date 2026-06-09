using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised by Gamification admin write handlers on the mutated <see cref="Entities.AggregateRoot"/>
/// BEFORE returning from the handler body — dispatched strictly AFTER the module's
/// <c>UnitOfWorkBehavior</c> calls <c>CommitAsync</c> (ADR 0002 §2 / P7-12 post-commit fix).
///
/// Mirrors <c>Learning.Domain.Events.AdminActionPerformedDomainEvent</c> exactly.
/// The relay handler (<c>AdminActionPerformedDomainEventRelayHandler</c>) reprojects it 1-to-1
/// into the <c>Shared.Contracts.Admin.AdminActionPerformedEvent</c> integration event.
/// </summary>
public sealed record AdminActionPerformedDomainEvent(
    int AdminUserId,
    string Action,
    string TargetEntityType,
    int TargetEntityId,
    string? Details) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
