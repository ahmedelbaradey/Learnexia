using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Commands.Adjust;

public class AdjustCommandHandler : BaseResponseHandler, ICommandHandler<AdjustCommand, BaseResponse<DebitResultDto>>
{
    private readonly ICreditLedgerService _ledger;
    private readonly ILoggerManager _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AdjustCommandHandler(
        ICreditLedgerService ledger, ILoggerManager logger, ICurrentUserService currentUser,
        IStringLocalizer<SharedResources> localizer)
    {
        _ledger = ledger; _logger = logger; _currentUser = currentUser; _localizer = localizer;
    }

    public async Task<BaseResponse<DebitResultDto>> Handle(AdjustCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ledger.AdjustAsync(
                childId        : request.ChildId,
                delta          : request.Delta,
                idempotencyKey : request.IdempotencyKey,
                adminNote      : request.AdminNote,
                actorUserId    : _currentUser.UserId ?? 0,
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

            if (result.Outcome == CreditLedgerOutcome.AccountNotFound)
                return NotFound<DebitResultDto>(_localizer[SharedResourcesKey.CreditAccountNotFound]);

            return Success(new DebitResultDto
            {
                ResultingTotal = result.ResultingTotal,
                Outcome        = DebitOutcome.Charged,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in AdjustCommand for childId={request.ChildId}");
            return ServerError<DebitResultDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}
