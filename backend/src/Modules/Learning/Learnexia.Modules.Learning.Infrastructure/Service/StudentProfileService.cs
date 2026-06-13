using System.Text.Json;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Mastery.Dtos;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// In-process implementation of <see cref="IStudentProfileService"/> (P3-13 BE-6).
///
/// Thin orchestrator: fetches signals from the repository, calls the pure engine, and persists
/// the result. Mirrors the shape of <see cref="AdaptivityService"/> (P3-08).
///
/// Registered as Scoped in <c>AddLearningInfrastructure</c>.
///
/// TRANSACTION NOTE — two call sites with different semantics:
/// (a) CompleteAttemptCommandHandler (BE-7 completion hook): runs INSIDE the ambient UoW
///     transaction. RecomputeProfile stages its changes via UpsertStudentLearningProfileAsync;
///     UoW commits everything atomically after the handler returns. Do NOT open a nested
///     transaction here.
/// (b) StudentProfileRecomputeJob (BE-7 nightly sweep): runs OUTSIDE any HTTP command pipeline.
///     The job creates its own scope; UpsertStudentLearningProfileAsync stages changes but the
///     job's scope does not have a UoW behavior — the DbContext.SaveChangesAsync is called
///     explicitly after each profile write inside the job's loop.
///
/// JSON serialization: the jsonb columns (QuestionTypeAffinity, RecurringErrorClusters,
/// FatigueSignal) hold System.Text.Json-serialized strings, matching the ContentBlock.Payload
/// pattern (application layer owns shape; DB stores opaque jsonb).
/// </summary>
public class StudentProfileService : IStudentProfileService
{
    private readonly ILearningRepository _repository;
    private readonly IMasteryService _masteryService;
    private readonly IOptions<StudentProfileOptions> _options;
    private readonly ILoggerManager _logger;

    // JSON options: camelCase for consistency with the rest of the API surface.
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public StudentProfileService(
        ILearningRepository repository,
        IMasteryService masteryService,
        IOptions<StudentProfileOptions> options,
        ILoggerManager logger)
    {
        _repository     = repository;
        _masteryService = masteryService;
        _options        = options;
        _logger         = logger;
    }

