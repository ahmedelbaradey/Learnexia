using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Commands.ApproveKGSuggestion;

/// <summary>
/// Validates <see cref="ApproveKGSuggestionCommand"/> (BL-03-BE-8).
/// Metadata-level only: SuggestionId must be positive. Business rules live in the handler.
/// </summary>
public class ApproveKGSuggestionCommandValidator : AbstractValidator<ApproveKGSuggestionCommand>
{
    public ApproveKGSuggestionCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(c => c.SuggestionId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
