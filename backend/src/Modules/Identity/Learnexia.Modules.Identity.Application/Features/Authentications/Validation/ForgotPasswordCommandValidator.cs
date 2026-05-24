using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.ForgotPassword;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Validation;

// Shape-only: email must be present and a valid format. Intentionally does NOT check existence
// (anti-enumeration) — the handler returns the same generic success regardless.
public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.ProfileRequiredField])
            .EmailAddress().WithMessage(localizer[SharedResourcesKey.ProfileInvalidEmailFormat]);
    }
}
