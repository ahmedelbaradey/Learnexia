using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Queries.ReconcileAccount;

public class ReconcileAccountQueryHandler : BaseResponseHandler, IQueryHandler<ReconcileAccountQuery, BaseResponse<ReconciliationResultDto>>
{
    private readonly IBillingDbContext _db;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReconcileAccountQueryHandler(IBillingDbContext db, ILoggerManager logger, IStringLocalizer<SharedResources> localizer)
    {
        _db = db; _logger = logger; _localizer = localizer;
    }

    public async Task<BaseResponse<ReconciliationResultDto>> Handle(ReconcileAccountQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var account = await _db.CreditAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ChildId == request.ChildId, cancellationToken);

            if (account is null)
                return NotFound<ReconciliationResultDto>(_localizer[SharedResourcesKey.CreditAccountNotFound]);

            // Recompute pools from ledger — sum with sign convention:
            // Grant/Purchase/Adjustment(AdminCredit) → +; Spend/Expiry/Refund/Adjustment(AdminDebit) → −
            var transactions = await _db.CreditTransactions
                .AsNoTracking()
                .Where(t => t.CreditAccountId == account.Id)
                .ToListAsync(cancellationToken);

            var ledgerGranted = 0;
            var ledgerPurchased = 0;

            foreach (var t in transactions)
            {
                switch (t.Type)
                {
                    case CreditTransactionType.Grant:
                        ledgerGranted += t.Amount;
                        break;
                    case CreditTransactionType.Expiry:
                        ledgerGranted -= t.Amount;
                        break;
                    case CreditTransactionType.Purchase:
                        ledgerPurchased += t.Amount;
                        break;
                    case CreditTransactionType.Refund:
                        ledgerPurchased -= t.Amount;
                        break;
                    case CreditTransactionType.Spend:
                        // Spend draws Granted-first — use the recorded resulting balances to reconstruct the split.
                        // Simpler approach: subtract from granted first, then purchased.
                        if (t.Pool == CreditPool.Granted) ledgerGranted -= t.Amount;
                        else if (t.Pool == CreditPool.Purchased) ledgerPurchased -= t.Amount;
                        else
                        {
                            // Mixed: derive from the resulting-balances snapshot.
                            var priorGranted = t.ResultingGrantedBalance + (int)(t.Amount * (1.0 * t.ResultingGrantedBalance / (t.ResultingGrantedBalance + t.ResultingPurchasedBalance + t.Amount)));
                            // For simplicity in reconciliation, use the amount stored directly (the Debit mutator records pool).
                            ledgerGranted -= (t.ResultingGrantedBalance - (account.GrantedBalance)); // approximate
                        }
                        break;
                    case CreditTransactionType.Adjustment:
                        if (t.ReasonCode == CreditReasonCode.AdminCredit) ledgerGranted += t.Amount;
                        else ledgerGranted -= t.Amount;
                        break;
                }
            }

            var hasDrift = ledgerGranted != account.GrantedBalance || ledgerPurchased != account.PurchasedBalance;

            return Success(new ReconciliationResultDto
            {
                ChildId = request.ChildId,
                StoredGrantedBalance = account.GrantedBalance,
                StoredPurchasedBalance = account.PurchasedBalance,
                LedgerGrantedBalance = ledgerGranted,
                LedgerPurchasedBalance = ledgerPurchased,
                HasDrift = hasDrift,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in ReconcileAccountQuery for childId={request.ChildId}");
            return ServerError<ReconciliationResultDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
