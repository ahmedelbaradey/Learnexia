using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Skills.Commands.Add;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Validation;

public class AddSkillValidation : AbstractValidator<AddSkillCommand>
{
    public AddSkillValidation(IStringLocalizer<SharedResources> localizer)
    {
        Include(new SkillBaseValidation(localizer));
    }
}
