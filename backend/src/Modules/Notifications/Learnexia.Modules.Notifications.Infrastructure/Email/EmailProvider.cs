namespace Learnexia.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// Fixed set of email-delivery providers selected via <c>Email:Provider</c> configuration.
/// <see cref="None"/> is the no-op/log sink (the Development default); <see cref="Smtp"/> is the
/// BCL SMTP adapter for staging/prod. A second real provider would be added here (and only then would
/// a selection Strategy be worth introducing — see the P1-13a pattern gate).
/// </summary>
public enum EmailProvider
{
    None = 0,
    Smtp = 1,
}
