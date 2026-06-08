using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Reorder;

/// <summary>
/// Batch-updates SequenceOrder on the given Subjects.
/// All Subjects must belong to the same (GradeId, Language) tree — cross-tree reorder is rejected.
/// The UnitOfWorkBehavior's transaction wraps all staged updates atomically (deferred-commit module).
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class ReorderSubjectsCommandHandler : BaseResponseHandler, ICommandHandler<ReorderSubjectsCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReorderSubjectsCommandHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        IPublisher publisher,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _publisher = publisher;
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
            var subjects = await _repository.Learning
                .GetByCondition<Subject>(s => request.SubjectIds.Contains(s.Id), trackChanges: true)
                .ToListAsync(cancellationToken);

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
                await _repository.Learning.UpdateAsync(subjectsById[request.SubjectIds[i]]);
            }

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.SubjectReordered,
                    TargetEntityType: nameof(Subject),
                    TargetEntityId: 0,
                    Details: $"Reordered {request.SubjectIds.Count} subjects"), cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, $"P7-01: AdminActionPerformedEvent publish failed for SubjectReorder");
            }

            return Success<string>(_localizer[SharedResourcesKey.OperationCompletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ReorderSubjectsCommand");
            return ServerError<string>();
        }
    }
}
