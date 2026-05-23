using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Grades.Commands.Edit;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Grades.Validation;

public class EditGradeValidation : AbstractValidator<EditGradeCommand>
{
    public EditGradeValidation(IStringLocalizer<SharedResources> localizer)
    {
        Include(new GradeBaseValidation(localizer));

        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
