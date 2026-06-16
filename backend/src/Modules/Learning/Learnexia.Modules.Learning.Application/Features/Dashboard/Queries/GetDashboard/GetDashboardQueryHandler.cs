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
/// P8-03-BE-5: The Grade-1 fallback subject set is now resolved per SubjectCode via
/// SubjectLanguageResolver using the student's JWT learning_language claim.
///
/// StudentId is always resolved from the JWT (_currentUser.UserId) — never from the request.
/// No SaveChangesAsync — read-only query.
///
/// Option C (no EF in Application): all DB access delegated to IDashboardQueryService
/// (via ILearningServiceManager.DashboardQueryService). Cross-module seam calls and
/// LearningPathEngine calls remain in the handler as they are EF-free.
/// </summary>
public class GetDashboardQueryHandler
    : BaseResponseHandler, IQueryHandler<GetDashboardQuery, BaseResponse<DashboardDto>>
{
    private readonly ILearningServiceManager _service;
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
    private static readonly SubjectCode[] FallbackSubjectCodeOrder =
    {
        SubjectCode.MATH,
        SubjectCode.SCIENCE,
        SubjectCode.ARABIC,
        SubjectCode.ENGLISH,
    };

    public GetDashboardQueryHandler(
        ILearningServiceManager service,
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
        _service           = service;
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
        // Belt-and-suspenders auth guard.
        if (_currentUser.UserId is not { } studentId)
            return Unauthorized<DashboardDto>(_localizer[SharedResourcesKey.Unauthorized]);

        try
        {
            // P8-03-BE-1: resolve the student's learning language from the JWT claim.
            var learnerLang = LearningLanguageClaimAccessor.GetLearningLanguage(_currentUser, _logger);

            // ── Step 2: Resolve primary subject ───────────────────────────────────────────────
            var recentSubjectId = await _service.DashboardQueryService
                .GetMostRecentActivitySubjectIdAsync(studentId, cancellationToken);

            // ── Step 3: Load all Grade-1 ACTIVE + PUBLISHED subjects ──────────────────────────
            var allGrade1Subjects = await _service.DashboardQueryService
                .GetPublishedActiveGrade1SubjectsAsync(cancellationToken);

            // P8-03-BE-5: Build the resolved Grade-1 subject list in deterministic SubjectCode order.
            var resolvedGrade1Subjects = new List<Subject>(FallbackSubjectCodeOrder.Length);
            foreach (var code in FallbackSubjectCodeOrder)
            {
                var resolved = SubjectLanguageResolver.Resolve(code, learnerLang);
                var match    = allGrade1Subjects.FirstOrDefault(s => s.SubjectCode == code && s.Language == resolved);

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
                var mathResolved = SubjectLanguageResolver.Resolve(SubjectCode.MATH, learnerLang);
                primarySubjectId = resolvedGrade1Subjects
                    .FirstOrDefault(s => s.SubjectCode == SubjectCode.MATH && s.Language == mathResolved)?.Id
                    ?? resolvedGrade1Subjects.FirstOrDefault(s => s.SubjectCode == SubjectCode.MATH)?.Id;
            }

            // ── Step 4+5: Resolve Continue target ─────────────────────────────────────────────
            ContinueTargetDto? continueTarget = null;

            if (primarySubjectId.HasValue)
            {
                continueTarget = await TryResolveContinueForSubjectAsync(
                    studentId, primarySubjectId.Value, resolvedGrade1Subjects, cancellationToken);
            }

            if (continueTarget is null)
            {
                foreach (var subject in resolvedGrade1Subjects.Where(s => s.Id != primarySubjectId))
                {
                    continueTarget = await TryResolveContinueForSubjectAsync(
                        studentId, subject.Id, resolvedGrade1Subjects, cancellationToken);
                    if (continueTarget is not null) break;
                }
            }

            // ── Step 8: Read cross-module snapshots ───────────────────────────────────────────
            var xpSnapshot = await _xpQuery.GetByStudentIdAsync(studentId, cancellationToken);
            var xp         = xpSnapshot?.TotalXp ?? 0;
            var level      = xpSnapshot?.CurrentLevel ?? 1;

            var streakSnapshot = await _streakQuery.GetByStudentIdAsync(studentId, cancellationToken);
            var streak         = streakSnapshot?.CurrentStreak ?? 0;
            var freezeBalance  = streakSnapshot?.FreezeBalance ?? 0;

            var heartsSnapshot = await _heartsQuery.GetByStudentIdAsync(studentId, cancellationToken);
            var hearts         = heartsSnapshot.Hearts;
            var inPracticeMode = heartsSnapshot.InPracticeMode;

            var badgesSnapshot   = await _badgesQuery.GetByStudentIdAsync(studentId, cancellationToken);
            var missionsSnapshot = await _missionsQuery.GetByStudentIdAsync(studentId, cancellationToken);
            var leagueSnapshot   = await _leagueQuery.GetByStudentIdAsync(studentId, cancellationToken);

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

            var leaguePreview = leagueSnapshot.GroupSize > 0
                ? new LeaguePreviewDto(
                    TierName:     leagueSnapshot.Tier.ToString(),
                    Rank:         leagueSnapshot.CurrentRank,
                    TotalPlayers: leagueSnapshot.GroupSize,
                    XpThisWeek:   leagueSnapshot.WeeklyXp)
                : null;

            // ── Step 9: Assemble DashboardDto ─────────────────────────────────────────────────
            var dto = new DashboardDto(
                Xp:                xp,
                Streak:            streak,
                LeaguePreview:     leaguePreview,
                Continue:          continueTarget,
                Level:             level,
                Hearts:            hearts,
                InPracticeMode:    inPracticeMode,
                BadgesCount:       badgesSnapshot.TotalCount,
                RecentBadges:      badgesSnapshot.Recent.Count > 0 ? badgesSnapshot.Recent : null,
                DailyMissions:     missionsSnapshot.Daily.Count > 0 ? missionsSnapshot.Daily : null,
                WeeklyMission:     missionsSnapshot.Weekly,
                FreezeBalance:     freezeBalance,
                ActiveTimedEvents: activeTimedEvents
            );

            return Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GetDashboardQuery for student {studentId}");
            return ServerError<DashboardDto>();
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
    /// All 6 DB fetches (nodes, edges, mastery, completedLessons, lessons, concepts+skills)
    /// plus name look-ups delegate to <c>IDashboardQueryService</c> — no EF in this handler.
    /// LearningPathEngine is pure Domain — no EF involved.
    /// </summary>
    private async Task<ContinueTargetDto?> TryResolveContinueForSubjectAsync(
        int studentId,
        int subjectId,
        List<Subject> preloadedSubjects,
        CancellationToken ct)
    {
        var db = _service.DashboardQueryService;

        // ── Bulk-fetch the 5 engine inputs ────────────────────────────────────────────────────
        var nodes              = await db.GetSubjectKnowledgeNodesAsync(subjectId, ct);
        var edges              = await db.GetSubjectKnowledgeEdgesAsync(subjectId, ct);
        var masteryBySkillId   = await db.GetSkillMasteryForStudentInSubjectAsync(studentId, subjectId, ct);
        var completedLessonIds = await db.GetCompletedLessonIdsForStudentInSubjectAsync(studentId, subjectId, ct);
        var lessons            = await db.GetSubjectLessonsAsync(subjectId, ct);

        if (lessons.Count == 0) return null;

        // ── Build skillsById ──────────────────────────────────────────────────────────────────
        var concepts = await db.GetConceptsWithSkillsForSubjectAsync(subjectId, ct);

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
        var subjectName = preloadedSubjects.FirstOrDefault(s => s.Id == subjectId)?.Name
            ?? await db.GetSubjectNameAsync(subjectId, ct)
            ?? string.Empty;

        var unitRow  = await db.GetUnitNameAsync(firstAvailable.UnitId, ct);
        var unitId   = unitRow?.Id ?? firstAvailable.UnitId;
        var unitName = unitRow?.Name ?? string.Empty;

        string? skillName = firstAvailable.SkillId.HasValue
            && skillsById.TryGetValue(firstAvailable.SkillId.Value, out var sk)
                ? sk.Name
                : null;

        return new ContinueTargetDto(
            SubjectId:   subjectId,
            SubjectName: subjectName,
            UnitId:      unitId,
            UnitName:    unitName,
            LessonId:    firstAvailable.Id,
            LessonName:  firstAvailable.Name,
            SkillId:     firstAvailable.SkillId,
            SkillName:   skillName,
            NodeState:   NodeState.Available,
            IsBoss:      firstAvailable.IsBoss
        );
    }
}
