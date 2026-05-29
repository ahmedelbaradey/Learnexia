using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Commands.CompleteAttempt;

/// <summary>
/// Validator for CompleteAttemptCommand. Commands are auto-validated by ValidationBehavior;
/// queries are NOT validated (per CLAUDE.md rule 4).
/// </summary>
public class CompleteAttemptValidation : AbstractValidator<CompleteAttemptCommand>
{
    public CompleteAttemptValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.AttemptId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.AttemptIdMustBePositive]);
    }
}
