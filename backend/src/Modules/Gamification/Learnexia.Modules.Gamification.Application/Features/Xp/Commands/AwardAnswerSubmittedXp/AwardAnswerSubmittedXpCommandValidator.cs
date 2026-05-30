using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardAnswerSubmittedXp;

/// <summary>
/// Structural validator for <see cref="AwardAnswerSubmittedXpCommand"/>.
/// Commands are trusted (come from the integration event payload), so only structural
/// checks are needed — positive IDs and non-empty EventId.
/// </summary>
public class AwardAnswerSubmittedXpCommandValidator : AbstractValidator<AwardAnswerSubmittedXpCommand>
{
    public AwardAnswerSubmittedXpCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .GreaterThan(0);

        RuleFor(x => x.LessonId)
            .GreaterThan(0);

        RuleFor(x => x.OriginEventId)
            .NotEqual(Guid.Empty);
    }
}
