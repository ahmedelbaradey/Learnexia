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

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Add;

/// <summary>
/// P7-02: Updated to emit <see cref="AdminActionPerformedDomainEvent"/> via post-commit domain-event relay
/// (ADR 0002 / P7-12 fix — event now fires strictly after commit, not before).
/// </summary>
public class AddLessonCommandHandler : BaseResponseHandler, ICommandHandler<AddLessonCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddLessonCommandHandler(
        ILearningServiceManager service,
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _logger = logger;
        _service = service;
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

            var result = await _service.LessonService.AddAsync<AddLessonCommand>(request, cancellationToken);

            if (result.Successed)
            {
                // The service staged a new Lesson in the EF ChangeTracker.
                // Query with trackChanges=true — EF's identity map returns the same tracked instance.
                var tracked = await _repository.Learning
                    .GetByCondition<Lesson>(
                        l => l.UnitId == request.UnitId && l.Name == request.Name,
                        trackChanges: true)
                    .FirstOrDefaultAsync(cancellationToken);

                // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
                tracked?.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.LessonCreated,
                    TargetEntityType: nameof(Lesson),
                    TargetEntityId: 0,
                    Details: $"UnitId={request.UnitId}"));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddLessonCommand");
            return ServerError<string>();
        }
    }
}
