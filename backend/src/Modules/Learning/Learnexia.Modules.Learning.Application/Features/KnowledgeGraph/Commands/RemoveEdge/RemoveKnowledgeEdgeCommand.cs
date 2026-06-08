using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Commands.RemoveEdge;

/// <summary>
/// Soft-deletes a <see cref="Domain.Entities.KnowledgeEdge"/> by setting IsDeleted = true.
/// No physical removal — the edge remains in the database and is invisible to standard queries.
/// </summary>
public record RemoveKnowledgeEdgeCommand : ICommand<BaseResponse<string>>
{
    public int EdgeId { get; init; }
}
