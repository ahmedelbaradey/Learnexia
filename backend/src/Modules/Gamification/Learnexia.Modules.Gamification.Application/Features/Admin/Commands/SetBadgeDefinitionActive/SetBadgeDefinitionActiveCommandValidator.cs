using FluentValidation;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.SetBadgeDefinitionActive;

public sealed class SetBadgeDefinitionActiveCommandValidator : AbstractValidator<SetBadgeDefinitionActiveCommand>
{
    public SetBadgeDefinitionActiveCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
