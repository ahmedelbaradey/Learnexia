using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.CancelSubscription;

public class CancelSubscriptionValidator : AbstractValidator<CancelSubscriptionCommand>
{
    public CancelSubscriptionValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.ParentUserId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.SubscriptionParentIdRequired]);
    }
}
