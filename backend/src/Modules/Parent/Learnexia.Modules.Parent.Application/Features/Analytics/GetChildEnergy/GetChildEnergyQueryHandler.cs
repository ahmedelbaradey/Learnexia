using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildEnergy;

/// <summary>
/// E7 — GET /Parent/Children/{id}/Energy
///
/// Pure-read: consumes <see cref="IChildEnergyUsageQuery"/> (P5-08-BE-4) which is explicitly
/// write-free (no bootstrap-on-read side effect). A child with no billing data returns zeroed
/// fields — not an error (AC3).
/// </summary>
public class GetChildEnergyQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetChildEnergyQuery, BaseResponse<ChildEnergyDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IChildEnergyUsageQuery _energyUsage;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetChildEnergyQueryHandler(
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IChildEnergyUsageQuery energyUsage,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser = currentUser;
        _parentChildQuery = parentChildQuery;
        _energyUsage = energyUsage;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<ChildEnergyDto>> Handle(
        GetChildEnergyQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.ChildId <= 0)
                return BadRequest<ChildEnergyDto>(_localizer[SharedResourcesKey.ChildIdMustBePositive]);

            var parentId = _currentUser.UserId;
            if (parentId is null)
                return Unauthorized<ChildEnergyDto>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var isOwned = await _parentChildQuery.IsParentOfChildAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isOwned)
                return Forbidden<ChildEnergyDto>(_localizer[SharedResourcesKey.ParentChildNotAuthorized]);

            var now     = DateTime.UtcNow;
            var weekAgo = now.AddDays(-7);

            ChildEnergySnapshot? snapshot = null;
            try
            {
                snapshot = await _energyUsage.GetSnapshotAsync(request.ChildId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IChildEnergyUsageQuery.GetSnapshotAsync failed for childId={request.ChildId}");
            }

            IReadOnlyList<EnergyUsageByKind> weeklyUsage = [];
            try
            {
                weeklyUsage = await _energyUsage.GetUsageByKindAsync(request.ChildId, weekAgo, now, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IChildEnergyUsageQuery.GetUsageByKindAsync failed for childId={request.ChildId}");
            }

            var dto = new ChildEnergyDto
            {
                Remaining        = snapshot?.Remaining        ?? 0,
                Allocated        = snapshot?.Allocated        ?? 0,
                Spent            = snapshot?.Spent            ?? 0,
                PurchasedBalance = snapshot?.PurchasedBalance ?? 0,
                CycleEndsAtUtc   = snapshot?.CycleEndsAtUtc,
                WeeklyUsage = weeklyUsage
                    .Select(u => new EnergyUsageItemDto { Kind = u.Kind, Count = u.Count })
                    .ToList(),
            };

            _logger.LogInfo($"Energy retrieved for childId={request.ChildId} by parentId={parentId}");
            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.EnergyRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetChildEnergyQueryHandler");
            return ServerError<ChildEnergyDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
