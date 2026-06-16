using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.GlobalSettings.Dtos;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IGlobalSettingService"/> (Option C).
///
/// <para>Owns ALL EF Core access, transaction management, post-commit event staging, and
/// cache invalidation for GlobalSetting admin operations. Application-layer handlers inject
/// this interface and stay EF-free.</para>
///
/// <para><strong>Post-commit ordering:</strong> <c>AdminActionPerformedEvent</c> is published
/// AFTER the commit (best-effort — audit failure must never roll back the setting update).
/// Cache invalidation via <c>ISettingsCacheInvalidator</c> also happens after commit.</para>
/// </summary>
public sealed class GlobalSettingService : IGlobalSettingService, IGlobalSettingValidationService
{
    private readonly BillingDbContext _db;
    private readonly ISettingsCacheInvalidator _cacheInvalidator;
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public GlobalSettingService(
        BillingDbContext db,
        ISettingsCacheInvalidator cacheInvalidator,
        IPublisher publisher,
        ILoggerManager logger)
    {
        _db               = db;
        _cacheInvalidator = cacheInvalidator;
        _publisher        = publisher;
        _logger           = logger;
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────────

    public async Task<GlobalSettingUpdateResult> UpdateAsync(
        string key,
        string newValue,
        int adminUserId,
        string updatedBy,
        CancellationToken ct)
    {
        var row = await _db.GlobalSettings
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        if (row is null)
            return GlobalSettingUpdateResult.Fail(GlobalSettingUpdateFailureReason.NotFound);

        var oldValue = row.Value;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        row.Update(newValue, updatedBy, DateTime.UtcNow);
        await _db.SaveChangesAsync(0); // GlobalSetting is not a FullAuditedEntity; userId unused.
        await tx.CommitAsync(ct);

        // Publish AdminActionPerformedEvent after commit (best-effort — audit failure must never
        // roll back the setting update; mirrors Gamification admin handler pattern).
        try
        {
            await _publisher.Publish(new AdminActionPerformedEvent(
                EventId:          Guid.NewGuid(),
                OccurredAtUtc:    DateTime.UtcNow,
                AdminUserId:      adminUserId,
                Action:           AdminActions.GlobalSettingUpdated,
                TargetEntityType: "GlobalSetting",
                TargetEntityId:   0,
                Details:          $"Key={key}; {oldValue} -> {newValue}"),
                ct);
        }
        catch (Exception auditEx)
        {
            _logger.LogWarn(
                $"GlobalSettingService: audit publish failed for key={key}. " +
                $"{auditEx.GetType().Name} — audit row may be missed but setting was committed.");
        }

        // Invalidate cache so the next read reloads from DB.
        _cacheInvalidator.InvalidateKey(key);

        _logger.LogInfo($"GlobalSettingService: updated key={key} by={updatedBy}.");

        return GlobalSettingUpdateResult.Ok();
    }

    // ── GetAllManagedAsync ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<GlobalSettingDto>> GetAllManagedAsync(CancellationToken ct)
    {
        // Materialize ManagedKeys to a List<string> first so EF Core can translate
        // the Contains call — IReadOnlySet<string> is not translatable by EF Core.
        var managedKeys = GlobalSettingKeys.ManagedKeys.ToList();

        var rows = await _db.GlobalSettings
            .AsNoTracking()
            .Where(s => managedKeys.Contains(s.Key))
            .OrderBy(s => s.Key)
            .Select(s => new GlobalSettingDto
            {
                Key       = s.Key,
                Value     = s.Value,
                Type      = s.Type,
                UpdatedBy = s.UpdatedBy,
                UpdatedAt = s.UpdatedAt,
            })
            .ToListAsync(ct);

        return rows;
    }

    // ── IGlobalSettingValidationService ───────────────────────────────────────────

    public async Task<string?> GetRawValueAsync(string key, CancellationToken ct)
    {
        var row = await _db.GlobalSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, ct);
        return row?.Value;
    }
}
