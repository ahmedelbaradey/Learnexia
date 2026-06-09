using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Commands.Add;

/// <summary>
/// P7-03: Creates a new Skill and auto-creates a wrapping <see cref="KnowledgeNode"/>
/// (NodeType=Skill) in the same transaction. Node creation uses the Skill's Concept → Subject
/// chain to resolve SubjectId and GradeId. If resolution fails, the handler returns
/// <c>Successed=false</c> and does NOT create an orphan skill.
///
/// P7-12: Domain event raised on the Skill aggregate (post-commit via UnitOfWorkBehavior, ADR 0002).
/// </summary>
public class AddSkillCommandHandler : BaseResponseHandler, ICommandHandler<AddSkillCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddSkillCommandHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(AddSkillCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // DEFECT-2 fix: explicit Concept existence check before staging the insert.
            // GetSubjectByConceptIdAsync returns null when the concept is missing, but making
            // the 404 explicit here ensures the correct status code and message is returned.
            var conceptExists = await _repository.Learning
                .AnyAsync<Concept>(c => c.Id == request.ConceptId);

            if (!conceptExists)
                return NotFound<string>(_localizer[SharedResourcesKey.ConceptNotFound]);

            // Resolve the Concept → Subject chain needed for the auto-created KnowledgeNode.
            var subject = await _repository.Learning.GetSubjectByConceptIdAsync(request.ConceptId, cancellationToken);

            if (subject is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.SkillNodeAutoCreateFailed]);

            // Map command → entity and stage the skill.
            var skill = _mapper.Map<Skill>(request);
            await _repository.Learning.AddAsync(skill, cancellationToken);

            // Auto-create the wrapping KnowledgeNode.
            var node = new KnowledgeNode
            {
                Name = request.Name,
                NodeType = KnowledgeNodeType.Skill,
                SubjectId = subject.Id,
                GradeId = subject.GradeId,
                Difficulty = 3,
                Skill = skill
            };
            await _repository.Learning.AddAsync(node, cancellationToken);

            // Raise domain event on the Skill aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            skill.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.SkillCreated,
                TargetEntityType: nameof(Skill),
                TargetEntityId: 0,
                Details: $"ConceptId={request.ConceptId}, SubjectId={subject.Id}"));

            return Success<string>(_localizer[SharedResourcesKey.RecordSavedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddSkillCommand");
            return ServerError<string>();
        }
    }
}
