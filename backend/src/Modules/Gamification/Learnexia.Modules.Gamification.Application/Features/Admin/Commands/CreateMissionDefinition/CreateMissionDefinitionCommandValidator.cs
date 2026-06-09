using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.CreateMissionDefinition;

public sealed class CreateMissionDefinitionCommandValidator : AbstractValidator<CreateMissionDefinitionCommand>
{
    public CreateMissionDefinitionCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
        RuleFor(x => x.IconKey).NotEmpty().MaximumLength(60);
        RuleFor(x => x.TitleKey).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Cadence).IsInEnum();
        RuleFor(x => x.TargetType).IsInEnum();
        RuleFor(x => x.Target).GreaterThan(0);
        RuleFor(x => x.RewardXp).GreaterThan(0);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
