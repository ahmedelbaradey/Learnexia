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

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Edit;

/// <summary>
/// P7-01: Updates a Subject. Detects duplicate-tree conflicts when the natural key fields change.
/// Publishes AdminActionPerformedEvent, best-effort.
/// </summary>
public class EditSubjectCommandHandler : BaseResponseHandler, ICommandHandler<EditSubjectCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditSubjectCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(EditSubjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // P7-01: Check for a duplicate (GradeId, SubjectCode, Language) tree
            // when the natural-key fields are being changed (exclude the current row).
            var conflict = await _repository.Learning
                .AnyAsync<Subject>(s =>
                    s.Id != request.Id
                    && s.GradeId == request.GradeId
                    && s.SubjectCode == request.SubjectCode
                    && s.Language == request.Language);

            if (conflict)
                return BadRequest<string>(_localizer[SharedResourcesKey.SubjectDuplicateTree]);

            var result = await _service.SubjectService.UpdateAsync(request);

            if (result.Successed)
            {
                try
                {
                    await _publisher.Publish(new AdminActionPerformedEvent(
                        EventId: Guid.NewGuid(),
                        OccurredAtUtc: DateTime.UtcNow,
                        AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                        Action: AdminActions.SubjectUpdated,
                        TargetEntityType: nameof(Subject),
                        TargetEntityId: request.Id,
                        Details: null),
                        cancellationToken);
                }
                catch (Exception publishEx)
                {
                    _logger.LogError(publishEx, $"P7-01: AdminActionPerformedEvent publish failed for SubjectId={request.Id}");
                }
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
