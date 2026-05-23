using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Grades.Commands.Add;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Grades.Validation;

public class AddGradeValidation : AbstractValidator<AddGradeCommand>
{
    public AddGradeValidation(IStringLocalizer<SharedResources> localizer)
    {
        Include(new GradeBaseValidation(localizer));
    }
}
