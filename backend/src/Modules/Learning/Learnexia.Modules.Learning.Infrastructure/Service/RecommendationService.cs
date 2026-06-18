using System.Text.Json;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// In-process implementation of <see cref="IRecommendationService"/> (P5-09-BE-3).
///
/// Orchestrates the three in-module inputs + the Identity grade seam, calls the pure
/// <see cref="RecommendationEngine"/>, serialises the result, and upserts the daily row.
///
/// OPTION C — EF only here (in Infrastructure), never in Application. Handlers and job inject
/// <see cref="IRecommendationService"/> from Application; only this class touches the DbContext.
///
/// TRANSACTION NOTE — two call sites:
/// (a) <c>RecommendationRecomputeJob</c> (nightly sweep): runs OUTSIDE any HTTP command pipeline.
///     Upsert stages the changes; the job calls <c>LearningDbContext.SaveChangesAsync(userId:0)</c>
///     explicitly after each student (ADR-0001 — background jobs own their own commits).
/// (b) (Future) — an HTTP handler could call this; in that case the UoW behavior commits.
///
/// COLD-START / EMPTY: delegates to <see cref="RecommendationEngine.Compute"/> which always
/// returns at least one Celebrate item — never an error.
///
/// JSON serialization: ItemsJson holds System.Text.Json-serialized <c>RecommendationItem[]</c>,
/// same pattern as <c>StudentLearningProfile.QuestionTypeAffinity</c> and ContentBlock.Payload.
/// </summary>
public sealed class RecommendationService : IRecommendationService
{
    private readonly ILearningRepository    _repository;
    private readonly IWeakAreaDetectorService _weakAreaDetector;
    private readonly IAdaptivityService      _adaptivityService;
    private readonly IStudentProfileService  _profileService;
    private readonly IChildGradeQuery        _gradeQuery;
    private readonly ILoggerManager          _logger;

    // Web defaults: camelCase + case-insensitive for consistency with other jsonb columns.
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public RecommendationService(
        ILearningRepository repository,
        IWeakAreaDetectorService weakAreaDetector,
        IAdaptivityService adaptivityService,
        IStudentProfileService profileService,
        IChildGradeQuery gradeQuery,
        ILoggerManager logger)
    {
        _repository       = repository;
        _weakAreaDetector = weakAreaDetector;
        _adaptivityService = adaptivityService;
        _profileService   = profileService;
        _gradeQuery       = gradeQuery;
        _logger           = logger;
    }

    /// <inheritdoc/>
    public async Task ComputeAndUpsertAsync(int studentId, DateOnly date, CancellationToken ct = default)
    {
        try
        {
            // ── 1. Grade from Identity seam (sentinel: returns null on unknown/error) ──────────────
            var grade = await _gradeQuery.GetGradeAsync(studentId, ct);

            // ── 2. Weak areas from the in-module detector (unlimited for the engine) ───────────────
            var weakAreas = await _weakAreaDetector.DetectAsync(studentId, maxResults: 0, ct);

            // ── 3. Per-skill adaptivity decision (pre-fetch for all weak-area skills) ───────────────
            var adaptivityDecisions = new Dictionary<int, Learnexia.Modules.Learning.Domain.Services.AdaptivityDecision>();
            foreach (var area in weakAreas)
            {
                if (!adaptivityDecisions.ContainsKey(area.SkillId))
                {
                    var decision = await _adaptivityService.GetTargetDifficulty(studentId, area.SkillId, ct);
                    adaptivityDecisions[area.SkillId] = decision;
                }
            }

            // ── 4. Behavioral profile (cold-start safe) ───────────────────────────────────────────
            var profile = await _profileService.GetProfile(studentId, ct);

            // ── 5. Run the pure engine (no I/O, deterministic) ───────────────────────────────────
            var items = RecommendationEngine.Compute(weakAreas, adaptivityDecisions, profile, grade);

            // ── 6. Serialise → upsert ─────────────────────────────────────────────────────────────
            var itemsJson = JsonSerializer.Serialize(items, JsonOpts);

            var recommendation = new StudentRecommendation
            {
                StudentId          = studentId,
                RecommendationDate = date,
                ItemsJson          = itemsJson,
                GeneratedAtUtc     = DateTime.UtcNow,
            };

            await _repository.UpsertStudentRecommendationAsync(recommendation, ct);

            _logger.LogInfo(
                $"P5-09: RecommendationService.ComputeAndUpsertAsync — studentId={studentId}, date={date}, itemCount={items.Length}.");
        }
        catch (Exception ex)
        {
            // Log but do NOT rethrow — a single-student failure must not abort the sweep.
            // Mirrors StudentProfileService.RecomputeProfile and AdaptivityService defensive coding.
            _logger.LogError(ex,
                $"P5-09: RecommendationService.ComputeAndUpsertAsync failed for studentId={studentId}, date={date}.");
        }
    }
}
