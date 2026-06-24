using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Commands.ApproveIngestionReviewItem;

/// <summary>
/// Validates <see cref="ApproveIngestionReviewItemCommand"/> (BL-05-BE-8).
/// Metadata-level only: ReviewItemId must be a positive integer.
/// Business-rule checks (existence, already-resolved) live in the handler.
/// </summary>
public class ApproveIngestionReviewItemCommandValidator
    : AbstractValidator<ApproveIngestionReviewItemCommand>
{
    public ApproveIngestionReviewItemCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(c => c.ReviewItemId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.IngestionReviewItemNotFound]);
    }
}
