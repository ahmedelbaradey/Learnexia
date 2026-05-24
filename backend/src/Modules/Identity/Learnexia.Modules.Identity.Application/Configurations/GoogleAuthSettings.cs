namespace Learnexia.Modules.Identity.Application.Configurations;

/// <summary>
/// Google social sign-in configuration (P1-12 BE-5). Bound from the "GoogleAuth" config section.
/// <see cref="ClientId"/> is the OAuth client id used as the expected audience when validating the
/// Google ID token. The committed value is a placeholder ("") — the real value is supplied
/// out-of-band via the GoogleAuth__ClientId environment variable in non-local environments.
/// </summary>
public class GoogleAuthSettings
{
    public string ClientId { get; set; } = string.Empty;
}
