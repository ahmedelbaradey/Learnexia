using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Edit;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Validation;

public class EditSubjectValidation : AbstractValidator<EditSubjectCommand>
{
    public EditSubjectValidation(IStringLocalizer<SharedResources> localizer)
    {
        Include(new SubjectBaseValidation(localizer));

        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
