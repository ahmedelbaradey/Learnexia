using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly IEmailSender _emailSender;
    private readonly IServiceProvider _services;

    public UserRegisteredIntegrationEventHandler(
        INotificationsDbContext db,
        ILoggerManager logger,
        IEmailSender emailSender,
        IServiceProvider services)
    {
        _db = db;
        _logger = logger;
        _emailSender = emailSender;
        _services = services;
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

        const string title = "Welcome to Learnexia";
        var body = $"Welcome {notification.UserName}! Your account has been created.";

        var welcome = Notification.CreateWelcome(
            externalUserId: notification.UserId,
            title: title,
            body: body);

        _db.Notifications.Add(welcome);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInfo(
            $"Notifications: created welcome notification {welcome.Id} for user {notification.UserId}.");

        // Best-effort welcome email: never let an email failure fail this handler (or, upstream, the
        // registration that produced the event). The in-app notification row above is the source of truth;
        // the email is an additive delivery channel. The event intentionally carries no email (PII), so we
        // resolve it through the Shared.Contracts IUserLookup seam if an implementation is registered;
        // otherwise we log and skip — keeping the module isolated and the path ready for when Identity
        // provides the adapter.
        await TrySendWelcomeEmailAsync(notification, title, body, cancellationToken);
    }

    private async Task TrySendWelcomeEmailAsync(
        UserRegisteredIntegrationEvent notification,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        try
        {
            var userLookup = _services.GetService<IUserLookup>();
            if (userLookup is null)
            {
                _logger.LogInfo(
                    $"Notifications: no IUserLookup registered; skipping welcome email for user {notification.UserId}.");
                return;
            }

            var user = await userLookup.FindByIdAsync(notification.UserId, cancellationToken);
            if (user is null || string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogInfo(
                    $"Notifications: no email resolved for user {notification.UserId}; skipping welcome email.");
                return;
            }

            var result = await _emailSender.SendAsync(user.Email, title, body, cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogWarn(
                    $"Notifications: welcome email delivery failed for user {notification.UserId} ({result.Error.Code}).");
            }
        }
        catch (Exception ex)
        {
            // Isolate the email failure: log and swallow so registration/notification-row write stand.
            _logger.LogError(ex, $"Notifications: welcome email send threw for user {notification.UserId}; isolated.");
        }
    }
}
