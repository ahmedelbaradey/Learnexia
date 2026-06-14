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
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.Add;

/// <summary>
/// P7-12 fix (Bucket C): Domain event is raised directly on the mapped Unit instance.
/// See AddSubjectCommandHandler for the full rationale — same GetByCondition-null bug and same fix.
/// </summary>
public class AddUnitCommandHandler : BaseResponseHandler, ICommandHandler<AddUnitCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly IMapper _mapper;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddUnitCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(AddUnitCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // DEFECT-2 fix: pre-check parent Subject existence before staging the insert.
            // Without this, a bad SubjectId reaches SaveChangesAsync → FK violation → DbUpdateException → 500.
            var subjectExists = await _repository.Learning
                .AnyAsync<Subject>(s => s.Id == request.SubjectId);

            if (!subjectExists)
                return NotFound<string>(_localizer[SharedResourcesKey.SubjectNotFound]);

            // P7-12 fix: map and stage inline so we retain the tracked-instance reference.
            var unit = _mapper.Map<LearningUnit>(request);
            await _repository.Learning.AddAsync(unit, cancellationToken);

            // P7-12 Bucket C fix: flush within the UoW's open transaction so the DB assigns
            // unit.Id BEFORE we raise the domain event. The UoW's own SaveChangesAsync after
            // this handler returns is then a no-op (no new staged changes). No double-insert.
            var adminUserId = _currentUser.UserId.GetValueOrDefault();
            await _repository.Learning.FlushAsync(adminUserId, cancellationToken);

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            // Details: structural ids only; no PII.
            unit.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: adminUserId,
                Action: AdminActions.UnitCreated,
                TargetEntityType: nameof(LearningUnit),
                TargetEntityId: unit.Id,
                Details: $"SubjectId={request.SubjectId}"));

            return Success<string>(_localizer[SharedResourcesKey.RecordSavedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddUnitCommand");
            return ServerError<string>();
        }
    }
}
