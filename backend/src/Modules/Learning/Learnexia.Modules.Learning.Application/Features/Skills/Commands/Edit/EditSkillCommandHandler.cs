using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Commands.Edit;

/// <summary>
/// P7-03: Updates an existing Skill.
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// Mass-assignment guard: IsActive and IsDeleted are ignored on the Edit map (see SkillsProfile).
/// </summary>
public class EditSkillCommandHandler : BaseResponseHandler, ICommandHandler<EditSkillCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditSkillCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(EditSkillCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var result = await _service.SkillService.UpdateAsync(request);

            if (result.Successed)
            {
                try
                {
                    await _publisher.Publish(new AdminActionPerformedEvent(
                        EventId: Guid.NewGuid(),
                        OccurredAtUtc: DateTime.UtcNow,
                        AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                        Action: AdminActions.SkillUpdated,
                        TargetEntityType: nameof(Skill),
                        TargetEntityId: request.Id,
                        Details: null),
                        cancellationToken);
                }
                catch (Exception publishEx)
                {
                    _logger.LogError(publishEx, $"P7-03: AdminActionPerformedEvent publish failed for EditSkillCommand, SkillId={request.Id}");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in EditSkillCommand");
            return ServerError<string>();
        }
    }
}
