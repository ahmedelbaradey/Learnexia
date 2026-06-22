using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.TransitionChildGrade;

/// <summary>
/// Handles <see cref="TransitionChildGradeCommand"/> (P5-06).
///
/// Guard order (mirrors <c>ChangeLearningLanguageCommandHandler</c> and <c>UpdateChildCommandHandler</c>):
///   1. Resolve parent id from JWT → <c>Unauthorized</c> if null.
///   2. Family-scope IDOR: <c>ILinkParentStudentService.IsLinkedAsync(parentId, ChildId)</c>
///      → generic <c>Forbidden(CannotEditChildNotInFamily)</c> if not linked (anti-enumeration:
///      the same 403 is returned whether the child is missing OR owned by another family).
///   3. Delegate to <see cref="IChildAccountService.TransitionGradeAsync"/>:
///      - <c>NotFound</c>       → <c>Forbidden</c> (anti-enumeration — child not found ≡ not yours).
///      - <c>GradeUpdateFailed</c> → <c>BadRequest</c>.
///      - Other failure        → <c>ServerError</c>.
///      - <c>IsNoOp = true</c> → <c>Success</c> (idempotent same-grade; no event published).
///      - Success              → <c>Success</c> with old/new grade.
///
/// The acting parent id is ALWAYS resolved from the JWT via <see cref="ICurrentUserService"/>
/// — NEVER from the request body (IDOR rule).
/// </summary>
public class TransitionChildGradeCommandHandler
    : BaseResponseHandler,
      ICommandHandler<TransitionChildGradeCommand, BaseResponse<TransitionedChildGradeResponse>>
{
    private readonly IChildAccountService _childAccountService;
    private readonly ILinkParentStudentService _linkService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public TransitionChildGradeCommandHandler(
        IChildAccountService childAccountService,
        ILinkParentStudentService linkService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _childAccountService = childAccountService;
        _linkService = linkService;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<TransitionedChildGradeResponse>> Handle(
        TransitionChildGradeCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Guard 1 — Resolve parent from JWT; null → Unauthorized.
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<TransitionedChildGradeResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Guard 2 — Family-scope IDOR: parent may transition grade ONLY for their own child.
            // Returns the SAME generic 403 whether the child is missing or owned by another family
            // to prevent enumeration of child ids.
            var isLinked = await _linkService.IsLinkedAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isLinked)
            {
                _logger.LogWarn($"Transition-Child-Grade forbidden: parent {parentId} is not linked to child {request.ChildId}.");
                return Forbidden<TransitionedChildGradeResponse>(_localizer[SharedResourcesKey.CannotEditChildNotInFamily]);
            }

            // Step 3 — Delegate to the Identity seam. The seam handles:
            //   • NotFound (child doesn't exist) → mapped to Forbidden (anti-enumeration)
            //   • Same-grade no-op              → IsNoOp = true; return success without event
            //   • GradeUpdateFailed             → BadRequest
            //   • Success                       → publishes ChildGradeChangedIntegrationEvent
            //                                     + AdminActionPerformedEvent (best-effort)
            var gradeResult = await _childAccountService.TransitionGradeAsync(
                request.ChildId,
                request.TargetGrade,
                parentId.Value,
                cancellationToken);

            if (!gradeResult.Succeeded)
            {
                _logger.LogError(null,
                    $"Transition-Child-Grade failed for parent {parentId}, child {request.ChildId}: {gradeResult.ErrorCode}.");
                return gradeResult.ErrorCode switch
                {
                    ChildAccountError.NotFound =>
                        Forbidden<TransitionedChildGradeResponse>(_localizer[SharedResourcesKey.CannotEditChildNotInFamily]),
                    ChildAccountError.GradeUpdateFailed =>
                        BadRequest<TransitionedChildGradeResponse>(_localizer[SharedResourcesKey.ChildGradeUpdateFailed]),
                    _ =>
                        ServerError<TransitionedChildGradeResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]),
                };
            }

            _logger.LogInfo(
                $"Transition-Child-Grade succeeded for parent {parentId}, child {request.ChildId}: " +
                $"{gradeResult.OldGrade} → {gradeResult.NewGrade} (noOp={gradeResult.IsNoOp}).");

            var successResponse = Success(new TransitionedChildGradeResponse
            {
                ChildId = gradeResult.ChildUserId,
                OldGrade = gradeResult.OldGrade,
                NewGrade = gradeResult.NewGrade,
                IsNoOp = gradeResult.IsNoOp,
            });
            successResponse.Message = _localizer[SharedResourcesKey.ChildGradeTransitionedSuccessfully];
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TransitionChildGradeCommand");
            return ServerError<TransitionedChildGradeResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}
