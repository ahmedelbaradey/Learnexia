using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Notifications.Infrastructure.Push;

/// <summary>
/// Expo Push API adapter (P4-09 B4-2 / AC4).
/// POSTs to <c>https://exp.host/--/api/v2/push/send</c> with a JSON array of message objects.
/// Identifies <c>DeviceNotRegistered</c> errors and returns the offending tokens in
/// <see cref="PushSendResult.InvalidTokens"/> so the caller can deactivate them.
///
/// <para>The Expo API accepts up to 100 messages per request; P4-09 sends one-per-dispatch so
/// batching is not needed at this scale. A cursor-paged batching extension can be added later.</para>
///
/// Fail-soft: non-2xx responses are logged and returned as failures; the method never throws
/// to its caller (ADR 0002 §3).
/// </summary>
public sealed class ExpoPushSender : IPushSender
{
    private const string ExpoPushEndpoint = "/push/send";
    private const string DeviceNotRegisteredError = "DeviceNotRegistered";

    private readonly HttpClient _http;
    private readonly PushSettings _settings;
    private readonly ILoggerManager _logger;

    public ExpoPushSender(HttpClient http, IOptions<PushSettings> settings, ILoggerManager logger)
    {
        _http     = http;
        _settings = settings.Value;
        _logger   = logger;
    }

    public async Task<PushSendResult> SendAsync(
        IReadOnlyList<string> expoPushTokens,
        string title,
        string body,
        string? dataJson,
        CancellationToken ct = default)
    {
        if (expoPushTokens.Count == 0)
            return new PushSendResult(0, 0, []);

        // Build the Expo message array — one message object per token.
        // data field: parse the JSON string into an object for the Expo payload.
        object? dataObject = null;
        if (!string.IsNullOrWhiteSpace(dataJson) && dataJson != "{}")
        {
            try { dataObject = JsonSerializer.Deserialize<object>(dataJson); }
            catch { /* ignore malformed data — send without data */ }
        }

        var messages = expoPushTokens.Select(token => new
        {
            to      = token,
            title   = title,
            body    = body,
            data    = dataObject,
            sound   = "default",
            priority = "high",
        }).ToList();

        var json = JsonSerializer.Serialize(messages);

        // F-03 security fix: use a per-request HttpRequestMessage so the Authorization header
        // is never mutated on the shared HttpClient instance (DefaultRequestHeaders is not
        // thread-safe; concurrent dispatches would race on that header).
        using var request = new HttpRequestMessage(HttpMethod.Post, ExpoPushEndpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(_settings.Expo.AccessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.Expo.AccessToken);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"P4-09: ExpoPushSender — HTTP call to Expo failed. tokens={expoPushTokens.Count}");
            return new PushSendResult(0, expoPushTokens.Count, []);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogInfo(
                $"P4-09: ExpoPushSender — non-2xx from Expo: {(int)response.StatusCode}. tokens={expoPushTokens.Count}");
            return new PushSendResult(0, expoPushTokens.Count, []);
        }

        // Parse the Expo response body.
        // Shape: { "data": [ { "status": "ok" | "error", "message"?: "...", "details"?: { "error": "..." } } ] }
        string responseBody;
        try { responseBody = await response.Content.ReadAsStringAsync(ct); }
        catch { return new PushSendResult(expoPushTokens.Count, 0, []); }

        return ParseExpoResponse(responseBody, expoPushTokens);
    }

    // -------------------------------------------------------------------------
    // Response parsing
    // -------------------------------------------------------------------------

    private PushSendResult ParseExpoResponse(string responseBody, IReadOnlyList<string> tokens)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return new PushSendResult(tokens.Count, 0, []);

            int sent = 0, failed = 0;
            var invalidTokens = new List<string>();

            int i = 0;
            foreach (var item in data.EnumerateArray())
            {
                var status = item.TryGetProperty("status", out var s) ? s.GetString() : null;
                if (status == "ok")
                {
                    sent++;
                }
                else
                {
                    failed++;
                    // Identify DeviceNotRegistered errors so the caller can deactivate the token.
                    if (item.TryGetProperty("details", out var details)
                        && details.TryGetProperty("error", out var errorProp)
                        && errorProp.GetString() == DeviceNotRegisteredError
                        && i < tokens.Count)
                    {
                        invalidTokens.Add(tokens[i]);
                    }
                }

                i++;
            }

            return new PushSendResult(sent, failed, invalidTokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "P4-09: ExpoPushSender — failed to parse Expo response.");
            return new PushSendResult(tokens.Count, 0, []);
        }
    }
}
