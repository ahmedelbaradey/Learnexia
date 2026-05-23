using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Validation;

public class LessonBaseValidation : AbstractValidator<LessonDto>
{
    public LessonBaseValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.EmptyNameValidation]);

        RuleFor(x => x.Difficulty)
            .IsInEnum().WithMessage(localizer[SharedResourcesKey.RequiredField]);

        RuleFor(x => x.UnitId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
