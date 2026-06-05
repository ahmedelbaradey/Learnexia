using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.ChangeChildLearningLanguage;

/// <summary>
/// Handles <see cref="ChangeLearningLanguageCommand"/>.
///
/// Guard order (locked decisions §3 / §4):
///   1. Confirm-gate: <c>ConfirmFreshStart == false</c> → <c>BusinessValidation</c> (424).
///      Fires BEFORE the link query and BEFORE any mutation or publish.
///   2. Family-scope: parent id from JWT + <c>IsLinkedAsync</c> → 403 if not own child.
///   3. Seam call: <see cref="IChildAccountService.ChangeLearningLanguageAsync"/> — Identity
///      commits immediately; best-effort publishes <c>LearningLanguageChangedIntegrationEvent</c>.
///   4. Same-language no-op: seam returns <c>IsNoOp = true</c> → success returned, no event,
///      no reset (locked decision §3: same-language request is a benign no-op success).
///
/// The acting parent id is ALWAYS resolved from the JWT via <see cref="ICurrentUserService"/>
/// — NEVER from the request body (IDOR rule, mirrors UpdateChildCommandHandler).
/// </summary>
public class ChangeLearningLanguageCommandHandler
    : BaseResponseHandler,
      ICommandHandler<ChangeLearningLanguageCommand, BaseResponse<ChangedLearningLanguageResponse>>
{
    private readonly IChildAccountService _childAccountService;
    private readonly ILinkParentStudentService _linkService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public ChangeLearningLanguageCommandHandler(
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

    public async Task<BaseResponse<ChangedLearningLanguageResponse>> Handle(
        ChangeLearningLanguageCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Guard 1 — Confirm-gate (locked decision §4): FIRST check, before link query or mutation.
            // A missing or false flag returns 424 FailedDependency; no state is changed.
            if (!request.ConfirmFreshStart)
            {
                _logger.LogWarn("Change-Learning-Language rejected: ConfirmFreshStart was false.");
                return BusinessValidation<ChangedLearningLanguageResponse>(
                    _localizer[SharedResourcesKey.ConfirmFreshStartRequired]);
            }

            // Guard 2 — Resolve parent from JWT; null → Unauthorized.
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<ChangedLearningLanguageResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Guard 3 — Family-scope IDOR: parent may change language ONLY for their own child.
            var isLinked = await _linkService.IsLinkedAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isLinked)
            {
                _logger.LogWarn($"Change-Learning-Language forbidden: parent {parentId} is not linked to child {request.ChildId}.");
                return Forbidden<ChangedLearningLanguageResponse>(_localizer[SharedResourcesKey.CannotEditChildNotInFamily]);
            }

            // Step 4 — Delegate to the Identity seam. The seam handles:
            //   • NotFound (child doesn't exist)  → mapped to Forbidden (no enumeration)
            //   • Same-language no-op             → IsNoOp = true; return success without reset
            //   • UpdateFailed / LanguageUpdateFailed → BadRequest
            //   • Success                         → publishes LearningLanguageChangedIntegrationEvent
            //                                        (best-effort, after commit)
            var changeResult = await _childAccountService.ChangeLearningLanguageAsync(
                request.ChildId,
                request.NewLearningLanguage,
                cancellationToken);

            if (!changeResult.Succeeded)
            {
                _logger.LogError(null, $"Change-Learning-Language failed for parent {parentId}, child {request.ChildId}: {changeResult.ErrorCode}.");
                return changeResult.ErrorCode switch
                {
                    ChildAccountError.NotFound =>
                        Forbidden<ChangedLearningLanguageResponse>(_localizer[SharedResourcesKey.CannotEditChildNotInFamily]),
                    ChildAccountError.LanguageUpdateFailed =>
                        BadRequest<ChangedLearningLanguageResponse>(_localizer[SharedResourcesKey.LearningLanguageUpdateFailed]),
                    _ =>
                        ServerError<ChangedLearningLanguageResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]),
                };
            }

            _logger.LogInfo($"Change-Learning-Language succeeded for parent {parentId}, child {request.ChildId}: '{changeResult.OldLanguage}' → '{changeResult.NewLanguage}' (noOp={changeResult.IsNoOp}).");

            var successResponse = Success(new ChangedLearningLanguageResponse
            {
                ChildId = changeResult.ChildUserId,
                NewLearningLanguage = changeResult.NewLanguage,
            });
            successResponse.Message = _localizer[SharedResourcesKey.LearningLanguageChangedSuccessfully];
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChangeLearningLanguageCommand");
            return ServerError<ChangedLearningLanguageResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}
