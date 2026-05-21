using Learnexia.Shared.Contracts.Identity;
using MediatR;

namespace Learnexia.Modules.Notifications.Application.IntegrationEventHandlers;

public sealed class UserRegisteredIntegrationEventHandler
    : INotificationHandler<UserRegisteredIntegrationEvent>
{
    public Task Handle(UserRegisteredIntegrationEvent notification, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
