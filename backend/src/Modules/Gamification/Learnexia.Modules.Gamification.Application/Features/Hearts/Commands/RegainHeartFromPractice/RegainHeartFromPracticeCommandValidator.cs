using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.RegainHeartFromPractice;

/// <summary>
/// Structural validator for <see cref="RegainHeartFromPracticeCommand"/>.
/// Commands are trusted (come from the integration event payload, not direct user input),
/// so only structural checks are needed — positive StudentId and non-empty OriginEventId.
/// </summary>
public class RegainHeartFromPracticeCommandValidator : AbstractValidator<RegainHeartFromPracticeCommand>
{
    public RegainHeartFromPracticeCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .GreaterThan(0);

        RuleFor(x => x.OriginEventId)
            .NotEqual(Guid.Empty);
    }
}
