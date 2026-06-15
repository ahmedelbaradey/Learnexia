using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Learnexia.Modules.Ai.Application.Cache;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Settings;
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
/// <para><strong>Cache-first (WI-B4):</strong> for the <em>Hint</em> intent, cache lookup is
/// performed before the Safety Layer call. For <em>WhyWrong</em> the compound key
/// (QuestionId + normalized wrong-answer hash + AgeBand) enables cross-student reuse while
/// the LRU cap prevents unbounded growth.</para>
///
/// <para><strong>WhyWrong is runtime cacheable (AI cost routing §5):</strong> the student's
/// wrong answer is normalized + hashed; the cache key uses the compound key with AgeBand.
/// Per-student uniqueness is NOT required — the same wrong-reasoning pattern can be reused
/// for all students at the same developmental level.</para>
/// </summary>
public sealed class GetHintCommandHandler : ICommandHandler<GetHintCommand, HintResult>
{
    private const string GradeClaim    = "Grade";
    private const string AgeClaim      = "Age";
    private const string LanguageClaim = "Language";
    private const string HintModelId   = "claude-sonnet-4-6";
    private const string ContextSourceLive  = "Live";
    private const string ContextSourceCache = "Cache";
    private const string PromptVersionV1      = "v1";
    private const string CurriculumVersionMvp = "mvp";

