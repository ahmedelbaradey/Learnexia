using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.ChangeChildLearningLanguage;

/// <summary>
/// Shape-only validation for <see cref="ChangeLearningLanguageCommand"/>.
///
/// Rules:
///   - <c>ChildId</c> must be positive.
///   - <c>NewLearningLanguage</c> must be "ar" or "en".
///
/// NOT validated here:
///   - <c>ConfirmFreshStart</c> — the 424 FailedDependency confirm-gate is enforced in the
///     handler (first check, before any link query or mutation). Putting it here would emit
///     HTTP 422 instead of the required 424.
///   - Family-scope / IDOR — link check is performed in the handler.
/// </summary>
public class ChangeLearningLanguageCommandValidator : AbstractValidator<ChangeLearningLanguageCommand>
{
    public ChangeLearningLanguageCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.ChildId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.ProfileRequiredField]);

        RuleFor(x => x.NewLearningLanguage)
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.ProfileRequiredField])
            .Must(lang => lang == "ar" || lang == "en")
            .WithMessage(localizer[SharedResourcesKey.InvalidLanguageCode]);
    }
}
