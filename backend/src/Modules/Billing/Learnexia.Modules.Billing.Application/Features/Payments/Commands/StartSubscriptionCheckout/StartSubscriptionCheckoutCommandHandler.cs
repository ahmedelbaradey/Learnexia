using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Payments.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Payments.Commands.StartSubscriptionCheckout;

/// <summary>
/// Thin handler (Option C): delegates all EF / provider / amount-resolution logic to
/// <see cref="ISubscriptionCheckoutService.StartAsync"/>. Stays EF-free.
/// </summary>
public sealed class StartSubscriptionCheckoutCommandHandler
    : BaseResponseHandler,
      ICommandHandler<StartSubscriptionCheckoutCommand, BaseResponse<CheckoutResultDto>>
{
    private readonly ISubscriptionCheckoutService _checkoutService;
    private readonly ILoggerManager _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public StartSubscriptionCheckoutCommandHandler(
        ISubscriptionCheckoutService checkoutService,
        ILoggerManager logger,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResources> localizer)
    {
        _checkoutService = checkoutService;
        _logger          = logger;
        _currentUser     = currentUser;
        _localizer       = localizer;
    }

    public async Task<BaseResponse<CheckoutResultDto>> Handle(
        StartSubscriptionCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var actorUserId = _currentUser.UserId ?? 0;

            var result = await _checkoutService.StartAsync(
                parentUserId : request.ParentUserId,
                actorUserId  : actorUserId,
                ct           : cancellationToken);

            return result.FailureReason switch
            {
                SubscriptionCheckoutFailureReason.SubscriptionNotFound => new BaseResponse<CheckoutResultDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.NotFound,
                    Message    = _localizer[SharedResourcesKey.SubscriptionNotFound],
                },
                SubscriptionCheckoutFailureReason.ProviderUnavailable => new BaseResponse<CheckoutResultDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.ServiceUnavailable,
                    Message    = _localizer[SharedResourcesKey.PaymentProviderUnavailable],
                },
                _ => new BaseResponse<CheckoutResultDto>
                {
                    Successed  = true,
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Message    = _localizer[SharedResourcesKey.CheckoutSessionCreated],
                    Data       = result.Data,
                },
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in StartSubscriptionCheckoutCommand for parentUserId={request.ParentUserId}");
            return ServerError<CheckoutResultDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}
