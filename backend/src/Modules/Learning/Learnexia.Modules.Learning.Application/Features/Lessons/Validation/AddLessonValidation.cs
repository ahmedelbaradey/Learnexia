using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Add;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Validation;

public class AddLessonValidation : AbstractValidator<AddLessonCommand>
{
    public AddLessonValidation(IStringLocalizer<SharedResources> localizer)
    {
        Include(new LessonBaseValidation(localizer));
    }
}
