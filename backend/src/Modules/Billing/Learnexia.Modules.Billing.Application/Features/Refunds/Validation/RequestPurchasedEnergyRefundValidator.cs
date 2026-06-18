using FluentValidation;
using Learnexia.Modules.Billing.Application.Features.Refunds.Commands.RequestPurchasedEnergyRefund;
using Learnexia.Modules.Billing.Domain.Enums;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Refunds.Validation;

/// <summary>
/// FluentValidation validator for <see cref="RequestPurchasedEnergyRefundCommand"/> (P10-17-BE-3).
/// Runs via <c>ValidationBehavior</c> (ICommand constraint).
/// </summary>
public sealed class RequestPurchasedEnergyRefundValidator
    : AbstractValidator<RequestPurchasedEnergyRefundCommand>
{
    public RequestPurchasedEnergyRefundValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.PurchasePaymentId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);

        RuleFor(x => x.Reason)
            .IsInEnum()
            .WithMessage(localizer[SharedResourcesKey.RequiredField]);
    }
}
