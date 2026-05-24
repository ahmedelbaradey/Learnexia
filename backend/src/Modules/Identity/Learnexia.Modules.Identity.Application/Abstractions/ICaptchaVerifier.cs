namespace Learnexia.Modules.Identity.Application.Abstractions;

/// <summary>
/// Verifies an anti-automation CAPTCHA token (P1-13 BE-4). Implemented in Infrastructure against
/// Cloudflare Turnstile's siteverify endpoint. Exists as a seam so tests can substitute a fake —
/// a real Turnstile-signed token isn't available in CI, and the verifier is config-gated.
/// </summary>
public interface ICaptchaVerifier
{
    /// <summary>
    /// Returns <c>true</c> when the token is valid. When CAPTCHA is disabled (config-gated) this
    /// returns <c>true</c> immediately without a token or network call (no-op pass-through). When
    /// enabled it fails closed: a missing/invalid token, or any transport/parse error, yields
    /// <c>false</c>. Never throws on an invalid token.
    /// </summary>
    /// <param name="token">The CAPTCHA response token submitted by the client.</param>
    /// <param name="remoteIp">The caller's IP (optional; siteverify works without it).</param>
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}
