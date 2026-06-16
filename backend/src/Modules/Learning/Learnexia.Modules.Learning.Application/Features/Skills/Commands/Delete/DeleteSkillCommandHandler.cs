using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Commands.Delete;

/// <summary>
/// P7-03: Soft-deletes a Skill by setting <c>IsDeleted = true</c>, and cascade-soft-deletes
/// the wrapping <c>KnowledgeNode</c> (the one with <c>SkillId == skill.Id</c>) plus any live
/// <c>KnowledgeEdge</c> that touches that node (source or target).
///
/// All three entity types are staged in the same EF change-tracker snapshot;
/// the <c>UnitOfWorkBehavior</c> wraps the handler in a single transaction.
/// No explicit nested transaction is required — the UoW transaction boundary covers everything.
///
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
///
/// Option-C refactor: FirstOrDefaultAsync + GetNodeBySkillIdAsync + GetEdgesForNodeAsync moved
/// into ISkillService (GetSkillTrackedAsync, GetNodeBySkillIdTrackedAsync, GetEdgesForNodeTrackedAsync,
/// StageSkillUpdateAsync, StageNodeUpdateAsync, StageEdgeUpdateAsync).
/// </summary>
public class DeleteSkillCommandHandler : BaseResponseHandler, ICommandHandler<DeleteSkillCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public DeleteSkillCommandHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _currentUser = currentUser;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(DeleteSkillCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var skill = await _service.SkillService.GetSkillTrackedAsync(request.Id, cancellationToken);

            if (skill is null)
                return NotFound<string>(_localizer[SharedResourcesKey.SkillNotFound]);

            // Cascade: find the wrapping KnowledgeNode (if any) and its live edges.
            var node = await _service.SkillService.GetNodeBySkillIdTrackedAsync(request.Id, cancellationToken);
            int edgeCount = 0;

            if (node is not null)
            {
                // Soft-delete all live edges that reference this node.
                var edges = await _service.SkillService.GetEdgesForNodeTrackedAsync(node.Id, cancellationToken);
                foreach (var edge in edges)
                {
                    edge.IsDeleted = true;
                    await _service.SkillService.StageEdgeUpdateAsync(edge, cancellationToken);
                }
                edgeCount = edges.Count;

                // Soft-delete the node itself.
                node.IsDeleted = true;
                await _service.SkillService.StageNodeUpdateAsync(node, cancellationToken);
            }

            // Soft-delete the skill; UnitOfWorkBehavior stamps DeletedAt/DeletedBy on commit.
            skill.IsDeleted = true;
            await _service.SkillService.StageSkillUpdateAsync(skill, cancellationToken);

            // Raise domain event on the tracked Skill aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            skill.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.SkillDeleted,
                TargetEntityType: nameof(Skill),
                TargetEntityId: request.Id,
                Details: node is not null
                    ? $"Cascade-soft-deleted node={node.Id}, edges={edgeCount}"
                    : null));

            return Success<string>(_localizer[SharedResourcesKey.ItemDeletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in DeleteSkillCommand");
            return ServerError<string>();
        }
    }
}
