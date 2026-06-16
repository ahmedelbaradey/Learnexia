using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Commands.GrantCredit;

public class GrantCreditCommandHandler : BaseResponseHandler, ICommandHandler<GrantCreditCommand, BaseResponse<DebitResultDto>>
{
    private readonly ICreditLedgerService _ledger;
    private readonly ILoggerManager _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GrantCreditCommandHandler(
        ICreditLedgerService ledger,
        ILoggerManager logger,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResources> localizer)
    {
        _ledger      = ledger;
        _logger      = logger;
        _currentUser = currentUser;
        _localizer   = localizer;
    }

    public async Task<BaseResponse<DebitResultDto>> Handle(GrantCreditCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actorUserId = _currentUser.UserId ?? 0;

            var result = await _ledger.GrantAsync(
                childId        : request.ChildId,
                amount         : request.Amount,
                expiresAtUtc   : request.ExpiresAtUtc,
                isPremium      : request.IsPremium,
                idempotencyKey : request.IdempotencyKey,
                actorUserId    : actorUserId,
                ct             : cancellationToken);

            if (result.Outcome == CreditLedgerOutcome.DuplicateIdempotent)
            {
                return new BaseResponse<DebitResultDto>
                {
                    Successed  = true,
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Message    = _localizer[SharedResourcesKey.CreditIdempotentDuplicate],
                    Data       = new DebitResultDto { Outcome = DebitOutcome.DuplicateIdempotent },
                };
            }

            return new BaseResponse<DebitResultDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.CreditGrantSucceeded],
                Data       = new DebitResultDto
                {
                    Charged       = false,
                    ResultingTotal = result.ResultingTotal,
                    Outcome       = DebitOutcome.Charged,
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
