using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Missions.Commands.IncrementMissionProgress;

/// <summary>
/// Structural validator for <see cref="IncrementMissionProgressCommand"/>.
/// Commands are trusted (come from internal notification handlers, not direct user input),
/// so only structural checks are needed — positive int IDs, positive increment, and non-empty Guid.
/// Note: <see cref="IncrementMissionProgressCommand.TargetType"/> is not validated here;
/// the enum is constrained to defined values by the source event handler which supplies it.
/// </summary>
public sealed class IncrementMissionProgressCommandValidator
    : AbstractValidator<IncrementMissionProgressCommand>
{
    public IncrementMissionProgressCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .GreaterThan(0);

        RuleFor(x => x.IncrementBy)
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
