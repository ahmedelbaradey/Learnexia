using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Commands.RemoveEdge;

/// <summary>
/// Handles <see cref="RemoveKnowledgeEdgeCommand"/>.
/// Soft-deletes the edge (sets IsDeleted = true).
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class RemoveKnowledgeEdgeCommandHandler : BaseResponseHandler, ICommandHandler<RemoveKnowledgeEdgeCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public RemoveKnowledgeEdgeCommandHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(RemoveKnowledgeEdgeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var edge = await _repository.Learning.GetKnowledgeEdgeByIdAsync(request.EdgeId, trackChanges: true, cancellationToken);

            if (edge is null)
                return NotFound<string>(_localizer[SharedResourcesKey.KnowledgeEdgeNotFound]);

            // Soft-delete: UnitOfWorkBehavior stamps DeletedAt/DeletedBy on commit.
            edge.IsDeleted = true;
            await _repository.Learning.UpdateAsync(edge);

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            edge.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.KnowledgeEdgeRemoved,
                TargetEntityType: nameof(KnowledgeEdge),
                TargetEntityId: request.EdgeId,
                Details: $"Source={edge.SourceNodeId}, Target={edge.TargetNodeId}, Type={edge.RelationshipType}"));

            return Success<string>(_localizer[SharedResourcesKey.KnowledgeEdgeRemovedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in RemoveKnowledgeEdgeCommand");
            return ServerError<string>();
        }
    }
}
