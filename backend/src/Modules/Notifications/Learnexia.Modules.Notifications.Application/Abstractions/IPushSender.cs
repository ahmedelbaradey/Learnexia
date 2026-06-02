namespace Learnexia.Modules.Notifications.Application.Abstractions;

/// <summary>
/// Channel seam for push notification delivery (P4-09 B4-2 / AC4).
/// Default implementation is <c>ExpoPushSender</c> (Expo Push API).
/// Dev/test implementation is <c>LogPushSender</c> (no-op + log).
///
/// Provider selected via <c>Notifications:Push:Provider</c> config using the same
/// enum-switch pattern as <c>IEmailSender</c>. No Strategy/Factory (rule 8).
/// </summary>
public interface IPushSender
{
    /// <summary>
    /// Sends a push notification to one or more Expo push tokens.
    /// </summary>
    /// <param name="expoPushTokens">Expo push tokens (e.g. <c>ExponentPushToken[xxx]</c>).</param>
    /// <param name="title">Notification title.</param>
    /// <param name="body">Notification body.</param>
    /// <param name="dataJson">Optional JSON data payload (forwarded to the Expo data field).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="PushSendResult"/> with the count of successful and failed sends, plus the list
    /// of invalid/expired tokens that should be deactivated.
    /// </returns>
    Task<PushSendResult> SendAsync(
        IReadOnlyList<string> expoPushTokens,
        string title,
        string body,
        string? dataJson,
        CancellationToken ct = default);
}

/// <summary>Result of a push-send attempt.</summary>
/// <param name="Sent">Number of tokens that accepted the notification.</param>
/// <param name="Failed">Number of tokens that produced an error.</param>
/// <param name="InvalidTokens">
///   Tokens reported as <c>DeviceNotRegistered</c> (Expo) or equivalent — should be
///   deactivated in <c>UserDeviceTokens</c> to avoid future failed sends.
/// </param>
public sealed record PushSendResult(
    int Sent,
    int Failed,
    IReadOnlyList<string> InvalidTokens);
