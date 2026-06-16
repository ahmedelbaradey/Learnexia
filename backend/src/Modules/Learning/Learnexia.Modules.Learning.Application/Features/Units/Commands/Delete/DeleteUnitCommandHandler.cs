using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.Delete;

/// <summary>
/// P7-01: Soft-deletes a Unit by setting IsDeleted = true (FullAuditedEntity pattern).
/// The UnitOfWorkBehavior will stamp DeletedAt/DeletedBy after SaveChangesAsync.
/// Blocks deletion when the Unit still has non-deleted Lessons ("unit not empty" guard — AC-7).
/// Publishes <see cref="AdminActionPerformedDomainEvent"/> post-commit, best-effort.
///
/// Option C: all EF queries delegated to IUnitService. Handler injects only ILearningServiceManager.
/// </summary>
public class DeleteUnitCommandHandler : BaseResponseHandler, ICommandHandler<DeleteUnitCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public DeleteUnitCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(DeleteUnitCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var unit = await _service.UnitService.GetUnitTrackedAsync(request.Id, cancellationToken);
            if (unit is null)
                return NotFound<string>(_localizer[SharedResourcesKey.UnitNotFound]);

            // "Unit not empty" guard (AC-7): block soft-delete when non-deleted Lessons still exist.
            var hasLessons = await _service.UnitService.UnitHasLessonsAsync(request.Id, cancellationToken);
            if (hasLessons)
                return BadRequest<string>(_localizer[SharedResourcesKey.UnitNotEmpty]);

            // Soft-delete: set the flag; UnitOfWorkBehavior stamps DeletedAt/DeletedBy.
            unit.IsDeleted = true;
            await _service.UnitService.StageUnitUpdateAsync(unit, cancellationToken);

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            unit.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.UnitDeleted,
                TargetEntityType: nameof(LearningUnit),
                TargetEntityId: request.Id,
                Details: null));

            return Success<string>(_localizer[SharedResourcesKey.ItemDeletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in DeleteUnitCommand");
            return ServerError<string>();
        }
    }
}
