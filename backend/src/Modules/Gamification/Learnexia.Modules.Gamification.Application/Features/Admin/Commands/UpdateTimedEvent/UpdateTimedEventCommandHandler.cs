using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.UpdateTimedEvent;

/// <summary>
/// Updates mutable fields (copy, window, multiplier, scope) on an existing <see cref="TimedEvent"/>.
/// Code and IsActive are NOT changed here — use separate activate/expire commands.
/// Raises <see cref="AdminActionPerformedDomainEvent"/> for post-commit audit.
/// </summary>
public sealed class UpdateTimedEventCommandHandler
    : BaseResponseHandler, ICommandHandler<UpdateTimedEventCommand, BaseResponse<bool>>
{
    private readonly IGamificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public UpdateTimedEventCommandHandler(
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
        UpdateTimedEventCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var timedEvent = await _repository.GetTimedEventByIdAsync(request.Id, cancellationToken);
            if (timedEvent is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationTimedEventNotFound]);

            timedEvent.AdminUpdate(
                request.NameEn,
                request.NameAr,
                request.DescriptionEn,
                request.DescriptionAr,
                request.StartUtc,
                request.EndUtc,
                request.Multiplier,
                request.Scope);

            timedEvent.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.TimedEventUpdated,
                TargetEntityType: nameof(TimedEvent),
                TargetEntityId: timedEvent.Id,
                Details: $"Code={timedEvent.Code}; Window={request.StartUtc:O}→{request.EndUtc:O}; Multiplier={request.Multiplier}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            // F3: ArgumentException from the entity factory is defence-in-depth (validators
            // already reject invalid window/multiplier at the FluentValidation layer with 422).
            // Let it fall through to the generic ServerError — no ex.Message leakage.
            _logger.LogError(ex, "Error: in UpdateTimedEventCommand");
            return ServerError<bool>();
        }
    }
}
