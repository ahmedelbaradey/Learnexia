using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Commands.RemoveEdge;

public class RemoveKnowledgeEdgeCommandValidator : AbstractValidator<RemoveKnowledgeEdgeCommand>
{
    public RemoveKnowledgeEdgeCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.EdgeId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
