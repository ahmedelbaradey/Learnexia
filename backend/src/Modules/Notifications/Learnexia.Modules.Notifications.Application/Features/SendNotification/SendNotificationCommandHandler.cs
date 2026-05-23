using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Results;
using MediatR;

namespace Learnexia.Modules.Notifications.Application.Features.SendNotification;

/// <summary>
/// Sends a notification by email via the configured <see cref="IEmailSender"/> (SMTP adapter in
/// staging/prod, no-op/log sink in dev). When no recipient email is supplied the email step is skipped and
/// the command still succeeds (the in-app notification path is unaffected). Transport failures are logged
/// server-side by the adapter and returned as a generic <see cref="Result"/> failure — no provider
/// internals leak to the caller.
/// </summary>
public sealed class SendNotificationCommandHandler(IEmailSender emailSender, ILoggerManager logger)
    : IRequestHandler<SendNotificationCommand, Result>
{
    public async Task<Result> Handle(SendNotificationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            logger.LogInfo($"SendNotification: no recipient email for user {request.RecipientUserId}; skipping email delivery.");
            return Result.Success();
        }

        var result = await emailSender.SendAsync(request.RecipientEmail, request.Title, request.Body, cancellationToken);

        if (result.IsFailure)
        {
            logger.LogWarn($"SendNotification: email delivery failed for user {request.RecipientUserId} ({result.Error.Code}).");
        }

        return result;
    }
}
