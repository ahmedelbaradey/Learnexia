using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.EnergyStatus.Dtos;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.EnergyStatus.Queries.GetEnergyStatus;

/// <summary>
/// Thin handler (Option C): IDOR scoping lives here; energy-status computation delegates to
/// <see cref="ICreditLedgerService.GetEnergyStatusAsync"/>. The daily-cap reset is lazy
/// (read-only path — no DB write here).
/// </summary>
public sealed class EnergyStatusQueryHandler
    : BaseResponseHandler,
      IQueryHandler<EnergyStatusQuery, BaseResponse<EnergyStatusDto>>
{
    private readonly ICreditLedgerService _ledger;
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EnergyStatusQueryHandler(
        ICreditLedgerService ledger,
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IGlobalSettingsProvider settings,
        ISystemClock clock,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _ledger           = ledger;
        _currentUser      = currentUser;
        _parentChildQuery = parentChildQuery;
        _settings         = settings;
        _clock            = clock;
        _logger           = logger;
        _localizer        = localizer;
    }

    public async Task<BaseResponse<EnergyStatusDto>> Handle(EnergyStatusQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // ── IDOR scoping ───────────────────────────────────────────────────────────
            var callerId = _currentUser.UserId;
            if (callerId is null)
                return Unauthorized<EnergyStatusDto>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var callerRoles = _currentUser.Roles;
            var isAdmin = callerRoles.Any(r =>
                string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase));

            if (!isAdmin)
            {
                var isOwner = callerId.Value == request.ChildId;
                if (!isOwner)
                {
                    var isParent = await _parentChildQuery.IsParentOfChildAsync(callerId.Value, request.ChildId, cancellationToken);
                    if (!isParent)
                        return Forbidden<EnergyStatusDto>(_localizer[SharedResourcesKey.NotAuthorizedForChild]);
                }
            }

            // ── Delegate all DB access to the service ──────────────────────────────────
            var dto = await _ledger.GetEnergyStatusAsync(request.ChildId, _settings, _clock, cancellationToken);

            return Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in EnergyStatusQuery for childId={request.ChildId}");
            return ServerError<EnergyStatusDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
