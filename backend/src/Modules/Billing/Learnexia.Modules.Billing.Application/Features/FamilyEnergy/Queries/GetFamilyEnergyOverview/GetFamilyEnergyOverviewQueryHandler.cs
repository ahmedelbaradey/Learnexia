using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Queries.GetFamilyEnergyOverview;

/// <summary>
/// Handler for <see cref="GetFamilyEnergyOverviewQuery"/> (P10-13-BE-10).
///
/// <para>IDOR guard: <see cref="ICurrentUserService.UserId"/> is the owning parent's JWT-resolved id.
/// The handler verifies the JWT user matches the requested parent before delegating to
/// <see cref="IFamilyEnergyQueryService"/> (Option C — no EF in Application).</para>
///
/// <para>Queries do NOT go through ValidationBehavior (CONVENTIONS rule 4).</para>
/// </summary>
public sealed class GetFamilyEnergyOverviewQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetFamilyEnergyOverviewQuery, BaseResponse<FamilyEnergyOverviewDto>>
{
    private readonly IFamilyEnergyQueryService _queryService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetFamilyEnergyOverviewQueryHandler(
        IFamilyEnergyQueryService queryService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _queryService = queryService;
        _currentUser  = currentUser;
        _logger       = logger;
        _localizer    = localizer;
    }

    public async Task<BaseResponse<FamilyEnergyOverviewDto>> Handle(
        GetFamilyEnergyOverviewQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // IDOR guard: parentUserId always from JWT.
            var parentUserId = _currentUser.UserId;
            if (parentUserId is null)
                return Unauthorized<FamilyEnergyOverviewDto>(
                    _localizer[SharedResourcesKey.UnauthorizedAccess]);

            var overview = await _queryService.GetOverviewAsync(parentUserId.Value, cancellationToken);

            if (overview is null)
                return NotFound<FamilyEnergyOverviewDto>(
                    _localizer[SharedResourcesKey.FamilyEnergyWalletNotFound]);

            return Success(overview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"GetFamilyEnergyOverviewQueryHandler: error for parentUserId={_currentUser.UserId}");
            return ServerError<FamilyEnergyOverviewDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
