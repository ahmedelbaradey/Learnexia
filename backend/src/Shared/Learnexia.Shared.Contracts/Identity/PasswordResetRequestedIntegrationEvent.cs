namespace Learnexia.Shared.Contracts.Identity;

// Cross-module seam (P1-12 BE-6): Identity publishes this AFTER a password-reset request resolves to an
// existing, active account; the Notifications module consumes it and delivers the reset email via its own
// IEmailSender. Identity never references the Notifications module (module isolation) — the email + URL
// travel on the event so the consumer needs no IUserLookup. Unlike UserRegisteredIntegrationEvent (which
// intentionally carries no PII), this event MUST carry the recipient email and the prebuilt reset URL,
// because the email body is the only delivery channel and Identity is the only place that can mint the
// single-use reset token. The token is embedded inside ResetUrl; consumers/log sinks must NOT log it.
public sealed record PasswordResetRequestedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    string Email,
    string ResetUrl,
    string? UserName,
    /// <summary>
    /// The recipient's preferred UI language (e.g. "ar-EG", "en-US"), resolved at emit time from the
    /// Identity <c>User.PreferredLanguage</c> field. Consumers use this to render the reset email in
    /// the recipient's own language (P6-06 BE-2). Nullable for backward compatibility — consumers
    /// must fall back to "ar-EG" (platform default) when null.
    /// </summary>
    string? Locale = null) : IIntegrationEvent;
