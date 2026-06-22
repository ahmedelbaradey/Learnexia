using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Payments.Commands.SimulateProviderPayment;

/// <summary>
/// Handles <see cref="SimulateProviderPaymentCommand"/>.
///
/// <para><strong>Option C — handler is EF-free.</strong> All persistence and signing logic
/// lives in <see cref="IPaymentSimulationService"/>. This handler only:
/// <list type="number">
///   <item>Checks the dual security gate via <c>IsSimulationAllowed()</c> — returns 404 when not allowed.</item>
///   <item>Calls <c>SimulateWebhookAsync</c> which reuses the real <c>IWebhookEventService</c> path.</item>
///   <item>Maps the typed result to <see cref="BaseResponse{T}"/>.</item>
/// </list>
/// </para>
///
/// <para>No <c>IBillingDbContext</c>, <c>DbSet</c>, or EF Core references appear in this file.</para>
/// </summary>
public sealed class SimulateProviderPaymentCommandHandler
    : BaseResponseHandler,
      ICommandHandler<SimulateProviderPaymentCommand, BaseResponse<SimulatePaymentResultDto>>
{
    private readonly IPaymentSimulationService _simulationService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SimulateProviderPaymentCommandHandler(
        IPaymentSimulationService simulationService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _simulationService = simulationService;
        _logger            = logger;
        _localizer         = localizer;
    }

    public async Task<BaseResponse<SimulatePaymentResultDto>> Handle(
        SimulateProviderPaymentCommand request,
        CancellationToken cancellationToken)
    {
        // ── Gate check: return 404 when simulation is not allowed ─────────────────────
        // Returns 404 (not 403/401) so the endpoint's existence is not disclosed.
        if (!_simulationService.IsSimulationAllowed())
        {
            _logger.LogInfo("SimulateProviderPayment: simulation not allowed — returning 404.");
            return NotFound<SimulatePaymentResultDto>(
                _localizer[SharedResourcesKey.SimulationNotAvailable]);
        }

        try
        {
            _logger.LogInfo(
                $"SimulateProviderPayment: paymentId={request.PaymentId}, eventType={request.EventType}.");

            var result = await _simulationService.SimulateWebhookAsync(
                request.PaymentId,
                request.EventType,
                cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogInfo(
                    $"SimulateProviderPayment: failed for paymentId={request.PaymentId}, reason={result.FailureReason}.");

                return result.FailureReason switch
                {
                    SimulatePaymentFailureReason.PaymentNotFound =>
                        NotFound<SimulatePaymentResultDto>(
                            _localizer[SharedResourcesKey.PaymentNotFound]),

                    SimulatePaymentFailureReason.ProviderRefMissing =>
                        new BaseResponse<SimulatePaymentResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.BadRequest,
                            Message    = _localizer[SharedResourcesKey.SimulationPaymentRefMissing],
                        },

                    SimulatePaymentFailureReason.UnsupportedEventType =>
                        new BaseResponse<SimulatePaymentResultDto>
                        {
                            Successed  = false,
                            StatusCode = System.Net.HttpStatusCode.BadRequest,
                            Message    = _localizer[SharedResourcesKey.SimulationEventTypeUnsupported],
                        },

                    _ => ServerError<SimulatePaymentResultDto>(
                        _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]),
                };
            }

            _logger.LogInfo(
                $"SimulateProviderPayment: succeeded — paymentId={request.PaymentId}, " +
                $"eventId={result.EventId}, outcome={result.Outcome}.");

            return new BaseResponse<SimulatePaymentResultDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.SimulationWebhookDispatched],
                Data       = new SimulatePaymentResultDto(
                    request.PaymentId,
                    result.EventId!,
                    request.EventType,
                    result.Outcome!),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error in SimulateProviderPaymentCommand for paymentId={request.PaymentId}.");
            return ServerError<SimulatePaymentResultDto>(
                _localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}
