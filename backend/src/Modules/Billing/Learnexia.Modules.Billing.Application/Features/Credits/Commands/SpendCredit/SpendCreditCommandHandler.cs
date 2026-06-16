using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Commands.SpendCredit;

/// <summary>
/// Thin handler (Option C): delegates all EF / concurrency / idempotency / transaction
/// logic to <see cref="ICreditLedgerService.SpendAsync"/>.
/// </summary>
public class SpendCreditCommandHandler : BaseResponseHandler, ICommandHandler<SpendCreditCommand, BaseResponse<DebitResultDto>>
{
    private readonly ICreditLedgerService _ledger;
    private readonly ILoggerManager _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SpendCreditCommandHandler(
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

    public async Task<BaseResponse<DebitResultDto>> Handle(SpendCreditCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actorUserId = _currentUser.UserId ?? 0;

            var result = await _ledger.SpendAsync(
                childId        : request.ChildId,
                amount         : request.Amount,
                reasonCode     : request.ReasonCode,
                idempotencyKey : request.IdempotencyKey,
                relatedActionId: request.RelatedActionId,
                actorUserId    : actorUserId,
                ct             : cancellationToken);

            return result.Outcome switch
            {
                CreditLedgerOutcome.AccountNotFound => new BaseResponse<DebitResultDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.NotFound,
                    Message    = _localizer[SharedResourcesKey.CreditAccountNotFound],
                    Data       = new DebitResultDto { Outcome = DebitOutcome.InsufficientBalance },
                },
                CreditLedgerOutcome.InsufficientBalance => new BaseResponse<DebitResultDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                    Message    = _localizer[SharedResourcesKey.InsufficientBalance],
                    Data       = new DebitResultDto { Outcome = DebitOutcome.InsufficientBalance },
                },
                CreditLedgerOutcome.DuplicateIdempotent => new BaseResponse<DebitResultDto>
                {
                    Successed  = true,
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Message    = _localizer[SharedResourcesKey.CreditIdempotentDuplicate],
                    Data       = new DebitResultDto
                    {
                        Charged        = true,
                        ResultingTotal = result.ResultingTotal,
                        Outcome        = DebitOutcome.DuplicateIdempotent,
                    },
                },
                _ => Success(new DebitResultDto
                {
                    Charged        = true,
                    FromGranted    = result.FromGranted,
                    FromPurchased  = result.FromPurchased,
                    ResultingTotal = result.ResultingTotal,
                    Outcome        = DebitOutcome.Charged,
                }),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in SpendCreditCommand for childId={request.ChildId}");
            return ServerError<DebitResultDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}
