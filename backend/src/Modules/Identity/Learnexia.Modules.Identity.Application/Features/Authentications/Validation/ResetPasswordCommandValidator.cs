using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.ResetPassword;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Validation;

// Shape-only: email format + token/newPassword presence. Password STRENGTH is enforced by the configured
// ASP.NET Identity policy inside ResetPasswordAsync (defense kept in one place), so we do not duplicate the
// complexity regex here — a policy failure surfaces as the generic "invalid link" failure in the handler.
public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.ProfileRequiredField])
            .EmailAddress().WithMessage(localizer[SharedResourcesKey.ProfileInvalidEmailFormat]);

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.ProfileRequiredField]);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.LoginPasswordRequired]);
    }
}
