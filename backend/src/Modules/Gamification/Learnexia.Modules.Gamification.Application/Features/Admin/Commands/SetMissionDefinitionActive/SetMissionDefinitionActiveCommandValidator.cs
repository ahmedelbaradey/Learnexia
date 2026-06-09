using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.SetMissionDefinitionActive;

public sealed class SetMissionDefinitionActiveCommandValidator : AbstractValidator<SetMissionDefinitionActiveCommand>
{
    public SetMissionDefinitionActiveCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
