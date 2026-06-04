using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Dashboard.Dtos;
using Learnexia.Modules.Learning.Application.Helpers;
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
/// Phase-4 shape (P4-06 updated):
///   Xp           = real XP from gamification module via IStudentXpQuery (P4-02)
///   Streak       = real streak from gamification module via IStudentStreakQuery (P4-03)
///   DailyMissions = current-period daily missions via IStudentMissionsQuery (P4-06)
///   WeeklyMission = top weekly mission summary via IStudentMissionsQuery (P4-06)
///   LeaguePreview = current league tier + rank (P4-07) via IStudentLeagueQuery; null for brand-new students
///   Continue      = the first Available lesson in the most-recently-active subject,
///                   or the first Available lesson across Grade-1 subjects (fallback),
///                   or null when no Available lesson exists anywhere.
///
/// P8-03-BE-5: The Grade-1 fallback subject set is now resolved per <see cref="SubjectCode"/> via
/// <see cref="SubjectLanguageResolver"/> using the student's JWT <c>learning_language</c> claim.
/// The hard-coded name-based <c>FallbackSubjectOrder</c> is replaced by a <see cref="SubjectCode"/>-ordered
/// list (<c>FallbackSubjectCodeOrder</c>). For each code the handler picks the subject whose Language matches
/// the resolved effective language; if absent, the other-language tree is used and a warning is logged.
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
    private readonly IStudentStreakQuery _streakQuery;
    private readonly IStudentHeartsQuery _heartsQuery;
    private readonly IStudentBadgesQuery _badgesQuery;
    private readonly IStudentMissionsQuery _missionsQuery;
    private readonly IStudentLeagueQuery _leagueQuery;
    private readonly IActiveTimedEventsQuery _activeEventsQuery;

    // P8-03-BE-5: Deterministic cross-subject fallback order keyed by SubjectCode (not by name).
    // Replaces the old name-based FallbackSubjectOrder which was fragile against bilingual name variations.
    private static readonly SubjectCode[] FallbackSubjectCodeOrder =
    {
        SubjectCode.MATH,
        SubjectCode.SCIENCE,
        SubjectCode.ARABIC,
        SubjectCode.ENGLISH,
    };

    public GetDashboardQueryHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer,
        IStudentXpQuery xpQuery,
        IStudentStreakQuery streakQuery,
        IStudentHeartsQuery heartsQuery,
        IStudentBadgesQuery badgesQuery,
        IStudentMissionsQuery missionsQuery,
        IStudentLeagueQuery leagueQuery,
        IActiveTimedEventsQuery activeEventsQuery)
    {
        _repository        = repository;
        _currentUser       = currentUser;
        _logger            = logger;
        _localizer         = localizer;
        _xpQuery           = xpQuery;
        _streakQuery       = streakQuery;
        _heartsQuery       = heartsQuery;
        _badgesQuery       = badgesQuery;
        _missionsQuery     = missionsQuery;
        _leagueQuery       = leagueQuery;
        _activeEventsQuery = activeEventsQuery;
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
            // P8-03-BE-1: resolve the student's learning language from the JWT claim.
            var learnerLang = LearningLanguageClaimAccessor.GetLearningLanguage(_currentUser, _logger);

            // ── Step 2: Resolve primary subject ────────────────────────────────────────────────
            // Q3 Option A: start with the subject of the student's most-recent Attempt.
            var recentSubjectId = await _repository.Learning
                .GetMostRecentActivitySubjectIdAsync(studentId, cancellationToken);

            // ── Step 3: Load all Grade-1 subjects for the cross-subject fallback ──────────────
            // Loaded once here and passed down to avoid repeated DB round-trips.
            var allGrade1Subjects = await _repository.Learning
                .GetByCondition<Subject>(s => s.Grade.Number == 1, trackChanges: false)
                .Include(s => s.Grade)
                .ToListAsync(cancellationToken);

            // P8-03-BE-5: Build the resolved Grade-1 subject list in deterministic SubjectCode order.
            // For each code, resolve the effective language and pick the matching subject.
            // Falls back to the other-language tree (with a warning) when the resolved tree is absent.
            var resolvedGrade1Subjects = new List<Subject>(FallbackSubjectCodeOrder.Length);
            foreach (var code in FallbackSubjectCodeOrder)
            {
                var resolved = SubjectLanguageResolver.Resolve(code, learnerLang);
                var match = allGrade1Subjects.FirstOrDefault(s => s.SubjectCode == code && s.Language == resolved);

                if (match is null)
                {
                    // Missing-tree fallback (AC-10): serve the other language tree and log a warning.
                    var fallbackLang = resolved == ContentLanguage.Ar ? ContentLanguage.En : ContentLanguage.Ar;
                    match = allGrade1Subjects.FirstOrDefault(s => s.SubjectCode == code && s.Language == fallbackLang);

                    if (match is not null)
                    {
                        _logger.LogWarn(
                            $"Missing subject tree for SubjectCode={code} Language={resolved} Grade=1." +
                            $" Falling back to {fallbackLang}.");
                    }
                }

                if (match is not null)
                    resolvedGrade1Subjects.Add(match);
            }

            // If no recent activity, fall back to Grade-1 Math (resolved language) as the primary subject.
            int? primarySubjectId = recentSubjectId;
            if (primarySubjectId is null)
            {
                // Pick the Math subject at the resolved language.
                var mathResolved = SubjectLanguageResolver.Resolve(SubjectCode.MATH, learnerLang);
                primarySubjectId = resolvedGrade1Subjects
                    .FirstOrDefault(s => s.SubjectCode == SubjectCode.MATH && s.Language == mathResolved)?.Id
                    ?? resolvedGrade1Subjects.FirstOrDefault(s => s.SubjectCode == SubjectCode.MATH)?.Id;
                // If still null (seeder not run), Continue will stay null — empty state is valid (Q8).
            }

            // ── Step 4+5: Resolve Continue target (primary subject first, then fallback) ────────
            ContinueTargetDto? continueTarget = null;

            if (primarySubjectId.HasValue)
            {
                continueTarget = await TryResolveContinueForSubjectAsync(
                    studentId, primarySubjectId.Value, resolvedGrade1Subjects, cancellationToken);
            }

            if (continueTarget is null)
            {
                // Cross-subject fallback: iterate resolved Grade-1 subjects in deterministic SubjectCode order
                // (MATH → SCIENCE → ARABIC → ENGLISH), skipping the primary already tried.
                foreach (var subject in resolvedGrade1Subjects.Where(s => s.Id != primarySubjectId))
                {
                    continueTarget = await TryResolveContinueForSubjectAsync(
                        studentId, subject.Id, resolvedGrade1Subjects, cancellationToken);
                    if (continueTarget is not null) break;
                }
            }

            // ── Step 8: Read XP + Streak + Hearts + Badges + Missions snapshots via cross-module seams ─
            // All five queries go to Gamification.Infrastructure through Shared.Contracts seams —
            // no direct DbContext reference from Learning (module isolation rule 1).
            var xpSnapshot = await _xpQuery.GetByStudentIdAsync(studentId, cancellationToken);
            var xp = xpSnapshot?.TotalXp ?? 0;
            var level = xpSnapshot?.CurrentLevel ?? 1;

            var streakSnapshot = await _streakQuery.GetByStudentIdAsync(studentId, cancellationToken);
            var streak = streakSnapshot?.CurrentStreak ?? 0;  // P4-03 — real streak from gamification module
            var freezeBalance = streakSnapshot?.FreezeBalance ?? 0;  // P4-11 — streak-freeze inventory

            // P4-04 — IStudentHeartsQuery always returns a sentinel (never null) so brand-new students
            // correctly see Hearts = Cap = 5 and InPracticeMode = false without leaking HeartsOptions.Cap
            // into this module.
            var heartsSnapshot = await _heartsQuery.GetByStudentIdAsync(studentId, cancellationToken);
            var hearts = heartsSnapshot.Hearts;
            var inPracticeMode = heartsSnapshot.InPracticeMode;

            // P4-05 — IStudentBadgesQuery always returns a sentinel (0, []) for brand-new students —
            // never null. Mirrors the IStudentHeartsQuery sentinel-snapshot pattern (D2).
            var badgesSnapshot = await _badgesQuery.GetByStudentIdAsync(studentId, cancellationToken);

            // P4-06 — IStudentMissionsQuery lazy-instantiates the current period's missions on first
            // call per period (D2 / AC4). Returns sentinel ([], null) for brand-new students.
            var missionsSnapshot = await _missionsQuery.GetByStudentIdAsync(studentId, cancellationToken);

            // P4-07 — IStudentLeagueQuery lazy-instantiates the current week's league membership on
            // first call per period (D12 / AC1). Returns sentinel (Bronze, 0, 0, 0) for brand-new
            // students with no profile. Never null (D13).
            var leagueSnapshot = await _leagueQuery.GetByStudentIdAsync(studentId, cancellationToken);

            // P4-11 — IActiveTimedEventsQuery returns the global list of currently active timed events.
            // Never null — returns an empty list when no events are active. Hand-projected to
            // ActiveTimedEventDto (no AutoMapper per CONVENTIONS).
            var activeEventSnapshots = await _activeEventsQuery.GetActiveAtAsync(DateTime.UtcNow, cancellationToken);
            var activeTimedEvents = activeEventSnapshots.Count > 0
                ? activeEventSnapshots
                    .Select(e => new ActiveTimedEventDto(
                        Code:       e.Code,
                        NameEn:     e.NameEn,
                        NameAr:     e.NameAr,
                        Multiplier: e.Multiplier,
                        EndUtc:     e.EndUtc))
                    .ToList()
                : null;

            // Map sentinel/real snapshot to LeaguePreviewDto (D14 — reuse existing nullable shape).
            // Brand-new students (GroupSize == 0) → null (no league pill shown on dashboard).
            var leaguePreview = leagueSnapshot.GroupSize > 0
                ? new LeaguePreviewDto(
                    TierName:    leagueSnapshot.Tier.ToString(),
                    Rank:        leagueSnapshot.CurrentRank,
                    TotalPlayers: leagueSnapshot.GroupSize,
                    XpThisWeek:  leagueSnapshot.WeeklyXp)
                : null;

            // ── Step 9: Assemble DashboardDto ──────────────────────────────────────────────────
            var dto = new DashboardDto(
                Xp: xp,
                Streak: streak,
                LeaguePreview: leaguePreview,   // P4-07 — wired; null only for brand-new students
                Continue: continueTarget,
                Level: level,
                Hearts: hearts,           // P4-04 — real hearts from gamification module
                InPracticeMode: inPracticeMode,  // P4-04 — derived from Hearts == 0
                BadgesCount: badgesSnapshot.TotalCount,                                        // P4-05
                RecentBadges: badgesSnapshot.Recent.Count > 0 ? badgesSnapshot.Recent : null,  // P4-05
                DailyMissions: missionsSnapshot.Daily.Count > 0 ? missionsSnapshot.Daily : null,  // P4-06
                WeeklyMission: missionsSnapshot.Weekly,                                        // P4-06
                FreezeBalance: freezeBalance,                                                  // P4-11
                ActiveTimedEvents: activeTimedEvents                                           // P4-11
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
