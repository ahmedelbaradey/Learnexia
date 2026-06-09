using FluentValidation;
using Learnexia.Modules.Gamification.Domain.Entities;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.GrantStreakFreeze;

public sealed class GrantStreakFreezeCommandValidator : AbstractValidator<GrantStreakFreezeCommand>
{
    public GrantStreakFreezeCommandValidator()
    {
        RuleFor(x => x.ChildId)
            .GreaterThan(0);

        RuleFor(x => x.Count)
            .GreaterThan(0)
            .LessThanOrEqualTo(StudentXpProfile.MaxFreezes);

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Confirm)
            .Equal(true).WithMessage("Confirm must be true to apply this override.");
    }
}
