using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Queries.ReconcileAccount;

/// <summary>
/// Reconciles the family wallet balances from the immutable ledger.
/// Family-scoped (by <see cref="ReconcileAccountQuery.ParentId"/>).
/// Does NOT touch <c>CreditAccount</c>.
/// </summary>
public class ReconcileAccountQueryHandler : BaseResponseHandler, IQueryHandler<ReconcileAccountQuery, BaseResponse<ReconciliationResultDto>>
{
    private readonly ICreditLedgerService _ledger;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReconcileAccountQueryHandler(
        ICreditLedgerService ledger, ILoggerManager logger, IStringLocalizer<SharedResources> localizer)
    {
        _ledger = ledger; _logger = logger; _localizer = localizer;
    }

    public async Task<BaseResponse<ReconciliationResultDto>> Handle(ReconcileAccountQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ledger.ReconcileFamilyWalletAsync(request.ParentId, cancellationToken);

            if (result is null)
                return NotFound<ReconciliationResultDto>(_localizer[SharedResourcesKey.CreditAccountNotFound]);

            return Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in ReconcileAccountQuery for parentId={request.ParentId}");
            return ServerError<ReconciliationResultDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
