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
/// Unlike the welcome handler this needs NO IUserLookup — the event already carries the recipient email and
/// the prebuilt reset URL. Delivery is best-effort and fully isolated: any send failure is logged and
/// swallowed so it can never fail the handler (or, upstream, the forgot-password request). The reset URL
/// embeds a single-use token, so we NEVER log the URL — only the fact that an email was attempted.
/// </summary>
public sealed class PasswordResetRequestedIntegrationEventHandler
    : INotificationHandler<PasswordResetRequestedIntegrationEvent>
{
    // Simple English copy for now (P1-12 BE-6). User-facing, but this is the Notifications module's email
    // body — there is no string-localizer wired into this email path yet; templating is a follow-up.
    private const string EmailSubject = "Reset your Learnexia password";

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
            var greetingName = string.IsNullOrWhiteSpace(notification.UserName) ? "there" : notification.UserName;
            var body =
                $"Hello {greetingName},<br/><br/>" +
                "We received a request to reset your Learnexia password. " +
                $"Click the link below to choose a new password:<br/><br/>" +
                $"<a href=\"{notification.ResetUrl}\">Reset my password</a><br/><br/>" +
                "If you did not request this, you can safely ignore this email — your password will not change.";

            var result = await _emailSender.SendAsync(notification.Email, EmailSubject, body, cancellationToken);
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
