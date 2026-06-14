using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
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
/// Previous approach (broken): called _service.SubjectService.AddAsync then did a GetByCondition
/// DB-query to retrieve the tracked entity. Because the entity is in EntityState.Added (not yet in
/// the database), the SQL query returned null and RaiseDomainEvent was silently skipped.
/// Fix: map and stage the entity inline so we retain the reference and can call RaiseDomainEvent
/// on the exact tracked instance.
/// </summary>
public class AddSubjectCommandHandler : BaseResponseHandler, ICommandHandler<AddSubjectCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly IMapper _mapper;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddSubjectCommandHandler(
        IMapper mapper,
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _mapper = mapper;
        _repository = repository;
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
            // Without this, a bad GradeId reaches SaveChangesAsync → FK violation → DbUpdateException → 500.
            var gradeExists = await _repository.Learning
                .AnyAsync<Grade>(g => g.Id == request.GradeId);

            if (!gradeExists)
                return NotFound<string>(_localizer[SharedResourcesKey.GradeNotFound]);

            // P7-01: Check for a soft-deleted tree with the same natural key first.
            // The UNIQUE index on (GradeId, SubjectCode, Language) is unfiltered, so a
            // soft-deleted row with the same key would trigger a DbUpdateException rather than
            // a clean validation error. Detect it here and return a clear message.
            var softDeletedExists = await _repository.Learning
                .GetByCondition<Subject>(
                    s => s.GradeId == request.GradeId
                      && s.SubjectCode == request.SubjectCode
                      && s.Language == request.Language,
                    trackChanges: false)
                .IgnoreQueryFilters()
                .AnyAsync(s => s.IsDeleted == true, cancellationToken);

            if (softDeletedExists)
                return BadRequest<string>(_localizer[SharedResourcesKey.SubjectSoftDeletedTreeExists]);

            // P7-01: Check for a live duplicate tree.
            var liveExists = await _repository.Learning
                .AnyAsync<Subject>(s =>
                    s.GradeId == request.GradeId
                    && s.SubjectCode == request.SubjectCode
                    && s.Language == request.Language);

            if (liveExists)
                return BadRequest<string>(_localizer[SharedResourcesKey.SubjectDuplicateTree]);

            // P7-12 fix: map and stage the entity inline to retain a direct reference to the
            // tracked AggregateRoot instance. GetByCondition (SQL query) cannot find an entity
            // in EntityState.Added (not yet in the database), so the previous approach silently
            // dropped the domain event.
            var subject = _mapper.Map<Subject>(request);
            await _repository.Learning.AddAsync(subject, cancellationToken);

            // P7-12 Bucket C fix: flush within the UoW's open transaction so the DB assigns
            // subject.Id BEFORE we raise the domain event. The UoW's own SaveChangesAsync after
            // this handler returns is then a no-op (no new staged changes). No double-insert.
            var adminUserId = _currentUser.UserId.GetValueOrDefault();
            await _repository.Learning.FlushAsync(adminUserId, cancellationToken);

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
