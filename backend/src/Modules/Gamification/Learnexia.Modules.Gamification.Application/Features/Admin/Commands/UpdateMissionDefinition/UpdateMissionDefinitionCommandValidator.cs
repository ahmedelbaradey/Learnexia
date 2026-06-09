using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.UpdateMissionDefinition;

public sealed class UpdateMissionDefinitionCommandValidator : AbstractValidator<UpdateMissionDefinitionCommand>
{
    public UpdateMissionDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.IconKey).NotEmpty().MaximumLength(60);
        RuleFor(x => x.TitleKey).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Cadence).IsInEnum();
        RuleFor(x => x.TargetType).IsInEnum();
        RuleFor(x => x.Target).GreaterThan(0);
        RuleFor(x => x.RewardXp).GreaterThan(0);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
