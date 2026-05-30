using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetSubjectLessons;

/// <summary>
/// Handles <see cref="GetSubjectLessonsQuery"/>.
/// Verifies the subject exists (404 if not), then loads Units ordered by SequenceOrder,
/// each with their Lessons ordered by SequenceOrder.
///
/// When the request is authenticated (<see cref="ICurrentUserService.UserId"/> is non-null),
/// the <see cref="LearningPathEngine"/> derives per-student <see cref="NodeState"/> and
/// <see cref="MissingPrerequisiteDto"/> for each lesson.
/// When not authenticated (unauthenticated path is now unreachable in production because
/// <c>[Authorize]</c> is applied to the controller action — Batch 4 — but the fallback is kept
/// for defense-in-depth), the static <c>IsLocked</c> placeholder is used.
///
/// A subject with no units returns 200 + empty collection.
/// </summary>
public class GetSubjectLessonsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetSubjectLessonsQuery, BaseResponse<List<UnitWithLessonsDto>>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetSubjectLessonsQueryHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<List<UnitWithLessonsDto>>> Handle(
        GetSubjectLessonsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var subjectExists = await _repository.Learning
                .AnyAsync<Subject>(s => s.Id == request.SubjectId);

            if (!subjectExists)
                return NotFound<List<UnitWithLessonsDto>>(_localizer[SharedResourcesKey.SubjectNotFound]);

            var units = await _repository.Learning
                .GetByCondition<Unit>(u => u.SubjectId == request.SubjectId, false)
                .Include(u => u.Lessons)
                .OrderBy(u => u.SequenceOrder)
                .ToListAsync(cancellationToken);

            if (!units.Any())
                return EmptyCollection(new List<UnitWithLessonsDto>());

            var studentId = _currentUser.UserId;

            if (studentId is not null)
            {
                // ── Authenticated path: derive NodeState from LearningPathEngine ────────────────────

                // Bulk-fetch the 5 engine inputs.
                var nodes = await _repository.Learning
                    .GetSubjectKnowledgeNodesAsync(request.SubjectId, cancellationToken);
                var edges = await _repository.Learning
                    .GetSubjectKnowledgeEdgesAsync(request.SubjectId, cancellationToken);
                var masteryBySkillId = await _repository.Learning
                    .GetSkillMasteryForStudentInSubjectAsync(studentId.Value, request.SubjectId, cancellationToken);
                var completedLessonIds = await _repository.Learning
                    .GetCompletedLessonIdsForStudentInSubjectAsync(studentId.Value, request.SubjectId, cancellationToken);
                var allLessons = await _repository.Learning
                    .GetSubjectLessonsAsync(request.SubjectId, cancellationToken);

                // Load skills for the subject (needed for MasteryThreshold + Name on MissingPrerequisiteDto).
                var skills = await _repository.Learning
                    .GetByCondition<Skill>(sk => sk.Concept.SubjectId == request.SubjectId, false)
                    .ToListAsync(cancellationToken);
                var skillsById = skills.ToDictionary(sk => sk.Id);

                // Run the engine once for the whole subject.
                var unlockStates = LearningPathEngine.ComputeStates(
                    nodes, edges, masteryBySkillId, completedLessonIds, allLessons, skillsById);

#pragma warning disable CS0612 // Obsolete member used intentionally for back-compat population
                var dtos = units.Select(u => new UnitWithLessonsDto
                {
                    UnitId        = u.Id,
                    Name          = u.Name,
                    SequenceOrder = u.SequenceOrder,
                    Lessons       = u.Lessons
                                     .OrderBy(l => l.SequenceOrder)
                                     .Select(l =>
                                     {
                                         if (!unlockStates.TryGetValue(l.Id, out var unlockState))
                                         {
                                             // Defensive: engine should always have an entry for every lesson,
                                             // but if somehow it doesn't, fall back to Available and log.
                                             _logger.LogWarn($"LearningPathEngine returned no state for lesson {l.Id} in subject {request.SubjectId}. Defaulting to Available.");
                                             return new LessonInUnitDto
                                             {
                                                 LessonId             = l.Id,
                                                 Name                 = l.Name,
                                                 Difficulty           = l.Difficulty,
                                                 SequenceOrder        = l.SequenceOrder,
                                                 IsLocked             = l.IsLocked,
                                                 SkillId              = l.SkillId,
                                                 IsBoss               = l.IsBoss,
                                                 State                = NodeState.Available,
                                                 MissingPrerequisites = Array.Empty<MissingPrerequisiteDto>()
                                             };
                                         }

                                         return new LessonInUnitDto
                                         {
                                             LessonId             = l.Id,
                                             Name                 = l.Name,
                                             Difficulty           = l.Difficulty,
                                             SequenceOrder        = l.SequenceOrder,
                                             IsLocked             = l.IsLocked,
                                             SkillId              = l.SkillId,
                                             IsBoss               = l.IsBoss,
                                             State                = unlockState.State,
                                             MissingPrerequisites = unlockState.MissingPrerequisites
                                         };
                                     }).ToList()
                }).ToList();
#pragma warning restore CS0612

                return Success(dtos);
            }
            else
            {
                // ── Anonymous/unauthenticated fallback ───────────────────────────────────────────────
                // This path is unreachable in production (the controller action now requires [Authorize]).
                // Kept for defense-in-depth. Uses the P2-02 static IsLocked placeholder.
#pragma warning disable CS0612 // Obsolete member used intentionally for back-compat population
                var dtos = units.Select(u => new UnitWithLessonsDto
                {
                    UnitId        = u.Id,
                    Name          = u.Name,
                    SequenceOrder = u.SequenceOrder,
                    Lessons       = u.Lessons
                                     .OrderBy(l => l.SequenceOrder)
                                     .Select(l => new LessonInUnitDto
                                     {
                                         LessonId      = l.Id,
                                         Name          = l.Name,
                                         Difficulty    = l.Difficulty,
                                         SequenceOrder = l.SequenceOrder,
                                         IsLocked      = l.IsLocked,
                                         SkillId       = l.SkillId,
                                         IsBoss        = l.IsBoss,
                                         State         = l.IsLocked ? NodeState.Locked : NodeState.Available,
                                         MissingPrerequisites = Array.Empty<MissingPrerequisiteDto>()
                                     }).ToList()
                }).ToList();
#pragma warning restore CS0612

                return Success(dtos);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetSubjectLessonsQuery");
            return ServerError<List<UnitWithLessonsDto>>(ex.Message);
        }
    }
}
