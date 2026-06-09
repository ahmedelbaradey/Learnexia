using FluentValidation;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.AdminChangeLearningLanguage;

/// <summary>
/// Validator for <see cref="AdminChangeLearningLanguageCommand"/> (P7-08 BE-6).
/// ICommand validators are picked up automatically by ValidationBehavior.
///
/// Rules:
///   - ChildId must be positive.
///   - LearningLanguage must be "ar" or "en" (short codes only — seam stores the short code).
///
/// NOT validated here:
///   - <c>ConfirmFreshStart</c> — the 424 FailedDependency confirm-gate fires in the handler
///     BEFORE any mutation; putting it here would emit HTTP 422 instead of the required 424.
///   - Child-role guard and family-scope — enforced in the handler.
/// </summary>
public class AdminChangeLearningLanguageCommandValidator
    : AbstractValidator<AdminChangeLearningLanguageCommand>
{
    public AdminChangeLearningLanguageCommandValidator()
    {
        RuleFor(x => x.ChildId)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.ChildIdRequired);

        RuleFor(x => x.LearningLanguage)
            .NotEmpty().WithMessage(SharedResourcesKey.InvalidLanguageCode)
            .Must(lang => lang == "ar" || lang == "en")
            .WithMessage(SharedResourcesKey.InvalidLanguageCode);
    }
}
