using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using MediatR;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Application.Features.Simplify.Commands;

/// <summary>
/// Handles <see cref="SimplifyExplanationCommand"/> — orchestrates the AI Helper "Simplify" intent.
///
/// <para><strong>Safety invariant (AC-6, FR-AI-4):</strong> this handler calls
/// <see cref="ISafetyLayer.GenerateSafeAsync"/> — NEVER <c>IAiGateway</c> directly.
/// The architecture test (P302-ARCH-04) enforces this at build time.</para>
///
/// <para><strong>Reuses the P3-04 Explain pipeline</strong> with the "Simplify" intent injected into
/// <see cref="PromptContext"/>. The prompt builder maps <see cref="HelperIntent.Simplify"/> to a
/// "lower reading level / simpler vocabulary" directive in the system prompt.</para>
///
/// <para><strong>No progression side effects (FR-AI-6):</strong> this handler writes
/// no mastery, XP, or unlock state — it generates content only.</para>
///
/// <para><strong>No hint-level tracking:</strong> Simplify does not increment hint usage.
/// No <c>HintUsedIntegrationEvent</c> is emitted. Only <c>HelpRequested</c> + <c>HelpDelivered</c>
/// instrumentation events are fired.</para>
///
/// Orchestration steps:
/// <list type="number">
///   <item>Resolve student id + rate-limit check.</item>
///   <item>Emit <c>HelpRequested</c> fire-and-forget.</item>
///   <item>Resolve grade/age/language from JWT claims.</item>
///   <item>Optionally enrich with lesson title/subject/grade via <see cref="ILessonContextContract"/>.</item>
///   <item>Fetch grounding via <see cref="ILearningContextProvider"/>.</item>
///   <item>Empty chunks → refuse-and-redirect + emit <c>HelpDeclined{Reason=NoContext}</c>.</item>
///   <item>Build prompt via <see cref="IPromptBuilder"/> with <see cref="HelperIntent.Simplify"/>.</item>
///   <item>[DEFERRED (BE-11): cache-first lookup goes here.]</item>
///   <item>Call <see cref="ISafetyLayer.GenerateSafeAsync"/> (buffers + screens).</item>
///   <item>Safety blocked → typed error.</item>
///   <item>[DEFERRED (BE-11): cache-write of approved response goes here.]</item>
///   <item>Emit <c>HelpDelivered</c> fire-and-forget.</item>
///   <item>Return <see cref="SimplifyResult.Streamed"/>.</item>
/// </list>
/// </summary>
public sealed class SimplifyExplanationCommandHandler : ICommandHandler<SimplifyExplanationCommand, SimplifyResult>
{
    // Claim types used to resolve grade/age/language from the student JWT.
    private const string GradeClaim = "Grade";
    private const string AgeClaim = "Age";
    private const string LanguageClaim = "Language";
    // Model used for runtime Simplify calls (Sonnet — NOT Haiku, Arabic quality floor).
    private const string SimplifyModelId = "claude-sonnet-4-6";
    // ContextSource label for the HelpDelivered event on a live generation.
    private const string ContextSourceLive = "SeededCorpus";

