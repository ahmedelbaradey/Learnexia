namespace Learnexia.Modules.Identity.Application.Configurations;

/// <summary>
/// Anti-automation CAPTCHA configuration (P1-13 BE-4). Bound from the "Captcha" config section.
/// Cloudflare Turnstile is the provider; verification happens server-side via the siteverify endpoint.
/// <para>
/// When <see cref="Enabled"/> is <c>false</c> (the committed default) the verifier is a no-op
/// pass-through — registration works with no token, so local dev and the integration tests are
/// unaffected. Staging/Production set <c>Captcha__Enabled=true</c> and supply the Turnstile secret
/// out-of-band via the <c>Captcha__SecretKey</c> environment variable (never committed).
/// </para>
/// </summary>
public class CaptchaSettings
{
    public bool Enabled { get; set; }

    public string SecretKey { get; set; } = string.Empty;
}
