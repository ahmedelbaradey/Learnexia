using FluentValidation;
using Resources;
using Microsoft.Extensions.Localization;

namespace Learnexia.Modules.Billing.Application.Features.Seats.Commands.StartSeatCheckout;

/// <summary>
/// FluentValidation validator for <see cref="StartSeatCheckoutCommand"/> (P10-14-BE-6).
/// Only structural/format validation here; business-rule checks (max-seats, tier gate)
/// live in the handler/service.
/// </summary>
public sealed class StartSeatCheckoutValidator : AbstractValidator<StartSeatCheckoutCommand>
{
    public StartSeatCheckoutValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.SeatQuantity)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.SeatCheckoutQuantityRequired]);
    }
}
