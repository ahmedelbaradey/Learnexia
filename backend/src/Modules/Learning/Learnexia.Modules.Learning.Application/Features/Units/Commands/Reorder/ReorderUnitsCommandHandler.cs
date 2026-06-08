using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.Reorder;

/// <summary>
/// Batch-updates SequenceOrder on the given Units.
/// All Units must belong to the same SubjectId — cross-subject reorder is rejected.
/// The UnitOfWorkBehavior's transaction wraps all staged updates atomically (deferred-commit module).
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class ReorderUnitsCommandHandler : BaseResponseHandler, ICommandHandler<ReorderUnitsCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReorderUnitsCommandHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        IPublisher publisher,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _publisher = publisher;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(ReorderUnitsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.UnitIds.Count == 0)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // Load all specified units with tracking.
            var units = await _repository.Learning
                .GetByCondition<LearningUnit>(u => request.UnitIds.Contains(u.Id), trackChanges: true)
                .ToListAsync(cancellationToken);

            if (units.Count != request.UnitIds.Count)
                return NotFound<string>(_localizer[SharedResourcesKey.UnitNotFound]);

            // Validate all belong to the same SubjectId — cross-subject reorder is forbidden.
            var subjectIds = units.Select(u => u.SubjectId).Distinct().ToList();
            if (subjectIds.Count > 1)
                return BadRequest<string>(_localizer[SharedResourcesKey.ReorderCrossTreeForbidden]);

            // Apply new SequenceOrder based on position in the requested list.
            var unitsById = units.ToDictionary(u => u.Id);
            for (var i = 0; i < request.UnitIds.Count; i++)
            {
                unitsById[request.UnitIds[i]].SequenceOrder = i;
                await _repository.Learning.UpdateAsync(unitsById[request.UnitIds[i]]);
            }

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.UnitReordered,
                    TargetEntityType: nameof(LearningUnit),
                    TargetEntityId: 0,
                    Details: $"Reordered {request.UnitIds.Count} units"), cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, $"P7-01: AdminActionPerformedEvent publish failed for UnitReorder");
            }

            return Success<string>(_localizer[SharedResourcesKey.OperationCompletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ReorderUnitsCommand");
            return ServerError<string>();
        }
    }
}
