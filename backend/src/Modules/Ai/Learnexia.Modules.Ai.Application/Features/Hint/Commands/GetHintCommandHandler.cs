using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Resources;

namespace Learnexia.Modules.Ai.Application.Features.Hint.Commands;

/// <summary>
/// Handles <see cref="GetHintCommand"/> — orchestrates the AI Helper "Hint" and "WhyWrong" intents.
///
/// <para><strong>Safety invariant (AC-6, FR-AI-4):</strong> this handler calls
/// <see cref="ISafetyLayer.GenerateSafeAsync"/> — NEVER <c>IAiGateway</c> directly.
/// The architecture test (P302-ARCH-04) enforces this at build time.</para>
///
/// <para><strong>Buffer → Safety → Emit pattern (OQ-2a):</strong> the full response is
/// buffered inside <see cref="ISafetyLayer"/>, safety-screened, then returned.
/// The SSE controller emits only the approved buffer. No unscreened token ever reaches
/// the student — lead-approved (SSE rule-8 exception).</para>
///
/// <para><strong>No-reveal guard (OQ-4, AC-1):</strong> for the Hint intent, after the safety
/// layer approves the content, we perform a post-generation check: if the content contains
/// the question's <see cref="QuestionAnswerDto.CorrectAnswer"/> string, the hint is blocked
/// (typed error + log). This prevents the model leaking the answer despite prompt instructions.</para>
///
/// <para><strong>Server-derived hint level (OQ-2b):</strong> the client-supplied
/// <see cref="GetHintCommand.HintLevel"/> is ignored in favour of the server-derived level
/// from <see cref="IQuestionAnswerContract.GetQuestionAnswerAsync"/> (Attempt.HintsUsedCount + 1).</para>
///
/// <para><strong>WhyWrong is always runtime (AC-2):</strong> the student's wrong answer is
/// the unique dynamic input — no cache check is performed for WhyWrong.</para>
///
/// <para><strong>No progression side effects (AC-9, FR-AI-6):</strong> this handler writes
/// no mastery, XP, or unlock state — it generates content only.</para>
///
/// Orchestration steps:
/// <list type="number">
///   <item>Resolve student id + rate-limit check.</item>
///   <item>Emit <c>HelpRequested</c> fire-and-forget.</item>
///   <item>Resolve grade/age/language from JWT claims.</item>
///   <item>Call <c>IQuestionAnswerContract</c> — CorrectAnswer + server-derived HintLevel.</item>
///   <item>MaxHintLevels bound check.</item>
///   <item>Fetch grounding via <c>ILearningContextProvider</c>.</item>
///   <item>Empty chunks → refuse-and-redirect + emit <c>HelpDeclined</c>.</item>
///   <item>Build prompt via <c>IPromptBuilder</c>.</item>
///   <item>[DEFERRED (BE-11/15): cache lookup goes here.]</item>
///   <item>Call <c>ISafetyLayer.GenerateSafeAsync</c> (buffers + screens).</item>
///   <item>Safety blocked → typed error.</item>
///   <item>For Hint: post-generation no-reveal check (OQ-4).</item>
///   <item>[DEFERRED (BE-11/15): cache-write of approved response goes here.]</item>
///   <item>Emit <c>HintUsedIntegrationEvent</c> fire-and-forget (usage recording).</item>
///   <item>Emit <c>HelpDelivered</c> fire-and-forget.</item>
///   <item>Return <see cref="HintResult.Streamed"/> with content + hint-level metadata.</item>
/// </list>
/// </summary>
public sealed class GetHintCommandHandler : ICommandHandler<GetHintCommand, HintResult>
{
    // Claim types used to resolve grade/age/language from the student JWT.
    private const string GradeClaim = "Grade";
    private const string AgeClaim = "Age";
    private const string LanguageClaim = "Language";
    // Model used for runtime Hint/WhyWrong calls (Sonnet — NOT Haiku, Arabic quality floor).
    private const string HintModelId = "claude-sonnet-4-6";
    // ContextSource label for the HelpDelivered event on a live generation.
    private const string ContextSourceLive = "SeededCorpus";

    private readonly ICurrentUserService _currentUser;
    private readonly IQuestionAnswerContract _questionAnswer;
    private readonly ILearningContextProvider _learningContext;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ISafetyLayer _safetyLayer;
    private readonly RedirectResponseBuilder _redirectBuilder;
    private readonly AiTutorRateLimiter _rateLimiter;
    private readonly IPublisher _publisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly HintOptions _hintOptions;

