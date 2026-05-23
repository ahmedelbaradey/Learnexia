using System.Linq;
using System.Net;
using System.Net.Mail;
using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Results;

namespace Learnexia.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// SMTP <see cref="IEmailSender"/> adapter over the BCL <see cref="SmtpClient"/> (no extra NuGet package).
/// Bound from <see cref="EmailSettings"/> (host/port/credentials/from/ssl), with credentials sourced from
/// the environment. Validates recipient/subject to block header (CRLF) injection, and on any transport
/// failure logs server-side and returns a generic <see cref="Result"/> failure that does not leak provider
/// internals to the caller.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    // Non-leaking failure surfaced to callers; details are logged server-side only.
    private static readonly Error SendFailed = new("Email.SendFailed", "The email could not be sent.");
    private static readonly Error InvalidRecipient = new("Email.InvalidRecipient", "The email recipient or subject is invalid.");

    private readonly EmailSettings _settings;
    private readonly ILoggerManager _logger;

    public SmtpEmailSender(EmailSettings settings, ILoggerManager logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<Result> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        // Header-injection guard: reject CRLF in the recipient/subject so an attacker cannot smuggle extra
        // SMTP headers (BCc, etc.). MailAddress also validates the recipient is a well-formed address.
        if (string.IsNullOrWhiteSpace(to) || HasControlChars(to) || HasControlChars(subject))
        {
            _logger.LogWarn("SMTP email rejected: recipient or subject failed validation (possible header injection).");
            return Result.Failure(InvalidRecipient);
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromDisplayName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
            };
            message.To.Add(new MailAddress(to));

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };

            if (!string.IsNullOrEmpty(_settings.UserName))
            {
                client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);
            }

            await client.SendMailAsync(message, cancellationToken);

            // Do not log the recipient address or subject verbatim (PII) — mask the recipient only.
            _logger.LogInfo($"SMTP email sent (recipient={MaskEmail(to)}).");
            return Result.Success();
        }
        catch (Exception ex)
        {
            // Log full detail server-side; return a generic failure so transport internals never leak.
            _logger.LogError(ex, $"SMTP email send failed (recipient={MaskEmail(to)}).");
            return Result.Failure(SendFailed);
        }
    }

    // Reject CR/LF and any other control characters so an attacker cannot smuggle extra SMTP headers.
    private static bool HasControlChars(string? value)
        => value is not null && value.Any(char.IsControl);

    // Mask an address for logs: keep the first character + domain (e.g. "a***@example.com").
    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "(none)";
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        return $"{email[0]}***{email[at..]}";
    }
}
