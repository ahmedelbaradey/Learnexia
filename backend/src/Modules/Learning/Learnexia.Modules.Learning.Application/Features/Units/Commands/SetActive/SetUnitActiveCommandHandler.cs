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

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.SetActive;

/// <summary>
/// Toggles <c>Unit.IsActive</c>. Inactive units are hidden from student-facing reads.
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class SetUnitActiveCommandHandler : BaseResponseHandler, ICommandHandler<SetUnitActiveCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SetUnitActiveCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(SetUnitActiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var unit = await _repository.Learning
                .GetByCondition<LearningUnit>(u => u.Id == request.UnitId, trackChanges: true)
                .FirstOrDefaultAsync(cancellationToken);

            if (unit is null)
                return NotFound<string>(_localizer[SharedResourcesKey.UnitNotFound]);

            unit.IsActive = request.IsActive;
            await _repository.Learning.UpdateAsync(unit);

            var action = request.IsActive ? AdminActions.UnitActivated : AdminActions.UnitDeactivated;

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: action,
                    TargetEntityType: nameof(LearningUnit),
                    TargetEntityId: request.UnitId,
                    Details: $"IsActive={request.IsActive}"), cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, $"P7-01: AdminActionPerformedEvent publish failed for UnitId={request.UnitId}");
            }

            var message = request.IsActive
                ? _localizer[SharedResourcesKey.UnitActivatedSuccessfully]
                : _localizer[SharedResourcesKey.UnitDeactivatedSuccessfully];

            return Success<string>(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in SetUnitActiveCommand");
            return ServerError<string>();
        }
    }
}
