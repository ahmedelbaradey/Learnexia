using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Commands.TransferAllocation;

/// <summary>
/// Handles <see cref="TransferAllocationCommand"/> (P10-16-BE-5).
///
/// <para><strong>Option C — handler is EF-free.</strong> All persistence, transaction, and
/// business-rule logic live in <see cref="IFamilyAllocationService.TransferAllocationAsync"/>.</para>
///
/// <para><strong>IDOR guard:</strong> <c>parentUserId</c> always resolved from the JWT via
/// <see cref="ICurrentUserService"/>. Never from the request body.</para>
///
/// <para><strong>Covers both use cases:</strong>
/// (a) rebalancing between children who both have existing allocations;
/// (b) allocating to a newly active mid-cycle child who has zero allocation.
/// The service handles both uniformly.</para>
/// </summary>
public sealed class TransferAllocationCommandHandler
    : BaseResponseHandler,
      ICommandHandler<TransferAllocationCommand, BaseResponse<TransferResultDto>>
{
    private readonly IFamilyAllocationService _allocationService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public TransferAllocationCommandHandler(
        IFamilyAllocationService allocationService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _allocationService = allocationService;
        _currentUser       = currentUser;
        _logger            = logger;
        _localizer         = localizer;
    }

    public async Task<BaseResponse<TransferResultDto>> Handle(
        TransferAllocationCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // IDOR guard: parentUserId always from JWT.
            var parentUserId = _currentUser.UserId;
            if (parentUserId is null)
                return Unauthorized<TransferResultDto>(
                    _localizer[SharedResourcesKey.UnauthorizedAccess]);

            var result = await _allocationService.TransferAllocationAsync(
                parentUserId  : parentUserId.Value,
                fromChildId   : request.FromChildId,
                toChildId     : request.ToChildId,
                amount        : request.Amount,
                idempotencyKey: request.IdempotencyKey,
                ct            : cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogInfo(
                    $"TransferAllocationCommand: failed for parentId={parentUserId}, reason={result.FailureReason}.");

                return result.FailureReason switch
                {
                    TransferAllocationFailureReason.ChildNotInFamily =>
                        new BaseResponse<TransferResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.Forbidden,
                            Message    = _localizer[SharedResourcesKey.AllocationTransferCrossFamily],
                        },
                    TransferAllocationFailureReason.AmountExceedsRemaining =>
                        new BaseResponse<TransferResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                            Message    = _localizer[SharedResourcesKey.AllocationTransferExceedsRemaining],
                        },
                    TransferAllocationFailureReason.SameSourceAndDestination =>
                        new BaseResponse<TransferResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                            Message    = _localizer[SharedResourcesKey.AllocationTransferSameChild],
                        },
                    TransferAllocationFailureReason.DestinationHasNoActiveSeat =>
                        new BaseResponse<TransferResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                            Message    = _localizer[SharedResourcesKey.AllocationTransferDestinationNoSeat],
                        },
                    TransferAllocationFailureReason.SourceHasNoActiveSeat =>
                        new BaseResponse<TransferResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                            Message    = _localizer[SharedResourcesKey.AllocationTransferDestinationNoSeat],
                        },
                    TransferAllocationFailureReason.WalletNotFound =>
                        NotFound<TransferResultDto>(_localizer[SharedResourcesKey.FamilyEnergyWalletNotFound]),
                    _ => ServerError<TransferResultDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]),
                };
            }

            _logger.LogInfo(
                $"TransferAllocationCommand: transferred {request.Amount} from childId={request.FromChildId} " +
                $"to childId={request.ToChildId} for parentId={parentUserId}.");

            return new BaseResponse<TransferResultDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.AllocationTransferSucceeded],
                Data       = result.Data,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error in TransferAllocationCommand for parentId={_currentUser.UserId}.");
            return ServerError<TransferResultDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}
