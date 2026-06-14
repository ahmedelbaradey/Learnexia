using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Events;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
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
/// all linked children are soft-deleted in the SAME DB transaction.
///
/// Atomicity: the enclosing <c>UnitOfWorkBehavior</c> (Identity.Infrastructure) opens an
/// explicit EF Core transaction that wraps the entire handler execution and commits once after
/// the handler returns. All <c>UserManager.UpdateAsync</c> calls flush changes to the DB within
/// that transaction; the commit is deferred to the UoW. The handler must NOT open a second
/// transaction via <c>BeginTransactionAsync</c> — doing so causes a nested-transaction conflict
/// on the same Npgsql connection (the original P7-07 HIGH production bug: HTTP 500 on delete).
///
/// Post-commit side-effects (P7-07 Security High #1 fix):
///   Session revocations, AccountDeletedIntegrationEvent, and AdminActionPerformedEvent are
///   deferred to the <see cref="IIdentityDomainEventsBuffer"/> and fired by <c>UnitOfWorkBehavior</c>
///   ONLY after a successful commit. A rollback discards the scoped buffer — no phantom revocations,
///   no phantom integration events, no phantom audit rows (also resolves Low #4).
/// </summary>
public class DeleteAccountCommandHandler : BaseResponseHandler, ICommandHandler<DeleteAccountCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IIdentityDomainEventsBuffer _eventsBuffer;
    private readonly ILoggerManager _logger;

    public DeleteAccountCommandHandler(
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        IParentChildQuery parentChildQuery,
        IStringLocalizer<SharedResources> localizer,
        IIdentityDomainEventsBuffer eventsBuffer,
        ILoggerManager logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _parentChildQuery = parentChildQuery;
        _localizer = localizer;
        _eventsBuffer = eventsBuffer;
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

            // ── Soft-delete the primary account ──
            // No inner transaction: the UnitOfWorkBehavior (Identity.Infrastructure) already
            // opened an enclosing EF Core transaction before invoking this handler. Opening a
            // second transaction on the same IdentityModuleDbContext connection causes a
            // nested-transaction conflict in Npgsql that surfaces as HTTP 500 (root cause of
            // the P7-07 HIGH production bug). All UpdateAsync calls here participate in the
            // UoW's transaction; the UoW commits atomically after this handler returns.
            ApplyDeletedStatus(user, request.Reason, adminUserId, utcNow);

            var updateResult = await _service.UserManagmentService.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                // Throw so the UoW behavior's enclosing transaction is rolled back (not committed).
                // The outer catch returns ServerError; the UoW disposes the transaction without
                // CommitAsync → Npgsql auto-rolls back. Mirrors cascade-failure handling below.
                _logger.LogError(null, $"DeleteAccountCommandHandler: UpdateAsync failed for user {request.UserId}.");
                throw new InvalidOperationException($"UpdateAsync failed for user {request.UserId}.");
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
                        // Throw so the UoW's enclosing transaction is NOT committed on return.
                        // The UoW behavior calls CommitAsync only when next() returns normally;
                        // an exception causes the await-using to dispose the transaction (rollback),
                        // preventing the already-flushed parent delete from being committed.
                        // The outer catch logs and returns ServerError (generic, no ex.Message leak).
                        _logger.LogError(null, $"DeleteAccountCommandHandler: cascade UpdateAsync failed for child {childId} — rolling back via exception.");
                        throw new InvalidOperationException($"Cascade UpdateAsync failed for child {childId}.");
                    }
                }
            }

            _logger.LogInfo($"Account {request.UserId} staged for delete by admin {adminUserId}. " +
                            $"CascadeChildren={request.CascadeChildren}, affected children: [{string.Join(",", affectedChildIds)}].");

            // ── Enqueue post-commit domain event ──────────────────────────────
            // The UnitOfWorkBehavior drains this buffer AFTER CommitAsync.
            // On rollback the scoped buffer is discarded — no phantom revocations or events fire.
            // (P7-07 Security High #1 + Low #4 fix)
            var auditDetails = $"status=Deleted;cascadeChildren={affectedChildIds.Count}";
            _eventsBuffer.Add(new AccountDeletedDomainEvent(
                UserId: request.UserId,
                DeletedByAdminUserId: adminUserId,
                AffectedChildIds: affectedChildIds,
                AuditDetails: auditDetails));

            return Success<string>(_localizer[SharedResourcesKey.AccountDeletedSuccessfully]);
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
}
