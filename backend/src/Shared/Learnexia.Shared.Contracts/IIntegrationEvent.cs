using MediatR;

namespace Learnexia.Shared.Contracts;

public interface IIntegrationEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}
