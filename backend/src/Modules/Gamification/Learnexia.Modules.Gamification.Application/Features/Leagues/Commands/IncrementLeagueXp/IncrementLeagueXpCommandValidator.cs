using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Leagues.Commands.IncrementLeagueXp;

/// <summary>
/// Structural validator for <see cref="IncrementLeagueXpCommand"/>.
/// Commands are trusted (sent by internal notification handlers, not direct user input),
/// so only structural checks are needed — positive int IDs, positive amount, non-empty Guid,
/// non-empty type string, and non-default timestamp.
/// </summary>
public sealed class IncrementLeagueXpCommandValidator
    : AbstractValidator<IncrementLeagueXpCommand>
{
    public IncrementLeagueXpCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .GreaterThan(0);

        RuleFor(x => x.Amount)
            .GreaterThan(0);

        RuleFor(x => x.OriginEventId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.OriginEventType)
            .NotEmpty()
            .MaximumLength(60);

        RuleFor(x => x.OccurredAtUtc)
            .NotEqual(default(DateTime));
    }
}
