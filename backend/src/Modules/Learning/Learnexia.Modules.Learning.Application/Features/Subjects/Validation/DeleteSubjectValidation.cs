using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Delete;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Validation;

public class DeleteSubjectValidation : AbstractValidator<DeleteSubjectCommand>
{
    public DeleteSubjectValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
