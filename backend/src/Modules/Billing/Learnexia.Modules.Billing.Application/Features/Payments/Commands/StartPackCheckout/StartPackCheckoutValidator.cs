using FluentValidation;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Payments.Commands.StartPackCheckout;

/// <summary>
/// FluentValidation validator for <see cref="StartPackCheckoutCommand"/>.
/// Runs automatically via <c>ValidationBehavior</c> (commands only — per Convention §4).
/// </summary>
public sealed class StartPackCheckoutValidator : AbstractValidator<StartPackCheckoutCommand>
{
    public StartPackCheckoutValidator()
    {
        RuleFor(x => x.ChildId)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.PackCheckoutChildIdRequired);
    }
}
