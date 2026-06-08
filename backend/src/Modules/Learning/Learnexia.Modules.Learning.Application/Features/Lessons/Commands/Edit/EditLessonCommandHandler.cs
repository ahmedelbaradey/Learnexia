using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Edit;

/// <summary>
/// P7-02: Updated to emit <see cref="AdminActionPerformedEvent"/> and fix ServerError (no ex.Message).
/// </summary>
public class EditLessonCommandHandler : BaseResponseHandler, ICommandHandler<EditLessonCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditLessonCommandHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        IPublisher publisher,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _logger = logger;
        _service = service;
        _currentUser = currentUser;
        _publisher = publisher;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(EditLessonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var result = await _service.LessonService.UpdateAsync(request);

            if (result.Successed)
            {
                try
                {
                    await _publisher.Publish(new AdminActionPerformedEvent(
                        EventId: Guid.NewGuid(),
                        OccurredAtUtc: DateTime.UtcNow,
                        AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                        Action: AdminActions.LessonUpdated,
                        TargetEntityType: nameof(Lesson),
                        TargetEntityId: request.Id,
                        Details: null),
                        cancellationToken);
                }
                catch (Exception publishEx)
                {
                    _logger.LogError(publishEx, $"P7-02: AdminActionPerformedEvent publish failed for EditLessonCommand, LessonId={request.Id}");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in EditLessonCommand");
            return ServerError<string>();
        }
    }
}
