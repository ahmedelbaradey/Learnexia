using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Delete;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Validation;

public class DeleteLessonValidation : AbstractValidator<DeleteLessonCommand>
{
    public DeleteLessonValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
