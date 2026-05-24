namespace Learnexia.Modules.Identity.Application.Abstractions;

/// <summary>
/// The verified Google identity extracted from a validated Google ID token.
/// </summary>
public sealed record GoogleUserInfo
{
    /// <summary>Google's stable unique user id (the JWT "sub" claim) — used as the external-login key.</summary>
    public required string Subject { get; init; }
    public required string Email { get; init; }
    public bool EmailVerified { get; init; }
    public string? Name { get; init; }
}

/// <summary>
/// Validates a Google ID token (issued to the frontend by Google) and returns the verified identity.
/// Implemented in Infrastructure via the Google.Apis.Auth SDK. Exists as a seam so tests can substitute
/// a fake — a real Google-signed token isn't available in CI (P1-12 BE-5).
/// </summary>
public interface IGoogleTokenValidator
{
    /// <summary>
    /// Returns the verified identity, or <c>null</c> if the token is missing, malformed, expired,
    /// signed by an unexpected key, or issued for a different audience. Never throws on an invalid token.
    /// </summary>
    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken);
}
