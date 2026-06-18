using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Queries.GetFamilyAllocation;

/// <summary>
/// Handler for <see cref="GetFamilyAllocationQuery"/> (P10-16-BE-4).
///
/// <para><strong>Option C — handler is EF-free.</strong> All query logic lives in
/// <see cref="IFamilyAllocationService.GetFamilyAllocationAsync"/>.</para>
///
/// <para><strong>IDOR guard:</strong> <c>parentUserId</c> always resolved from the JWT via
/// <see cref="ICurrentUserService"/>. Never from the request body or URL.</para>
///
/// <para>The response includes ALL active-seat children — including mid-cycle-added children who
/// have no <c>ChildEnergyAllocation</c> row for the current cycle. Those children appear with
/// <c>Allocated=0, Spent=0, Remaining=0, IsMidCycleNoGrant=true</c>.</para>
/// </summary>
public sealed class GetFamilyAllocationQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetFamilyAllocationQuery, BaseResponse<FamilyAllocationDto>>
{
    private readonly IFamilyAllocationService _allocationService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetFamilyAllocationQueryHandler(
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

    public async Task<BaseResponse<FamilyAllocationDto>> Handle(
        GetFamilyAllocationQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // IDOR guard: parentUserId always from JWT.
            var parentUserId = _currentUser.UserId;
            if (parentUserId is null)
                return Unauthorized<FamilyAllocationDto>(
                    _localizer[SharedResourcesKey.UnauthorizedAccess]);

            var dto = await _allocationService.GetFamilyAllocationAsync(parentUserId.Value, cancellationToken);

            if (dto is null)
                return NotFound<FamilyAllocationDto>(
                    _localizer[SharedResourcesKey.FamilyEnergyWalletNotFound]);

            return Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"GetFamilyAllocationQueryHandler: error for parentUserId={_currentUser.UserId}.");
            return ServerError<FamilyAllocationDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
