using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.ResendRegistrationMessage;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Validation;

public class ResendRegistrationMessageValidator : AbstractValidator<ResendRegistrationMessageCommand>
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ResendRegistrationMessageValidator(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
        ApplyValidationRules();
    }

    private void ApplyValidationRules()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField]);
    }
}
