using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Commands.AddEdge;

public class AddKnowledgeEdgeCommandValidator : AbstractValidator<AddKnowledgeEdgeCommand>
{
    public AddKnowledgeEdgeCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.SourceNodeId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);

        RuleFor(x => x.TargetNodeId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);

        // Strength is optional; when provided it must be within [0.0, 1.0].
        RuleFor(x => x.Strength)
            .InclusiveBetween(0.0m, 1.0m)
            .When(x => x.Strength.HasValue)
            .WithMessage(localizer[SharedResourcesKey.KnowledgeEdgeStrengthOutOfRange]);
    }
}
