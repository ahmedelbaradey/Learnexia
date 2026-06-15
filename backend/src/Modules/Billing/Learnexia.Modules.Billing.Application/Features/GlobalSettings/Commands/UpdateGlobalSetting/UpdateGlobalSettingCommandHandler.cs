using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.GlobalSettings.Commands.UpdateGlobalSetting;

/// <summary>
/// Handles <see cref="UpdateGlobalSettingCommand"/>:
/// <list type="number">
///   <item>Derives <c>UpdatedBy</c> server-side from JWT claims via <c>ICurrentUserService</c>
///         (prevents request-body spoofing).</item>
///   <item>Loads the <c>GlobalSetting</c> row (allowlist already checked by the validator).</item>
///   <item>Captures the old value for the audit trail.</item>
///   <item>Calls <c>row.Update(newValue, updatedBy, UtcNow)</c>.</item>
///   <item>Saves via explicit transaction + commits.</item>
///   <item>Publishes <see cref="AdminActionPerformedEvent"/> (best-effort, outside txn) so the
///         Moderation <c>AuditLogEventHandler</c> persists an immutable audit row.</item>
///   <item>Invalidates the in-memory cache via <see cref="ISettingsCacheInvalidator"/>.</item>
/// </list>
/// Returns <c>BaseResponse&lt;bool&gt;</c> with <c>Successed = true</c> on success.
/// </summary>
public sealed class UpdateGlobalSettingCommandHandler
    : BaseResponseHandler,
      ICommandHandler<UpdateGlobalSettingCommand, BaseResponse<bool>>
{
    private readonly IBillingDbContext _db;
    private readonly ISettingsCacheInvalidator _cacheInvalidator;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public UpdateGlobalSettingCommandHandler(
        IBillingDbContext db,
        ISettingsCacheInvalidator cacheInvalidator,
        ICurrentUserService currentUser,
        IPublisher publisher,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _db               = db;
        _cacheInvalidator = cacheInvalidator;
        _currentUser      = currentUser;
        _publisher        = publisher;
        _logger           = logger;
        _localizer        = localizer;
    }

    public async Task<BaseResponse<bool>> Handle(
        UpdateGlobalSettingCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Derive UpdatedBy server-side from JWT — prevents request-body spoofing.
            var adminUserId = _currentUser.UserId ?? 0;
            var updatedBy   = _currentUser.UserName ?? adminUserId.ToString();

            var row = await _db.GlobalSettings
                .FirstOrDefaultAsync(s => s.Key == request.Key, cancellationToken);

            if (row is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GlobalSettingNotFound]);

            var oldValue = row.Value;

            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            row.Update(request.NewValue, updatedBy, DateTime.UtcNow);
            await _db.SaveChangesAsync(0); // GlobalSetting is not a FullAuditedEntity; userId unused.
            await tx.CommitAsync(cancellationToken);

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
                    Details:          $"Key={request.Key}; {oldValue} -> {request.NewValue}"),
                    cancellationToken);
            }
            catch (Exception auditEx)
            {
                _logger.LogWarn(
                    $"UpdateGlobalSettingCommandHandler: audit publish failed for key={request.Key}. " +
                    $"{auditEx.GetType().Name} — audit row may be missed but setting was committed.");
            }

            // Invalidate cache so the next read reloads from DB.
            _cacheInvalidator.InvalidateKey(request.Key);

            _logger.LogInfo($"UpdateGlobalSettingCommandHandler: updated key={request.Key} by={updatedBy}.");
            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"UpdateGlobalSettingCommandHandler: failed for key={request.Key}.");
            return ServerError<bool>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}
