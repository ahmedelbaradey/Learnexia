using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Results;
using MediatR;

namespace Learnexia.Modules.Notifications.Application.Features.SendNotification;

public sealed class SendNotificationCommandHandler(INotificationsDbContext db)
    : IRequestHandler<SendNotificationCommand, Result>
{
    public Task<Result> Handle(SendNotificationCommand request, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
