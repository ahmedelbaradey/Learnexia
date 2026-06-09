using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Delete;

/// <summary>
/// P7-01: Soft-deletes a Subject by setting IsDeleted = true (FullAuditedEntity pattern).
/// The UnitOfWorkBehavior will stamp DeletedAt/DeletedBy after SaveChangesAsync.
/// Blocks deletion when the Subject still has non-deleted Units ("Subject not empty" guard).
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class DeleteSubjectCommandHandler : BaseResponseHandler, ICommandHandler<DeleteSubjectCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public DeleteSubjectCommandHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(DeleteSubjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var subject = await _repository.Learning
                .GetByCondition<Subject>(s => s.Id == request.Id, trackChanges: true)
                .FirstOrDefaultAsync(cancellationToken);

            if (subject is null)
                return NotFound<string>(_localizer[SharedResourcesKey.SubjectNotFound]);

            // "Subject not empty" guard — block soft-delete when non-deleted Units still exist.
            var hasUnits = await _repository.Learning
                .AnyAsync<LearningUnit>(u => u.SubjectId == request.Id);

            if (hasUnits)
                return BadRequest<string>(_localizer[SharedResourcesKey.SubjectNotEmpty]);

            // Soft-delete: set the flag; UnitOfWorkBehavior stamps DeletedAt/DeletedBy.
            subject.IsDeleted = true;
            await _repository.Learning.UpdateAsync(subject);

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            subject.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.SubjectDeleted,
                TargetEntityType: nameof(Subject),
                TargetEntityId: request.Id,
                Details: null));

            return Success<string>(_localizer[SharedResourcesKey.ItemDeletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in DeleteSubjectCommand");
            return ServerError<string>();
        }
    }
}
