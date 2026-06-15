using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.RequestDowngrade;

public class RequestDowngradeValidator : AbstractValidator<RequestDowngradeCommand>
{
    public RequestDowngradeValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.ParentUserId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.SubscriptionParentIdRequired]);
    }
}
