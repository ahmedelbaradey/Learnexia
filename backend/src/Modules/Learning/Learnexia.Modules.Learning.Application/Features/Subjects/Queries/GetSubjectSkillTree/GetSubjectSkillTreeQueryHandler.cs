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

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetSubjectSkillTree;

/// <summary>
/// Handles <see cref="GetSubjectSkillTreeQuery"/>.
/// Verifies the subject exists (404 if not), then loads Concepts with their Skills and the Skills' Lessons.
///
/// P8-03: Before loading, the handler verifies that the requested subject's <see cref="ContentLanguage"/>
/// matches the effective language resolved for its <see cref="SubjectCode"/> from the student's JWT claim.
/// If it does not match, it attempts to serve the correct-language tree (falling back if absent) and logs a
/// warning. This prevents a student from accessing the wrong-language tree via a direct <c>SubjectId</c>.
///
/// When the request is authenticated (<see cref="ICurrentUserService.UserId"/> is non-null),
/// the <see cref="LearningPathEngine"/> derives per-student <see cref="NodeState"/> for each skill.
/// When not authenticated (unauthenticated path is now unreachable in production because
/// <c>[Authorize]</c> is applied to the controller action — Batch 4 — but the fallback is kept
/// for defense-in-depth), the static placeholder is used.
///
/// Concepts ordered by Id (surrogate for absent SequenceOrder — deferred to P2-11).
/// Skills within each concept ordered by Id.
/// </summary>
public class GetSubjectSkillTreeQueryHandler
    : BaseResponseHandler, IQueryHandler<GetSubjectSkillTreeQuery, BaseResponse<List<ConceptNodeDto>>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetSubjectSkillTreeQueryHandler(
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

    public async Task<BaseResponse<List<ConceptNodeDto>>> Handle(
        GetSubjectSkillTreeQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load the subject to verify it exists and to check its language.
            var subject = await _repository.Learning
                .GetByCondition<Subject>(s => s.Id == request.SubjectId, false)
                .FirstOrDefaultAsync(cancellationToken);

            if (subject is null)
                return NotFound<List<ConceptNodeDto>>(_localizer[SharedResourcesKey.SubjectNotFound]);

            // P8-03-BE-1/BE-4: resolve the effective language for this subject code.
            var learnerLang = LearningLanguageClaimAccessor.GetLearningLanguage(_currentUser, _logger);
            var resolved = SubjectLanguageResolver.Resolve(subject.SubjectCode, learnerLang);

            // P8-03-BE-4/BE-6: if the requested subject is in the wrong language, redirect to the
            // correct-language tree for the same SubjectCode in the same Grade.
            if (subject.Language != resolved)
            {
                var correctSubject = await _repository.Learning
                    .GetByCondition<Subject>(
                        s => s.GradeId == subject.GradeId
                          && s.SubjectCode == subject.SubjectCode
                          && s.Language == resolved,
                        trackChanges: false)
                    .FirstOrDefaultAsync(cancellationToken);

                if (correctSubject is not null)
                {
                    // Silently serve the correct-language tree.
                    subject = correctSubject;
                }
                else
                {
                    // Missing-tree fallback (AC-10): the resolved tree is absent; serve what was requested
                    // and log a warning.
                    _logger.LogWarn(
                        $"Missing subject tree for SubjectCode={subject.SubjectCode} Language={resolved}" +
                        $" GradeId={subject.GradeId}. Falling back to Language={subject.Language}.");
                }
            }

            var effectiveSubjectId = subject.Id;

            var concepts = await _repository.Learning
                .GetByCondition<Concept>(c => c.SubjectId == effectiveSubjectId, false)
                .Include(c => c.Skills)
                    .ThenInclude(sk => sk.Lessons)
                .OrderBy(c => c.Id)
                .ToListAsync(cancellationToken);

            if (!concepts.Any())
                return EmptyCollection(new List<ConceptNodeDto>());

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
                var lessons = await _repository.Learning
                    .GetSubjectLessonsAsync(effectiveSubjectId, cancellationToken);

                // Build skillsById for the engine (all skills in subject, already loaded via concepts).
                var skillsById = concepts
                    .SelectMany(c => c.Skills)
                    .DistinctBy(sk => sk.Id)
                    .ToDictionary(sk => sk.Id);

                // Run the engine once for the whole subject.
                var unlockStates = LearningPathEngine.ComputeStates(
                    nodes, edges, masteryBySkillId, completedLessonIds, lessons, skillsById);

                // Build a lookup: skillId -> list of lesson unlock states for that skill.
                var statesBySkillId = unlockStates.Values
                    .Where(s => lessons.Any(l => l.Id == s.LessonId && l.SkillId.HasValue))
                    .GroupBy(s => lessons.First(l => l.Id == s.LessonId).SkillId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var dtos = concepts.Select(c => new ConceptNodeDto
                {
                    ConceptId = c.Id,
                    Name      = c.Name,
                    Skills    = c.Skills
                                 .OrderBy(sk => sk.Id)
                                 .Select(sk =>
                                 {
                                     statesBySkillId.TryGetValue(sk.Id, out var skillLessonStates);
                                     skillLessonStates ??= new List<LessonUnlockStateDto>();

                                     // Skill NodeState derivation (per plan):
                                     //   ANY lesson Completed → Completed
                                     //   ANY lesson Available → Available
                                     //   else → Locked (or Available if no lessons / not in engine output)
                                     NodeState skillState;
                                     if (!skillLessonStates.Any())
                                     {
                                         // No lessons → default Available (plan: skills with no lessons referencing them).
                                         skillState = NodeState.Available;
                                     }
                                     else if (skillLessonStates.Any(ls => ls.State == NodeState.Completed))
                                     {
                                         skillState = NodeState.Completed;
                                     }
                                     else if (skillLessonStates.Any(ls => ls.State == NodeState.Available))
                                     {
                                         skillState = NodeState.Available;
                                     }
                                     else
                                     {
                                         skillState = NodeState.Locked;
                                     }

                                     // MissingPrerequisites: union of all locked lessons' prereqs, deduplicated by PrereqSkillId.
                                     IReadOnlyList<MissingPrerequisiteDto>? missingPrereqs = null;
                                     if (skillState == NodeState.Locked)
                                     {
                                         missingPrereqs = skillLessonStates
                                             .Where(ls => ls.State == NodeState.Locked)
                                             .SelectMany(ls => ls.MissingPrerequisites)
                                             .DistinctBy(mp => mp.PrereqSkillId)
                                             .ToList();
                                     }

                                     return new SkillNodeDto
                                     {
                                         SkillId              = sk.Id,
                                         Name                 = sk.Name,
                                         MasteryThreshold     = sk.MasteryThreshold,
                                         EstimatedTimeMinutes = sk.EstimatedTimeMinutes,
                                         State                = skillState,
                                         LessonIds            = sk.Lessons.Select(l => l.Id).ToList(),
                                         MissingPrerequisites = missingPrereqs
                                     };
                                 }).ToList()
                }).ToList();

                return Success(dtos);
            }
            else
            {
                // ── Anonymous/unauthenticated fallback ───────────────────────────────────────────────
                // This path is unreachable in production (the controller action now requires [Authorize]).
                // Kept for defense-in-depth. Uses the P2-02 static placeholder derivation.
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
                                     State                = DeriveNodeStatePlaceholder(sk),
                                     LessonIds            = sk.Lessons.Select(l => l.Id).ToList(),
                                     MissingPrerequisites = null
                                 }).ToList()
                }).ToList();

                return Success(dtos);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetSubjectSkillTreeQuery");
            return ServerError<List<ConceptNodeDto>>();  // P8-SEC-3: do not expose ex.Message to the client.
        }
    }

    /// <summary>
    /// P2-02 static placeholder (unauthenticated fallback — unreachable in production post P2-04).
    /// If the skill has lessons and ALL of them are locked → Locked; otherwise → Available.
    /// </summary>
    [Obsolete("P2-02 placeholder. Superseded by LearningPathEngine in P2-04. Will be removed in P2-09 or P6-06.")]
    private static NodeState DeriveNodeStatePlaceholder(Skill skill)
    {
        if (!skill.Lessons.Any())
            return NodeState.Available;
        return skill.Lessons.All(l => l.IsLocked)
            ? NodeState.Locked
            : NodeState.Available;
    }
}
