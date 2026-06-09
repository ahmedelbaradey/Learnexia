using FluentValidation;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.UpdateChildProfile;

/// <summary>
/// Validator for <see cref="UpdateChildProfileCommand"/> (P7-08 BE-1).
/// ICommand validators are picked up automatically by ValidationBehavior.
///
/// Validation rules:
///   - ChildId must be positive.
///   - PreferredLanguage, when supplied, must be one of "ar", "en", "ar-EG", "en-US".
///   - Country, when supplied, must not exceed 100 chars.
/// </summary>
public class UpdateChildProfileCommandValidator : AbstractValidator<UpdateChildProfileCommand>
{
    private static readonly HashSet<string> ValidLanguageCodes =
        new(StringComparer.OrdinalIgnoreCase) { "ar", "en", "ar-EG", "en-US" };

    public UpdateChildProfileCommandValidator()
    {
        RuleFor(x => x.ChildId)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.ChildIdRequired);

        When(x => x.PreferredLanguage is not null, () =>
        {
            RuleFor(x => x.PreferredLanguage!)
                .Must(lang => ValidLanguageCodes.Contains(lang))
                .WithMessage(SharedResourcesKey.InvalidLanguageCode);
        });

        When(x => x.Country is not null, () =>
        {
            RuleFor(x => x.Country!)
                .MaximumLength(100)
                .WithMessage(SharedResourcesKey.ProfileCountryTooLong);
        });
    }
}
