using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Commands.RejectKGSuggestion;

/// <summary>
/// Validates <see cref="RejectKGSuggestionCommand"/> (BL-03-BE-9).
/// Metadata-level only: SuggestionId must be positive. Business rules live in the handler.
/// </summary>
public class RejectKGSuggestionCommandValidator : AbstractValidator<RejectKGSuggestionCommand>
{
    public RejectKGSuggestionCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(c => c.SuggestionId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
