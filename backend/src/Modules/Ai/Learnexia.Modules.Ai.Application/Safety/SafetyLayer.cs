using System.Text.Json;
using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Modules.Ai.Domain.Safety;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Resources;

namespace Learnexia.Modules.Ai.Application.Safety;

/// <summary>
/// The mandatory AI-content safety facade. Implements <see cref="ISafetyLayer"/>.
///
/// <para><strong>AC1 (P3-02) — no-bypass invariant:</strong>
/// This is the ONLY class that calls <see cref="IAiGateway.CompleteAsync"/> and
/// produces <see cref="SafeAiResult"/>. Feature handlers (P3-04/05/06) are wired to
/// <see cref="ISafetyLayer"/>, never to <see cref="IAiGateway"/> directly.
/// The architecture test (P3-02-BE-10) enforces this at build time.</para>
///
/// <para><strong>8-step flow (Q3 decision — facade pattern approved by lead):</strong></para>
/// <list type="number">
///   <item>Optional light input screen via <see cref="IToxicityCheck"/> (Q4).</item>
///   <item>Call <see cref="IAiGateway.CompleteAsync"/>.</item>
///   <item>Run all enabled output checks concurrently (Task.WhenAll, Q7).</item>
///   <item>Collect all <see cref="CheckVerdict"/> results.</item>
///   <item>If any <see cref="CheckOutcome.Block"/> → go to step 6.</item>
///   <item>If any <see cref="CheckOutcome.NeedsRegeneration"/> (and no Block) → bounded regen (Q7).</item>
///   <item>Block path: write <see cref="SafetyEvent"/> via <see cref="IAiSafetyEventStore"/>
///         (reason codes only — never raw content), return localized fallback.</item>
///   <item>Pass path: return screened content as <see cref="SafeAiResult.Allowed"/> = true,
///         with <see cref="SafeAiResult.Confidence"/> set to the safety-pass confidence
///         from <see cref="IGlobalSettingsProvider"/> (<c>ai.cache.safetyPassConfidence</c>).</item>
/// </list>
///
/// <para><strong>Fail-closed on every exception path.</strong> Cancellation, gateway errors,
/// check exceptions — all result in a blocked fallback, never unscreened content.</para>
///
/// <para><strong>Confidence signal (AI Cache Activation):</strong> because the three safety
/// checks (Toxicity, AgeAppropriateness, Hallucination) are boolean pass/fail classifiers —
/// none produces a numeric model-confidence score at this phase — the confidence is set from
/// a configuration-driven safety-pass value (<c>AiHelper:Cache:safetyPassConfidence</c>,
/// default 0.90). This value is assigned ONLY when ALL enabled checks pass. Blocked, fallback,
/// and error results receive <c>null</c> so they can never reach the auto-approve gate.</para>
///
/// <para><strong>R5 coupling (runtime-review gate):</strong> the returned <see cref="SafeAiResult"/>
/// carries <c>Confidence</c> for use by feature handlers (P3-04/05/06) to decide
/// <c>ReviewStatus</c> when writing to <c>ai.AiResponseCache</c>. The Safety Layer is the
/// gatekeeper; the cache write is owned by the calling handler.</para>
/// </summary>
public sealed class SafetyLayer : ISafetyLayer
{
    // IGlobalSettingsProvider key for the safety-pass confidence value.
    // Resolved via BootstrapDefaultGlobalSettingsProvider → AiHelper:Cache:safetyPassConfidence.
    private const string SafetyPassConfidenceKey = "ai.cache.safetyPassConfidence";

    private readonly IAiGateway                        _gateway;
    private readonly IToxicityCheck                    _toxicityCheck;
    private readonly IAgeAppropriatenessCheck          _ageCheck;
    private readonly IHallucinationCheck               _hallucinationCheck;
    private readonly IAiSafetyEventStore               _eventStore;
    private readonly IAiOutputFlaggedPublisher         _flaggedPublisher;
    private readonly SafetyOptions                     _options;
    private readonly IGlobalSettingsProvider           _settings;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager                    _logger;

    public SafetyLayer(
        IAiGateway gateway,
        IToxicityCheck toxicityCheck,
        IAgeAppropriatenessCheck ageCheck,
        IHallucinationCheck hallucinationCheck,
        IAiSafetyEventStore eventStore,
        IAiOutputFlaggedPublisher flaggedPublisher,
        IOptions<SafetyOptions> options,
        IGlobalSettingsProvider settings,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _gateway            = gateway;
        _toxicityCheck      = toxicityCheck;
        _ageCheck           = ageCheck;
        _hallucinationCheck = hallucinationCheck;
        _eventStore         = eventStore;
        _flaggedPublisher   = flaggedPublisher;
        _options            = options.Value;
        _settings           = settings;
        _localizer          = localizer;
        _logger             = logger;
    }

