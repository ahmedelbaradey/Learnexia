using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Grades.Dtos;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Grades.Validation;

public class GradeBaseValidation : AbstractValidator<GradeDto>
{
    public GradeBaseValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.EmptyNameValidation]);

        RuleFor(x => x.Number)
            .InclusiveBetween(1, 6).WithMessage(localizer[SharedResourcesKey.GradeOutOfRange]);
    }
}
