using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Validation;

public class SubjectBaseValidation : AbstractValidator<SubjectDto>
{
    public SubjectBaseValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.EmptyNameValidation]);

        RuleFor(x => x.GradeId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