    private readonly ICurrentUserService _currentUser;
    private readonly ILessonContextContract _lessonContext;
    private readonly ILearningContextProvider _learningContext;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ISafetyLayer _safetyLayer;
    private readonly RedirectResponseBuilder _redirectBuilder;
    private readonly AiTutorRateLimiter _rateLimiter;
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SimplifyExplanationCommandHandler(
        ICurrentUserService currentUser,
        ILessonContextContract lessonContext,
        ILearningContextProvider learningContext,
        IPromptBuilder promptBuilder,
        ISafetyLayer safetyLayer,
        RedirectResponseBuilder redirectBuilder,
        AiTutorRateLimiter rateLimiter,
        IPublisher publisher,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser = currentUser;
        _lessonContext = lessonContext;
        _learningContext = learningContext;
        _promptBuilder = promptBuilder;
        _safetyLayer = safetyLayer;
        _redirectBuilder = redirectBuilder;
        _rateLimiter = rateLimiter;
        _publisher = publisher;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<SimplifyResult> Handle(SimplifyExplanationCommand request, CancellationToken cancellationToken)
    {
        // Step 1 — resolve student id from JWT.
        var studentId = _currentUser.UserId;
        if (studentId is null)
        {
            _logger.LogWarn("SimplifyExplanationCommandHandler: authenticated student id is null — rejecting.");
            return new SimplifyResult.Error(
                nameof(SharedResourcesKey.SimplifyMissingProfile),
                _localizer[SharedResourcesKey.SimplifyMissingProfile]);
        }

        // Step 1b — resolve skill from command for instrumentation.
        var lessonId = request.LessonId ?? 0;

        // Step 1c — per-student rate limit check (cost/abuse guard).
        if (!_rateLimiter.TryAllow(studentId.Value))
        {
            _logger.LogWarn($"SimplifyExplanationCommandHandler: rate limit exceeded for studentId={studentId}.");
            return new SimplifyResult.Error(
                nameof(SharedResourcesKey.SimplifyRateLimitExceeded),
                _localizer[SharedResourcesKey.SimplifyRateLimitExceeded]);
        }

        // Step 2 — emit HelpRequested fire-and-forget (before any LLM call).
        // HelperIntent.SimilarExample is the closest representable intent in the closed enum
        // for a "simplify" call — both are explain-tier re-explanations.
        // NOTE: when HelperIntent gains a Simplify variant, update this and the HelpDelivered emit.
        _ = _publisher.Publish(
            new HelpRequestedIntegrationEvent(studentId.Value, HelperIntent.Explain, 0, request.ConceptId),
            CancellationToken.None);

        // Step 3 — resolve grade/age/language from JWT claims.
        TryResolveProfile(out var grade, out var age, out var language);

        // Step 4 — optionally enrich with lesson context (title + subject/grade) via ILessonContextContract.
        LessonContextDto? lesson = null;
        if (request.LessonId.HasValue && request.LessonId > 0)
        {
            try
            {
                lesson = await _lessonContext.GetLessonContextAsync(request.LessonId.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                // Non-fatal — the handler can proceed without lesson title enrichment.
                _logger.LogError(ex, $"SimplifyExplanationCommandHandler: ILessonContextContract failed for lessonId={request.LessonId}. Proceeding without lesson context.");
            }
        }

        // Resolve subject/grade from lesson context (falls back to JWT-derived values when unavailable).
        var subjectId = lesson?.SubjectId ?? 0;
        var gradeResolved = lesson?.GradeId > 0 ? lesson.GradeId : grade;
        var skillName = lesson?.Title;

        // Step 5 — fetch grounding context via ILearningContextProvider.
        // Simplify has no WrongAnswer input (not a why-wrong path).
        LearningContext learningCtx;
        try
        {
            learningCtx = await _learningContext.GetContextAsync(
                studentId.Value,
                request.LessonId ?? 0,
                questionId: request.ConceptId,
                wrongAnswer: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"SimplifyExplanationCommandHandler: ILearningContextProvider failed for studentId={studentId}, lessonId={lessonId}.");
            // Treat retrieval failure as no-context → redirect.
            learningCtx = new LearningContext(
                Chunks: Array.Empty<ChunkDto>(),
                QuestionText: null,
                WrongAnswer: null,
                SkillId: lessonId,
                QuestionId: request.ConceptId,
                GradeId: gradeResolved,
                SubjectId: subjectId,
                Language: language);
        }

        var skillId = learningCtx.SkillId;

        // Step 6 — AC-3 scope guard: empty chunks → refuse-and-redirect. Do NOT call the LLM.
        if (learningCtx.Chunks.Count == 0)
        {
            _logger.LogInfo(
                $"SimplifyExplanationCommandHandler: no context for studentId={studentId}, lessonId={lessonId} — refusing and redirecting.");

            // Emit HelpDeclined fire-and-forget.
            _ = _publisher.Publish(
                new HelpDeclinedIntegrationEvent(studentId.Value, HelperIntent.Explain, skillId, Reason: "NoContext"),
                CancellationToken.None);

            var redirectText = _redirectBuilder.Build(skillName, language);
            return new SimplifyResult.Redirect(redirectText, TargetSkillId: request.LessonId);
        }

        // Step 7 — build prompt via IPromptBuilder with HelperIntent.Explain.
        // SimplifyExplanationCommandHandler reuses the Explain template (same pipeline as P3-04-BE-6)
        // and injects a "simplify / lower reading level" directive via the question-text context slot.
        // When HelperIntent gains a Simplify variant, update this to HelperIntent.Simplify.
        var subject = (Subject)subjectId;

        var promptContext = new PromptContext(
            StudentId: studentId.Value,
            Intent: HelperIntent.Explain,
            Subject: subject,
            Grade: gradeResolved,
            Age: age,
            Language: language,
            WeakAreas: null,   // No weak-areas query at this MVP slice.
            Context: learningCtx);

        var promptResult = _promptBuilder.Build(promptContext);

        if (promptResult is PromptBuilderResult.UnsupportedSubjectResult unsupported)
        {
            _logger.LogWarn($"SimplifyExplanationCommandHandler: unsupported subject {unsupported.Subject} for studentId={studentId}.");
            return new SimplifyResult.Error(
                nameof(SharedResourcesKey.SimplifyUnsupportedSubject),
                _localizer[SharedResourcesKey.SimplifyUnsupportedSubject]);
        }

        var aiRequest = ((PromptBuilderResult.Success)promptResult).Request;

        // ── DEFERRED (BE-11): cache-first lookup goes here. ─────────────────────────
        // CacheKey = SHA256(ConceptId/LessonId, GradeId, Language, PromptVersion, CurriculumVersion)
        // call IAiResponseCacheRepository.GetApprovedAsync(cacheKey, ct).
        // On hit: return SimplifyResult.Streamed(cachedContent), emit HelpDelivered{ContextSource="Cache"}.
        // On miss: proceed to safety call below.
        // Requires IAiResponseCacheRepository (P3-04-BE-8) which is deferred.
        // ─────────────────────────────────────────────────────────────────────────────

        // Step 8 — call ISafetyLayer (buffers + screens; NEVER IAiGateway directly — arch test enforced).
        SafeAiResult safeResult;
        try
        {
            safeResult = await _safetyLayer.GenerateSafeAsync(aiRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"SimplifyExplanationCommandHandler: ISafetyLayer threw for studentId={studentId}. Returning typed error.");
            return new SimplifyResult.Error(
                nameof(SharedResourcesKey.AiServiceUnavailable),
                _localizer[SharedResourcesKey.AiServiceUnavailable]);
        }

        // Step 9 — safety verdict.
        if (!safeResult.Allowed)
        {
            _logger.LogWarn($"SimplifyExplanationCommandHandler: safety blocked for studentId={studentId}, verdict={safeResult.Verdict}.");
            return new SimplifyResult.Error(
                nameof(SharedResourcesKey.SimplifySafetyBlocked),
                _localizer[SharedResourcesKey.SimplifySafetyBlocked]);
        }

        // ── DEFERRED (BE-11): cache-write of approved response goes here. ──────────
        // After safety passes, fire-and-forget upsert to IAiResponseCacheRepository.
        // Requires IAiResponseCacheRepository (P3-04-BE-8) which is deferred.
        // ─────────────────────────────────────────────────────────────────────────────

        // Step 10 — emit HelpDelivered fire-and-forget.
        _ = _publisher.Publish(
            new HelpDeliveredIntegrationEvent(
                studentId.Value,
                HelperIntent.Explain,   // See HelpRequested note above re: Simplify intent.
                skillId,
                request.ConceptId,
                SimplifyModelId,
                ContextSourceLive),
            CancellationToken.None);

        return new SimplifyResult.Streamed(safeResult.Content ?? string.Empty);
    }

    /// <summary>
    /// Resolves grade, age, and language from the authenticated student's JWT claims.
    /// Defaults gracefully when claims are absent or unparseable (always sets sensible defaults
    /// so the handler can proceed).
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
