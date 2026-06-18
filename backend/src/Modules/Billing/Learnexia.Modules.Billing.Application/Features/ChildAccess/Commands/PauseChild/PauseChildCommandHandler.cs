using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.ChildAccess.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.ChildAccess.Commands.PauseChild;

/// <summary>
/// Handles <see cref="PauseChildCommand"/> (P10-18-BE-6).
///
/// <para><strong>Option C — handler is EF-free.</strong> All persistence and transaction logic
/// live in <see cref="IPauseChildService.PauseChildAsync"/>. This handler resolves the parent
/// identity from the JWT and delegates to the service.</para>
/// </summary>
public sealed class PauseChildCommandHandler
    : BaseResponseHandler,
      ICommandHandler<PauseChildCommand, BaseResponse<PauseResultDto>>
{
    private readonly IPauseChildService _pauseService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public PauseChildCommandHandler(
        IPauseChildService pauseService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _pauseService = pauseService;
        _currentUser  = currentUser;
        _logger       = logger;
        _localizer    = localizer;
    }

    public async Task<BaseResponse<PauseResultDto>> Handle(
        PauseChildCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUser.UserId
                ?? throw new InvalidOperationException("Parent JWT claim missing.");

            var result = await _pauseService.PauseChildAsync(parentId, request.ChildId, cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogWarn(
                    $"PauseChildCommand: failed for parentId={parentId}, childId={request.ChildId}, reason={result.FailureReason}.");

                return new BaseResponse<PauseResultDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.Forbidden,
                    Message    = _localizer[SharedResourcesKey.ChildNotOwnedByParent],
                };
            }

            var messageKey = result.Data!.WasNoOp
                ? SharedResourcesKey.ChildAlreadyPaused
                : SharedResourcesKey.ChildAccessPausedSuccessfully;

            _logger.LogInfo(
                $"PauseChildCommand: success for parentId={parentId}, childId={request.ChildId}, noOp={result.Data.WasNoOp}.");

            return new BaseResponse<PauseResultDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[messageKey],
                Data       = result.Data,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in PauseChildCommand for parentId={_currentUser.UserId}, childId={request.ChildId}.");
            return ServerError<PauseResultDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}
