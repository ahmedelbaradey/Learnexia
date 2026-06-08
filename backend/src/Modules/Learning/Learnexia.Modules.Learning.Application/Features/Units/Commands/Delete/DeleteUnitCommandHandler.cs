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

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.Delete;

/// <summary>
/// P7-01: Soft-deletes a Unit by setting IsDeleted = true (FullAuditedEntity pattern).
/// The UnitOfWorkBehavior will stamp DeletedAt/DeletedBy after SaveChangesAsync.
/// Blocks deletion when the Unit still has non-deleted Lessons ("unit not empty" guard — AC-7).
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class DeleteUnitCommandHandler : BaseResponseHandler, ICommandHandler<DeleteUnitCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public DeleteUnitCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(DeleteUnitCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var unit = await _repository.Learning
                .GetByCondition<LearningUnit>(u => u.Id == request.Id, trackChanges: true)
                .FirstOrDefaultAsync(cancellationToken);

            if (unit is null)
                return NotFound<string>(_localizer[SharedResourcesKey.UnitNotFound]);

            // "Unit not empty" guard (AC-7): block soft-delete when non-deleted Lessons still exist.
            // The global IsDeleted filter is active, so AnyAsync here only counts non-deleted lessons.
            var hasLessons = await _repository.Learning
                .AnyAsync<Lesson>(l => l.UnitId == request.Id);

            if (hasLessons)
                return BadRequest<string>(_localizer[SharedResourcesKey.UnitNotEmpty]);

            // Soft-delete: set the flag; UnitOfWorkBehavior stamps DeletedAt/DeletedBy.
            unit.IsDeleted = true;
            await _repository.Learning.UpdateAsync(unit);

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.UnitDeleted,
                    TargetEntityType: nameof(LearningUnit),
                    TargetEntityId: request.Id,
                    Details: null), cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, $"P7-01: AdminActionPerformedEvent publish failed for UnitId={request.Id}");
            }

            return Success<string>(_localizer[SharedResourcesKey.ItemDeletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in DeleteUnitCommand");
            return ServerError<string>();
        }
    }
}
