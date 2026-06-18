using FluentValidation;
using Learnexia.Modules.Billing.Application.Features.Refunds.Commands.AdminRequestPurchasedEnergyRefund;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Refunds.Validation;

/// <summary>
/// FluentValidation validator for <see cref="AdminRequestPurchasedEnergyRefundCommand"/> (P10-17-BE-6).
/// Runs via <c>ValidationBehavior</c> (ICommand constraint).
/// </summary>
public sealed class AdminRequestPurchasedEnergyRefundValidator
    : AbstractValidator<AdminRequestPurchasedEnergyRefundCommand>
{
    public AdminRequestPurchasedEnergyRefundValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.PurchasePaymentId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);

        RuleFor(x => x.Reason)
            .IsInEnum()
            .WithMessage(localizer[SharedResourcesKey.RequiredField]);
    }
}
