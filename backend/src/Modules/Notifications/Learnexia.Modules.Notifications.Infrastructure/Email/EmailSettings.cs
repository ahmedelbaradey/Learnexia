namespace Learnexia.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// Bound from the <c>Email</c> configuration section (mirrors how Identity binds <c>JwtSettings</c>).
/// Secrets (<see cref="UserName"/>/<see cref="Password"/>) MUST come from environment variables / a secret
/// store (e.g. <c>Email__Password</c>) — never committed to appsettings.json, which carries placeholders only.
/// </summary>
public sealed record EmailSettings
{
    public const string SectionName = "Email";

    /// <summary>Selected delivery provider; defaults to the no-op/log sink so dev runs with no SMTP server.</summary>
    public EmailProvider Provider { get; set; } = EmailProvider.None;

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>From address used as the envelope sender for all outgoing mail.</summary>
    public string FromAddress { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = string.Empty;
}
