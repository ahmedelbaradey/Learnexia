using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.SuspendAccount;

/// <summary>
/// Handles <see cref="SuspendAccountCommand"/> (P7-07 BE-2).
///
/// State machine: Active → Suspended. Rejected if already Deleted (terminal) or already Suspended.
/// Self-protection: an admin cannot suspend their own account.
/// SuperAdmin protection: the SuperAdmin account is guarded.
///
/// Session revocation (MVP per lead decision — Q-B2):
///   (a) IsActive = false → blocks future sign-in via the existing SignInCommandHandler IsActive gate.
///   (b) Redis refresh token removed via IDistributedCache.RemoveAsync("userrefreshtoken-{userId}").
///   (c) All tracked sessions terminated via ISessionManagementService.GetUserSessionsAsync + TerminateSessionAsync.
///   Residual window: existing issued access JWTs expire naturally (documented G2 follow-up).
///
/// Event publish: best-effort, published at end of handler body (within the UoW transaction but
/// after all DB mutations — mirrors the curriculum wave pre-commit publish pattern; post-commit
/// is architecturally not achievable for ICommand&lt;T&gt; handlers under UnitOfWorkBehavior without
/// modifying the UoW, tracked as a wave-wide follow-up).
/// </summary>
public class SuspendAccountCommandHandler : BaseResponseHandler, ICommandHandler<SuspendAccountCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISessionManagementService _sessionManagementService;
    private readonly IDistributedCache _distributedCache;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public SuspendAccountCommandHandler(
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        ISessionManagementService sessionManagementService,
        IDistributedCache distributedCache,
        IStringLocalizer<SharedResources> localizer,
        IPublisher publisher,
        ILoggerManager logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _sessionManagementService = sessionManagementService;
        _distributedCache = distributedCache;
        _localizer = localizer;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(SuspendAccountCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var adminUserId = _currentUserService.UserId.GetValueOrDefault();

            // Self-protection: admin cannot suspend their own account.
            if (request.UserId == adminUserId)
                return BadRequest<string>(_localizer[SharedResourcesKey.CannotActOnOwnAccount]);

            var user = await _service.UserManagmentService.FindByIdWithRolesAsync(request.UserId.ToString());
            if (user is null)
                return NotFound<string>(_localizer[SharedResourcesKey.UserNotFound]);

            // SuperAdmin protection guard.
            var userRoles = await _service.UserManagmentService.GetUserRolesAsync(user);
            if (userRoles.Any(r => string.Equals(r, Roles.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase)))
                return BadRequest<string>(_localizer[SharedResourcesKey.CannotActOnSuperAdminAccount]);

            // State-machine guard: Deleted is terminal.
            if (user.AccountStatus == AccountStatus.Deleted)
                return BadRequest<string>(_localizer[SharedResourcesKey.AccountAlreadyDeleted]);

            // State-machine guard: no-op / reject if already Suspended.
            if (user.AccountStatus == AccountStatus.Suspended)
                return BadRequest<string>(_localizer[SharedResourcesKey.AccountAlreadySuspended]);

            // Apply status change.
            user.AccountStatus = AccountStatus.Suspended;
            user.IsActive = false;
            user.LastStatusReason = request.Reason;
            user.StatusChangedBy = adminUserId;
            user.StatusChangedAtUtc = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = adminUserId;

            var updateResult = await _service.UserManagmentService.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError(null, $"SuspendAccountCommandHandler: UpdateAsync failed for user {request.UserId}.");
                return BadRequest<string>(_localizer[SharedResourcesKey.AccountLifecycleSystemError]);
            }

            // Session revocation (MVP — best-effort; failure does not abort the status change).
            await RevokeUserSessionsAsync(request.UserId, cancellationToken);

            _logger.LogInfo($"Account {request.UserId} suspended by admin {adminUserId}.");

            // Best-effort post-mutation event publish (see handler summary for timing note).
            await PublishSuspendedEventAsync(request.UserId, adminUserId, cancellationToken);
            // Finding #9 — audit Details must contain ids/enum states only, no free-text.
            await PublishAuditEventAsync(adminUserId, AdminActions.AccountSuspended, request.UserId,
                $"status=Suspended", cancellationToken);

            return Success<string>(_localizer[SharedResourcesKey.AccountSuspendedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in SuspendAccountCommandHandler for user {request.UserId}");
            return ServerError<string>(_localizer[SharedResourcesKey.AccountLifecycleSystemError]);
        }
    }

    // ── Session revocation ─────────────────────────────────────────────────────

    private async Task RevokeUserSessionsAsync(int userId, CancellationToken cancellationToken)
    {
        // (b) Remove the Redis refresh token so it cannot be rotated.
        try
        {
            await _distributedCache.RemoveAsync($"userrefreshtoken-{userId}", cancellationToken);
            _logger.LogInfo($"Revoked refresh token for user {userId} (suspend).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to revoke refresh token for user {userId} (suspend) — ignored.");
        }

        // (c) Terminate all tracked sessions via the new P6-07 helper (DRY).
        try
        {
            var count = await _sessionManagementService.TerminateAllUserSessionsAsync(userId, Domain.Enums.SessionTerminationReason.AdminTermination);
            _logger.LogInfo($"Terminated {count} session(s) for user {userId} (suspend).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to terminate sessions for user {userId} (suspend) — ignored.");
        }
    }

    // ── Event helpers ─────────────────────────────────────────────────────────

    private async Task PublishSuspendedEventAsync(
        int userId, int adminUserId, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(
                new AccountSuspendedIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredOnUtc: DateTime.UtcNow,
                    UserId: userId,
                    SuspendedByAdminUserId: adminUserId),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to publish AccountSuspendedIntegrationEvent for user {userId} — ignored (best-effort).");
        }
    }

    private async Task PublishAuditEventAsync(
        int adminUserId, string action, int targetId, string? details, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(
                new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: adminUserId,
                    Action: action,
                    TargetEntityType: "User",
                    TargetEntityId: targetId,
                    Details: details),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish AdminActionPerformedEvent (best-effort, ignored).");
        }
    }
}
