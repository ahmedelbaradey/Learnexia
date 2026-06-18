using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.ChildAccess.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.ChildAccess.Queries.GetChildAccessStatus;

/// <summary>
/// Handles <see cref="GetChildAccessStatusQuery"/> (P10-18-BE-6).
///
/// <para><strong>Option C — handler is EF-free.</strong> All persistence logic lives in
/// <see cref="IPauseChildService.GetChildAccessStatusAsync"/>. This handler resolves the parent
/// identity from the JWT and delegates to the service.</para>
/// </summary>
public sealed class GetChildAccessStatusQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetChildAccessStatusQuery, BaseResponse<ChildAccessStatusDto>>
{
    private readonly IPauseChildService _pauseService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetChildAccessStatusQueryHandler(
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

    public async Task<BaseResponse<ChildAccessStatusDto>> Handle(
        GetChildAccessStatusQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Queries are not auto-validated — validate inside the handler.
            if (request.ChildId <= 0)
            {
                return new BaseResponse<ChildAccessStatusDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.BadRequest,
                    Message    = _localizer[SharedResourcesKey.EmptyIdValidation],
                };
            }

            var parentId = _currentUser.UserId
                ?? throw new InvalidOperationException("Parent JWT claim missing.");

            var result = await _pauseService.GetChildAccessStatusAsync(parentId, request.ChildId, cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogWarn(
                    $"GetChildAccessStatusQuery: IDOR rejection for parentId={parentId}, childId={request.ChildId}.");

                return new BaseResponse<ChildAccessStatusDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.Forbidden,
                    Message    = _localizer[SharedResourcesKey.ChildNotOwnedByParent],
                };
            }

            _logger.LogInfo(
                $"GetChildAccessStatusQuery: retrieved for parentId={parentId}, childId={request.ChildId}, " +
                $"seatState={result.Data!.SeatState}, pauseState={result.Data.ParentPauseState}.");

            return new BaseResponse<ChildAccessStatusDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.ChildAccessStatusRetrieved],
                Data       = result.Data,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GetChildAccessStatusQuery for parentId={_currentUser.UserId}, childId={request.ChildId}.");
            return ServerError<ChildAccessStatusDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}
