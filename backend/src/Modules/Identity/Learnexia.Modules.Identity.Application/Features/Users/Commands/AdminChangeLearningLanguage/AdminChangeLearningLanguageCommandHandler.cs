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

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.AdminChangeLearningLanguage;

/// <summary>
/// Handles <see cref="AdminChangeLearningLanguageCommand"/> (P7-08 BE-6).
///
/// Mirrors the parent <c>ChangeLearningLanguageCommandHandler</c> guard order exactly,
/// replacing the family-link IDOR check with the class-level AdminOnly policy on the controller.
///
/// Guard order (locked per P7-08 plan — MUST NOT be changed):
///   1. Confirm-gate FIRST: <c>ConfirmFreshStart == false</c> → <c>BusinessValidation</c> (424)
///      BEFORE any DB call, mutation, or publish. A failed confirm never triggers reset.
///   2. Resolve target user; missing → NotFound.
///   3. Validate user holds Student role → not a child → BadRequest.
///   4. Call <c>IChildAccountService.ChangeLearningLanguageAsync</c>.
///      The seam handles: same-language no-op (IsNoOp = true), commit, and best-effort
///      <c>LearningLanguageChangedIntegrationEvent</c> publish. The Learning consumer
///      (<c>LearningLanguageChangedIntegrationEventHandler</c>) then hard-deletes Math/Science
///      attempts — the same consumer fires regardless of whether origin is parent or admin.
///   5. Map seam error codes to appropriate BaseResponse status codes.
///   6. Best-effort publish <c>AdminActionPerformedEvent(ChildLearningLanguageChanged)</c>.
///
/// Reused seams:
///   - <c>IChildAccountService.ChangeLearningLanguageAsync</c> — Identity seam; unchanged.
///   - <c>LearningLanguageChangedIntegrationEvent</c> + its Learning consumer — unchanged.
///   - 424 confirm-gate pattern — same as parent P8-04 path.
///
/// Not reused (admin-specific):
///   - No <c>IsLinkedAsync</c> family-scope check (admin acts on any child).
///   - AdminOnly authorization vs parent JWT.
///
/// Token staleness note (Q-C3): <c>learning_language</c> is in the JWT. The new value lands on
/// the child's NEXT token (refresh/sign-in). This is the same bounded-staleness as P8-04.
/// </summary>
public class AdminChangeLearningLanguageCommandHandler
    : BaseResponseHandler,
      ICommandHandler<AdminChangeLearningLanguageCommand, BaseResponse<AdminChangedLearningLanguageResponseDto>>
{
    private readonly IChildAccountService _childAccountService;
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public AdminChangeLearningLanguageCommandHandler(
        IChildAccountService childAccountService,
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        IPublisher publisher,
        ILoggerManager logger)
    {
        _childAccountService = childAccountService;
        _service = service;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<BaseResponse<AdminChangedLearningLanguageResponseDto>> Handle(
        AdminChangeLearningLanguageCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var adminUserId = _currentUserService.UserId.GetValueOrDefault();

            // Guard 1 — Confirm-gate FIRST (LOCKED order — mirrors parent handler exactly).
            // A false/absent flag returns 424 FailedDependency; NO state is changed.
            // This fires BEFORE any DB lookup or mutation (security rule: the destructive
            // Math/Science reset must never run on an unconfirmed request).
            if (!request.ConfirmFreshStart)
            {
                _logger.LogWarn(
                    $"AdminChangeLearningLanguageCommandHandler: rejected — ConfirmFreshStart was false " +
                    $"(childId={request.ChildId}, adminId={adminUserId}).");
                return BusinessValidation<AdminChangedLearningLanguageResponseDto>(
                    _localizer[SharedResourcesKey.ConfirmFreshStartRequired]);
            }

            // Guard 2 — Resolve target user.
            var child = await _service.UserManagmentService.FindByIdWithRolesAsync(request.ChildId.ToString());
            if (child is null)
                return NotFound<AdminChangedLearningLanguageResponseDto>(_localizer[SharedResourcesKey.UserNotFound]);

            // Guard 3 — Validate this is a child/student account.
            var roles = await _service.UserManagmentService.GetUserRolesAsync(child);
            if (!roles.Any(r => string.Equals(r, Roles.Student.ToString(), StringComparison.OrdinalIgnoreCase)))
                return BadRequest<AdminChangedLearningLanguageResponseDto>(_localizer[SharedResourcesKey.TargetUserIsNotAChild]);

            // Step 4 — Delegate to the Identity seam.
            // The seam handles: NotFound, same-language no-op (IsNoOp=true), commit,
            // and best-effort LearningLanguageChangedIntegrationEvent publish.
            // The admin path does NOT call IsLinkedAsync (no family-link restriction).
            var changeResult = await _childAccountService.ChangeLearningLanguageAsync(
                request.ChildId,
                request.LearningLanguage,
                cancellationToken);

            // Step 5 — Map seam error codes.
            if (!changeResult.Succeeded)
            {
                _logger.LogError(null,
                    $"AdminChangeLearningLanguageCommandHandler: seam failed for child " +
                    $"{request.ChildId}: {changeResult.ErrorCode}.");

                return changeResult.ErrorCode switch
                {
                    ChildAccountError.NotFound =>
                        NotFound<AdminChangedLearningLanguageResponseDto>(_localizer[SharedResourcesKey.UserNotFound]),
                    ChildAccountError.LanguageUpdateFailed =>
                        BadRequest<AdminChangedLearningLanguageResponseDto>(_localizer[SharedResourcesKey.LearningLanguageUpdateFailed]),
                    _ =>
                        ServerError<AdminChangedLearningLanguageResponseDto>(_localizer[SharedResourcesKey.ChildAdminOperationSystemError]),
                };
            }

            _logger.LogInfo(
                $"AdminChangeLearningLanguageCommandHandler: child {request.ChildId} learning language " +
                $"changed from '{changeResult.OldLanguage}' to '{changeResult.NewLanguage}' " +
                $"by admin {adminUserId} (noOp={changeResult.IsNoOp}).");

            // Step 6 — Best-effort audit event post-seam (only ids + language codes, no PII).
            await PublishAuditEventAsync(
                adminUserId, request.ChildId, changeResult.OldLanguage, changeResult.NewLanguage, cancellationToken);

            var successResponse = Success(new AdminChangedLearningLanguageResponseDto(
                ChildId: changeResult.ChildUserId,
                NewLearningLanguage: changeResult.NewLanguage));

            successResponse.Message = _localizer[SharedResourcesKey.LearningLanguageChangedSuccessfully];
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in AdminChangeLearningLanguageCommandHandler for child {request.ChildId}");
            return ServerError<AdminChangedLearningLanguageResponseDto>(_localizer[SharedResourcesKey.ChildAdminOperationSystemError]);
        }
    }

    // ── Audit helper ───────────────────────────────────────────────────────────

    private async Task PublishAuditEventAsync(
        int adminUserId, int childId, string oldLang, string newLang, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(
                new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: adminUserId,
                    Action: AdminActions.ChildLearningLanguageChanged,
                    TargetEntityType: "User",
                    TargetEntityId: childId,
                    Details: $"oldLang={oldLang};newLang={newLang}"),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish AdminActionPerformedEvent(ChildLearningLanguageChanged) — ignored (best-effort).");
        }
    }
}
