using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetSubjectSkillTree;

/// <summary>
/// Handles <see cref="GetSubjectSkillTreeQuery"/>.
/// Verifies the subject exists (404 if not), then loads Concepts with their Skills and the Skills' Lessons.
/// NodeState is derived statically from Lesson.IsLocked — replaced by real progress in P2-03/P2-04.
/// Concepts ordered by Id (surrogate for absent SequenceOrder — deferred to P2-11).
/// Skills within each concept ordered by Id.
/// </summary>
public class GetSubjectSkillTreeQueryHandler
    : BaseResponseHandler, IQueryHandler<GetSubjectSkillTreeQuery, BaseResponse<List<ConceptNodeDto>>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetSubjectSkillTreeQueryHandler(
        ILearningRepositoryManager repository,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<List<ConceptNodeDto>>> Handle(
        GetSubjectSkillTreeQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var subjectExists = await _repository.Learning
                .AnyAsync<Subject>(s => s.Id == request.SubjectId);

            if (!subjectExists)
                return NotFound<List<ConceptNodeDto>>(_localizer[SharedResourcesKey.SubjectNotFound]);

            var concepts = await _repository.Learning
                .GetByCondition<Concept>(c => c.SubjectId == request.SubjectId, false)
                .Include(c => c.Skills)
                    .ThenInclude(sk => sk.Lessons)
                .OrderBy(c => c.Id)
                .ToListAsync(cancellationToken);

            if (!concepts.Any())
                return EmptyCollection(new List<ConceptNodeDto>());

            var dtos = concepts.Select(c => new ConceptNodeDto
            {
                ConceptId = c.Id,
                Name      = c.Name,
                Skills    = c.Skills
                             .OrderBy(sk => sk.Id)
                             .Select(sk => new SkillNodeDto
                             {
                                 SkillId              = sk.Id,
                                 Name                 = sk.Name,
                                 MasteryThreshold     = sk.MasteryThreshold,
                                 EstimatedTimeMinutes = sk.EstimatedTimeMinutes,
                                 State                = DeriveNodeState(sk),
                                 LessonIds            = sk.Lessons.Select(l => l.Id).ToList()
                             }).ToList()
            }).ToList();

            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetSubjectSkillTreeQuery");
            return ServerError<List<ConceptNodeDto>>(ex.Message);
        }
    }

    /// <summary>
    /// P2-02 static placeholder: if the skill has lessons and ALL of them are locked → Locked;
    /// otherwise → Available. P2-03/P2-04 replaces this derivation with real per-student progress.
    /// </summary>
    private static NodeState DeriveNodeState(Skill skill)
    {
        if (!skill.Lessons.Any())
            return NodeState.Available;
        return skill.Lessons.All(l => l.IsLocked)
            ? NodeState.Locked
            : NodeState.Available;
    }
}
