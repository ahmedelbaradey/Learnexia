namespace Learnexia.Shared.Contracts.Identity;

public interface IUserLookup
{
    Task<UserSummary?> FindByIdAsync(int userId, CancellationToken ct = default);
}

/// <summary>
/// Lightweight cross-module projection of a platform user.
/// <para><c>PreferredLanguage</c> is nullable for backward-compatibility; callers should fall back
/// to <c>"ar-EG"</c> when null (the platform default). Added in P4-09 so Notifications can resolve
/// the correct copy-template locale without referencing the Identity module directly.</para>
/// </summary>
public sealed record UserSummary(int Id, string UserName, string Email, string? PreferredLanguage = null);
