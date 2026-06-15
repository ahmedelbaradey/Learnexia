using FluentValidation;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Payments.Commands.StartSubscriptionCheckout;

/// <summary>
/// Validates <see cref="StartSubscriptionCheckoutCommand"/> before it reaches the handler.
/// Minimal: the parentId is set from JWT by the controller so it is always > 0 in production;
/// the validator guards against programmatic misuse.
/// </summary>
public sealed class StartSubscriptionCheckoutValidator : AbstractValidator<StartSubscriptionCheckoutCommand>
{
    public StartSubscriptionCheckoutValidator()
    {
        RuleFor(x => x.ParentUserId)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.SubscriptionParentIdRequired);
    }
}
