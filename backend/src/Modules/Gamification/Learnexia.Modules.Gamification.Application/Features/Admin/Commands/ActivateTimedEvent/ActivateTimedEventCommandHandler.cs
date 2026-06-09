using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.ActivateTimedEvent;

/// <summary>
/// Manually activates a <see cref="TimedEvent"/> (sets <c>IsActive = true</c>).
/// Rejects the transition if the event is already active (illegal transition → Successed=false).
/// Raises <see cref="AdminActionPerformedDomainEvent"/> for post-commit audit.
/// </summary>
public sealed class ActivateTimedEventCommandHandler
    : BaseResponseHandler, ICommandHandler<ActivateTimedEventCommand, BaseResponse<bool>>
{
    private readonly IGamificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ActivateTimedEventCommandHandler(
        IGamificationRepository repository,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<bool>> Handle(
        ActivateTimedEventCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var timedEvent = await _repository.GetTimedEventByIdAsync(request.Id, cancellationToken);
            if (timedEvent is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationTimedEventNotFound]);

            if (timedEvent.IsActive)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationTimedEventAlreadyActive]);

            timedEvent.Activate();

            timedEvent.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.TimedEventActivated,
                TargetEntityType: nameof(TimedEvent),
                TargetEntityId: timedEvent.Id,
                Details: $"Code={timedEvent.Code}; IsActive=true"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ActivateTimedEventCommand");
            return ServerError<bool>();
        }
    }
}
