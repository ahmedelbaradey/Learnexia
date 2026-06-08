using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Modules.Learning.Application.Helpers;
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
/// P8-03: Before loading, the handler verifies that the requested subject's <see cref="ContentLanguage"/>
/// matches the effective language resolved for its <see cref="SubjectCode"/> from the student's JWT claim.
/// If it does not match, it redirects to the correct-language tree (falling back if absent with a warning).
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
            // Load the subject to verify it exists and to check its language.
            // P7-01: IsActive == true filter — inactive subject returns 404 for students.
            var subject = await _repository.Learning
                .GetByCondition<Subject>(s => s.Id == request.SubjectId && s.IsActive, false)
                .FirstOrDefaultAsync(cancellationToken);

            if (subject is null)
                return NotFound<List<UnitWithLessonsDto>>(_localizer[SharedResourcesKey.SubjectNotFound]);

            // P8-03-BE-1/BE-4: resolve the effective language for this subject code.
            var learnerLang = LearningLanguageClaimAccessor.GetLearningLanguage(_currentUser, _logger);
            var resolved = SubjectLanguageResolver.Resolve(subject.SubjectCode, learnerLang);

            // P8-03-BE-4/BE-6: if the requested subject is in the wrong language, redirect to the
            // correct-language tree for the same SubjectCode in the same Grade.
            if (subject.Language != resolved)
            {
                // P7-01: Also require IsActive on the redirected subject (student path).
                var correctSubject = await _repository.Learning
                    .GetByCondition<Subject>(
                        s => s.GradeId == subject.GradeId
                          && s.SubjectCode == subject.SubjectCode
                          && s.Language == resolved
                          && s.IsActive,
                        trackChanges: false)
                    .FirstOrDefaultAsync(cancellationToken);

                if (correctSubject is not null)
                {
                    subject = correctSubject;
                }
                else
                {
                    // Missing-tree fallback (AC-10): resolved tree absent; log and continue with the requested tree.
                    _logger.LogWarn(
                        $"Missing subject tree for SubjectCode={subject.SubjectCode} Language={resolved}" +
                        $" GradeId={subject.GradeId}. Falling back to Language={subject.Language}.");
                }
            }

            var effectiveSubjectId = subject.Id;

            // P7-01: IsActive == true filter — inactive units hidden from student reads.
            var units = await _repository.Learning
                .GetByCondition<Unit>(u => u.SubjectId == effectiveSubjectId && u.IsActive, false)
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
                    .GetSubjectKnowledgeNodesAsync(effectiveSubjectId, cancellationToken);
                var edges = await _repository.Learning
                    .GetSubjectKnowledgeEdgesAsync(effectiveSubjectId, cancellationToken);
                var masteryBySkillId = await _repository.Learning
                    .GetSkillMasteryForStudentInSubjectAsync(studentId.Value, effectiveSubjectId, cancellationToken);
                var completedLessonIds = await _repository.Learning
                    .GetCompletedLessonIdsForStudentInSubjectAsync(studentId.Value, effectiveSubjectId, cancellationToken);
                var allLessons = await _repository.Learning
                    .GetSubjectLessonsAsync(effectiveSubjectId, cancellationToken);

                // Load skills for the subject (needed for MasteryThreshold + Name on MissingPrerequisiteDto).
                var skills = await _repository.Learning
                    .GetByCondition<Skill>(sk => sk.Concept.SubjectId == effectiveSubjectId, false)
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
                                     .Where(l => l.IsActive)
                                     .OrderBy(l => l.SequenceOrder)
                                     .Select(l =>
                                     {
                                         if (!unlockStates.TryGetValue(l.Id, out var unlockState))
                                         {
                                             // Defensive: engine should always have an entry for every lesson,
                                             // but if somehow it doesn't, fall back to Available and log.
                                             _logger.LogWarn($"LearningPathEngine returned no state for lesson {l.Id} in subject {effectiveSubjectId}. Defaulting to Available.");
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
                                     .Where(l => l.IsActive)
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
            return ServerError<List<UnitWithLessonsDto>>();  // P8-SEC-3: do not expose ex.Message to the client.
        }
    }
}
