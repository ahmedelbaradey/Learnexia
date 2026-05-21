using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.SignIn;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Validation;

public class SignInValidatior : AbstractValidator<SignInCommand>
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SignInValidatior(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
        ApplyValidationsRules();
    }

    public void ApplyValidationsRules()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.LoginUsernameRequired])
            .NotNull().WithMessage(_localizer[SharedResourcesKey.LoginUsernameRequired]);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.LoginPasswordRequired])
            .NotNull().WithMessage(_localizer[SharedResourcesKey.LoginPasswordRequired]);
    }
}
