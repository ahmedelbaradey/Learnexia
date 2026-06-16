using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Notifications.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="UserRegisteredIntegrationEvent"/> (produced by the Identity module
/// after a user is committed). Proves the end-to-end fan-out path (ADR 0002 §3/§4): Identity publishes the
/// Shared.Contracts event → the host's unified MediatR registration delivers it here → this handler writes
/// a welcome <see cref="Domain.Entities.Notification"/> into the Notifications module's OWN DbContext.
///
/// Persistence and idempotency live in <see cref="INotificationInboxService"/> (Option-C rule).
/// The write is idempotent-friendly: a welcome notification is created at most once per originating user id.
/// Per-handler failures are isolated + logged by the IsolatedNotificationPublisher.
/// </summary>
public sealed class UserRegisteredIntegrationEventHandler
    : INotificationHandler<UserRegisteredIntegrationEvent>
{
    private readonly INotificationInboxService _inboxService;
    private readonly ILoggerManager _logger;
    private readonly IEmailSender _emailSender;
    private readonly IServiceProvider _services;

    public UserRegisteredIntegrationEventHandler(
        INotificationInboxService inboxService,
        ILoggerManager logger,
        IEmailSender emailSender,
        IServiceProvider services)
    {
        _inboxService = inboxService;
        _logger       = logger;
        _emailSender  = emailSender;
        _services     = services;
    }

    public async Task Handle(UserRegisteredIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInfo(
            $"Notifications: received UserRegisteredIntegrationEvent (EventId={notification.EventId}) " +
            $"for user {notification.UserId}.");

        const string title = "Welcome to Learnexia";
        var body = $"Welcome {notification.UserName}! Your account has been created.";

        var written = await _inboxService.WriteWelcomeIfAbsentAsync(
            notification.UserId, title, body, cancellationToken);

        if (!written)
        {
            _logger.LogInfo(
                $"Notifications: welcome notification already exists for user {notification.UserId}; skipping.");
            return;
        }

        _logger.LogInfo(
            $"Notifications: created welcome notification for user {notification.UserId}.");

        // Best-effort welcome email: never let an email failure fail this handler.
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
            _logger.LogError(ex, $"Notifications: welcome email send threw for user {notification.UserId}; isolated.");
        }
    }
}
