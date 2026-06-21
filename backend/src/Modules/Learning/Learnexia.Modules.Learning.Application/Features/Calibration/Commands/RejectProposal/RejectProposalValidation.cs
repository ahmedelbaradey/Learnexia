using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Commands.RejectProposal;

/// <summary>
/// FluentValidation validator for <see cref="RejectProposalCommand"/>.
/// </summary>
public class RejectProposalValidation : AbstractValidator<RejectProposalCommand>
{
    public RejectProposalValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.ProposalId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.CalibrationProposalIdMustBePositive]);
    }
}
