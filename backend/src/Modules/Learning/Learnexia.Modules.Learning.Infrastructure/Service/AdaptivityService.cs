using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Logging;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// In-process implementation of <see cref="IAdaptivityService"/>.
///
/// Fetches the four FR-AD-1 signals from the Learning repository and the persisted mastery percentage
/// from <see cref="IMasteryService"/>, resolves <see cref="AdaptivityOptions"/> from DI, then delegates
/// to the pure <see cref="AdaptivityEngine.DecideDifficulty"/> for the deterministic band decision.
///
/// Null-skillId path: returns (Medium, IsDefault=true) immediately without any DB calls.
/// Cold-start path: when TotalAnswers == 0 the signals record has IsDefault=true and the engine
///   returns (Medium, IsDefault=true) per Q4.
///
/// P3-13 optional fatigue hook: stubbed — the service signature accepts the signal vector as-is;
///   AdaptivityOptions.FatigueSignalWeight (defaulted 0) is the extension point.
/// </summary>
public class AdaptivityService : IAdaptivityService
{
    private readonly ILearningRepository _repository;
    private readonly IMasteryService _masteryService;
    private readonly IOptions<AdaptivityOptions> _options;
    private readonly ILoggerManager _logger;

    public AdaptivityService(
        ILearningRepository repository,
        IMasteryService masteryService,
        IOptions<AdaptivityOptions> options,
        ILoggerManager logger)
    {
        _repository     = repository;
        _masteryService = masteryService;
        _options        = options;
        _logger         = logger;
    }

    /// <inheritdoc/>
    public async Task<AdaptivityDecision> GetTargetDifficulty(int studentId, int? skillId, CancellationToken ct = default)
    {
        // ── Null-skillId fast path ────────────────────────────────────────────────────────────────────
        // Lessons with no linked skill have no per-skill signal → return default (Q2/Q4).
        if (skillId is null)
            return new AdaptivityDecision(DifficultyLevel.Medium, IsDefault: true, Score: 0.0);

        try
        {
            var sid = skillId.Value;

            // ── Step 1: Fetch raw answer aggregate (4 FR-AD-1 signals) ──────────────────────────────
            var agg = await _repository.GetAdaptivitySignalsAsync(studentId, sid, ct);

            // ── Step 2: Fetch persisted mastery percentage (P3-09 seam) ─────────────────────────────
            // GetMasteryForStudent returns all skills; find the one we need.
            // If not found (student has never completed an attempt for this skill), masteryPct = 0.
            var masteryList = await _masteryService.GetMasteryForStudent(studentId, ct);
            var masteryPct  = masteryList.FirstOrDefault(m => m.SkillId == sid)?.MasteryPercentage ?? 0;

            // ── Step 3: Build AdaptivitySignals ──────────────────────────────────────────────────────
            // Cold-start: TotalAnswers == 0 → IsDefault=true (engine returns Medium).
            var isDefault = agg.TotalAnswers == 0;

            var accuracyPct = agg.TotalAnswers == 0
                ? 0.0
                : Math.Clamp((double)agg.CorrectAnswers / agg.TotalAnswers, 0.0, 1.0);

            var hintRate = agg.TotalAnswers == 0
                ? 0.0
                : Math.Clamp((double)agg.HintCount / agg.TotalAnswers, 0.0, 1.0);

            var signals = new AdaptivitySignals(
                AccuracyPct:    accuracyPct,
                AvgTimeSeconds: agg.AvgTimeSeconds,
                HintRate:       hintRate,
                RetryCount:     agg.RetryCount,
                MasteryPct:     masteryPct,
                IsDefault:      isDefault);

            // ── Step 4: Call pure engine ──────────────────────────────────────────────────────────────
            return AdaptivityEngine.DecideDifficulty(signals, _options.Value);
        }
        catch (Exception ex)
        {
            // Log server-side; return safe default (Medium) — never propagate.
            _logger.LogError(ex, "Error in AdaptivityService.GetTargetDifficulty");
            return new AdaptivityDecision(DifficultyLevel.Medium, IsDefault: true, Score: 0.0);
        }
    }
}
