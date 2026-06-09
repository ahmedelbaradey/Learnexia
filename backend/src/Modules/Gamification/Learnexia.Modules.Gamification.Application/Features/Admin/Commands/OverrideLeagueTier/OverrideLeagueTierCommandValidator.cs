using FluentValidation;
using Learnexia.Modules.Gamification.Domain.Enums;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.OverrideLeagueTier;

public sealed class OverrideLeagueTierCommandValidator : AbstractValidator<OverrideLeagueTierCommand>
{
    public OverrideLeagueTierCommandValidator()
    {
        RuleFor(x => x.ChildId)
            .GreaterThan(0);

        RuleFor(x => x.NewTier)
            .IsInEnum();

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Confirm)
            .Equal(true).WithMessage("Confirm must be true to apply this override.");
    }
}
