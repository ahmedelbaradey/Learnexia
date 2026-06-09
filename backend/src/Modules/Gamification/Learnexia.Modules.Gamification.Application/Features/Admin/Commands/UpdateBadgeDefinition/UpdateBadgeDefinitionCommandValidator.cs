using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.UpdateBadgeDefinition;

public sealed class UpdateBadgeDefinitionCommandValidator : AbstractValidator<UpdateBadgeDefinitionCommand>
{
    public UpdateBadgeDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(80);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(240);

        RuleFor(x => x.IconKey)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Rarity).IsInEnum();
        RuleFor(x => x.TriggerType).IsInEnum();
        RuleFor(x => x.RewardXp).GreaterThan(0);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
