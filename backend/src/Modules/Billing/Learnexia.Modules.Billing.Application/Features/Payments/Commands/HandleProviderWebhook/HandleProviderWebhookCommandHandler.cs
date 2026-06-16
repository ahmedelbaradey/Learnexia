using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Payments.Commands.HandleProviderWebhook;

/// <summary>
/// Thin handler (Option C): performs signature verification and payload parsing (security pipeline),
/// then delegates all EF / transaction / idempotency / event-staging logic to
/// <see cref="IWebhookEventService"/>. Stays EF-free.
///
/// <para><strong>Security pipeline (order is mandatory):</strong>
/// <list type="number">
///   <item><strong>Signature verify FIRST</strong> — before any DB read or state mutation.</item>
///   <item><strong>Parse</strong> the verified webhook body.</item>
///   <item><strong>Idempotency check</strong> — delegate to service.</item>
///   <item><strong>Route</strong> to the appropriate service method.</item>
/// </list>
/// </para>
/// </summary>
public sealed class HandleProviderWebhookCommandHandler
    : BaseResponseHandler,
      ICommandHandler<HandleProviderWebhookCommand, BaseResponse<WebhookHandledDto>>
{
    private readonly IPaymentProvider _paymentProvider;
    private readonly IWebhookEventService _webhookEventService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public HandleProviderWebhookCommandHandler(
        IPaymentProvider paymentProvider,
        IWebhookEventService webhookEventService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _paymentProvider     = paymentProvider;
        _webhookEventService = webhookEventService;
        _logger              = logger;
        _localizer           = localizer;
    }

    public async Task<BaseResponse<WebhookHandledDto>> Handle(
        HandleProviderWebhookCommand request,
        CancellationToken cancellationToken)
    {
        // ── STEP 1: Signature verification — MUST be first, before any state access ──
        if (!_paymentProvider.VerifyWebhookSignature(request.RawBody, request.SignatureHeader))
        {
            _logger.LogInfo("HandleProviderWebhook: signature verification failed — rejecting.");
            return new BaseResponse<WebhookHandledDto>
            {
                Successed  = false,
                StatusCode = System.Net.HttpStatusCode.Unauthorized,
                Message    = _localizer[SharedResourcesKey.WebhookSignatureInvalid],
            };
        }

        try
        {
            // ── STEP 2: Parse the (now-verified) webhook body ─────────────────────────
            ParsedWebhookEvent parsed;
            try
            {
                parsed = _paymentProvider.ParseWebhookEvent(request.RawBody);
            }
            catch (Exception parseEx)
            {
                _logger.LogError(parseEx, "HandleProviderWebhook: failed to parse verified webhook body.");
                return new BaseResponse<WebhookHandledDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.BadRequest,
                    Message    = _localizer[SharedResourcesKey.WebhookPayloadInvalid],
                };
            }

            // ── STEP 3: Idempotency check ─────────────────────────────────────────────
            if (await _webhookEventService.IsAlreadyProcessedAsync(parsed.ProviderEventId, cancellationToken))
            {
                _logger.LogInfo($"HandleProviderWebhook: duplicate event {parsed.ProviderEventId} — no-op.");
                return new BaseResponse<WebhookHandledDto>
                {
                    Successed  = true,
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Message    = _localizer[SharedResourcesKey.WebhookEventAlreadyProcessed],
                    Data       = new WebhookHandledDto(parsed.ProviderEventId, "Duplicate"),
                };
            }

            // ── STEP 4: Route to the appropriate service method ───────────────────────
            WebhookProcessResult result = parsed.EventType switch
            {
                WebhookEventTypes.PaymentSucceeded =>
                    await _webhookEventService.HandlePaymentSucceededAsync(parsed, cancellationToken),

                WebhookEventTypes.PaymentFailed =>
                    await _webhookEventService.HandlePaymentFailedAsync(parsed, cancellationToken),

                WebhookEventTypes.ChargeFailed =>
                    await _webhookEventService.HandleChargeFailedAsync(parsed, cancellationToken),

                WebhookEventTypes.RefundSucceeded =>
                    await _webhookEventService.HandleRefundSucceededAsync(parsed, cancellationToken),

                _ => await _webhookEventService.HandleUnknownEventAsync(parsed, cancellationToken),
            };

            // Concurrent 23505 duplicate — idempotent success.
            if (result.WasConcurrentDuplicate)
            {
                return new BaseResponse<WebhookHandledDto>
                {
                    Successed  = true,
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Message    = _localizer[SharedResourcesKey.WebhookEventAlreadyProcessed],
                    Data       = new WebhookHandledDto(parsed.ProviderEventId, "ConcurrentDuplicate"),
                };
            }

            return new BaseResponse<WebhookHandledDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.WebhookProcessed],
                Data       = new WebhookHandledDto(parsed.ProviderEventId, result.Outcome),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleProviderWebhookCommand.");
            return ServerError<WebhookHandledDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}
