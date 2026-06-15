using FluentValidation;
using Learnexia.Modules.Billing.Domain.Enums;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.RequestUpgrade;

public class RequestUpgradeValidator : AbstractValidator<RequestUpgradeCommand>
{
    public RequestUpgradeValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.ParentUserId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.SubscriptionParentIdRequired]);

        RuleFor(x => x.BillingPeriod)
            .IsInEnum()
            .WithMessage(localizer[SharedResourcesKey.SubscriptionBillingPeriodInvalid]);
    }
}
