using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Commands.ConfirmFlag;

/// <summary>
/// FluentValidation validator for <see cref="ConfirmFlagCommand"/>.
/// </summary>
public class ConfirmFlagValidation : AbstractValidator<ConfirmFlagCommand>
{
    public ConfirmFlagValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.QuestionId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.CalibrationQuestionIdMustBePositive]);
    }
}
