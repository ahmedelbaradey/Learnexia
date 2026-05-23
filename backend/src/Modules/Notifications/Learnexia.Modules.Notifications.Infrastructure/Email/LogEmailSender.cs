using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Results;

namespace Learnexia.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// No-op / log sink <see cref="IEmailSender"/> — the Development default (and the fallback whenever
/// <c>Email:Provider</c> is <see cref="EmailProvider.None"/>). It does not contact any SMTP server, so the
/// stack runs locally with no email infrastructure; it just records the send via <see cref="ILoggerManager"/>
/// for observability. Always succeeds.
/// </summary>
public sealed class LogEmailSender : IEmailSender
{
    private readonly ILoggerManager _logger;

    public LogEmailSender(ILoggerManager logger) => _logger = logger;

    public Task<Result> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        // Avoid logging the recipient address or subject verbatim (PII): mask the recipient, omit subject.
        _logger.LogInfo($"Email (log sink): recipient={MaskEmail(to)}, subjectLength={subject?.Length ?? 0}, bodyLength={htmlBody?.Length ?? 0}. No SMTP server contacted.");
        return Task.FromResult(Result.Success());
    }

    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "(none)";
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        return $"{email[0]}***{email[at..]}";
    }
}
