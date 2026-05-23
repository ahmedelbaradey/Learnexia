using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Grades.Commands.Delete;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Grades.Validation;

public class DeleteGradeValidation : AbstractValidator<DeleteGradeCommand>
{
    public DeleteGradeValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
