using MediatR;

namespace Learnexia.Shared.Kernel.DomainEvents;

/// <summary>
/// Default in-process <see cref="IDomainEventDispatcher"/> (ADR 0002 §5, Tier 1). Publishes each domain
/// event via MediatR's <see cref="IPublisher"/>, which fans out to <c>INotificationHandler&lt;T&gt;</c>s in
/// any module (cross-module fan-out is enabled by the host's unified MediatR registration — Batch 2).
///
/// Handler isolation (one failing handler must not abort its siblings) is provided by the registered
/// <c>IsolatedNotificationPublisher</c>, not here — this dispatcher only delegates to <see cref="IPublisher"/>.
///
/// Replaceable by a transactional Outbox implementation (Tier 2) without changing callers.
/// </summary>
public sealed class InProcessDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IPublisher _publisher;

    public InProcessDomainEventDispatcher(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        if (domainEvents is null)
            return;

        foreach (var domainEvent in domainEvents)
            await _publisher.Publish(domainEvent, cancellationToken);
    }
}