    public GetHintCommandHandler(
        ICurrentUserService currentUser,
        IQuestionAnswerContract questionAnswer,
        ILearningContextProvider learningContext,
        IPromptBuilder promptBuilder,
        ISafetyLayer safetyLayer,
        RedirectResponseBuilder redirectBuilder,
        AiTutorRateLimiter rateLimiter,
        IPublisher publisher,
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer,
        IOptions<HintOptions> hintOptions)
    {
        _currentUser = currentUser;
        _questionAnswer = questionAnswer;
        _learningContext = learningContext;
        _promptBuilder = promptBuilder;
        _safetyLayer = safetyLayer;
        _redirectBuilder = redirectBuilder;
        _rateLimiter = rateLimiter;
        _publisher = publisher;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _localizer = localizer;
        _hintOptions = hintOptions.Value;
    }

    public async Task<HintResult> Handle(GetHintCommand request, CancellationToken cancellationToken)
    {
        // Step 1 — resolve student id from JWT.
        var studentId = _currentUser.UserId;
        if (studentId is null)
        {
            _logger.LogWarn("GetHintCommandHandler: authenticated student id is null — rejecting.");
            return new HintResult.Error(
                nameof(SharedResourcesKey.ExplainConceptMissingProfile),
                _localizer[SharedResourcesKey.ExplainConceptMissingProfile]);
        }

        // Step 1b — per-student rate limit check (BE-8 cost/abuse guard).
        if (!_rateLimiter.TryAllow(studentId.Value))
        {
            _logger.LogWarn($"GetHintCommandHandler: rate limit exceeded for studentId={studentId}.");
            return new HintResult.Error(
                nameof(SharedResourcesKey.HintRateLimitExceeded),
                _localizer[SharedResourcesKey.HintRateLimitExceeded]);
        }

        // Step 2 — emit HelpRequested fire-and-forget (BE-13 AiTutor-side instrumentation).
        // Fresh scope: the handler may be Scoped; publishing into a disposed request scope
        // would cause ObjectDisposedException in any Scoped notification handler.
        var helpRequestedEvent = new HelpRequestedIntegrationEvent(
            studentId.Value,
            request.Intent,
            0,                          // SkillId — will be resolved from context below
            request.QuestionId);
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                await publisher.Publish(helpRequestedEvent, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetHintCommandHandler: HelpRequested fire-and-forget failed.");
            }
        });

        // Step 3 — resolve grade/age/language from JWT claims.
        TryResolveProfile(out var grade, out var age, out var language);

        // Step 4 — get CorrectAnswer + server-derived hint level (OQ-2b + OQ-4).
        // SECURITY (IDOR guard): pass the authenticated studentId so the adapter rejects attempts
        // that belong to a different student. Returns null when not found OR not owned.
        // We REFUSE before any LLM call when null — this closes the IDOR for BOTH Hint and WhyWrong.
        QuestionAnswerDto? questionAnswer = null;
        try
        {
            questionAnswer = await _questionAnswer.GetQuestionAnswerAsync(
                request.QuestionId,
                request.AttemptId,
                studentId.Value,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"GetHintCommandHandler: IQuestionAnswerContract failed for questionId={request.QuestionId}, studentId={studentId}.");
        }

        // Fail-closed: if the contract returned null (attempt not found OR not owned by this student),
        // refuse immediately before any LLM call. Use a generic "not found" message that does NOT
        // reveal whether the attempt exists vs. belongs to a different student (anti-enumeration).
        if (questionAnswer is null)
        {
            _logger.LogWarn(
                $"GetHintCommandHandler: attempt not found or not owned — refusing. " +
                $"studentId={studentId}, attemptId={request.AttemptId}, questionId={request.QuestionId}.");
            return new HintResult.Error(
                nameof(SharedResourcesKey.HintQuestionNotFound),
                _localizer[SharedResourcesKey.HintQuestionNotFound]);
        }

        // Server-derived hint level (OQ-2b): ignore client-supplied HintLevel in favour of
        // Attempt.HintsUsedCount + 1.
        var currentHintLevel = questionAnswer.CurrentHintLevel;
        var maxLevels = _hintOptions.MaxHintLevels;

        // Step 5 — MaxHintLevels bound check (BE-8).
        if (request.Intent == HelperIntent.Hint && currentHintLevel > maxLevels)
        {
            _logger.LogInfo(
                $"GetHintCommandHandler: student {studentId} has reached max hint level ({maxLevels}) " +
                $"for questionId={request.QuestionId} — signalling to offer Simplify.");
            return new HintResult.Error(
                nameof(SharedResourcesKey.HintMaxLevelsReached),
                _localizer[SharedResourcesKey.HintMaxLevelsReached]);
        }

        // Step 6 — fetch grounding context via ILearningContextProvider.
        LearningContext learningCtx;
        try
        {
            learningCtx = await _learningContext.GetContextAsync(
                studentId.Value,
                skillId: 0,
                questionId: request.QuestionId,
                wrongAnswer: request.Intent == HelperIntent.WhyWrong ? request.WrongAnswer : null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"GetHintCommandHandler: ILearningContextProvider failed for studentId={studentId}, questionId={request.QuestionId}.");
            // Treat retrieval failure as no-context → redirect.
            learningCtx = new LearningContext(
                Chunks: Array.Empty<ChunkDto>(),
                QuestionText: null,
                WrongAnswer: null,
                SkillId: 0,
                QuestionId: request.QuestionId,
                GradeId: grade,
                SubjectId: 0,
                Language: language);
        }

        var skillId = learningCtx.SkillId;

        // Step 7 — AC-3 scope guard: empty chunks → refuse-and-redirect. Do NOT call the LLM.
        if (learningCtx.Chunks.Count == 0)
        {
            _logger.LogInfo(
                $"GetHintCommandHandler: no context for studentId={studentId}, questionId={request.QuestionId} — refusing and redirecting.");

            // Emit HelpDeclined fire-and-forget (BE-13).
            // Fresh scope: ensures notification handlers with Scoped dependencies run with a live DI scope.
            var helpDeclinedEvent = new HelpDeclinedIntegrationEvent(studentId.Value, request.Intent, skillId, Reason: "NoContext");
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                    await publisher.Publish(helpDeclinedEvent, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "GetHintCommandHandler: HelpDeclined fire-and-forget failed.");
                }
            });

            var redirectText = _redirectBuilder.Build(skillName: null, language);
            return new HintResult.Redirect(redirectText, TargetSkillId: null);
        }

        // Step 8 — build prompt via IPromptBuilder with the appropriate intent.
        var subject = (Subject)learningCtx.SubjectId;
        var gradeResolved = learningCtx.GradeId > 0 ? learningCtx.GradeId : grade;

        var promptContext = new PromptContext(
            StudentId: studentId.Value,
            Intent: request.Intent,
            Subject: subject,
            Grade: gradeResolved,
            Age: age,
            Language: language,
            WeakAreas: null,       // No weak-areas query at this MVP slice.
            Context: learningCtx);

        var promptResult = _promptBuilder.Build(promptContext);

        if (promptResult is PromptBuilderResult.UnsupportedSubjectResult unsupported)
        {
            _logger.LogWarn($"GetHintCommandHandler: unsupported subject {unsupported.Subject} for studentId={studentId}.");
            return new HintResult.Error(
                nameof(SharedResourcesKey.ExplainConceptUnsupportedSubject),
                _localizer[SharedResourcesKey.ExplainConceptUnsupportedSubject]);
        }

        var aiRequest = ((PromptBuilderResult.Success)promptResult).Request;

        // ── DEFERRED (BE-11/15): cache-first lookup goes here. ────────────────────────
        // For Hint intent:
        //   CacheKey = SHA256(QuestionId, HintLevel, Language, PromptVersion, CurriculumVersion)
        //   call IAiResponseCacheRepository.GetApprovedAsync(cacheKey, ct).
        //   On hit: return HintResult.Streamed(cachedContent, currentHintLevel, nextHintLevel),
        //   emit HintUsedIntegrationEvent + HelpDelivered{ContextSource="Cache"}.
        // For WhyWrong intent (BE-15):
        //   CacheKey = SHA256(QuestionId, SHA256(NormalizedWrongAnswer), Language, AgeBand, PromptVersion, CurriculumVersion)
        //   Same lookup / hit path. On miss: proceed to safety call below.
        // These paths require IAiResponseCacheRepository (P3-04-BE-8) + WrongAnswerNormalizer (P3-05-BE-14)
        // which are deferred to P3-01-BE-12/13/14.
        // ─────────────────────────────────────────────────────────────────────────────────

        // Step 9 — call ISafetyLayer (buffers + screens; NEVER IAiGateway directly — arch test enforced).
        SafeAiResult safeResult;
        try
        {
            safeResult = await _safetyLayer.GenerateSafeAsync(aiRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"GetHintCommandHandler: ISafetyLayer threw for studentId={studentId}. Returning typed error.");
            return new HintResult.Error(
                nameof(SharedResourcesKey.AiServiceUnavailable),
                _localizer[SharedResourcesKey.AiServiceUnavailable]);
        }

        // Step 10 — safety verdict.
        if (!safeResult.Allowed)
        {
            _logger.LogWarn($"GetHintCommandHandler: safety blocked for studentId={studentId}, intent={request.Intent}, verdict={safeResult.Verdict}.");
            return new HintResult.Error(
                nameof(SharedResourcesKey.ExplainConceptSafetyBlocked),
                _localizer[SharedResourcesKey.ExplainConceptSafetyBlocked]);
        }

        var content = safeResult.Content ?? string.Empty;

        // Step 11 — post-generation no-reveal check (OQ-4, AC-1) — Hint intent only.
        // WhyWrong intent: answer explanation is expected to mention the wrong reasoning,
        // not necessarily avoid the correct answer (and its CorrectAnswer string varies in format).
        //
        // SECURITY (Fix 2 — normalization-aware no-reveal check):
        // The naive Contains(OrdinalIgnoreCase) is bypassable via Arabic-Indic digits, tashkeel,
        // whitespace variations, or mixed casing. We normalize BOTH sides before comparing so reveals
        // are caught regardless of surface-form variation. The guard errs on MORE matching — strips
        // tashkeel regardless of subject so any diacriticised form of the answer is caught.
        if (request.Intent == HelperIntent.Hint)
        {
            // questionAnswer is guaranteed non-null here: the fail-closed guard above returned early.
            var correctAnswer = questionAnswer.CorrectAnswer;
            if (!string.IsNullOrEmpty(correctAnswer))
            {
                var normalizedContent       = NormalizeForRevealCheck(content);
                var normalizedCorrectAnswer = NormalizeForRevealCheck(correctAnswer);

                if (normalizedContent.Contains(normalizedCorrectAnswer, StringComparison.Ordinal))
                {
                    _logger.LogWarn(
                        $"GetHintCommandHandler: no-reveal violation (normalized) — hint for questionId={request.QuestionId} " +
                        $"contains CorrectAnswer after normalization. Blocking response (studentId={studentId}).");
                    return new HintResult.Error(
                        nameof(SharedResourcesKey.HintNoRevealViolation),
                        _localizer[SharedResourcesKey.HintNoRevealViolation]);
                }
            }
        }

        // ── DEFERRED (BE-11/15): cache-write of approved response goes here. ──────────
        // After safety passes + no-reveal check:
        // For Hint:
        //   fire-and-forget upsert to IAiResponseCacheRepository(Type=Hint, QuestionId=request.QuestionId):
        //   ReviewStatus = Confidence >= autoApprovalThreshold ? Approved : PendingReview
        // For WhyWrong (BE-15):
        //   enforce cap=50 LRU eviction, upsert (Type=WhyWrong, QuestionId=request.QuestionId).
        // Requires IAiResponseCacheRepository (P3-04-BE-8) which is deferred.
        // ─────────────────────────────────────────────────────────────────────────────────

        // Step 12 — fire-and-forget HintUsedIntegrationEvent (usage recording, AC-7, OQ-3).
        // FIX (P3-05-DEFECT-1): use a fresh DI scope so the Scoped LearningDbContext inside
        // HintUsedIntegrationEventHandler is NOT the already-disposed request-scope instance.
        // Without this, the background continuation races against scope disposal and the
        // ObjectDisposedException is silently swallowed, leaving HintsUsedCount un-incremented.
        var hintUsedEvent = new HintUsedIntegrationEvent(
            QuestionId: request.QuestionId,
            AttemptId: request.AttemptId,
            HintLevel: currentHintLevel,
            StudentId: studentId.Value);
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                await publisher.Publish(hintUsedEvent, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetHintCommandHandler: HintUsed fire-and-forget failed.");
            }
        });

        // Step 13 — emit HelpDelivered fire-and-forget (BE-13 AiTutor-side instrumentation).
        // Fresh scope: ensures notification handlers with Scoped dependencies run with a live DI scope.
        var helpDeliveredEvent = new HelpDeliveredIntegrationEvent(
            studentId.Value,
            request.Intent,
            skillId,
            request.QuestionId,
            HintModelId,
            ContextSourceLive);
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                await publisher.Publish(helpDeliveredEvent, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetHintCommandHandler: HelpDelivered fire-and-forget failed.");
            }
        });

        // Step 14 — return result.
        // For Hint: include current/next level so P3-12 can show "Hint n of N".
        // For WhyWrong: no level metadata.
        if (request.Intent == HelperIntent.Hint)
        {
            var nextLevel = currentHintLevel < maxLevels ? currentHintLevel + 1 : (int?)null;
            return new HintResult.Streamed(content, currentHintLevel, nextLevel);
        }

        // WhyWrong — no level metadata.
        return new HintResult.Streamed(content, CurrentHintLevel: null, NextHintLevel: null);
    }

    /// <summary>
    /// Normalizes a string for the post-generation no-reveal comparison (Fix 2 — OQ-4).
    ///
    /// <para>Steps applied (in order):</para>
    /// <list type="number">
    ///   <item>Unicode NFC normalization — canonical equivalence (ä == a + combining-diaeresis).</item>
    ///   <item>Strip Arabic tashkeel / diacritics — Unicode category NonSpacingMark covers fatḥa,
    ///     kasra, ḍamma, sukūn, shadda, tanwīn, and maddah. Applied unconditionally so a
    ///     diacriticised form of the correct answer is always caught regardless of subject.</item>
    ///   <item>Canonicalize Arabic-Indic numerals ٠١٢٣٤٥٦٧٨٩ → 0-9.</item>
    ///   <item>Collapse / normalize whitespace — all whitespace sequences become a single ASCII space.</item>
    ///   <item>Case-fold (lower-case) — catches mixed-case English leakage.</item>
    /// </list>
    ///
    /// <para>Errs on MORE matching: when in doubt a surface-form variation is treated as a match.
    /// This is the correct bias for a security check.</para>
    /// </summary>
    private static string NormalizeForRevealCheck(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Step 1: Unicode NFC (canonical composition).
        var nfc = input.Normalize(NormalizationForm.FormC);

        // Step 2: Strip Arabic tashkeel / diacritics via Unicode category NonSpacingMark.
        var stripped = new string(
            nfc.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
               .ToArray());

        // Step 3: Canonicalize Arabic-Indic digits ٠١٢٣٤٥٦٧٨٩ → 0-9.
        var arabicIndicMap = new[] { '٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩' };
        var sb = new StringBuilder(stripped.Length);
        foreach (var ch in stripped)
        {
            var idx = Array.IndexOf(arabicIndicMap, ch);
            sb.Append(idx >= 0 ? (char)('0' + idx) : ch);
        }
        var digitNorm = sb.ToString();

        // Step 4: Collapse whitespace — all whitespace sequences → single space; trim.
        var wsNorm = Regex.Replace(digitNorm, @"\s+", " ").Trim();

        // Step 5: Case-fold.
        return wsNorm.ToLowerInvariant();
    }

    /// <summary>
    /// Resolves grade, age, and language from the authenticated student's JWT claims.
    /// Defaults gracefully when claims are absent or unparseable (never returns false — always sets
    /// sensible defaults so the handler can proceed).
    /// </summary>
    private void TryResolveProfile(out int grade, out int age, out TutorLanguage language)
    {
        grade = 4;   // Safe default.
        age = 10;    // Safe default.
        language = TutorLanguage.Ar;

        var gradeStr    = _currentUser.GetClaimValue(GradeClaim);
        var ageStr      = _currentUser.GetClaimValue(AgeClaim);
        var languageStr = _currentUser.GetClaimValue(LanguageClaim);

        if (int.TryParse(gradeStr, out var parsedGrade) && parsedGrade > 0)
            grade = parsedGrade;

        if (int.TryParse(ageStr, out var parsedAge) && parsedAge > 0)
            age = parsedAge;

        if (string.Equals(languageStr, "en", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(languageStr, "en-US", StringComparison.OrdinalIgnoreCase))
            language = TutorLanguage.En;
    }
}
