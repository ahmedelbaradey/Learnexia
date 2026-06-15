using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Payments.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Payments.Commands.StartPackCheckout;

/// <summary>
/// Handles <see cref="StartPackCheckoutCommand"/>.
///
/// <para><strong>Option C — handler is EF-free.</strong> All persistence, provider calls, and
/// transaction logic live in <see cref="IEnergyPackService"/>. This handler only:
/// <list type="number">
///   <item>Resolves <c>parentId</c> from the JWT via <see cref="ICurrentUserService"/>.</item>
///   <item>Builds a per-request idempotency key.</item>
///   <item>Calls <see cref="IEnergyPackService.StartPackCheckoutAsync"/> and maps its result to
///         <see cref="BaseResponse{T}"/>.</item>
/// </list>
/// </para>
///
/// <para>No <c>IBillingDbContext</c>, <c>DbSet</c>, or EF Core references appear in this file.</para>
/// </summary>
public sealed class StartPackCheckoutCommandHandler
    : BaseResponseHandler,
      ICommandHandler<StartPackCheckoutCommand, BaseResponse<PackCheckoutResultDto>>
{
    private readonly IEnergyPackService _packService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public StartPackCheckoutCommandHandler(
        IEnergyPackService packService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _packService  = packService;
        _currentUser  = currentUser;
        _logger       = logger;
        _localizer    = localizer;
    }

    public async Task<BaseResponse<PackCheckoutResultDto>> Handle(
        StartPackCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── parentId is ALWAYS from the JWT, never the request body ────────────────
            var parentId = _currentUser.UserId
                ?? throw new InvalidOperationException("Parent JWT claim missing.");

            // ── Generate a per-attempt idempotency key ─────────────────────────────────
            var idempotencyKey = $"pack-checkout:{parentId}:{request.ChildId}:{Guid.NewGuid():N}";

            // ── Delegate all EF / payment-provider / transaction work to the service ───
            var result = await _packService.StartPackCheckoutAsync(
                parentId,
                request.ChildId,
                idempotencyKey,
                cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogInfo(
                    $"StartPackCheckout: failed for parentId={parentId}, childId={request.ChildId}, reason={result.FailureReason}.");

                return result.FailureReason switch
                {
                    PackCheckoutFailureReason.ChildNotOwnedByParent =>
                        new BaseResponse<PackCheckoutResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.Forbidden,
                            Message    = _localizer[SharedResourcesKey.PackCheckoutChildNotOwned],
                        },

                    PackCheckoutFailureReason.ProviderUnavailable =>
                        new BaseResponse<PackCheckoutResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.ServiceUnavailable,
                            Message    = _localizer[SharedResourcesKey.PaymentProviderUnavailable],
                        },

                    _ => ServerError<PackCheckoutResultDto>(
                        _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]),
                };
            }

            _logger.LogInfo(
                $"StartPackCheckout: paymentId={result.PaymentId}, parentId={parentId}, childId={request.ChildId}.");

            return new BaseResponse<PackCheckoutResultDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.PackCheckoutSessionCreated],
                Data       = new PackCheckoutResultDto
                {
                    PaymentId          = result.PaymentId,
                    RedirectUrl        = result.RedirectUrl!,
                    ProviderPaymentRef = result.ProviderPaymentRef!,
                },
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error in StartPackCheckoutCommand for parentUserId={_currentUser.UserId}, childId={request.ChildId}.");
            return ServerError<PackCheckoutResultDto>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}
