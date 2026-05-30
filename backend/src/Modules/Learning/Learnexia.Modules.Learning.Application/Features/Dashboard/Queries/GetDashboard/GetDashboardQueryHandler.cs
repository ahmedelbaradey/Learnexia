using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Dashboard.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Dashboard.Queries.GetDashboard;

/// <summary>
/// Handles <see cref="GetDashboardQuery"/>.
/// Returns the home-screen dashboard for the JWT-resolved student.
///
/// Phase-2 shape:
///   Xp = 0       (TODO P4-02 — real XP from gamification module)
///   Streak = 0   (TODO P4-03 — real streak from gamification module)
///   DailyMission = null  (TODO P4-06 — daily mission engine)
///   LeaguePreview = null (TODO P4-07 — leagues engine)
///   Continue = the first Available lesson in the most-recently-active subject,
///              or the first Available lesson across Grade-1 subjects (fallback),
///              or null when no Available lesson exists anywhere.
///
/// StudentId is always resolved from the JWT (_currentUser.UserId) — never from the request.
/// No SaveChangesAsync — read-only query.
/// </summary>
public class GetDashboardQueryHandler
    : BaseResponseHandler, IQueryHandler<GetDashboardQuery, BaseResponse<DashboardDto>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IStudentXpQuery _xpQuery;

    // Deterministic cross-subject fallback order (Q3 Option A step 5).
    private static readonly string[] FallbackSubjectOrder =
        { "Math", "Science", "Arabic", "English" };

    public GetDashboardQueryHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer,
        IStudentXpQuery xpQuery)
    {
        _repository = repository;
        _currentUser = currentUser;
        _logger = logger;
        _localizer = localizer;
        _xpQuery = xpQuery;
    }

    public async Task<BaseResponse<DashboardDto>> Handle(
        GetDashboardQuery request,
        CancellationToken cancellationToken)
    {
        // Belt-and-suspenders auth guard — [Authorize] on the controller action already
        // blocks anonymous requests; this null-check is defense-in-depth (mirrors
        // GetStudentAttemptsQueryHandler step 2).
        if (_currentUser.UserId is not { } studentId)
            return Unauthorized<DashboardDto>(_localizer[SharedResourcesKey.Unauthorized]);

        try
        {
            // ── Step 2: Resolve primary subject ────────────────────────────────────────────────
            // Q3 Option A: start with the subject of the student's most-recent Attempt.
            var recentSubjectId = await _repository.Learning
                .GetMostRecentActivitySubjectIdAsync(studentId, cancellationToken);

            // ── Step 3: Load Grade-1 subjects for the cross-subject fallback ────────────────────
            // Loaded once here and passed down to avoid repeated DB round-trips.
            // Order: deterministic per FallbackSubjectOrder array.
            var grade1Subjects = await _repository.Learning
                .GetByCondition<Subject>(s => s.Grade.Number == 1, trackChanges: false)
                .Include(s => s.Grade)
                .ToListAsync(cancellationToken);

            // If no recent activity, fall back to Grade-1 Math as the primary subject (Q3.bis).
            int? primarySubjectId = recentSubjectId;
            if (primarySubjectId is null)
            {
                primarySubjectId = grade1Subjects
                    .FirstOrDefault(s => s.Name == "Math")?.Id;
                // If still null (seeder not run), Continue will stay null — empty state is valid (Q8).
            }

            // ── Step 4+5: Resolve Continue target (primary subject first, then fallback) ────────
            ContinueTargetDto? continueTarget = null;

            if (primarySubjectId.HasValue)
            {
                continueTarget = await TryResolveContinueForSubjectAsync(
                    studentId, primarySubjectId.Value, grade1Subjects, cancellationToken);
            }

            if (continueTarget is null)
            {
                // Cross-subject fallback: iterate Grade-1 subjects in deterministic Name order
                // (Math → Science → Arabic → English), skipping the primary already tried (Q3 step 5).
                var ordered = grade1Subjects
                    .Where(s => s.Id != primarySubjectId)
                    .OrderBy(s =>
                    {
                        var idx = Array.IndexOf(FallbackSubjectOrder, s.Name);
                        return idx < 0 ? int.MaxValue : idx;
                    });

                foreach (var subject in ordered)
                {
                    continueTarget = await TryResolveContinueForSubjectAsync(
                        studentId, subject.Id, grade1Subjects, cancellationToken);
                    if (continueTarget is not null) break;
                }
            }

            // ── Step 8: Read XP snapshot via cross-module seam (P4-02) ────────────────────────
            // IStudentXpQuery is implemented by Gamification.Infrastructure — no direct DbContext
            // reference from Learning (module isolation rule 1). Returns null for brand-new students.
            var xpSnapshot = await _xpQuery.GetByStudentIdAsync(studentId, cancellationToken);
            var xp = xpSnapshot?.TotalXp ?? 0;
            var level = xpSnapshot?.CurrentLevel ?? 1;

            // ── Step 9: Assemble DashboardDto ──────────────────────────────────────────────────
            var dto = new DashboardDto(
                Xp: xp,
                Streak: 0,                // TODO P4-03 — wire real streak from gamification module
                DailyMission: null,       // TODO P4-06 — wire daily mission engine
                LeaguePreview: null,      // TODO P4-07 — wire leagues engine
                Continue: continueTarget,
                Level: level
            );

            return Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GetDashboardQuery for student {studentId}");
            return ServerError<DashboardDto>();   // do NOT echo ex.Message to the client
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // Private helper
    // ════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runs the <see cref="LearningPathEngine"/> for one subject and returns a
    /// <see cref="ContinueTargetDto"/> for the first <see cref="NodeState.Available"/> lesson
    /// (lowest SequenceOrder then lowest Id), or null if none is available.
    ///
    /// Mirrors the engine-invocation block in <c>GetSubjectSkillTreeQueryHandler</c> lines 77–96
    /// for the 5 bulk-fetch calls + skillsById construction.
    /// </summary>
    private async Task<ContinueTargetDto?> TryResolveContinueForSubjectAsync(
        int studentId,
        int subjectId,
        List<Subject> preloadedSubjects,
        CancellationToken ct)
    {
        // ── Bulk-fetch the 5 engine inputs (mirrors GetSubjectSkillTreeQueryHandler 77–96) ────
        var nodes = await _repository.Learning
            .GetSubjectKnowledgeNodesAsync(subjectId, ct);
        var edges = await _repository.Learning
            .GetSubjectKnowledgeEdgesAsync(subjectId, ct);
        var masteryBySkillId = await _repository.Learning
            .GetSkillMasteryForStudentInSubjectAsync(studentId, subjectId, ct);
        var completedLessonIds = await _repository.Learning
            .GetCompletedLessonIdsForStudentInSubjectAsync(studentId, subjectId, ct);
        var lessons = await _repository.Learning
            .GetSubjectLessonsAsync(subjectId, ct);

        if (lessons.Count == 0) return null;

        // ── Build skillsById for the engine (mirrors GetSubjectSkillTreeQueryHandler line 89–92)
        // Load Concepts with their Skills for this subject.
        var concepts = await _repository.Learning
            .GetByCondition<Concept>(c => c.SubjectId == subjectId, trackChanges: false)
            .Include(c => c.Skills)
            .ToListAsync(ct);

        var skillsById = concepts
            .SelectMany(c => c.Skills)
            .DistinctBy(sk => sk.Id)
            .ToDictionary(sk => sk.Id);

        // ── Run the engine once for the whole subject ─────────────────────────────────────────
        var unlockStates = LearningPathEngine.ComputeStates(
            nodes, edges, masteryBySkillId, completedLessonIds, lessons, skillsById);

        // ── Pick the first Available lesson (SequenceOrder ASC, Id ASC) ─────────────────────
        var firstAvailable = lessons
            .Where(l => unlockStates.TryGetValue(l.Id, out var s) && s.State == NodeState.Available)
            .OrderBy(l => l.SequenceOrder)
            .ThenBy(l => l.Id)
            .FirstOrDefault();

        if (firstAvailable is null) return null;

        // ── Resolve names ─────────────────────────────────────────────────────────────────────
        // Subject name: from the pre-loaded list (no extra DB call).
        var subjectName = preloadedSubjects.FirstOrDefault(s => s.Id == subjectId)?.Name
            ?? await _repository.Learning
                .GetByCondition<Subject>(s => s.Id == subjectId, trackChanges: false)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(ct)
            ?? string.Empty;

        // Unit name: load from the Units set — one targeted query (GetSubjectLessonsAsync
        // does NOT eager-load the Unit navigation, so we fetch it here).
        var unitRow = await _repository.Learning
            .GetByCondition<Unit>(u => u.Id == firstAvailable.UnitId, trackChanges: false)
            .Select(u => new { u.Id, u.Name })
            .FirstOrDefaultAsync(ct);

        var unitId = unitRow?.Id ?? firstAvailable.UnitId;
        var unitName = unitRow?.Name ?? string.Empty;

        // Skill name: from the skillsById dictionary built above (no extra DB call).
        string? skillName = firstAvailable.SkillId.HasValue
            && skillsById.TryGetValue(firstAvailable.SkillId.Value, out var sk)
                ? sk.Name
                : null;

        return new ContinueTargetDto(
            SubjectId: subjectId,
            SubjectName: subjectName,
            UnitId: unitId,
            UnitName: unitName,
            LessonId: firstAvailable.Id,
            LessonName: firstAvailable.Name,
            SkillId: firstAvailable.SkillId,
            SkillName: skillName,
            NodeState: NodeState.Available,    // Continue target is always Available
            IsBoss: firstAvailable.IsBoss      // P2-03
        );
    }
}
