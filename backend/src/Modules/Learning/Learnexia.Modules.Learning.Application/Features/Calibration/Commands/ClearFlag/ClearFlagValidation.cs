using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Commands.ClearFlag;

/// <summary>
/// FluentValidation validator for <see cref="ClearFlagCommand"/>.
/// </summary>
public class ClearFlagValidation : AbstractValidator<ClearFlagCommand>
{
    public ClearFlagValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.QuestionId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.CalibrationQuestionIdMustBePositive]);
    }
}
