using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Badges.Commands.AwardBadge;

/// <summary>
/// Structural validator for <see cref="AwardBadgeCommand"/>.
/// Commands are trusted (come from internal notification handlers, not direct user input),
/// so only structural checks are needed — positive int IDs and non-empty Guid.
/// </summary>
public sealed class AwardBadgeCommandValidator : AbstractValidator<AwardBadgeCommand>
{
    public AwardBadgeCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .GreaterThan(0);

        RuleFor(x => x.BadgeDefinitionId)
            .GreaterThan(0);

        RuleFor(x => x.OriginEventId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.OriginEventType)
            .NotEmpty()
            .MaximumLength(60);

        RuleFor(x => x.AwardedAtUtc)
            .NotEqual(default(DateTime));
    }
}
