using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Account.Commands.UpdateMyPreferredLanguage;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Account.Validation;

// Mirrors UpdateChildProfileCommandValidator's language allowed-values rule.
// ValidationBehavior runs this automatically for ICommand<> requests → HTTP 422.
public class UpdateMyPreferredLanguageCommandValidator : AbstractValidator<UpdateMyPreferredLanguageCommand>
{
    private static readonly HashSet<string> ValidLanguageCodes =
        new(StringComparer.OrdinalIgnoreCase) { "ar", "en", "ar-EG", "en-US" };

    public UpdateMyPreferredLanguageCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.UserPreferredLanguage)
            .NotEmpty()
            .WithMessage(localizer[SharedResourcesKey.InvalidLanguageCode])
            .Must(lang => ValidLanguageCodes.Contains(lang))
            .WithMessage(localizer[SharedResourcesKey.InvalidLanguageCode]);
    }
}
