using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Commands.Add;

/// <summary>
/// P7-03: Creates a new Skill and auto-creates a wrapping <see cref="KnowledgeNode"/>
/// (NodeType=Skill) in the same transaction. Node creation uses the Skill's Concept → Subject
/// chain to resolve SubjectId and GradeId. If resolution fails, the handler returns
/// <c>Successed=false</c> and does NOT create an orphan skill.
///
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class AddSkillCommandHandler : BaseResponseHandler, ICommandHandler<AddSkillCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddSkillCommandHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        IPublisher publisher,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _publisher = publisher;
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

            // Resolve the Concept → Subject chain needed for the auto-created KnowledgeNode.
            var subject = await _repository.Learning.GetSubjectByConceptIdAsync(request.ConceptId, cancellationToken);

            if (subject is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.SkillNodeAutoCreateFailed]);

            // Map command → entity and stage the skill.
            var skill = _mapper.Map<Skill>(request);
            await _repository.Learning.AddAsync(skill, cancellationToken);

            // Auto-create the wrapping KnowledgeNode.
            // Difficulty: use a sensible default (3 = mid-range on the 1–5 scale) since the
            // AddSkillDto does not carry a Difficulty field. The admin can adjust it via
            // a future graph-node edit endpoint (out of P7-03 scope).
            var node = new KnowledgeNode
            {
                Name = request.Name,
                NodeType = KnowledgeNodeType.Skill,
                SubjectId = subject.Id,
                GradeId = subject.GradeId,
                Difficulty = 3,
                // SkillId is set after EF assigns the Skill.Id on commit.
                // EF Core navigations: we store the navigation reference so EF resolves the FK.
                Skill = skill
            };
            await _repository.Learning.AddAsync(node, cancellationToken);

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.SkillCreated,
                    TargetEntityType: nameof(Skill),
                    TargetEntityId: 0,
                    Details: $"ConceptId={request.ConceptId}, SubjectId={subject.Id}"),
                    cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, "P7-03: AdminActionPerformedEvent publish failed for AddSkillCommand");
            }

            return Success<string>(_localizer[SharedResourcesKey.RecordSavedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddSkillCommand");
            return ServerError<string>();
        }
    }
}
