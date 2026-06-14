using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Identity.Infrastructure.Events;

/// <summary>
/// Scoped, in-memory implementation of <see cref="IIdentityDomainEventsBuffer"/>.
///
/// One instance lives per request scope (same DI scope as the handler and the UoW DbContext).
/// The handler enqueues events; the UoW drains them after commit. If the transaction rolls back
/// the instance is disposed with the scope — <see cref="Drain"/> is never called, so nothing fires.
///
/// Not thread-safe; only one request processes this scope at a time (ASP.NET Core request pipeline
/// is single-threaded per request scope).
/// </summary>
internal sealed class IdentityDomainEventsBuffer : IIdentityDomainEventsBuffer
{
    private readonly List<IDomainEvent> _events = new();

    public void Add(IDomainEvent domainEvent) => _events.Add(domainEvent);

    public IReadOnlyList<IDomainEvent> Drain()
    {
        var snapshot = _events.ToList();
        _events.Clear();
        return snapshot;
    }
}
