using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Application.IntegrationEventHandlers.Reengagement;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Templates;
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
///
/// P9-10: Welcome copy (inbox title/body + email subject/body) is now localized. Locale is resolved
/// via <see cref="ReengagementHandlerHelper.GetLocaleAsync"/> — the same single seam used by all
/// re-engagement handlers — which reads the recipient's <c>PreferredLanguage</c> (UI language)
/// via <see cref="IUserLookup.FindByIdAsync"/> and falls back to "ar-EG" (platform default).
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

        // P9-10 (BE-5): locale comes from recipient PreferredLanguage (UI language) ONLY —
        // NEVER from LearningLanguage (P8 curriculum medium). These are distinct user settings.
        // GetLocaleAsync is the single locale seam for all recipient-bound notifications.
        var userLookup = _services.GetService<IUserLookup>();
        var locale     = await ReengagementHandlerHelper.GetLocaleAsync(userLookup, notification.UserId, cancellationToken);

        // P9-10 (BE-2): welcome copy rendered from template (ar-EG primary, en-US fallback).
        // Template key: System:WELCOME:{locale}. Placeholder: {userName}.
        var (title, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.System,
            "WELCOME",
            locale,
            ("userName", notification.UserName));

        var written = await _inboxService.WriteWelcomeIfAbsentAsync(
            notification.UserId, title, body, cancellationToken);

        if (!written)
        {
            _logger.LogInfo(
                $"Notifications: welcome notification already exists for user {notification.UserId}; skipping.");
            return;
        }

        _logger.LogInfo(
            $"Notifications: created welcome notification for user {notification.UserId} (locale={locale}).");

        // Best-effort welcome email: never let an email failure fail this handler.
        // P9-10 (BE-3): email subject/body use the same rendered locale-correct copy (title/body).
        await TrySendWelcomeEmailAsync(notification, userLookup, title, body, cancellationToken);
    }

    private async Task TrySendWelcomeEmailAsync(
        UserRegisteredIntegrationEvent notification,
        IUserLookup? userLookup,
        string emailSubject,
        string emailBody,
        CancellationToken cancellationToken)
    {
        try
        {
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

            // P9-10 (BE-3): subject = template title, body = template body (confirmed by brief OQ-3).
            // Both are already locale-correct from the Render call in Handle().
            var result = await _emailSender.SendAsync(user.Email, emailSubject, emailBody, cancellationToken);
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
