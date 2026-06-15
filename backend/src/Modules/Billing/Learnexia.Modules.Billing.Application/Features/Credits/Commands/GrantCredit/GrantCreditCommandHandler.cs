using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Commands.GrantCredit;

public class GrantCreditCommandHandler : BaseResponseHandler, ICommandHandler<GrantCreditCommand, BaseResponse<DebitResultDto>>
{
    private readonly IBillingDbContext _db;
    private readonly ILoggerManager _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GrantCreditCommandHandler(
        IBillingDbContext db,
        ILoggerManager logger,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResources> localizer)
    {
        _db = db;
        _logger = logger;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    public async Task<BaseResponse<DebitResultDto>> Handle(GrantCreditCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            // Idempotency pre-check.
            if (await _db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == request.IdempotencyKey, cancellationToken))
            {
                await tx.RollbackAsync(cancellationToken);
                return new BaseResponse<DebitResultDto>
                {
                    Successed = true,
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Message = _localizer[SharedResourcesKey.CreditIdempotentDuplicate],
                    Data = new DebitResultDto { Outcome = DebitOutcome.DuplicateIdempotent },
                };
            }

            var account = await _db.CreditAccounts
                .FirstOrDefaultAsync(a => a.ChildId == request.ChildId, cancellationToken);

            if (account is null)
            {
                account = CreditAccount.CreateEmpty(request.ChildId);
                await _db.CreditAccounts.AddAsync(account, cancellationToken);
            }

            var reasonCode = request.IsPremium ? CreditReasonCode.MonthlyGrantPremium : CreditReasonCode.MonthlyGrantFree;
            var creditTransaction = account.ApplyGrant(request.Amount, request.ExpiresAtUtc, reasonCode, request.IdempotencyKey);
            await _db.CreditTransactions.AddAsync(creditTransaction, cancellationToken);

            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            await tx.CommitAsync(cancellationToken);

            return new BaseResponse<DebitResultDto>
            {
                Successed = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message = _localizer[SharedResourcesKey.CreditGrantSucceeded],
                Data = new DebitResultDto
                {
                    Charged = false,
                    ResultingTotal = account.TotalBalance,
                    Outcome = DebitOutcome.Charged,
                },
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GrantCreditCommand for childId={request.ChildId}");
            return ServerError<DebitResultDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}
