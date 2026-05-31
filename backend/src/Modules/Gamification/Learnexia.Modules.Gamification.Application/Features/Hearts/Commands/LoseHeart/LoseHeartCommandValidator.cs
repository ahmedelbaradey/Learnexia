using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.LoseHeart;

/// <summary>
/// Structural validator for <see cref="LoseHeartCommand"/>.
/// Commands are trusted (come from the integration event payload, not direct user input),
/// so only structural checks are needed — positive StudentId and non-empty OriginEventId.
/// </summary>
public class LoseHeartCommandValidator : AbstractValidator<LoseHeartCommand>
{
    public LoseHeartCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .GreaterThan(0);

        RuleFor(x => x.OriginEventId)
            .NotEqual(Guid.Empty);
    }
}
