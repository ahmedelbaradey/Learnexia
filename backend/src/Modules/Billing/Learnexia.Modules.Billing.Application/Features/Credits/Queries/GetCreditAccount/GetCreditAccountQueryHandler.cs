using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Queries.GetCreditAccount;

public class GetCreditAccountQueryHandler : BaseResponseHandler, IQueryHandler<GetCreditAccountQuery, BaseResponse<CreditAccountDto>>
{
    private readonly IBillingDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetCreditAccountQueryHandler(
        IBillingDbContext db,
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _db = db;
        _currentUser = currentUser;
        _parentChildQuery = parentChildQuery;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<CreditAccountDto>> Handle(GetCreditAccountQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var callerId = _currentUser.UserId;
            if (callerId is null)
                return Unauthorized<CreditAccountDto>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var callerRoles = _currentUser.Roles;
            var isAdmin = callerRoles.Any(r =>
                string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase));

            if (!isAdmin)
            {
                var isOwner = callerId.Value == request.ChildId;
                if (!isOwner)
                {
                    // Caller is not the child themselves — check parent-child ownership via Shared.Contracts seam.
                    // Anti-IDOR: same generic Forbidden whether the child exists or is not linked to caller.
                    var isParent = await _parentChildQuery.IsParentOfChildAsync(callerId.Value, request.ChildId, cancellationToken);
                    if (!isParent)
                        return Forbidden<CreditAccountDto>(_localizer[SharedResourcesKey.NotAuthorizedForChild]);
                }
            }

            var account = await _db.CreditAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ChildId == request.ChildId, cancellationToken);

            if (account is null)
                return NotFound<CreditAccountDto>(_localizer[SharedResourcesKey.CreditAccountNotFound]);

            return Success(new CreditAccountDto
            {
                ChildId = account.ChildId,
                GrantedBalance = account.GrantedBalance,
                PurchasedBalance = account.PurchasedBalance,
                TotalBalance = account.TotalBalance,
                GrantExpiresAtUtc = account.GrantExpiresAtUtc,
                DailyUsed = account.DailyUsed,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GetCreditAccountQuery for childId={request.ChildId}");
            return ServerError<CreditAccountDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
