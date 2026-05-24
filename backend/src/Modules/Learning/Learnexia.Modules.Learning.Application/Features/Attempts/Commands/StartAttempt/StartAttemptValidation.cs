using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Commands.StartAttempt;

/// <summary>
/// Validator for StartAttemptCommand. Commands are auto-validated by ValidationBehavior;
/// queries are NOT validated (per CLAUDE.md rule 4).
/// </summary>
public class StartAttemptValidation : AbstractValidator<StartAttemptCommand>
{
    public StartAttemptValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.LessonId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.LessonIdMustBePositive]);
    }
}