    /// <inheritdoc/>
    public async Task RecomputeProfile(int studentId, CancellationToken ct = default)
    {
        try
        {
            var opts = _options.Value;

            // ── 1. Fetch answer signals from the repository (P2-08 data). ─────────────────────────
            var signals = await _repository.GetStudentAnswerSignalsAsync(studentId, ct);

            // ── 2. Fetch mastery data (P3-09) and enrich the SkillErrorCounts signal. ─────────────
            // The mastery service already aggregates per-skill data; we use its NeedsReview status
            // as an additional data point. For v1, SkillErrorCounts from raw answers is the primary
            // input; mastery NeedsReview supplements it by confirming the pattern persists.
            // (P3-09 mastery is the canonical weak-area store; raw wrong-count is the leading indicator.)
            var masteryRows = await _masteryService.GetMasteryForStudent(studentId, ct);

            // Build an enriched signal: skills flagged NeedsReview by mastery but not yet in the
            // raw error counts (e.g. skills practiced before this approach was in place) get a
            // synthetic wrong-count of RecurringErrorThreshold so they are included in clusters.
            var rawErrorSkillIds = signals.SkillErrorCounts.Select(e => e.SkillId).ToHashSet();
            var masteryErrorSkills = masteryRows
                .Where(m => m.Status == MasteryStatus.NeedsReview && !rawErrorSkillIds.Contains(m.SkillId))
                .Select(m => (m.SkillId, WrongCount: opts.RecurringErrorThreshold))
                .ToList();

            // Merge raw + mastery-derived error counts into an enriched list.
            var enrichedErrorCounts = signals.SkillErrorCounts
                .Concat(masteryErrorSkills)
                .ToList();

            var enrichedSignals = new StudentSignals(
                AnswersByType:          signals.AnswersByType,
                SkillErrorCounts:       enrichedErrorCounts,
                SessionAccuracyBuckets: signals.SessionAccuracyBuckets,
                OverallAccuracy:        signals.OverallAccuracy,
                TotalAnswers:           signals.TotalAnswers,
                HintAnswerCountByType:  signals.HintAnswerCountByType);

            // ── 3. Call the pure engine. ─────────────────────────────────────────────────────────
            var derived = StudentProfileEngine.Derive(enrichedSignals, opts);

            // ── 4. Map DerivedProfile → StudentLearningProfile entity. ─────────────────────────
            var profile = await _repository.GetOrCreateStudentLearningProfileAsync(studentId, ct);

            profile.StudentId   = studentId;
            profile.DataPointCount            = derived.DataPointCount;
            profile.PreferredExplanationStyle = derived.PreferredExplanationStyle;
            profile.AttentionSpanMinutes      = derived.AttentionSpanMinutes;
            profile.LastRecomputedAt          = DateTime.UtcNow;

            // Serialize the jsonb columns via System.Text.Json (same approach as ContentBlock.Payload).
            profile.QuestionTypeAffinity = derived.QuestionTypeAffinity.Count > 0
                ? JsonSerializer.Serialize(derived.QuestionTypeAffinity, JsonOpts)
                : null;

            profile.RecurringErrorClusters = derived.RecurringErrorSkillIds.Count > 0
                ? JsonSerializer.Serialize(derived.RecurringErrorSkillIds, JsonOpts)
                : null;

            // FatigueSignal stores the bucket-level detail (raw buckets used for derivation).
            // This is the detail behind AttentionSpanMinutes — useful for debugging/transparency.
            var fatigueDetail = enrichedSignals.SessionAccuracyBuckets
                .Select(b => new { minuteBucket = b.MinuteBucket, accuracy = b.Accuracy })
                .ToList();
            profile.FatigueSignal = fatigueDetail.Count > 0
                ? JsonSerializer.Serialize(fatigueDetail, JsonOpts)
                : null;

            // ── 5. Upsert (stage only — caller's UoW or explicit SaveChanges commits). ─────────
            await _repository.UpsertStudentLearningProfileAsync(profile, ct);

            _logger.LogInfo(
                $"P3-13: StudentProfileService.RecomputeProfile — studentId={studentId}, dataPointCount={derived.DataPointCount}, style={derived.PreferredExplanationStyle}.");
        }
        catch (Exception ex)
        {
            // Log but do NOT rethrow — a profile recompute failure must not abort the enclosing
            // command (CompleteAttemptCommandHandler) or the sweep job iteration.
            _logger.LogError(ex, $"P3-13: StudentProfileService.RecomputeProfile failed for studentId={studentId}.");
        }
    }

    /// <inheritdoc/>
    public async Task<DerivedProfile> GetProfile(int studentId, CancellationToken ct = default)
    {
        var row = await _repository.GetStudentLearningProfileAsync(studentId, ct);

        if (row is null)
        {
            // Cold-start: no profile row exists yet. Return neutral defaults.
            return new DerivedProfile(
                QuestionTypeAffinity:      new Dictionary<string, double>(),
                RecurringErrorSkillIds:    Array.Empty<int>(),
                AttentionSpanMinutes:      null,
                PreferredExplanationStyle: ExplanationStyle.Standard,
                DataPointCount:            0);
        }

        // Deserialize the jsonb columns. Null / empty / malformed → safe empty defaults.
        var affinity = DeserializeJsonbDict(row.QuestionTypeAffinity);
        var clusters = DeserializeJsonbList(row.RecurringErrorClusters);

        return new DerivedProfile(
            QuestionTypeAffinity:      affinity,
            RecurringErrorSkillIds:    clusters,
            AttentionSpanMinutes:      row.AttentionSpanMinutes,
            PreferredExplanationStyle: row.PreferredExplanationStyle,
            DataPointCount:            row.DataPointCount);
    }

    // ── Private helpers ─────────────────────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, double> DeserializeJsonbDict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, double>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json, JsonOpts)
                   ?? new Dictionary<string, double>();
        }
        catch
        {
            return new Dictionary<string, double>();
        }
    }

    private static IReadOnlyList<int> DeserializeJsonbList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<int>();

        try
        {
            return (IReadOnlyList<int>?)JsonSerializer.Deserialize<List<int>>(json, JsonOpts) ?? Array.Empty<int>();
        }
        catch
        {
            return Array.Empty<int>();
        }
    }
}
