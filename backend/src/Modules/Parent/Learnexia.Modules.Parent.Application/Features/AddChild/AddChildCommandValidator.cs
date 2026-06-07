using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.AddChild;

// Shape-only validation (runs via ValidationBehavior for ICommand<> → 422). Does NOT do an existence
// lookup — the duplicate-email rejection is a single BadRequest from the seam. Password complexity mirrors
// the configured Identity policy.
public class AddChildCommandValidator : AbstractValidator<AddChildCommand>
{
    private const string PasswordPolicyPattern =
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{6,}$";

    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddChildCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField])
            .MaximumLength(255).WithMessage(_localizer[SharedResourcesKey.ProfileFullNameTooLong]);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField])
            .EmailAddress().WithMessage(_localizer[SharedResourcesKey.ProfileInvalidEmailFormat]);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.LoginPasswordRequired])
            .Matches(PasswordPolicyPattern).WithMessage(_localizer[SharedResourcesKey.PasswordComplexityError]);

        RuleFor(x => x.Grade)
            .InclusiveBetween(1, 6).WithMessage(_localizer[SharedResourcesKey.GradeOutOfRange]);

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField])
            .Must(lang => lang == "ar" || lang == "en")
            .WithMessage(_localizer[SharedResourcesKey.InvalidLanguageCode]);

        // P8-01: medium-of-instruction language — required, mirrors the Language rule above.
        RuleFor(x => x.LearningLanguage)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField])
            .Must(lang => lang == "ar" || lang == "en")
            .WithMessage(_localizer[SharedResourcesKey.InvalidLanguageCode]);

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField])
            .MaximumLength(100).WithMessage(_localizer[SharedResourcesKey.ProfileCountryTooLong]);
    }
}
