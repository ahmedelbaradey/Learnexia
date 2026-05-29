using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Commands.AbandonAttempt;

/// <summary>
/// Validator for AbandonAttemptCommand. Commands are auto-validated by ValidationBehavior;
/// queries are NOT validated (per CLAUDE.md rule 4).
/// </summary>
public class AbandonAttemptValidation : AbstractValidator<AbandonAttemptCommand>
{
    public AbandonAttemptValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.AttemptId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.AttemptIdMustBePositive]);
    }
}
