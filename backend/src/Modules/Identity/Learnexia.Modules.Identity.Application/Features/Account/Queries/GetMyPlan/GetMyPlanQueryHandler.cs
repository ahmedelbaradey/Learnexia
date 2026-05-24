using Learnexia.Modules.Identity.Application.Features.Account.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Account.Queries.GetMyPlan;

/// <summary>
/// Returns the caller's subscription plan stub (P2-12 PLAN). No DB hit — hardcoded Free tier until the
/// payments module exists. Requires authentication (self-scope) but returns the same value for every user.
/// </summary>
/// <remarks>
/// TODO P2-12-PAYMENTS: replace hardcoded stub with a real plan lookup once the payments module exists.
/// </remarks>
public sealed class GetMyPlanQueryHandler
    : BaseResponseHandler, IQueryHandler<GetMyPlanQuery, BaseResponse<CurrentPlanResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    // Plan-name and status constants for the Free-tier stub.
    // TODO P2-12-PAYMENTS: replace with enum / real plan entity lookup.
    private const string FreePlanName = "Free";
    private const string ActiveStatus = "Active";

    public GetMyPlanQueryHandler(
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _currentUserService = currentUserService;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<CurrentPlanResponse>> Handle(GetMyPlanQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<CurrentPlanResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // No DB hit — stub returns the hardcoded Free tier for every authenticated user.
            var plan = new CurrentPlanResponse
            {
                PlanName = FreePlanName,
                Status = ActiveStatus,
            };

            return await Task.FromResult(Success(plan));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetMyPlanQuery");
            return ServerError<CurrentPlanResponse>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
