using System.Net;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Templates;
using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Notifications.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="PasswordResetRequestedIntegrationEvent"/> (produced by the Identity
/// module after a forgot-password request resolves to an existing, active account). Mirrors
/// <see cref="UserRegisteredIntegrationEventHandler"/>: Identity publishes the Shared.Contracts event →
/// the host's unified MediatR registration delivers it here → this handler sends the reset email via the
/// Notifications module's <see cref="IEmailSender"/>.
///
/// Unlike the welcome handler this needs NO IUserLookup — the event already carries the recipient email,
/// the prebuilt reset URL, and (P6-06 BE-2) the recipient's <c>Locale</c> (PreferredLanguage). Delivery
/// is best-effort and fully isolated: any send failure is logged and swallowed so it can never fail the
/// handler (or, upstream, the forgot-password request — which is now out-of-band per BE-1). The reset URL
/// embeds a single-use token, so we NEVER log the URL — only the fact that an email was attempted.
///
/// P6-06 BE-2: subject and body are now localized via <see cref="ReengagementCopyTemplates"/> (the same
/// established email-copy store used for the welcome email in P9-10), NOT via resx/IStringLocalizer.
/// Locale falls back to "ar-EG" (platform default) when <see cref="PasswordResetRequestedIntegrationEvent.Locale"/>
/// is null (backward-compatible with events emitted before BE-2 was deployed).
/// </summary>
public sealed class PasswordResetRequestedIntegrationEventHandler
    : INotificationHandler<PasswordResetRequestedIntegrationEvent>
{
    // Template code for the password-reset email. Technical non-user-facing identifier (dictionary key).
    private const string PasswordResetCode = "PASSWORD_RESET";

    private readonly ILoggerManager _logger;
    private readonly IEmailSender _emailSender;

    public PasswordResetRequestedIntegrationEventHandler(
        ILoggerManager logger,
        IEmailSender emailSender)
    {
        _logger = logger;
        _emailSender = emailSender;
    }

    public async Task Handle(PasswordResetRequestedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        // Never log the email or the reset URL (the URL carries the single-use token).
        _logger.LogInfo(
            $"Notifications: received PasswordResetRequestedIntegrationEvent (EventId={notification.EventId}).");

        try
        {
            // P6-06 BE-2: resolve locale from the event (falls back to ar-EG when null — backward-compatible).
            var locale = string.IsNullOrWhiteSpace(notification.Locale) ? "ar-EG" : notification.Locale;
            // HTML-encode the user-controlled display name before it is substituted into the HTML email
            // body (the template body contains <a href> markup). Prevents a crafted display name from
            // injecting markup/script into the recipient's reset email. {resetUrl} is app-generated and
            // must stay a valid href, so it is NOT encoded.
            var greetingName = WebUtility.HtmlEncode(
                string.IsNullOrWhiteSpace(notification.UserName) ? string.Empty : notification.UserName);

            // Render subject + body from the template store (mirrors UserRegisteredIntegrationEventHandler).
            // The {resetUrl} placeholder substitutes the prebuilt reset link (carries the single-use token —
            // confirmed not logged; only the fact of the send is logged above).
            var (subject, body) = ReengagementCopyTemplates.Render(
                NotificationCategory.System,
                PasswordResetCode,
                locale,
                ("userName", greetingName),
                ("resetUrl", notification.ResetUrl));

            var result = await _emailSender.SendAsync(notification.Email, subject, body, cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogWarn(
                    $"Notifications: password-reset email delivery failed ({result.Error.Code}).");
            }
        }
        catch (Exception ex)
        {
            // Isolate the email failure: log and swallow so the upstream request stands.
            _logger.LogError(ex, "Notifications: password-reset email send threw; isolated.");
        }
    }
}
