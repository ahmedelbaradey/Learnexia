using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Edit;

/// <summary>
/// P7-01: Updates a Subject. Detects duplicate-tree conflicts when the natural key fields change.
/// Publishes AdminActionPerformedEvent, best-effort.
///
/// Option C: all EF queries delegated to ISubjectService. Handler injects only ILearningServiceManager.
/// </summary>
public class EditSubjectCommandHandler : BaseResponseHandler, ICommandHandler<EditSubjectCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditSubjectCommandHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _currentUser = currentUser;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(EditSubjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // P2-QC-DEFECT-1 (immutability): SubjectCode and Language are ignored in the
            // AutoMapper Edit mapping — they cannot be changed via this endpoint. Load the
            // stored natural-key values so the duplicate check uses the ACTUAL key fields.
            var existing = await _service.SubjectService.GetSubjectNoTrackingAsync(request.Id, cancellationToken);
            if (existing is null)
                return NotFound<string>(_localizer[SharedResourcesKey.SubjectNotFound]);

            // P7-01: Detect a GradeId move that would collide with an existing tree slot.
            // SubjectCode and Language come from the stored row — they cannot be changed.
            var conflict = await _service.SubjectService.SubjectConflictExistsAsync(
                request.Id, request.GradeId, existing.SubjectCode, existing.Language, cancellationToken);
            if (conflict)
                return BadRequest<string>(_localizer[SharedResourcesKey.SubjectDuplicateTree]);

            var result = await _service.SubjectService.UpdateAsync(request);

            if (result.Successed)
            {
                // The service fetched the Subject with trackChanges=true and it is in the EF ChangeTracker.
                // Retrieve the tracked instance to raise the domain event on it.
                var tracked = await _service.SubjectService.GetSubjectTrackedAsync(request.Id, cancellationToken);

                // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
                tracked?.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.SubjectUpdated,
                    TargetEntityType: nameof(Subject),
                    TargetEntityId: request.Id,
                    Details: null));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in EditSubjectCommand");
            return ServerError<string>();
        }
    }
}
