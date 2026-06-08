using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Localization;
using Resources;
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.Add;

public class AddUnitCommandHandler : BaseResponseHandler, ICommandHandler<AddUnitCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddUnitCommandHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        IPublisher publisher,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _currentUser = currentUser;
        _publisher = publisher;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(AddUnitCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var result = await _service.UnitService.AddAsync<AddUnitCommand>(request, cancellationToken);

            if (result.Successed)
            {
                try
                {
                    await _publisher.Publish(new AdminActionPerformedEvent(
                        EventId: Guid.NewGuid(),
                        OccurredAtUtc: DateTime.UtcNow,
                        AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                        Action: AdminActions.UnitCreated,
                        TargetEntityType: nameof(LearningUnit),
                        TargetEntityId: 0,
                        Details: $"SubjectId={request.SubjectId}"),
                        cancellationToken);
                }
                catch (Exception publishEx)
                {
                    _logger.LogError(publishEx, "P7-01: AdminActionPerformedEvent publish failed for AddUnitCommand");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddUnitCommand");
            return ServerError<string>();
        }
    }
}
