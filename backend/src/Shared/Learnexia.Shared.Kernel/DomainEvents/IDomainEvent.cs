using MediatR;

namespace Learnexia.Shared.Kernel.DomainEvents;

public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}
