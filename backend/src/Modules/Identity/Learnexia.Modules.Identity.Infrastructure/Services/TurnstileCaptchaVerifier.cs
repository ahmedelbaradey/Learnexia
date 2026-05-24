using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Configurations;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Verifies a Cloudflare Turnstile token via the siteverify endpoint (P1-13 BE-4) using a typed
/// <see cref="HttpClient"/> — no SDK. Config-gated by <see cref="CaptchaSettings.Enabled"/>:
/// <list type="bullet">
/// <item>Disabled (default): returns <c>true</c> immediately (no token, no network) so dev/tests run unchanged.</item>
/// <item>Enabled: POSTs the secret + response token to siteverify and returns the parsed <c>success</c> flag.</item>
/// </list>
/// Fails closed when enabled — a missing token or any HTTP/parse error is logged (never the secret)
/// and surfaced as <c>false</c>.
/// </summary>
public class TurnstileCaptchaVerifier : ICaptchaVerifier
{
    // Cloudflare Turnstile server-side verification endpoint. Technical identifier, not user-facing text.
    private const string SiteVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    private readonly HttpClient _httpClient;
    private readonly CaptchaSettings _settings;
    private readonly ILoggerManager _logger;

    public TurnstileCaptchaVerifier(HttpClient httpClient, IOptions<CaptchaSettings> settings, ILoggerManager logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        // No-op pass-through when disabled: local dev and integration tests run without a token or network.
        if (!_settings.Enabled)
            return true;

        // Enabled but no token supplied → reject (fail closed).
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var form = new List<KeyValuePair<string, string>>
            {
                new("secret", _settings.SecretKey),
                new("response", token),
            };
            if (!string.IsNullOrWhiteSpace(remoteIp))
                form.Add(new("remoteip", remoteIp));

            using var content = new FormUrlEncodedContent(form);
            using var response = await _httpClient.PostAsync(SiteVerifyUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<TurnstileVerifyResponse>(cancellationToken);
            return payload?.Success ?? false;
        }
        catch (Exception ex)
        {
            // Never log the secret. Log the failure detail server-side and fail closed.
            _logger.LogError(ex, "Error verifying Turnstile CAPTCHA token");
            return false;
        }
    }

    /// <summary>Subset of the Turnstile siteverify JSON response we consume.</summary>
    private sealed record TurnstileVerifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }
    }
}
