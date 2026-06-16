using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Add;

/// <summary>
/// P7-02: Creates a Lesson.
///
/// P7-12 fix (Bucket C): Domain event is raised directly on the mapped Lesson instance BEFORE
/// returning from the handler. See AddSubjectCommandHandler for the full rationale — same
/// GetByCondition-null bug (entity in EntityState.Added not visible via DB query) and same fix
/// (map and stage inline to retain the tracked-instance reference).
///
/// Option-C: all EF calls moved into ILessonService (Infrastructure). Handler is now thin.
/// </summary>
public class AddLessonCommandHandler : BaseResponseHandler, ICommandHandler<AddLessonCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly IMapper _mapper;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddLessonCommandHandler(
        IMapper mapper,
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _logger = logger;
        _mapper = mapper;
        _service = service;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(AddLessonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // DEFECT-2 fix: pre-check parent Unit existence before staging the insert.
            var unitExists = await _service.LessonService.UnitExistsAsync(request.UnitId, cancellationToken);
            if (!unitExists)
                return NotFound<string>(_localizer[SharedResourcesKey.UnitNotFound]);

            // DEFECT-2 fix: pre-check optional Skill FK when SkillId is supplied.
            if (request.SkillId.HasValue)
            {
                var skillExists = await _service.LessonService.SkillExistsAsync(request.SkillId.Value, cancellationToken);
                if (!skillExists)
                    return NotFound<string>(_localizer[SharedResourcesKey.SkillNotFound]);
            }

            // P7-12 fix: map and stage inline so we retain the tracked-instance reference.
            var lesson = _mapper.Map<Lesson>(request);
            var adminUserId = _currentUser.UserId.GetValueOrDefault();

            // StageAddLessonAsync: stages AddAsync then FlushAsync to obtain lesson.Id within the UoW transaction.
            await _service.LessonService.StageAddLessonAsync(lesson, adminUserId, cancellationToken);

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            lesson.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: adminUserId,
                Action: AdminActions.LessonCreated,
                TargetEntityType: nameof(Lesson),
                TargetEntityId: lesson.Id,
                Details: $"UnitId={request.UnitId}"));

            return Success<string>(_localizer[SharedResourcesKey.RecordSavedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddLessonCommand");
            return ServerError<string>();
        }
    }
}
