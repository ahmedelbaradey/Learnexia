using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.Reorder;

/// <summary>
/// Batch-updates SequenceOrder on the given Units.
/// All Units must belong to the same SubjectId — cross-subject reorder is rejected.
/// The UnitOfWorkBehavior's transaction wraps all staged updates atomically (deferred-commit module).
/// Publishes <see cref="AdminActionPerformedDomainEvent"/> post-commit, best-effort.
///
/// Option C: all EF queries delegated to IUnitService. Handler injects only ILearningServiceManager.
/// </summary>
public class ReorderUnitsCommandHandler : BaseResponseHandler, ICommandHandler<ReorderUnitsCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReorderUnitsCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(ReorderUnitsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.UnitIds.Count == 0)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // Load all specified units with tracking.
            var units = await _service.UnitService.GetUnitsTrackedByIdsAsync(request.UnitIds, cancellationToken);

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
                await _service.UnitService.StageUnitUpdateAsync(unitsById[request.UnitIds[i]], cancellationToken);
            }

            // Raise domain event on the first tracked unit (representative for the batch).
            // Dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            unitsById[request.UnitIds[0]].RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.UnitReordered,
                TargetEntityType: nameof(LearningUnit),
                TargetEntityId: 0,
                Details: $"Reordered {request.UnitIds.Count} units"));

            return Success<string>(_localizer[SharedResourcesKey.OperationCompletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ReorderUnitsCommand");
            return ServerError<string>();
        }
    }
}
