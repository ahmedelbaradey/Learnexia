using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Account.Commands.UpdateMyProfile;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Account.Validation;

// Shape-only validation (runs via ValidationBehavior for ICommand<> → 422). Mirrors
// AddChildCommandValidator's localized-message style. FullName is required + length-bounded; Phone
// and Country are optional but length/format-bounded when present. No id/email/role is on the
// command, so there is nothing to validate there (self-scope is enforced in the handler).
public class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    private const int FullNameMaxLength = 100;
    private const int CountryMaxLength = 100;

    // E.164-ish: optional leading '+' then 7–15 digits. Length-bounds and format-guards the input
    // without binding to a single country's national format.
    private const string PhonePattern = @"^\+?[0-9]{7,15}$";

    private readonly IStringLocalizer<SharedResources> _localizer;

    public UpdateMyProfileCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField])
            .MaximumLength(FullNameMaxLength).WithMessage(_localizer[SharedResourcesKey.ProfileFullNameTooLong]);

        RuleFor(x => x.Phone)
            .Matches(PhonePattern).WithMessage(_localizer[SharedResourcesKey.ProfileInvalidPhoneFormat])
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Country)
            .MaximumLength(CountryMaxLength).WithMessage(_localizer[SharedResourcesKey.ProfileCountryTooLong])
            .When(x => !string.IsNullOrWhiteSpace(x.Country));
    }
}