    private readonly ICurrentUserService _currentUser;
    private readonly IQuestionAnswerContract _questionAnswer;
    private readonly ILearningContextProvider _learningContext;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ISafetyLayer _safetyLayer;
    private readonly IAiResponseCache _aiCache;
    private readonly IGlobalSettingsProvider _settings;
    private readonly RedirectResponseBuilder _redirectBuilder;
    private readonly IAiTutorRateLimiter _rateLimiter;
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
        IAiResponseCache aiCache,
        IGlobalSettingsProvider settings,
        RedirectResponseBuilder redirectBuilder,
        IAiTutorRateLimiter rateLimiter,
        IPublisher publisher,
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer,
        IOptions<HintOptions> hintOptions)
    {
        _currentUser     = currentUser;
        _questionAnswer  = questionAnswer;
        _learningContext = learningContext;
        _promptBuilder   = promptBuilder;
        _safetyLayer     = safetyLayer;
        _aiCache         = aiCache;
        _settings        = settings;
        _redirectBuilder = redirectBuilder;
        _rateLimiter     = rateLimiter;
        _publisher       = publisher;
        _scopeFactory    = scopeFactory;
        _logger          = logger;
        _localizer       = localizer;
        _hintOptions     = hintOptions.Value;
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

        // Step 2 — emit HelpRequested fire-and-forget.
        var helpRequestedEvent = new HelpRequestedIntegrationEvent(
            studentId.Value, request.Intent, 0, request.QuestionId);
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

        // Step 4 — get CorrectAnswer + server-derived hint level (OQ-2b + OQ-4 IDOR guard).
        QuestionAnswerDto? questionAnswer = null;
        try
        {
            questionAnswer = await _questionAnswer.GetQuestionAnswerAsync(
                request.QuestionId, request.AttemptId, studentId.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"GetHintCommandHandler: IQuestionAnswerContract failed for questionId={request.QuestionId}, studentId={studentId}.");
        }

        if (questionAnswer is null)
        {
            _logger.LogWarn(
                $"GetHintCommandHandler: attempt not found or not owned — refusing. " +
                $"studentId={studentId}, attemptId={request.AttemptId}, questionId={request.QuestionId}.");
            return new HintResult.Error(
                nameof(SharedResourcesKey.HintQuestionNotFound),
                _localizer[SharedResourcesKey.HintQuestionNotFound]);
        }

        var currentHintLevel = questionAnswer.CurrentHintLevel;
        var maxLevels        = _hintOptions.MaxHintLevels;

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

        // Step 7 — AC-3 scope guard: empty chunks → refuse-and-redirect.
        if (learningCtx.Chunks.Count == 0)
        {
            _logger.LogInfo(
                $"GetHintCommandHandler: no context for studentId={studentId}, questionId={request.QuestionId} — refusing and redirecting.");

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

        // Step 8 — build prompt via IPromptBuilder.
        var subject      = (Subject)learningCtx.SubjectId;
        var gradeResolved = learningCtx.GradeId > 0 ? learningCtx.GradeId : grade;

        var promptContext = new PromptContext(
            StudentId: studentId.Value,
            Intent: request.Intent,
            Subject: subject,
            Grade: gradeResolved,
            Age: age,
            Language: language,
            WeakAreas: null,
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

        // ── WI-B4: cache-first lookup (AC-B1) ───────────────────────────────────
        // Security: `grade` (from JWT claim, server-trusted) is used for the key's AgeBand
        // in BOTH Hint and WhyWrong intents. `gradeResolved` (from learningCtx, which may
        // reflect the lesson's grade rather than the student's JWT grade) continues to be
        // used for prompt grounding via PromptContext, but must NOT drive the cache key.
        // `learningCtx.SubjectId` is included so Math and Science questions with the same
        // QuestionId are never keyed into the same cache slot.
        string cacheKey;
        if (request.Intent == HelperIntent.Hint)
        {
            cacheKey = AiCacheKeyBuilder.ForHint(
                subjectId:         learningCtx.SubjectId,
                questionId:        request.QuestionId,
                hintLevel:         currentHintLevel,
                jwtGrade:          grade,      // JWT-claim grade — server-trusted, not spoofable.
                language:          language,
                promptVersion:     PromptVersionV1,
                curriculumVersion: CurriculumVersionMvp);
        }
        else
        {
            // WhyWrong: compound key includes normalized wrong-answer hash + AgeBand(jwtGrade).
            cacheKey = AiCacheKeyBuilder.ForWhyWrong(
                subjectId:         learningCtx.SubjectId,
                questionId:        request.QuestionId,
                wrongAnswer:       request.WrongAnswer ?? string.Empty,
                language:          language,
                jwtGrade:          grade,      // JWT-claim grade — server-trusted, not spoofable.
                promptVersion:     PromptVersionV1,
                curriculumVersion: CurriculumVersionMvp);
        }

        var cachedContent = await _aiCache.GetApprovedAsync(cacheKey, cancellationToken);
        if (cachedContent is not null)
        {
            _logger.LogInfo($"GetHintCommandHandler: cache HIT for intent={request.Intent}, studentId={studentId}, key={cacheKey[..8]}…");

            // Fire-and-forget HintUsed on cache HIT too (for accurate usage tracking).
            await FireHintUsedAsync(request, studentId.Value, currentHintLevel);

            var helpDeliveredCacheEvent = new HelpDeliveredIntegrationEvent(
                studentId.Value, request.Intent, skillId, request.QuestionId, HintModelId, ContextSourceCache);
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                    await publisher.Publish(helpDeliveredCacheEvent, CancellationToken.None);
                }
                catch (Exception ex) { _logger.LogError(ex, "GetHintCommandHandler: HelpDelivered (cache) fire-and-forget failed."); }
            });

            if (request.Intent == HelperIntent.Hint)
            {
                var nextLevel = currentHintLevel < maxLevels ? currentHintLevel + 1 : (int?)null;
                return new HintResult.Streamed(cachedContent, currentHintLevel, nextLevel);
            }
            return new HintResult.Streamed(cachedContent, CurrentHintLevel: null, NextHintLevel: null);
        }
        // ─────────────────────────────────────────────────────────────────────────

        // Step 9 — cache MISS: call ISafetyLayer.
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
            // Safety-FAILED: DO NOT cache.
            return new HintResult.Error(
                nameof(SharedResourcesKey.ExplainConceptSafetyBlocked),
                _localizer[SharedResourcesKey.ExplainConceptSafetyBlocked]);
        }

        var content = safeResult.Content ?? string.Empty;

        // Step 11 — post-generation no-reveal check (OQ-4, AC-1) — Hint intent only.
        if (request.Intent == HelperIntent.Hint)
        {
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
                    // No-reveal violation: DO NOT cache this response.
                    return new HintResult.Error(
                        nameof(SharedResourcesKey.HintNoRevealViolation),
                        _localizer[SharedResourcesKey.HintNoRevealViolation]);
                }
            }
        }

        // ── WI-B4: cache-write of approved response (fire-and-forget) ───────────
        // Only safety-PASSED + (for Hint) no-reveal-passed responses are cached.
        var approvalThreshold = _settings.GetDecimal("ai.cache.autoApprovalConfidence", 0.85m);
        var reviewStatus      = (safeResult.Confidence >= approvalThreshold)
            ? AiCacheReviewStatusDto.Approved
            : AiCacheReviewStatusDto.PendingReview;

        var entryType = request.Intent == HelperIntent.Hint
            ? AiCacheEntryTypeDto.Hint
            : AiCacheEntryTypeDto.WhyWrong;

        var writeEntry = new AiCacheWriteEntry(
            CacheKey:          cacheKey,
            Response:          content,
            Type:              entryType,
            SkillKey:          skillId.ToString(),
            QuestionId:        request.QuestionId,   // Decision 4: populated for Hint/WhyWrong.
            CurriculumVersion: CurriculumVersionMvp,
            PromptVersion:     PromptVersionV1,
            ModelVersion:      HintModelId,
            ReviewStatus:      reviewStatus,
            Confidence:        safeResult.Confidence);

        // Fire-and-forget the cache write on a fresh scope so the scoped AiDbContext
        // lifetime is fully independent of the SSE request scope (DEFECT-3 fix).
        // The request scope may be disposed before the detached write task runs;
        // creating a new scope here ensures the write has its own AiDbContext lifetime.
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var cache = scope.ServiceProvider.GetRequiredService<IAiResponseCache>();
                await cache.WriteAsync(writeEntry, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarn($"GetHintCommandHandler: cache write fire-and-forget failed. {ex.GetType().Name}");
            }
        });
        // ─────────────────────────────────────────────────────────────────────────

        // Step 12 — fire-and-forget HintUsedIntegrationEvent.
        await FireHintUsedAsync(request, studentId.Value, currentHintLevel);

        // Step 13 — emit HelpDelivered fire-and-forget.
        var helpDeliveredEvent = new HelpDeliveredIntegrationEvent(
            studentId.Value, request.Intent, skillId, request.QuestionId, HintModelId, ContextSourceLive);
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
        if (request.Intent == HelperIntent.Hint)
        {
            var nextLevel = currentHintLevel < maxLevels ? currentHintLevel + 1 : (int?)null;
            return new HintResult.Streamed(content, currentHintLevel, nextLevel);
        }

        return new HintResult.Streamed(content, CurrentHintLevel: null, NextHintLevel: null);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private Task FireHintUsedAsync(GetHintCommand request, int studentId, int currentHintLevel)
    {
        var hintUsedEvent = new HintUsedIntegrationEvent(
            QuestionId: request.QuestionId,
            AttemptId:  request.AttemptId,
            HintLevel:  currentHintLevel,
            StudentId:  studentId);
        return Task.Run(async () =>
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
    }

    private static string NormalizeForRevealCheck(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var nfc = input.Normalize(NormalizationForm.FormC);

        var stripped = new string(
            nfc.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
               .ToArray());

        var arabicIndicMap = new[] { '٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩' };
        var sb = new StringBuilder(stripped.Length);
        foreach (var ch in stripped)
        {
            var idx = Array.IndexOf(arabicIndicMap, ch);
            sb.Append(idx >= 0 ? (char)('0' + idx) : ch);
        }
        var digitNorm = sb.ToString();

        var wsNorm = Regex.Replace(digitNorm, @"\s+", " ").Trim();

        return wsNorm.ToLowerInvariant();
    }

    private void TryResolveProfile(out int grade, out int age, out TutorLanguage language)
    {
        grade    = 4;
        age      = 10;
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
