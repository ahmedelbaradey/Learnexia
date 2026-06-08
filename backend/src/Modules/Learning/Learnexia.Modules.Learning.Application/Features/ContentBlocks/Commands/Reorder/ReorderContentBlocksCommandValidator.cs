using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Reorder;

public class ReorderContentBlocksCommandValidator : AbstractValidator<ReorderContentBlocksCommand>
{
    // A lesson can have many content blocks; 500 is generous enough to accommodate
    // real lessons (typically <50 blocks) while bounding runaway requests.
    private const int MaxBlocksPerReorder = 500;

    public ReorderContentBlocksCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.ContentBlockIds)
            .NotNull().WithMessage(localizer[SharedResourcesKey.EmptyRequestValidation])
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.EmptyRequestValidation])
            .Must(ids => ids.Count <= MaxBlocksPerReorder)
                .WithMessage(localizer[SharedResourcesKey.ReorderListTooLong]);

        RuleForEach(x => x.ContentBlockIds)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
