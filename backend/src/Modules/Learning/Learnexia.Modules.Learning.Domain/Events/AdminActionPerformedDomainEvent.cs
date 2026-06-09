using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Learning.Domain.Events;

/// <summary>
/// Raised by Learning admin write handlers on the mutated <see cref="Entities.AggregateRoot"/>
/// BEFORE returning from the handler body — but dispatched strictly AFTER the module's
/// <c>UnitOfWorkBehavior</c> calls <c>CommitAsync</c> (ADR 0002 §2 / P7-12 post-commit fix).
///
/// Carries the same payload as <c>Shared.Contracts.Admin.AdminActionPerformedEvent</c> so the
/// relay handler (<c>AdminActionPerformedDomainEventRelayHandler</c>) can reproject it 1-to-1
/// into the integration event without additional lookups.
///
/// Module-isolation rule: this type lives in <c>Learning.Domain</c> (references Shared.Kernel only);
/// the integration event (<c>Shared.Contracts.Admin.AdminActionPerformedEvent</c>) is published by
/// the relay handler in <c>Learning.Application</c>.
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
