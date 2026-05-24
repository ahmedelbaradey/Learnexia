using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.UnlinkChild;

// NEW for P2-12. Removes the (parent, child) link with two guards:
//   1. Family-scope / anti-enumeration: if the caller is not linked to the child, return a generic 404
//      (NotFound) — never disclose whether the child exists or belongs to another family.
//   2. Last-parent guard: block the unlink if it would drop the child's parent count to 0, so a minor is
//      never left with no guardian. Returns a clear BadRequest failure.
public class UnlinkChildCommandHandler : BaseResponseHandler, ICommandHandler<UnlinkChildCommand, BaseResponse<bool>>
{
    private readonly ILinkParentStudentService _linkService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public UnlinkChildCommandHandler(
        ILinkParentStudentService linkService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _linkService = linkService;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<bool>> Handle(UnlinkChildCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<bool>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Family-scope: only act on a child the caller is actually linked to. A non-linked / other-parent
            // child collapses to a generic 404 (anti-enumeration) — same shape regardless of existence.
            var isLinked = await _linkService.IsLinkedAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isLinked)
            {
                _logger.LogWarn($"Unlink-Child rejected: parent {parentId} is not linked to child {request.ChildId}.");
                return NotFound<bool>(_localizer[SharedResourcesKey.CannotEditChildNotInFamily]);
            }

            // Last-parent guard + TOCTOU fix: the count-check and delete are performed atomically inside a
            // REPEATABLE READ transaction (UnlinkIfNotLastParentAsync) so two concurrent unlinks cannot both
            // pass the guard and orphan the child. Returns false when blocked (only 1 parent left).
            var unlinked = await _linkService.UnlinkIfNotLastParentAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!unlinked)
            {
                _logger.LogWarn($"Unlink-Child blocked: would leave child {request.ChildId} with no parent (parent {parentId}).");
                return BadRequest<bool>(_localizer[SharedResourcesKey.CannotUnlinkLastParent]);
            }

            _logger.LogInfo($"Unlinked child {request.ChildId} from parent {parentId}.");
            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UnlinkChildCommand");
            return ServerError<bool>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}
