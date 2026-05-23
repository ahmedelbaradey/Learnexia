using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Skills.Commands.Delete;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Validation;

public class DeleteSkillValidation : AbstractValidator<DeleteSkillCommand>
{
    public DeleteSkillValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
