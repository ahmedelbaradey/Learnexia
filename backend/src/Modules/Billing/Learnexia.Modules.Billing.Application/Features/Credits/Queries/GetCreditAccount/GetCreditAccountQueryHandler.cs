using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Queries.GetCreditAccount;

public class GetCreditAccountQueryHandler : BaseResponseHandler, IQueryHandler<GetCreditAccountQuery, BaseResponse<CreditAccountDto>>
{
    private readonly ICreditLedgerService _ledger;
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetCreditAccountQueryHandler(
        ICreditLedgerService ledger,
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _ledger           = ledger;
        _currentUser      = currentUser;
        _parentChildQuery = parentChildQuery;
        _logger           = logger;
        _localizer        = localizer;
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
                    // Anti-IDOR: same generic Forbidden whether the child exists or is not linked to caller.
                    var isParent = await _parentChildQuery.IsParentOfChildAsync(callerId.Value, request.ChildId, cancellationToken);
                    if (!isParent)
                        return Forbidden<CreditAccountDto>(_localizer[SharedResourcesKey.NotAuthorizedForChild]);
                }
            }

            var account = await _ledger.GetCreditAccountAsync(request.ChildId, cancellationToken);

            if (account is null)
                return NotFound<CreditAccountDto>(_localizer[SharedResourcesKey.CreditAccountNotFound]);

            return Success(account);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GetCreditAccountQuery for childId={request.ChildId}");
            return ServerError<CreditAccountDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
