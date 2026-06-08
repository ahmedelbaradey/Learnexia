using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Add;

/// <summary>
/// P7-01: Creates a Subject. Validates the 4-SubjectCode limit and detects duplicate
/// (GradeId, SubjectCode, Language) trees — including soft-deleted ones (which block re-creation
/// because the UNIQUE index is unfiltered). Publishes AdminActionPerformedEvent, best-effort.
/// </summary>
public class AddSubjectCommandHandler : BaseResponseHandler, ICommandHandler<AddSubjectCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddSubjectCommandHandler(
        ILearningServiceManager service,
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        IPublisher publisher,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _repository = repository;
        _currentUser = currentUser;
        _publisher = publisher;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(AddSubjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

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

            var result = await _service.SubjectService.AddAsync<AddSubjectCommand>(request, cancellationToken);

            if (result.Successed)
            {
                try
                {
                    await _publisher.Publish(new AdminActionPerformedEvent(
                        EventId: Guid.NewGuid(),
                        OccurredAtUtc: DateTime.UtcNow,
                        AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                        Action: AdminActions.SubjectCreated,
                        TargetEntityType: nameof(Subject),
                        TargetEntityId: 0,
                        Details: $"SubjectCode={request.SubjectCode}, Language={request.Language}, GradeId={request.GradeId}"),
                        cancellationToken);
                }
                catch (Exception publishEx)
                {
                    _logger.LogError(publishEx, "P7-01: AdminActionPerformedEvent publish failed for AddSubjectCommand");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddSubjectCommand");
            return ServerError<string>();
        }
    }
}
