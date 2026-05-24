using Google.Apis.Auth;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Configurations;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Validates a Google ID token with the Google.Apis.Auth SDK (P1-12 BE-5).
/// <see cref="GoogleJsonWebSignature.ValidateAsync"/> verifies the signature against Google's published
/// keys, the issuer, expiry, and — via <see cref="GoogleJsonWebSignature.ValidationSettings.Audience"/> —
/// that the token was issued for our OAuth client id. Any failure is logged server-side and surfaced as a
/// null result; we never leak validation internals to the caller.
/// </summary>
public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleAuthSettings _settings;
    private readonly ILoggerManager _logger;

    public GoogleTokenValidator(IOptions<GoogleAuthSettings> settings, ILoggerManager logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        try
        {
            var validationSettings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _settings.ClientId },
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);

            return new GoogleUserInfo
            {
                Subject = payload.Subject,
                Email = payload.Email,
                EmailVerified = payload.EmailVerified,
                Name = payload.Name,
            };
        }
        catch (InvalidJwtException ex)
        {
            // Expected for a forged/expired/wrong-audience token — log detail server-side, return null.
            _logger.LogWarn($"Google ID token rejected: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            // Transport/key-fetch or any other failure — never bubble internals to the caller.
            _logger.LogError(ex, "Error validating Google ID token");
            return null;
        }
    }
}
