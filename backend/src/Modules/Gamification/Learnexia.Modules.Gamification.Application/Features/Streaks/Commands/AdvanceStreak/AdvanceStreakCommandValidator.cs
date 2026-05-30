using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Streaks.Commands.AdvanceStreak;

/// <summary>
/// Structural validator for <see cref="AdvanceStreakCommand"/>.
/// Commands are trusted (come from the integration event payload, not direct user input),
/// so only structural checks are needed — positive StudentId and non-empty OriginEventId.
/// </summary>
public class AdvanceStreakCommandValidator : AbstractValidator<AdvanceStreakCommand>
{
    public AdvanceStreakCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .GreaterThan(0);

        RuleFor(x => x.OriginEventId)
            .NotEqual(Guid.Empty);
    }
}
