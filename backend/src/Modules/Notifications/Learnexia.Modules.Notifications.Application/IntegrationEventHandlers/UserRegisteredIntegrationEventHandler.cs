using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Notifications.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="UserRegisteredIntegrationEvent"/> (produced by the Identity module
/// after a user is committed). Proves the end-to-end fan-out path (ADR 0002 §3/§4): Identity publishes the
/// Shared.Contracts event → the host's unified MediatR registration delivers it here → this handler writes
/// a welcome <see cref="Notification"/> into the Notifications module's OWN DbContext.
///
/// The write happens in this handler's own (request) scope against <see cref="INotificationsDbContext"/> —
/// NOT inside Identity's transaction (cross-module = eventual consistency, CONVENTIONS §8/§12). It is
/// idempotent-friendly: a welcome notification is created at most once per originating user id, so a
/// redelivery does not duplicate it. Per-handler failures are isolated + logged by the
/// IsolatedNotificationPublisher; we also log here for observability.
/// </summary>
public sealed class UserRegisteredIntegrationEventHandler
    : INotificationHandler<UserRegisteredIntegrationEvent>
{
    private readonly INotificationsDbContext _db;
    private readonly ILoggerManager _logger;

    public UserRegisteredIntegrationEventHandler(INotificationsDbContext db, ILoggerManager logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(UserRegisteredIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInfo(
            $"Notifications: received UserRegisteredIntegrationEvent (EventId={notification.EventId}) " +
            $"for user {notification.UserId}.");

        // Idempotency: do not create a second welcome notification for the same originating user.
        var alreadyExists = await _db.Notifications
            .AnyAsync(n => n.RecipientExternalUserId == notification.UserId, cancellationToken);

        if (alreadyExists)
        {
            _logger.LogInfo($"Notifications: welcome notification already exists for user {notification.UserId}; skipping.");
            return;
        }

        var welcome = Notification.CreateWelcome(
            externalUserId: notification.UserId,
            title: "Welcome to Learnexia",
            body: $"Welcome {notification.UserName}! Your account has been created.");

        _db.Notifications.Add(welcome);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInfo(
            $"Notifications: created welcome notification {welcome.Id} for user {notification.UserId}.");
    }
}
