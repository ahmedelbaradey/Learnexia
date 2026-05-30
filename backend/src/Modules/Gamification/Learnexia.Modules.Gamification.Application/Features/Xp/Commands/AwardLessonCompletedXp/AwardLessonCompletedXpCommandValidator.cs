using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardLessonCompletedXp;

/// <summary>
/// Structural validator for <see cref="AwardLessonCompletedXpCommand"/>.
/// Commands are trusted (come from the integration event payload), so only structural
/// checks are needed — positive IDs and non-empty EventId.
/// </summary>
public class AwardLessonCompletedXpCommandValidator : AbstractValidator<AwardLessonCompletedXpCommand>
{
    public AwardLessonCompletedXpCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .GreaterThan(0);

        RuleFor(x => x.LessonId)
            .GreaterThan(0);

        RuleFor(x => x.OriginEventId)
            .NotEqual(Guid.Empty);
    }
}
