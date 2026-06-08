using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Commands.SetActive;

public class SetSkillActiveCommandValidator : AbstractValidator<SetSkillActiveCommand>
{
    public SetSkillActiveCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.SkillId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