    /// <inheritdoc />
    public async Task<SafeAiResult> GenerateSafeAsync(AiRequest request, CancellationToken ct = default)
    {
        try
        {
            // ── Step 1: Optional light input screen (Q4 decision) ─────────────────
            // Run input toxicity check to catch overtly unsafe student prompts before calling gateway.
            var inputVerdict = await _toxicityCheck.CheckAsync(
                request.Prompt, DetectLanguage(request), ct);

            if (inputVerdict.Outcome == CheckOutcome.Block)
            {
                _logger.LogWarn("SafetyLayer: input blocked by toxicity check. Writing SafetyEvent and returning fallback.");

                await PersistSafetyEventAsync(
                    taskKind:     request.Task.ToString(),
                    failedChecks: ["ToxicityCheck"],
                    reasonCodes:  [inputVerdict.ReasonCode],
                    action:       "Blocked",
                    modelId:      "input-screen",
                    ct);

                return BlockedResult(
                    [new CheckResult("ToxicityCheck", CheckOutcome.Block, inputVerdict.ReasonCode, inputVerdict.Score)]);
            }

            // ── Step 2: Call the gateway ───────────────────────────────────────────
            var rawResult = await _gateway.CompleteAsync(request, ct);

            if (!rawResult.Successed || rawResult.Content is null)
            {
                _logger.LogWarn(
                    $"SafetyLayer: gateway failed ({rawResult.Error?.Kind}). Fail-closed → fallback.");
                return GatewayFallbackResult();
            }

            // ── Steps 3–6: Run checks concurrently; block/regen loop ───────────────
            var content = rawResult.Content;
            var modelId = rawResult.Usage?.ModelId ?? "unknown";

            for (int attempt = 0; attempt <= _options.MaxRegenerationAttempts; attempt++)
            {
                var checkResults = await RunChecksAsync(content, DetectLanguage(request), ct);
                bool hasBlock   = checkResults.Any(r => r.Outcome == CheckOutcome.Block);
                bool hasRegen   = checkResults.Any(r => r.Outcome == CheckOutcome.NeedsRegeneration);

                if (!hasBlock && !hasRegen)
                {
                    // ── Step 7 (pass path) ────────────────────────────────────────
                    // All enabled checks passed. Assign a setting-driven safety-pass confidence.
                    // The three safety checks (Toxicity, Age, Hallucination) are boolean classifiers
                    // at this phase — none produces a numeric model-confidence score. We therefore
                    // assign a configuration-driven sentinel that expresses "this response passed ALL
                    // safety gates" (default 0.90, raise/lower via AiHelper:Cache:safetyPassConfidence).
                    // This MUST only be set here on the genuine pass path — never on block/fallback.
                    var safetyPassConfidence = _settings.GetDecimal(
                        SafetyPassConfidenceKey, defaultValue: 0.90m);

                    return new SafeAiResult(
                        Allowed:    true,
                        Content:    content,
                        Verdict:    SafetyVerdict.Allowed,
                        Results:    checkResults,
                        Confidence: safetyPassConfidence);
                }

                if (hasBlock || attempt >= _options.MaxRegenerationAttempts)
                {
                    // ── Block path (step 6) ───────────────────────────────────────
                    _logger.LogWarn(
                        $"SafetyLayer: output blocked on attempt {attempt}/{_options.MaxRegenerationAttempts}. " +
                        $"hasBlock={hasBlock} hasRegen={hasRegen}. Writing SafetyEvent.");

                    var failedNames = checkResults
                        .Where(r => r.Outcome != CheckOutcome.Pass)
                        .Select(r => r.CheckName)
                        .ToList();

                    var failedCodes = checkResults
                        .Where(r => r.Outcome != CheckOutcome.Pass)
                        .Select(r => r.ReasonCode)
                        .ToList();

                    var action = attempt > 0 ? "FallbackReturned" : "Blocked";

                    await PersistSafetyEventAsync(
                        taskKind:     request.Task.ToString(),
                        failedChecks: failedNames,
                        reasonCodes:  failedCodes,
                        action:       action,
                        modelId:      modelId,
                        ct);

                    return BlockedResult(checkResults);
                }

                // ── NeedsRegeneration: re-prompt gateway (bounded) ─────────────────
                _logger.LogDebug(
                    $"SafetyLayer: NeedsRegeneration on attempt {attempt}. Re-prompting (max={_options.MaxRegenerationAttempts}).");

                await PersistSafetyEventAsync(
                    taskKind:     request.Task.ToString(),
                    failedChecks: checkResults.Where(r => r.Outcome != CheckOutcome.Pass).Select(r => r.CheckName).ToList(),
                    reasonCodes:  checkResults.Where(r => r.Outcome != CheckOutcome.Pass).Select(r => r.ReasonCode).ToList(),
                    action:       "Regenerated",
                    modelId:      modelId,
                    ct);

                var regen = await _gateway.CompleteAsync(request, ct);

                if (!regen.Successed || regen.Content is null)
                {
                    _logger.LogWarn("SafetyLayer: gateway failed during regeneration. Fail-closed → fallback.");
                    return GatewayFallbackResult();
                }

                content = regen.Content;
                modelId = regen.Usage?.ModelId ?? modelId;
            }

            // Should never reach here, but fail-closed as a safety net.
            _logger.LogWarn("SafetyLayer: unexpectedly exited regen loop. Fail-closed → fallback.");
            return GatewayFallbackResult();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarn("SafetyLayer: operation cancelled. Fail-closed → fallback.");
            return GatewayFallbackResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SafetyLayer: unexpected exception in GenerateSafeAsync. Fail-closed → fallback.");
            return GatewayFallbackResult();
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Runs all enabled output checks concurrently (Task.WhenAll — Q7 decision).
    /// Each check is individually fail-closed; exceptions inside each check produce Block verdicts.
    /// </summary>
    private async Task<List<CheckResult>> RunChecksAsync(
        string content, string language, CancellationToken ct)
    {
        var tasks = new List<Task<(string Name, CheckVerdict Verdict)>>();

        if (_options.EnableToxicityCheck)
            tasks.Add(RunCheckSafe("ToxicityCheck", () => _toxicityCheck.CheckAsync(content, language, ct)));

        if (_options.EnableAgeCheck)
            tasks.Add(RunCheckSafe("AgeAppropriatenessCheck", () => _ageCheck.CheckAsync(content, language, ct)));

        if (_options.EnableHallucinationCheck)
            tasks.Add(RunCheckSafe("HallucinationCheck", () => _hallucinationCheck.CheckAsync(content, language, ct)));

        if (tasks.Count == 0)
        {
            // All checks disabled via config (should not happen in prod; FR-AI-4 enforces defaults).
            _logger.LogWarn("SafetyLayer: all checks disabled — allowing content without screening (config violation).");
            return [];
        }

        var verdicts = await Task.WhenAll(tasks);

        return verdicts
            .Select(v => new CheckResult(
                CheckName:  v.Name,
                Outcome:    v.Verdict.Outcome,
                ReasonCode: v.Verdict.ReasonCode,
                Score:      v.Verdict.Score))
            .ToList();
    }

    /// <summary>
    /// Wraps a check delegate and catches any exception it throws (defensive: checks should
    /// already be fail-closed, but this is an extra safety net). On exception → Block.
    /// </summary>
    private async Task<(string Name, CheckVerdict Verdict)> RunCheckSafe(
        string name, Func<Task<CheckVerdict>> check)
    {
        try
        {
            var verdict = await check();
            return (name, verdict);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"SafetyLayer: check '{name}' threw an unhandled exception. Fail-closed → Block.");
            return (name, CheckVerdict.Block(ReasonCodes.ToxicityCheckError));
        }
    }

    /// <summary>
    /// Persists a <see cref="SafetyEvent"/> via <see cref="IAiSafetyEventStore"/> and then
    /// publishes an <c>AiOutputFlaggedIntegrationEvent</c> (fail-soft) for the Moderation module.
    /// Reason codes and check names only — never raw AI content (Q5).
    /// </summary>
    private async Task PersistSafetyEventAsync(
        string taskKind,
        IReadOnlyList<string> failedChecks,
        IReadOnlyList<string> reasonCodes,
        string action,
        string modelId,
        CancellationToken ct)
    {
        var ev = new SafetyEvent
        {
            StudentId     = 0,  // 0 = unknown; set by calling handler in P3-04+ if student context is available.
            TaskKind      = taskKind,
            FailedChecks  = JsonSerializer.Serialize(failedChecks),
            ReasonCodes   = JsonSerializer.Serialize(reasonCodes),
            ActionTaken   = action,
            ModelId       = modelId,
            OccurredAtUtc = DateTime.UtcNow,
        };

        await _eventStore.AppendAsync(ev, ct);

        // Post-persist, fail-soft: publish integration event for the Moderation module (P7-09).
        // A publish failure MUST NOT propagate here — the child-facing fallback is already decided.
        // Only blocked/regenerated/fallback events are published (never the Allowed path).
        await _flaggedPublisher.PublishAsync(ev, ct);
    }

    private SafeAiResult BlockedResult(IReadOnlyList<CheckResult> results)
    {
        var fallback = _localizer[SharedResourcesKey.AiContentBlocked].Value;
        return new SafeAiResult(
            Allowed: false,
            Content: fallback,
            Verdict: SafetyVerdict.Blocked,
            Results: results);
    }

    private SafeAiResult GatewayFallbackResult()
    {
        var fallback = _localizer[SharedResourcesKey.AiServiceUnavailable].Value;
        return new SafeAiResult(
            Allowed: false,
            Content: fallback,
            Verdict: SafetyVerdict.Fallback,
            Results: []);
    }

    /// <summary>
    /// Detect the content language from the request prompt heuristic (Arabic unicode block).
    /// Returns <c>"ar"</c> if the prompt contains Arabic characters; <c>"en"</c> otherwise.
    /// The P3-03 prompt builder will carry explicit language metadata in a future iteration.
    /// </summary>
    private static string DetectLanguage(AiRequest request)
    {
        foreach (var ch in request.Prompt)
        {
            if (ch >= '؀' && ch <= 'ۿ')
                return "ar";
        }

        return "en";
    }
}
