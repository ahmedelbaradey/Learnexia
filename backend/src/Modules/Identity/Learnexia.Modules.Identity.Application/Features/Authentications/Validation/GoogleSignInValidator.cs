using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.GoogleSignIn;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Validation;

public class GoogleSignInValidator : AbstractValidator<GoogleSignInCommand>
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GoogleSignInValidator(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
        ApplyValidationsRules();
    }

    public void ApplyValidationsRules()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.GoogleIdTokenRequired])
            .NotNull().WithMessage(_localizer[SharedResourcesKey.GoogleIdTokenRequired]);
    }
}
