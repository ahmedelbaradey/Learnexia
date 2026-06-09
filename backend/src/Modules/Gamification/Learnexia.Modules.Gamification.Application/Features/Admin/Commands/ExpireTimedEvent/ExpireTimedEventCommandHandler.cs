using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.ExpireTimedEvent;

/// <summary>
/// Manually expires a <see cref="TimedEvent"/> (sets <c>IsActive = false</c>).
/// Rejects the transition if the event is already inactive (illegal transition → Successed=false).
/// Raises <see cref="AdminActionPerformedDomainEvent"/> for post-commit audit.
/// </summary>
public sealed class ExpireTimedEventCommandHandler
    : BaseResponseHandler, ICommandHandler<ExpireTimedEventCommand, BaseResponse<bool>>
{
    private readonly IGamificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ExpireTimedEventCommandHandler(
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
        ExpireTimedEventCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var timedEvent = await _repository.GetTimedEventByIdAsync(request.Id, cancellationToken);
            if (timedEvent is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationTimedEventNotFound]);

            if (!timedEvent.IsActive)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationTimedEventAlreadyInactive]);

            timedEvent.Deactivate();

            timedEvent.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.TimedEventExpired,
                TargetEntityType: nameof(TimedEvent),
                TargetEntityId: timedEvent.Id,
                Details: $"Code={timedEvent.Code}; IsActive=false"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ExpireTimedEventCommand");
            return ServerError<bool>();
        }
    }
}
