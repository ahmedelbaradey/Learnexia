using Learnexia.Modules.Notifications.Domain.Entities;

namespace Learnexia.Modules.Notifications.Application.Abstractions;

/// <summary>
/// Owns all persistence for <c>UserDeviceToken</c> rows:
/// register (global-lookup + reassign-or-insert + F-05 per-user cap eviction),
/// deactivate (IDOR-safe, user-scoped), and active-token lookup for push dispatch.
/// </summary>
public interface IDeviceTokenService
{
    /// <summary>
    /// Registers or updates the <paramref name="expoPushToken"/> for <paramref name="userId"/>.
    /// Upsert rules (in order):
    /// 1. Token already belongs to this user → re-activate + refresh LastUsedAtUtc.
    /// 2. Token belongs to a different user (device rotation) → transfer ownership to current user.
    /// 3. Token is new → insert; if the user already has MaxTokensPerUser active tokens,
    ///    deactivate the oldest one first (F-05 cap).
    /// </summary>
    Task RegisterAsync(
        int userId,
        string expoPushToken,
        string platform,
        DateTime nowUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Deactivates the token with id <paramref name="tokenId"/> if it belongs to <paramref name="userId"/>.
    /// Returns true if found and owned; false if not found or not owned (anti-IDOR — caller
    /// decides how to respond, e.g. NotFound).
    /// Idempotent: an already-deactivated token returns true without writing.
    /// </summary>
    Task<bool> DeactivateAsync(
        int userId,
        int tokenId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the active <c>ExpoPushToken</c> strings for <paramref name="userId"/>
    /// along with their row ids (needed by the NudgeDispatcher to deactivate invalid tokens).
    /// </summary>
    Task<List<ActiveTokenInfo>> GetActiveTokensAsync(
        int userId,
        CancellationToken ct = default);

    /// <summary>
    /// Deactivates the tokens identified by <paramref name="tokenIds"/> in bulk.
    /// Called by NudgeDispatcher after the push provider reports tokens as invalid.
    /// </summary>
    Task DeactivateByIdsAsync(
        IReadOnlyList<int> tokenIds,
        CancellationToken ct = default);
}

/// <summary>Minimal projection for an active device token (id + push-token string).</summary>
public sealed record ActiveTokenInfo(int Id, string ExpoPushToken);
