using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Notifications.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IDeviceTokenService"/>.
/// Owns register (global-lookup + reassign-or-insert + F-05 cap eviction),
/// deactivate (IDOR-safe), and active-token lookup for push dispatch.
/// </summary>
internal sealed class DeviceTokenService : IDeviceTokenService
{
    // F-05: hard cap on active device tokens per user.
    private const int MaxTokensPerUser = 10;

    private readonly NotificationsDbContext _db;
    private readonly ILoggerManager _logger;

    public DeviceTokenService(NotificationsDbContext db, ILoggerManager logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task RegisterAsync(
        int userId,
        string expoPushToken,
        string platform,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        // Look for the token globally (not just for this user) — handles rotation/transfer.
        var existing = await _db.UserDeviceTokens
            .FirstOrDefaultAsync(t => t.ExpoPushToken == expoPushToken, ct);

        if (existing is not null)
        {
            // Transfer ownership if the token moved to a new user (device shared/rotated).
            existing.ReassignTo(userId, nowUtc);
        }
        else
        {
            // F-05: enforce per-user active token cap before inserting a new row.
            int activeCount = await _db.UserDeviceTokens
                .CountAsync(t => t.UserId == userId && t.IsActive, ct);

            if (activeCount >= MaxTokensPerUser)
            {
                var oldest = await _db.UserDeviceTokens
                    .Where(t => t.UserId == userId && t.IsActive)
                    .OrderBy(t => t.RegisteredAtUtc)
                    .FirstAsync(ct);

                oldest.Deactivate();
                _logger.LogInfo(
                    $"P4-09: device token cap ({MaxTokensPerUser}) reached for userId={userId}; deactivating oldest tokenId={oldest.Id}.");
            }

            var newToken = UserDeviceToken.Register(
                userId, expoPushToken, platform.ToLowerInvariant(), nowUtc);
            await _db.UserDeviceTokens.AddAsync(newToken, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeactivateAsync(
        int userId,
        int tokenId,
        CancellationToken ct = default)
    {
        // Anti-IDOR: only deactivate if the token row exists AND belongs to the current user.
        var token = await _db.UserDeviceTokens
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Id == tokenId, ct);

        if (token is null)
            return false;

        if (token.IsActive)
        {
            token.Deactivate();
            await _db.SaveChangesAsync(ct);
        }

        return true;
    }

    public async Task<List<ActiveTokenInfo>> GetActiveTokensAsync(
        int userId,
        CancellationToken ct = default)
    {
        return await _db.UserDeviceTokens
            .Where(t => t.UserId == userId && t.IsActive)
            .Select(t => new ActiveTokenInfo(t.Id, t.ExpoPushToken))
            .ToListAsync(ct);
    }

    public async Task DeactivateByIdsAsync(
        IReadOnlyList<int> tokenIds,
        CancellationToken ct = default)
    {
        if (tokenIds.Count == 0)
            return;

        var rows = await _db.UserDeviceTokens
            .Where(t => tokenIds.Contains(t.Id))
            .ToListAsync(ct);

        foreach (var row in rows)
            row.Deactivate();

        await _db.SaveChangesAsync(ct);
    }
}
