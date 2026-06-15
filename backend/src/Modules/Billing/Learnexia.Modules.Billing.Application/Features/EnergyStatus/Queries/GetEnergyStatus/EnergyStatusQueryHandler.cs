using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.EnergyStatus.Dtos;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Application.Services;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.EnergyStatus.Queries.GetEnergyStatus;

/// <summary>
/// Handles <see cref="EnergyStatusQuery"/>.
///
/// <para><strong>IDOR scoping (mirrors <c>GetCreditAccountQueryHandler</c>):</strong>
/// <list type="bullet">
///   <item>Admin roles → allowed for any child.</item>
///   <item>Caller is the child themselves (student JWT) → allowed.</item>
///   <item>Caller is a parent → <see cref="IParentChildQuery.IsParentOfChildAsync"/> gate (anti-IDOR).</item>
///   <item>Any other caller → generic 403.</item>
/// </list>
/// </para>
///
/// <para><strong>Daily-usage lazy reset:</strong> if <c>DailyUsedDateLocal</c> is stale (not today in
/// the child's timezone), the handler treats <c>DailyUsed</c> as zero for the status computation
/// — it does NOT write back here (read-only path). The reset write only happens inside the
/// <c>SpendCreditCommandHandler</c> debit transaction.</para>
/// </summary>
public sealed class EnergyStatusQueryHandler
    : BaseResponseHandler,
      IQueryHandler<EnergyStatusQuery, BaseResponse<EnergyStatusDto>>
{
    private readonly IBillingDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EnergyStatusQueryHandler(
        IBillingDbContext db,
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IGlobalSettingsProvider settings,
        ISystemClock clock,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _db = db;
        _currentUser = currentUser;
        _parentChildQuery = parentChildQuery;
        _settings = settings;
        _clock = clock;
        _logger = logger;
        _localizer = localizer;
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

            // ── Load account (no-track — read-only path) ───────────────────────────────
            var account = await _db.CreditAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ChildId == request.ChildId, cancellationToken);

            // ── Caps from GlobalSettings ───────────────────────────────────────────────
            // Using Free-tier defaults until P10-05 subscription tier is available.
            // After P10-05 lands, swap to per-child tier resolution.
            var dailyCap = _settings.GetInt(GlobalSettingKeys.FreeDailyCap, 10);
            var monthlyAllotment = _settings.GetInt(GlobalSettingKeys.FreeMonthlyCredits, 100);

            if (account is null)
            {
                // No account yet — new child has zero balance and no warnings.
                return Success(new EnergyStatusDto
                {
                    GrantedBalance = 0,
                    PurchasedBalance = 0,
                    TotalBalance = 0,
                    GrantExpiresAtUtc = null,
                    DailyUsed = 0,
                    DailyCap = dailyCap,
                    DailyCapReached = false,
                    LowEnergy = false,
                    WarningState = WarningState.None,
                });
            }

            // ── Lazy daily-reset (read-side: treat DailyUsed as 0 when stale; no DB write here) ──
            var effectiveDailyUsed = DailyCapHelper.IsStale(account.DailyUsedDateLocal, account.ChildTimeZoneId, _clock)
                ? 0
                : account.DailyUsed;

            var dailyCapReached = effectiveDailyUsed >= dailyCap;
            var approaching = !dailyCapReached && effectiveDailyUsed >= (int)(dailyCap * 0.8);
            var total = account.TotalBalance;
            var lowThreshold = (int)Math.Ceiling(monthlyAllotment * 0.10);
            var lowEnergy = total > 0 && total <= lowThreshold;
            var empty = total == 0;

            // Highest-severity wins.
            var warningState = empty
                ? WarningState.Empty
                : dailyCapReached
                    ? WarningState.DailyCapReached
                    : lowEnergy
                        ? WarningState.LowBalance
                        : approaching
                            ? WarningState.ApproachingDailyCap
                            : WarningState.None;

            return Success(new EnergyStatusDto
            {
                GrantedBalance = account.GrantedBalance,
                PurchasedBalance = account.PurchasedBalance,
                TotalBalance = total,
                GrantExpiresAtUtc = account.GrantExpiresAtUtc,
                DailyUsed = effectiveDailyUsed,
                DailyCap = dailyCap,
                DailyCapReached = dailyCapReached,
                LowEnergy = lowEnergy,
                WarningState = warningState,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in EnergyStatusQuery for childId={request.ChildId}");
            return ServerError<EnergyStatusDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
