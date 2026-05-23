using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Add;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Validation;

public class AddSubjectValidation : AbstractValidator<AddSubjectCommand>
{
    public AddSubjectValidation(IStringLocalizer<SharedResources> localizer)
    {
        Include(new SubjectBaseValidation(localizer));
    }
}
