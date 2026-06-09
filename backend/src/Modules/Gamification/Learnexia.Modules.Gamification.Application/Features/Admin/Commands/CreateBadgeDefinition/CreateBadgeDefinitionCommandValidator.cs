using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.CreateBadgeDefinition;

public sealed class CreateBadgeDefinitionCommandValidator : AbstractValidator<CreateBadgeDefinitionCommand>
{
    public CreateBadgeDefinitionCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(80);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(240);

        RuleFor(x => x.IconKey)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Rarity)
            .IsInEnum();

        RuleFor(x => x.TriggerType)
            .IsInEnum();

        RuleFor(x => x.RewardXp)
            .GreaterThan(0);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0);
    }
}
