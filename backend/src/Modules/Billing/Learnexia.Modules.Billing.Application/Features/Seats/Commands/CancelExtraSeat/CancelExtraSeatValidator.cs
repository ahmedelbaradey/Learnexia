using FluentValidation;
using Resources;
using Microsoft.Extensions.Localization;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Commands.CancelExtraSeat;

/// <summary>
/// FluentValidation validator for <see cref="CancelExtraSeatCommand"/> (P10-14-BE-8).
/// Structural/format validation only; business-rule checks (insufficient extra seats, no active sub)
/// live in the handler/service.
/// </summary>
public sealed class CancelExtraSeatValidator : AbstractValidator<CancelExtraSeatCommand>
{
    public CancelExtraSeatValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.SeatQuantity)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.SeatCheckoutQuantityRequired]);
    }
}
