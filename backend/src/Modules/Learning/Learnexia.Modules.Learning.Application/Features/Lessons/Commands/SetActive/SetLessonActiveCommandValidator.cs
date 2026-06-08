using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.SetActive;

public class SetLessonActiveCommandValidator : AbstractValidator<SetLessonActiveCommand>
{
    public SetLessonActiveCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.LessonId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
