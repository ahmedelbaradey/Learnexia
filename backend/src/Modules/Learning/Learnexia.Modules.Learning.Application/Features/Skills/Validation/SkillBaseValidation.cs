using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Skills.Dtos;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Validation;

public class SkillBaseValidation : AbstractValidator<SkillDto>
{
    public SkillBaseValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.EmptyNameValidation]);

        RuleFor(x => x.MasteryThreshold)
            .InclusiveBetween(0, 100).WithMessage(localizer[SharedResourcesKey.RequiredField]);

        RuleFor(x => x.ConceptId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
