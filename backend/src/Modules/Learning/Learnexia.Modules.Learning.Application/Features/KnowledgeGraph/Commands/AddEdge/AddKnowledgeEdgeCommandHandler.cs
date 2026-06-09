using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Commands.AddEdge;

/// <summary>
/// Handles <see cref="AddKnowledgeEdgeCommand"/>.
///
/// Guard order:
///   1. Both node IDs must resolve to non-deleted KnowledgeNodes.
///   2. Cross-language guard: both nodes' owning Subjects must share the same Language.
///      Fails closed if either Subject cannot be resolved.
///   3. Duplicate guard: (SourceNodeId, TargetNodeId, RelationshipType) must be unique.
///   4. For Prerequisite edges only: acyclic guard via <see cref="SkillGraphValidator.AssertAcyclic"/>.
///      Catches <see cref="InvalidOperationException"/> → Successed=false + cycle message.
///
/// On success: persists the new edge and publishes <see cref="AdminActionPerformedEvent"/>.
/// </summary>
public class AddKnowledgeEdgeCommandHandler : BaseResponseHandler, ICommandHandler<AddKnowledgeEdgeCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddKnowledgeEdgeCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(AddKnowledgeEdgeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // Guard 1 — both nodes must exist.
            var sourceNode = await _repository.Learning.GetKnowledgeNodeByIdAsync(request.SourceNodeId, trackChanges: false, cancellationToken);
            if (sourceNode is null)
                return NotFound<string>(_localizer[SharedResourcesKey.KnowledgeNodeNotFound]);

            var targetNode = await _repository.Learning.GetKnowledgeNodeByIdAsync(request.TargetNodeId, trackChanges: false, cancellationToken);
            if (targetNode is null)
                return NotFound<string>(_localizer[SharedResourcesKey.KnowledgeNodeNotFound]);

            // Guard 2 — cross-language check: both nodes' subjects must share the same Language.
            var sourceSubject = await _repository.Learning.GetSubjectForNodeAsync(request.SourceNodeId, cancellationToken);
            if (sourceSubject is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.KnowledgeNodeSubjectNotResolvable]);

            var targetSubject = await _repository.Learning.GetSubjectForNodeAsync(request.TargetNodeId, cancellationToken);
            if (targetSubject is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.KnowledgeNodeSubjectNotResolvable]);

            if (sourceSubject.Language != targetSubject.Language)
                return BadRequest<string>(_localizer[SharedResourcesKey.KnowledgeEdgeCrossLanguageForbidden]);

            // Guard 3 — duplicate composite-unique triple.
            var isDuplicate = await _repository.Learning.KnowledgeEdgeDuplicateExistsAsync(
                request.SourceNodeId,
                request.TargetNodeId,
                (int)request.RelationshipType,
                cancellationToken);

            if (isDuplicate)
                return BadRequest<string>(_localizer[SharedResourcesKey.KnowledgeEdgeDuplicate]);

            // Guard 4 — acyclic check for Prerequisite edges.
            if (request.RelationshipType == EdgeRelationshipType.Prerequisite)
            {
                var existingPrereqEdges = await _repository.Learning.GetAllPrerequisiteEdgesAsync(cancellationToken);

                // Build a synthetic proposed edge for the validator (Id=0 is fine — unused by DFS).
                var proposedEdge = new KnowledgeEdge
                {
                    SourceNodeId = request.SourceNodeId,
                    TargetNodeId = request.TargetNodeId,
                    RelationshipType = EdgeRelationshipType.Prerequisite,
                    Strength = request.Strength ?? 1.0m
                };

                var allEdgesForCheck = existingPrereqEdges.Append(proposedEdge);

                // KNOWN RACE (P7-03 Finding #3 — accepted low-risk, tracked in HANDOFF):
                // This is a non-atomic check-then-insert. Two concurrent AddEdge requests for
                // complementary edges (A→B and B→A) could each read the existing edge set before
                // the other's row is committed, both pass the DFS acyclic check independently,
                // and then both persist — silently creating a cycle. The DB unique index only
                // guards against exact-duplicate (SourceNodeId, TargetNodeId, RelationshipType)
                // triples, not acyclicity.
                // For the current single-admin-at-a-time phase this is an accepted low-likelihood
                // risk. The proper fix is to wrap the read+check+insert in a SERIALIZABLE transaction
                // (or advisory lock). Do NOT add a transaction here without consulting the lead —
                // the deferred-commit UoW pattern in this module makes transaction nesting
                // non-trivial (see ADR 0001/0002).
                try
                {
                    SkillGraphValidator.AssertAcyclic(allEdgesForCheck);
                }
                catch (InvalidOperationException cycleEx)
                {
                    // Log as Info — cycle detection is expected business logic, not an error.
                    _logger.LogError(cycleEx, "P7-03: Acyclic check rejected a proposed edge");
                    return BadRequest<string>(_localizer[SharedResourcesKey.KnowledgeEdgeWouldCreateCycle]);
                }
            }

            // Persist the new edge.
            var edge = new KnowledgeEdge
            {
                SourceNodeId = request.SourceNodeId,
                TargetNodeId = request.TargetNodeId,
                RelationshipType = request.RelationshipType,
                Strength = request.Strength ?? 1.0m
            };
            await _repository.Learning.AddAsync(edge, cancellationToken);

            // Raise domain event on the tracked KnowledgeEdge aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            edge.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.KnowledgeEdgeAdded,
                TargetEntityType: nameof(KnowledgeEdge),
                TargetEntityId: 0,
                Details: $"Source={request.SourceNodeId}, Target={request.TargetNodeId}, Type={request.RelationshipType}"));

            return Success<string>(_localizer[SharedResourcesKey.KnowledgeEdgeAddedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddKnowledgeEdgeCommand");
            return ServerError<string>();
        }
    }
}
