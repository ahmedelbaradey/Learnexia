using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.DeleteAccount;

/// <summary>
/// Handles <see cref="DeleteAccountCommand"/> (P7-07 BE-4).
///
/// Two-step confirm gate: Confirm = false → HTTP 424 FailedDependency (no mutation).
/// Consistent with P8-04 ConfirmFreshStart pattern.
///
/// State machine: Active → Deleted, Suspended → Deleted. Terminal state — no further transitions.
/// Rejected if the target is already Deleted.
/// Self-protection: an admin cannot delete their own account.
/// SuperAdmin protection: the SuperAdmin account is guarded.
///
/// Cascade (parent): if the target holds the Parent role and CascadeChildren = true,
/// all linked children are soft-deleted in the SAME explicit DB transaction (Finding #4).
///
/// Atomicity: Identity's UserManager commits per UpdateAsync call (there is no UoW).
/// The handler opens an explicit IDbContextTransaction via IUserManagmentService.BeginTransactionAsync
/// so that primary-account and cascade-children mutations either all commit or all roll back.
/// Session revocations (Redis, SessionManagementService) are best-effort and are performed
/// AFTER the DB commit; they do NOT participate in the transaction.
///
/// Event publish: AccountDeletedIntegrationEvent with AffectedChildIds (cascade case),
/// AdminActionPerformedEvent — best-effort, after all mutations (see SuspendAccountCommandHandler
/// for publish-timing note).
/// </summary>
public class DeleteAccountCommandHandler : BaseResponseHandler, ICommandHandler<DeleteAccountCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISessionManagementService _sessionManagementService;
    private readonly IDistributedCache _distributedCache;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public DeleteAccountCommandHandler(
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        ISessionManagementService sessionManagementService,
        IDistributedCache distributedCache,
        IParentChildQuery parentChildQuery,
        IStringLocalizer<SharedResources> localizer,
        IPublisher publisher,
        ILoggerManager logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _sessionManagementService = sessionManagementService;
        _distributedCache = distributedCache;
        _parentChildQuery = parentChildQuery;
        _localizer = localizer;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // ── Confirm-gate: must run FIRST, before any DB lookup or mutation ──
            // Returns HTTP 424 (FailedDependency) per lead decision / P8-04 pattern.
            if (!request.Confirm)
            {
                _logger.LogWarn($"DeleteAccountCommand rejected: Confirm was false for user {request.UserId}.");
                return BusinessValidation<string>(_localizer[SharedResourcesKey.ConfirmAccountDeletionRequired]);
            }

            var adminUserId = _currentUserService.UserId.GetValueOrDefault();

            // Self-protection: admin cannot delete their own account.
            if (request.UserId == adminUserId)
                return BadRequest<string>(_localizer[SharedResourcesKey.CannotActOnOwnAccount]);

            var user = await _service.UserManagmentService.FindByIdWithRolesAsync(request.UserId.ToString());
            if (user is null)
                return NotFound<string>(_localizer[SharedResourcesKey.UserNotFound]);

            // SuperAdmin protection guard.
            var userRoles = await _service.UserManagmentService.GetUserRolesAsync(user);
            if (userRoles.Any(r => string.Equals(r, Roles.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase)))
                return BadRequest<string>(_localizer[SharedResourcesKey.CannotActOnSuperAdminAccount]);

            // State-machine guard: already Deleted is terminal.
            if (user.AccountStatus == AccountStatus.Deleted)
                return BadRequest<string>(_localizer[SharedResourcesKey.AccountAlreadyDeleted]);

            var utcNow = DateTime.UtcNow;

            // ── Open explicit transaction (Finding #4 — atomicity for parent + cascade children) ──
            // UserManager.UpdateAsync commits per call; without a transaction, a partial cascade failure
            // would leave the parent deleted but some children still active (inconsistent state).
            // The transaction wraps ALL DB mutations; session revocations are best-effort and run after commit.
            await using var tx = await _service.UserManagmentService.BeginTransactionAsync(cancellationToken);
            try
            {
                // ── Soft-delete the primary account ──
                ApplyDeletedStatus(user, request.Reason, adminUserId, utcNow);

                var updateResult = await _service.UserManagmentService.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    await tx.RollbackAsync(cancellationToken);
                    _logger.LogError(null, $"DeleteAccountCommandHandler: UpdateAsync failed for user {request.UserId}.");
                    return BadRequest<string>(_localizer[SharedResourcesKey.AccountLifecycleSystemError]);
                }

                // ── Cascade: soft-delete linked children if requested ──
                var affectedChildIds = new List<int>();
                if (request.CascadeChildren &&
                    userRoles.Any(r => string.Equals(r, Roles.Parent.ToString(), StringComparison.OrdinalIgnoreCase)))
                {
                    var childIds = await _parentChildQuery.GetChildIdsForParentAsync(request.UserId, cancellationToken);
                    foreach (var childId in childIds)
                    {
                        var child = await _service.UserManagmentService.FindByIdAsync(childId.ToString());
                        if (child is null || child.AccountStatus == AccountStatus.Deleted)
                            continue;

                        ApplyDeletedStatus(child, request.Reason, adminUserId, utcNow);

                        var childUpdateResult = await _service.UserManagmentService.UpdateAsync(child);
                        if (childUpdateResult.Succeeded)
                        {
                            affectedChildIds.Add(childId);
                        }
                        else
                        {
                            // Any cascade failure rolls back the whole transaction (parent + committed children).
                            await tx.RollbackAsync(cancellationToken);
                            _logger.LogError(null, $"DeleteAccountCommandHandler: cascade UpdateAsync failed for child {childId} — rolling back.");
                            return BadRequest<string>(_localizer[SharedResourcesKey.AccountLifecycleSystemError]);
                        }
                    }
                }

                // All DB mutations succeeded — commit atomically.
                await tx.CommitAsync(cancellationToken);

                // ── Session revocations (best-effort, post-commit) ──
                await RevokeUserSessionsAsync(request.UserId, cancellationToken);
                foreach (var childId in affectedChildIds)
                    await RevokeUserSessionsAsync(childId, cancellationToken);

                _logger.LogInfo($"Account {request.UserId} deleted by admin {adminUserId}. " +
                                $"CascadeChildren={request.CascadeChildren}, affected children: [{string.Join(",", affectedChildIds)}].");

                // Best-effort post-mutation event publish.
                await PublishDeletedEventAsync(request.UserId, adminUserId, affectedChildIds, cancellationToken);
                await PublishAuditEventAsync(adminUserId, AdminActions.AccountDeleted, request.UserId,
                    $"status=Deleted;cascadeChildren={affectedChildIds.Count}", cancellationToken);

                return Success<string>(_localizer[SharedResourcesKey.AccountDeletedSuccessfully]);
            }
            catch (Exception)
            {
                // Guard: roll back if an unhandled exception escapes the try body before CommitAsync.
                try { await tx.RollbackAsync(cancellationToken); } catch { /* ignore rollback failure */ }
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in DeleteAccountCommandHandler for user {request.UserId}");
            return ServerError<string>(_localizer[SharedResourcesKey.AccountLifecycleSystemError]);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void ApplyDeletedStatus(User user, string reason, int adminUserId, DateTime utcNow)
    {
        user.AccountStatus = AccountStatus.Deleted;
        user.IsActive = false;
        user.LastStatusReason = reason;
        user.StatusChangedBy = adminUserId;
        user.StatusChangedAtUtc = utcNow;
        user.UpdatedAt = utcNow;
        user.UpdatedBy = adminUserId;
        // Mirror the soft-delete fields used by the legacy DeleteUserCommand.
        user.IsDeleted = true;
        user.DeletedAt = utcNow;
        user.DeletedBy = adminUserId;
    }

    private async Task RevokeUserSessionsAsync(int userId, CancellationToken cancellationToken)
    {
        // Remove the Redis refresh token.
        try
        {
            await _distributedCache.RemoveAsync($"userrefreshtoken-{userId}", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to revoke refresh token for user {userId} (delete) — ignored.");
        }

        // Terminate all tracked sessions.
        try
        {
            var sessions = await _sessionManagementService.GetUserSessionsAsync(userId);
            foreach (var session in sessions)
            {
                try
                {
                    await _sessionManagementService.TerminateSessionAsync(
                        session.SessionId, SessionTerminationReason.AdminTermination);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to terminate session {session.SessionId} for user {userId} (delete) — ignored.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to enumerate sessions for user {userId} (delete) — ignored.");
        }
    }

    private async Task PublishDeletedEventAsync(
        int userId, int adminUserId,
        IReadOnlyList<int> affectedChildIds, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(
                new AccountDeletedIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredOnUtc: DateTime.UtcNow,
                    UserId: userId,
                    DeletedByAdminUserId: adminUserId,
                    AffectedChildIds: affectedChildIds),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to publish AccountDeletedIntegrationEvent for user {userId} — ignored (best-effort).");
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
