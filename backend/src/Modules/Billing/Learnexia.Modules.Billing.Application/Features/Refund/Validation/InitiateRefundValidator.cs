using FluentValidation;
using Learnexia.Modules.Billing.Application.Features.Refund.Commands.InitiateRefund;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Refund.Validation;

/// <summary>
/// FluentValidation validator for <see cref="InitiateRefundCommand"/>.
/// Runs via <c>ValidationBehavior</c> (ICommand constraint).
/// </summary>
public sealed class InitiateRefundValidator : AbstractValidator<InitiateRefundCommand>
{
    public InitiateRefundValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.PaymentId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage(localizer[SharedResourcesKey.RequiredField])
            .MaximumLength(1000)
            .WithMessage(localizer[SharedResourcesKey.MaxLength]);
    }
}
