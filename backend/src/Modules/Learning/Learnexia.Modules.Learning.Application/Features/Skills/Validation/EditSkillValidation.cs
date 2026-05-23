using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Skills.Commands.Edit;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Validation;

public class EditSkillValidation : AbstractValidator<EditSkillCommand>
{
    public EditSkillValidation(IStringLocalizer<SharedResources> localizer)
    {
        Include(new SkillBaseValidation(localizer));

        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
