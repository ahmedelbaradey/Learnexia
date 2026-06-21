using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Commands.PromoteProposal;

/// <summary>
/// FluentValidation validator for <see cref="PromoteProposalCommand"/>.
/// Runs automatically via <c>ValidationBehavior</c> (commands only, per CLAUDE rule §4).
/// </summary>
public class PromoteProposalValidation : AbstractValidator<PromoteProposalCommand>
{
    public PromoteProposalValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.ProposalId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.CalibrationProposalIdMustBePositive]);
    }
}
