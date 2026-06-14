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
/// </summary>
public class AddLessonCommandHandler : BaseResponseHandler, ICommandHandler<AddLessonCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly IMapper _mapper;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddLessonCommandHandler(
        IMapper mapper,
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _logger = logger;
        _mapper = mapper;
        _repository = repository;
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
            // Without this, a bad UnitId reaches SaveChangesAsync → FK violation → DbUpdateException → 500.
            var unitExists = await _repository.Learning
                .AnyAsync<Unit>(u => u.Id == request.UnitId);

            if (!unitExists)
                return NotFound<string>(_localizer[SharedResourcesKey.UnitNotFound]);

            // DEFECT-2 fix: pre-check optional Skill FK when SkillId is supplied.
            // SetNull behaviour only applies on Skill DELETE, not INSERT — the DB still enforces
            // the FK constraint on insert, so a non-existent SkillId causes a 500 without this guard.
            if (request.SkillId.HasValue)
            {
                var skillExists = await _repository.Learning
                    .AnyAsync<Skill>(sk => sk.Id == request.SkillId.Value);

                if (!skillExists)
                    return NotFound<string>(_localizer[SharedResourcesKey.SkillNotFound]);
            }

            // P7-12 fix: map and stage inline so we retain the tracked-instance reference.
            var lesson = _mapper.Map<Lesson>(request);
            await _repository.Learning.AddAsync(lesson, cancellationToken);

            // P7-12 Bucket C fix: flush within the UoW's open transaction so the DB assigns
            // lesson.Id BEFORE we raise the domain event. The UoW's own SaveChangesAsync after
            // this handler returns is then a no-op (no new staged changes). No double-insert.
            var adminUserId = _currentUser.UserId.GetValueOrDefault();
            await _repository.Learning.FlushAsync(adminUserId, cancellationToken);

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            // Details: structural ids only; no PII.
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
