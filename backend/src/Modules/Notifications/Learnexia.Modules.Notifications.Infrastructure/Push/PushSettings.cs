namespace Learnexia.Modules.Notifications.Infrastructure.Push;

/// <summary>
/// Configuration options for the push notification channel (P4-09 B4-2).
/// Bound from <c>appsettings.json</c> section <c>"Notifications:Push"</c>.
/// Mirrors the <c>EmailSettings</c> shape in the same module.
/// </summary>
public sealed class PushSettings
{
    public const string SectionName = "Notifications:Push";

    /// <summary>Push provider to use. Default is <see cref="PushProvider.None"/> (dev no-op).</summary>
    public PushProvider Provider { get; set; } = PushProvider.None;

    /// <summary>Expo-specific configuration sub-section.</summary>
    public ExpoConfig Expo { get; set; } = new();

    public sealed class ExpoConfig
    {
        /// <summary>Expo Push API base URL. Default: <c>https://exp.host/--/api/v2</c>.</summary>
        public string BaseUrl { get; set; } = "https://exp.host/--/api/v2";

        /// <summary>
        /// Optional Expo access token for enhanced send receipts.
        /// Set via <c>Notifications__Push__Expo__AccessToken</c> env var in production.
        /// Leave empty for open sends (Expo allows unauthenticated pushes to Expo-managed tokens).
        /// </summary>
        public string AccessToken { get; set; } = "";
    }
}

/// <summary>Push provider selector (mirrors <see cref="Email.EmailProvider"/>).</summary>
public enum PushProvider
{
    /// <summary>No-op dev sink — logs the push without actually sending.</summary>
    None = 0,

    /// <summary>Expo Push Notifications API (https://expo.dev/push-notifications).</summary>
    Expo = 1,
}
