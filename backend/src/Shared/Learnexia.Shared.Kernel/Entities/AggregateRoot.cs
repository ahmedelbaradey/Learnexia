using System.ComponentModel.DataAnnotations.Schema;
using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Shared.Kernel.Entities;

/// <summary>
/// Event-capable audited aggregate base for NEW modules (Learning, Gamification, Curriculum, ...).
/// Layers the <see cref="FullAuditedEntity"/> audit fields (Id, CreatedAt/By, UpdatedAt/By, DeletedAt/By, IsDeleted)
/// with the <c>Entity&lt;TId&gt;</c> domain-event pattern so an aggregate can BOTH be audited AND raise
/// <see cref="IDomainEvent"/>s that the after-commit dispatcher collects (ADR 0002).
///
/// Do NOT retrofit Catalog or <see cref="FullAuditedEntity"/> — this base exists only for new modules.
/// <see cref="DomainEvents"/> is <c>[NotMapped]</c>; domain events are transient and never persisted by EF.
/// </summary>
public abstract class AggregateRoot : FullAuditedEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
