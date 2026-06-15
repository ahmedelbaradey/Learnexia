using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Payments.Dtos;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Payments.Commands.StartSubscriptionCheckout;

/// <summary>
/// Handles <see cref="StartSubscriptionCheckoutCommand"/>.
///
/// <para>Pipeline:
/// <list type="number">
///   <item>Resolve the parent's <c>PendingPayment</c> subscription.</item>
///   <item>Resolve charge amount SERVER-SIDE from <c>BillingPeriod</c> + GlobalSettings keys.</item>
///   <item>Create a <c>Payment{Kind=Subscription, Status=Initiated}</c> row.</item>
///   <item>Call <see cref="IPaymentProvider.CreateCheckoutSessionAsync"/>.</item>
///   <item>Persist the provider ref on the payment row and return the redirect URL.</item>
/// </list>
/// </para>
///
/// <para>Security: amount is never client-supplied; parentId from JWT only.</para>
/// </summary>
public sealed class StartSubscriptionCheckoutCommandHandler
    : BaseResponseHandler,
      ICommandHandler<StartSubscriptionCheckoutCommand, BaseResponse<CheckoutResultDto>>
{
    private readonly IBillingDbContext _db;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ILoggerManager _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public StartSubscriptionCheckoutCommandHandler(
        IBillingDbContext db,
        IPaymentProvider paymentProvider,
        IGlobalSettingsProvider settings,
        ILoggerManager logger,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResources> localizer)
    {
        _db = db;
        _paymentProvider = paymentProvider;
        _settings = settings;
        _logger = logger;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    public async Task<BaseResponse<CheckoutResultDto>> Handle(
        StartSubscriptionCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── 1. Load the PendingPayment subscription ───────────────────────────────
            var subscription = await _db.Subscriptions
                .Where(s => s.ParentUserId == request.ParentUserId
                         && s.Status == SubscriptionStatus.PendingPayment)
                .FirstOrDefaultAsync(cancellationToken);

            if (subscription is null)
            {
                return new BaseResponse<CheckoutResultDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.NotFound,
                    Message    = _localizer[SharedResourcesKey.SubscriptionNotFound],
                };
            }

            // ── 2. Resolve amount SERVER-SIDE — client never supplies the amount ───────
            var amount = subscription.BillingPeriod == BillingPeriod.Annual
                ? _settings.GetDecimal(GlobalSettingKeys.SubscriptionAnnualPriceEgp, 1990m)
                : _settings.GetDecimal(GlobalSettingKeys.SubscriptionMonthlyPriceEgp, 199m);

            // ── 3. Create the Payment row (Initiated) with a unique idempotency key ────
            var idempotencyKey = $"sub-checkout:{request.ParentUserId}:{subscription.Id}:{Guid.NewGuid():N}";

            var payment = new Payment
            {
                SubscriptionId = subscription.Id,
                ParentUserId   = request.ParentUserId,
                Amount         = amount,
                Currency       = PaymentCurrency.EGP,
                Status         = PaymentStatus.Initiated,
                Kind           = PaymentKind.Subscription,
                IdempotencyKey = idempotencyKey,
            };

            await _db.Payments.AddAsync(payment, cancellationToken);
            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);

            // ── 4. Call the provider (outside the write transaction — provider call can be slow) ──
            CheckoutSession session;
            try
            {
                session = await _paymentProvider.CreateCheckoutSessionAsync(payment, cancellationToken);
            }
            catch (Exception provEx)
            {
                _logger.LogError(provEx, $"StartSubscriptionCheckout: provider call failed for parentId={request.ParentUserId}");
                // Payment row stays Initiated — reconcile job will sweep it later.
                return new BaseResponse<CheckoutResultDto>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.ServiceUnavailable,
                    Message    = _localizer[SharedResourcesKey.PaymentProviderUnavailable],
                };
            }

            // ── 5. Persist the provider ref ───────────────────────────────────────────
            payment.ProviderPaymentRef = session.ProviderPaymentRef;
            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);

            _logger.LogInfo($"StartSubscriptionCheckout: paymentId={payment.Id}, parentId={request.ParentUserId}, period={subscription.BillingPeriod}, amount={amount}.");

            return new BaseResponse<CheckoutResultDto>
            {
                Successed  = true,
                StatusCode = System.Net.HttpStatusCode.OK,
                Message    = _localizer[SharedResourcesKey.CheckoutSessionCreated],
                Data       = new CheckoutResultDto
                {
                    PaymentId          = payment.Id,
                    RedirectUrl        = session.RedirectUrl,
                    ProviderPaymentRef = session.ProviderPaymentRef,
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
