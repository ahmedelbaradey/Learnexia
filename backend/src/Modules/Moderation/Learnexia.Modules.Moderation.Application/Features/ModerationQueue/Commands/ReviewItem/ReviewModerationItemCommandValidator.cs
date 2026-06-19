using FluentValidation;
using Learnexia.Modules.Moderation.Domain.Enums;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Commands.ReviewItem;

/// <summary>
/// FluentValidation validator for <see cref="ReviewModerationItemCommand"/>.
/// Runs via <c>ValidationBehavior</c> (ICommand pipeline).
///
/// <para>Rules:</para>
/// <list type="bullet">
///   <item><c>Id</c> must be &gt; 0.</item>
///   <item><c>Decision</c> must be a valid <see cref="ModerationStatus"/> and must NOT be
///     <see cref="ModerationStatus.Pending"/> (Pending is the initial state, not a valid decision).</item>
///   <item><c>Reason</c> is required when <c>Decision == Rejected</c>.</item>
///   <item><c>Reason</c> max length is 2000 characters.</item>
/// </list>
/// </summary>
public class ReviewModerationItemCommandValidator : AbstractValidator<ReviewModerationItemCommand>
{
    public ReviewModerationItemCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.ModerationItemIdRequired]);

        RuleFor(x => x.Decision)
            .IsInEnum()
            .WithMessage(localizer[SharedResourcesKey.ModerationReviewDecisionInvalid])
            .Must(d => d != ModerationStatus.Pending)
            .WithMessage(localizer[SharedResourcesKey.ModerationReviewDecisionInvalid]);

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage(localizer[SharedResourcesKey.ModerationReviewReasonRequired])
            .When(x => x.Decision == ModerationStatus.Rejected);

        RuleFor(x => x.Reason)
            .MaximumLength(2000)
            .WithMessage(localizer[SharedResourcesKey.ModerationReviewReasonTooLong])
            .When(x => x.Reason is not null);
    }
}
