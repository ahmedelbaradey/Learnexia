namespace Learnexia.Modules.Notifications.Domain.Entities;

using Learnexia.Shared.Kernel.Entities;

/// <summary>
/// Stores the Expo / FCM push token for a registered device (P4-09).
/// One row per (UserId, ExpoPushToken) — the unique index prevents duplicate registrations.
/// <see cref="UserId"/> is a plain int soft-reference to Identity.User (no cross-module FK — rule 1).
/// </summary>
public sealed class UserDeviceToken : FullAuditedEntity
{
    public int UserId { get; private set; }                 // soft-ref to Identity.User
    public string ExpoPushToken { get; private set; } = null!;
    public string Platform { get; private set; } = null!;   // "ios" / "android" / "web"
    public DateTime RegisteredAtUtc { get; private set; }
    public DateTime LastUsedAtUtc { get; private set; }
    public bool IsActive { get; private set; } = true;

    private UserDeviceToken() { }

    public static UserDeviceToken Register(int userId, string expoPushToken, string platform, DateTime nowUtc)
        => new()
        {
            UserId = userId,
            ExpoPushToken = expoPushToken,
            Platform = platform,
            RegisteredAtUtc = nowUtc,
            LastUsedAtUtc = nowUtc,
            IsActive = true,
        };

    public void TouchUsedAt(DateTime nowUtc) => LastUsedAtUtc = nowUtc;
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Transfers ownership of this token to a new user (device rotation / new device sign-in),
    /// re-activates the token, and stamps <see cref="LastUsedAtUtc"/>.
    /// </summary>
    public void ReassignTo(int newUserId, DateTime nowUtc)
    {
        UserId = newUserId;
        IsActive = true;
        LastUsedAtUtc = nowUtc;
    }
}
