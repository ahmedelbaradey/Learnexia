using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.ReactivateAccount;

/// <summary>
/// Handles <see cref="ReactivateAccountCommand"/> (P7-07 BE-3).
///
/// State machine: Suspended → Active.
/// Rejected if the target is Deleted (terminal — a deleted account cannot be reactivated
/// via this path per lead decision; deletion is a terminal/irreversible governance state).
///
/// Reactivation restores IsActive = true so the existing SignInCommandHandler IsActive gate
/// allows sign-in again. No new sessions/tokens are issued here — the user must sign in fresh.
/// </summary>
public class ReactivateAccountCommandHandler : BaseResponseHandler, ICommandHandler<ReactivateAccountCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public ReactivateAccountCommandHandler(
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        IPublisher publisher,
        ILoggerManager logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(ReactivateAccountCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var adminUserId = _currentUserService.UserId.GetValueOrDefault();

            var user = await _service.UserManagmentService.FindByIdAsync(request.UserId.ToString());
            if (user is null)
                return NotFound<string>(_localizer[SharedResourcesKey.UserNotFound]);

            // State-machine guard: Deleted is terminal — cannot be reactivated.
            if (user.AccountStatus == AccountStatus.Deleted)
                return BadRequest<string>(_localizer[SharedResourcesKey.CannotReactivateDeletedAccount]);

            // State-machine guard (Finding #8 — idempotency): already-Active → reject.
            // Previously this was a silent no-op that still mutated and published, creating
            // phantom audit entries. Now it returns a clear failure (Successed=false, 400).
            if (user.AccountStatus == AccountStatus.Active)
                return BadRequest<string>(_localizer[SharedResourcesKey.AccountAlreadyActive]);

            // State-machine: apply Suspended → Active transition.
            user.AccountStatus = AccountStatus.Active;
            user.IsActive = true;
            user.LastStatusReason = request.Reason;
            user.StatusChangedBy = adminUserId;
            user.StatusChangedAtUtc = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = adminUserId;

            var updateResult = await _service.UserManagmentService.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError(null, $"ReactivateAccountCommandHandler: UpdateAsync failed for user {request.UserId}.");
                return BadRequest<string>(_localizer[SharedResourcesKey.AccountLifecycleSystemError]);
            }

            _logger.LogInfo($"Account {request.UserId} reactivated by admin {adminUserId}.");

            // Best-effort post-mutation event publish.
            await PublishReactivatedEventAsync(request.UserId, adminUserId, cancellationToken);
            await PublishAuditEventAsync(adminUserId, AdminActions.AccountReactivated, request.UserId,
                $"status=Active", cancellationToken);

            return Success<string>(_localizer[SharedResourcesKey.AccountReactivatedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in ReactivateAccountCommandHandler for user {request.UserId}");
            return ServerError<string>(_localizer[SharedResourcesKey.AccountLifecycleSystemError]);
        }
    }

    private async Task PublishReactivatedEventAsync(
        int userId, int adminUserId, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(
                new AccountReactivatedIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredOnUtc: DateTime.UtcNow,
                    UserId: userId,
                    ReactivatedByAdminUserId: adminUserId),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to publish AccountReactivatedIntegrationEvent for user {userId} — ignored (best-effort).");
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
