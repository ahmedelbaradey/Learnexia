using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Reorder;

/// <summary>
/// Batch-updates SequenceOrder on the given Subjects.
/// All Subjects must belong to the same (GradeId, Language) tree — cross-tree reorder is rejected.
/// The UnitOfWorkBehavior's transaction wraps all staged updates atomically (deferred-commit module).
/// Publishes <see cref="AdminActionPerformedDomainEvent"/> post-commit, best-effort.
///
/// Option C: all EF queries delegated to ISubjectService. Handler injects only ILearningServiceManager.
/// </summary>
public class ReorderSubjectsCommandHandler : BaseResponseHandler, ICommandHandler<ReorderSubjectsCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReorderSubjectsCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(ReorderSubjectsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.SubjectIds.Count == 0)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // Load all specified subjects with tracking.
            var subjects = await _service.SubjectService.GetSubjectsTrackedByIdsAsync(request.SubjectIds, cancellationToken);

            if (subjects.Count != request.SubjectIds.Count)
                return NotFound<string>(_localizer[SharedResourcesKey.SubjectNotFound]);

            // Validate all belong to the same (GradeId, Language) tree — cross-tree reorder is forbidden.
            var gradeIds = subjects.Select(s => s.GradeId).Distinct().ToList();
            var languages = subjects.Select(s => s.Language).Distinct().ToList();

            if (gradeIds.Count > 1 || languages.Count > 1)
                return BadRequest<string>(_localizer[SharedResourcesKey.ReorderCrossTreeForbidden]);

            // Apply new SequenceOrder based on position in the requested list.
            var subjectsById = subjects.ToDictionary(s => s.Id);
            for (var i = 0; i < request.SubjectIds.Count; i++)
            {
                subjectsById[request.SubjectIds[i]].SequenceOrder = i;
                await _service.SubjectService.StageSubjectUpdateAsync(subjectsById[request.SubjectIds[i]], cancellationToken);
            }

            // Raise domain event on the first tracked subject (representative for the batch).
            // Dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            subjectsById[request.SubjectIds[0]].RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.SubjectReordered,
                TargetEntityType: nameof(Subject),
                TargetEntityId: 0,
                Details: $"Reordered {request.SubjectIds.Count} subjects"));

            return Success<string>(_localizer[SharedResourcesKey.OperationCompletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ReorderSubjectsCommand");
            return ServerError<string>();
        }
    }
}
