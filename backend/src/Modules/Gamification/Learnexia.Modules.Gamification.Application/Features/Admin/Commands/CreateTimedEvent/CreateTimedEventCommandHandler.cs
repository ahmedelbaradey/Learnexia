using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.CreateTimedEvent;

/// <summary>
/// Creates a new <see cref="TimedEvent"/> catalog row. Validates Code uniqueness and window/multiplier
/// invariants (also enforced by the entity factory as defence-in-depth).
/// Raises <see cref="AdminActionPerformedDomainEvent"/> for post-commit audit.
/// </summary>
public sealed class CreateTimedEventCommandHandler
    : BaseResponseHandler, ICommandHandler<CreateTimedEventCommand, BaseResponse<int>>
{
    private readonly IGamificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public CreateTimedEventCommandHandler(
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

    public async Task<BaseResponse<int>> Handle(
        CreateTimedEventCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await _repository.TimedEventCodeExistsAsync(request.Code, cancellationToken))
                return BusinessValidation<int>(_localizer[SharedResourcesKey.GamificationTimedEventCodeAlreadyExists]);

            var timedEvent = TimedEvent.Create(
                request.Code,
                request.NameEn,
                request.NameAr,
                request.DescriptionEn,
                request.DescriptionAr,
                request.StartUtc,
                request.EndUtc,
                request.Multiplier,
                request.Scope);

            await _repository.AddTimedEventAsync(timedEvent, cancellationToken);

            timedEvent.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.TimedEventCreated,
                TargetEntityType: nameof(TimedEvent),
                TargetEntityId: timedEvent.Id,
                Details: $"Code={request.Code}; Window={request.StartUtc:O}→{request.EndUtc:O}; Multiplier={request.Multiplier}"));

            return Created(timedEvent.Id);
        }
        catch (Exception ex)
        {
            // F3: ArgumentException from the entity factory is defence-in-depth (validators
            // already reject invalid window/multiplier at the FluentValidation layer with 422).
            // Let it fall through to the generic ServerError — no ex.Message leakage.
            _logger.LogError(ex, "Error: in CreateTimedEventCommand");
            return ServerError<int>();
        }
    }
}
