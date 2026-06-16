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

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Add;

/// <summary>
/// P7-01: Creates a Subject. Validates the 4-SubjectCode limit and detects duplicate
/// (GradeId, SubjectCode, Language) trees — including soft-deleted ones (which block re-creation
/// because the UNIQUE index is unfiltered). Publishes AdminActionPerformedEvent, best-effort.
///
/// P7-12 fix (Bucket C): Domain event is raised directly on the mapped Subject instance BEFORE
/// returning from the handler. The UnitOfWorkBehavior (ADR 0002) commits first, then collects and
/// dispatches domain events from all tracked AggregateRoot instances. This guarantees post-commit
/// delivery and exactly-one semantics — a rolled-back command produces zero events because the
/// ChangeTracker is discarded on rollback.
///
/// Option C: all EF queries delegated to ISubjectService (Application/Abstractions).
/// This handler injects only ILearningServiceManager — no ILearningRepositoryManager, no EF types.
/// </summary>
public class AddSubjectCommandHandler : BaseResponseHandler, ICommandHandler<AddSubjectCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly IMapper _mapper;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddSubjectCommandHandler(
        IMapper mapper,
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _mapper = mapper;
        _service = service;
        _currentUser = currentUser;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(AddSubjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // DEFECT-2 fix: pre-check parent Grade existence before staging the insert.
            var gradeExists = await _service.SubjectService.GradeExistsAsync(request.GradeId, cancellationToken);
            if (!gradeExists)
                return NotFound<string>(_localizer[SharedResourcesKey.GradeNotFound]);

            // P7-01: Check for a soft-deleted tree with the same natural key first.
            // The UNIQUE index on (GradeId, SubjectCode, Language) is unfiltered, so a
            // soft-deleted row with the same key would trigger a DbUpdateException.
            var softDeletedExists = await _service.SubjectService.SoftDeletedSubjectExistsAsync(
                request.GradeId, request.SubjectCode, request.Language, cancellationToken);
            if (softDeletedExists)
                return BadRequest<string>(_localizer[SharedResourcesKey.SubjectSoftDeletedTreeExists]);

            // P7-01: Check for a live duplicate tree.
            var liveExists = await _service.SubjectService.LiveSubjectExistsAsync(
                request.GradeId, request.SubjectCode, request.Language, cancellationToken);
            if (liveExists)
                return BadRequest<string>(_localizer[SharedResourcesKey.SubjectDuplicateTree]);

            // P7-12 fix: map and stage the entity; service flushes to obtain DB-assigned Id.
            var subject = _mapper.Map<Subject>(request);
            var adminUserId = _currentUser.UserId.GetValueOrDefault();
            await _service.SubjectService.StageAddSubjectAsync(subject, adminUserId, cancellationToken);

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            // Details: enum-safe identifiers only; no PII (name excluded per BE-TC-PII-1).
            subject.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: adminUserId,
                Action: AdminActions.SubjectCreated,
                TargetEntityType: nameof(Subject),
                TargetEntityId: subject.Id,
                Details: $"SubjectCode={request.SubjectCode}, Language={request.Language}, GradeId={request.GradeId}"));

            return Success<string>(_localizer[SharedResourcesKey.RecordSavedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddSubjectCommand");
            return ServerError<string>();
        }
    }
}
