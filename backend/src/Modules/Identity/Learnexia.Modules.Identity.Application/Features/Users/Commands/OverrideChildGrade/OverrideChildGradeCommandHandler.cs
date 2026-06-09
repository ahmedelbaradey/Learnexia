using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.OverrideChildGrade;

/// <summary>
/// Handles <see cref="OverrideChildGradeCommand"/> (P7-08 BE-3).
///
/// Non-destructive grade override: updates <c>User.Grade</c>, preserves all learning history
/// (XP, badges, streaks, mastery, attempts). Emits <c>ChildGradeChangedIntegrationEvent</c>
/// post-commit best-effort so consumers can re-scope curriculum reads.
///
/// Guard order:
///   1. Soft confirm-gate: <c>Confirm == false</c> → HTTP 400 (NOT 424 — override is non-destructive).
///   2. Resolve target user; missing → NotFound.
///   3. Validate user holds the Student role (not a parent/admin).
///   4. No-op guard: same grade → BadRequest (no phantom event emitted).
///   5. Mutate <c>User.Grade</c>, stamp actor/time, commit via UserManager.
///   6. Best-effort post-commit publish <c>ChildGradeChangedIntegrationEvent</c>.
///   7. Best-effort post-commit publish <c>AdminActionPerformedEvent(ChildGradeOverridden)</c>.
///
/// Grade-claim staleness note (Q-C3): Grade is NOT currently a JWT claim, so there is no
/// token-staleness concern for grade. The next student read/query automatically uses the new
/// grade value stored on the User row.
/// </summary>
public class OverrideChildGradeCommandHandler
    : BaseResponseHandler,
      ICommandHandler<OverrideChildGradeCommand, BaseResponse<ChildGradeOverrideResponseDto>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public OverrideChildGradeCommandHandler(
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

    public async Task<BaseResponse<ChildGradeOverrideResponseDto>> Handle(
        OverrideChildGradeCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var adminUserId = _currentUserService.UserId.GetValueOrDefault();

            // Guard 1 — Soft UX confirm-gate (HTTP 400, NOT 424 — this is non-destructive).
            // The locked plan decision: "grade override = re-scope + preserve; confirm is a soft
            // UX gate (200 with confirm flag), not a 424." (P7-user-account-wave.md)
            if (!request.Confirm)
                return BadRequest<ChildGradeOverrideResponseDto>(
                    _localizer[SharedResourcesKey.ChildGradeOverrideConfirmRequired]);

            // Guard 2 — Resolve target user.
            var child = await _service.UserManagmentService.FindByIdWithRolesAsync(request.ChildId.ToString());
            if (child is null)
                return NotFound<ChildGradeOverrideResponseDto>(_localizer[SharedResourcesKey.UserNotFound]);

            // Guard 3 — Validate this is a child/student account.
            var roles = await _service.UserManagmentService.GetUserRolesAsync(child);
            if (!roles.Any(r => string.Equals(r, Roles.Student.ToString(), StringComparison.OrdinalIgnoreCase)))
                return BadRequest<ChildGradeOverrideResponseDto>(_localizer[SharedResourcesKey.TargetUserIsNotAChild]);

            // Guard 4 — No-op guard: reject if grade unchanged (prevents phantom events).
            var oldGrade = child.Grade ?? 0;
            if (oldGrade == request.Grade)
                return BadRequest<ChildGradeOverrideResponseDto>(_localizer[SharedResourcesKey.ChildGradeUnchanged]);

            // Step 5 — Apply grade override. NON-DESTRUCTIVE: only User.Grade is changed.
            // XP, badges, streaks, mastery, and all Attempt rows are untouched.
            child.Grade = request.Grade;
            child.UpdatedAt = DateTime.UtcNow;
            child.UpdatedBy = adminUserId;

            var updateResult = await _service.UserManagmentService.UpdateAsync(child);
            if (!updateResult.Succeeded)
            {
                _logger.LogError(null, $"OverrideChildGradeCommandHandler: UpdateAsync failed for child {request.ChildId}.");
                return BadRequest<ChildGradeOverrideResponseDto>(_localizer[SharedResourcesKey.ChildAdminOperationSystemError]);
            }

            _logger.LogInfo(
                $"OverrideChildGradeCommandHandler: child {request.ChildId} grade changed " +
                $"from {oldGrade} to {request.Grade} by admin {adminUserId}.");

            // Step 6 — Best-effort post-commit publish ChildGradeChangedIntegrationEvent.
            // The event is NON-DESTRUCTIVE: the Learning consumer is a no-op stub because
            // Learning reads key off User.Grade at query time (Q-C2 finding — no persisted
            // grade-scoped state needs refreshing). The event is informational for P5-06 future use.
            await PublishGradeChangedEventAsync(request.ChildId, oldGrade, request.Grade, adminUserId, cancellationToken);

            // Step 7 — Best-effort audit event (only opaque ids + grade codes, no PII).
            await PublishAuditEventAsync(
                adminUserId, request.ChildId, oldGrade, request.Grade, cancellationToken);

            return Success(new ChildGradeOverrideResponseDto(
                ChildUserId: request.ChildId,
                OldGrade: oldGrade,
                NewGrade: request.Grade));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in OverrideChildGradeCommandHandler for child {request.ChildId}");
            return ServerError<ChildGradeOverrideResponseDto>(_localizer[SharedResourcesKey.ChildAdminOperationSystemError]);
        }
    }

    // ── Event helpers ─────────────────────────────────────────────────────────

    private async Task PublishGradeChangedEventAsync(
        int childId, int oldGrade, int newGrade, int adminUserId, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(
                new ChildGradeChangedIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredOnUtc: DateTime.UtcNow,
                    ChildUserId: childId,
                    OldGrade: oldGrade,
                    NewGrade: newGrade,
                    ChangedByAdminUserId: adminUserId),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Failed to publish ChildGradeChangedIntegrationEvent for child {childId} " +
                $"({oldGrade} → {newGrade}) — ignored (best-effort). Grade change is committed.");
        }
    }

    private async Task PublishAuditEventAsync(
        int adminUserId, int childId, int oldGrade, int newGrade, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(
                new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: adminUserId,
                    Action: AdminActions.ChildGradeOverridden,
                    TargetEntityType: "User",
                    TargetEntityId: childId,
                    Details: $"oldGrade={oldGrade};newGrade={newGrade}"),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish AdminActionPerformedEvent(ChildGradeOverridden) — ignored (best-effort).");
        }
    }
}
