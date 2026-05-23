using Learnexia.Shared.Kernel.Results;

namespace Learnexia.Modules.Notifications.Application.Abstractions;

/// <summary>
/// Single email-delivery seam for the Notifications module. Implemented in Infrastructure by exactly one
/// adapter selected at startup from configuration (<c>Email:Provider</c>): an SMTP adapter for
/// staging/prod and a no-op/log sink for Development. Callers (the SendNotification handler, integration-
/// event consumers) depend only on this abstraction — no provider internals leak out, and the returned
/// <see cref="Result"/> never surfaces transport details to the caller (failures are logged server-side).
/// </summary>
public interface IEmailSender
{
    Task<Result> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
