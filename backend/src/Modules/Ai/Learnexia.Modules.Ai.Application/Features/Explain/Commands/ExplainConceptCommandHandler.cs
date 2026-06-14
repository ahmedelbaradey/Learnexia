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

namespace Learnexia.Modules.Ai.Application.Features.Explain.Commands;

/// <summary>
/// Handles <see cref="ExplainConceptCommand"/> — orchestrates the AI Helper "Explain" intent.
///
/// <para><strong>Safety invariant (AC-2, FR-AI-4):</strong> this handler calls
/// <see cref="ISafetyLayer.GenerateSafeAsync"/> — NEVER <c>IAiGateway</c> directly.
/// The architecture test (P302-ARCH-04) enforces this at build time.</para>
///
/// <para><strong>Buffer → Safety → Emit pattern (OQ-2a):</strong> the full response is
/// buffered inside <see cref="ISafetyLayer"/>, safety-screened, then returned.
/// The SSE controller emits only the approved buffer. No unscreened token ever reaches
/// the student — lead-approved (SSE rule-8 exception).</para>
///
/// <para><strong>No progression side effects (AC-5, FR-AI-6):</strong> this handler
/// writes no mastery, XP, or unlock state — it generates content only.</para>
///
/// Orchestration steps:
/// <list type="number">
///   <item>Emit <c>HelpRequested</c> fire-and-forget.</item>
///   <item>Resolve grade/age/language from JWT claims via <see cref="ICurrentUserService"/>.</item>
///   <item>Optionally enrich with lesson title/subject/grade via <see cref="ILessonContextContract"/>.</item>
///   <item>Fetch grounding via <see cref="ILearningContextProvider"/>.</item>
///   <item>Empty chunks → refuse-and-redirect; emit <c>HelpDeclined{Reason=NoContext}</c>.</item>
///   <item>Non-empty → build <see cref="AiRequest"/> via <see cref="IPromptBuilder"/>.</item>
///   <item>[DEFERRED P3-04-BE-9: cache-first lookup will slot in here before the safety call.]</item>
///   <item>Call <see cref="ISafetyLayer.GenerateSafeAsync"/> (buffers + screens).</item>
///   <item>Blocked → return <see cref="ExplainResult.Error"/>.</item>
///   <item>Allowed → return <see cref="ExplainResult.Streamed"/>; emit <c>HelpDelivered</c>.</item>
///   <item>[DEFERRED P3-04-BE-9: cache-write of approved response will slot in here.]</item>
/// </list>
/// </summary>
public sealed class ExplainConceptCommandHandler : ICommandHandler<ExplainConceptCommand, ExplainResult>
{
    // Claim types used to resolve grade/age/language from the student JWT.
    private const string GradeClaim = "Grade";
    private const string AgeClaim = "Age";
    private const string LanguageClaim = "Language";
    // Model used for runtime Explain calls (Sonnet — NOT Opus).
    private const string ExplainModelId = "claude-sonnet-4-6";
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

    public ExplainConceptCommandHandler(
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

    public async Task<ExplainResult> Handle(ExplainConceptCommand request, CancellationToken cancellationToken)
    {
        // Step 1 — resolve student id from JWT.
        var studentId = _currentUser.UserId;
        if (studentId is null)
        {
            _logger.LogWarn("ExplainConceptCommandHandler: authenticated student id is null — rejecting.");
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.ExplainConceptMissingProfile),
                _localizer[SharedResourcesKey.ExplainConceptMissingProfile]);
        }

        // Step 1b — resolve skill/question from command for instrumentation.
        var skillId = request.SkillId ?? 0;
        var questionId = request.ConceptId;   // ConceptId doubles as questionId when SkillId absent.

        // Step 1c — per-student rate limit check (BE-5 cost/abuse guard).
        if (!_rateLimiter.TryAllow(studentId.Value))
        {
            _logger.LogWarn($"ExplainConceptCommandHandler: rate limit exceeded for studentId={studentId}.");
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.ExplainConceptRateLimitExceeded),
                _localizer[SharedResourcesKey.ExplainConceptRateLimitExceeded]);
        }

        // Step 2 — emit HelpRequested fire-and-forget (before any LLM call).
        _ = _publisher.Publish(
            new HelpRequestedIntegrationEvent(studentId.Value, HelperIntent.Explain, skillId, questionId),
            CancellationToken.None);

        // Step 3 — resolve grade/age/language from JWT claims.
        if (!TryResolveProfile(out var grade, out var age, out var language))
        {
            _logger.LogWarn($"ExplainConceptCommandHandler: missing grade/language claims for studentId={studentId}.");
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.ExplainConceptMissingProfile),
                _localizer[SharedResourcesKey.ExplainConceptMissingProfile]);
        }

        // Step 4 — optionally enrich with lesson context (title → used in redirect copy + subject/grade).
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
                _logger.LogError(ex, $"ExplainConceptCommandHandler: ILessonContextContract failed for lessonId={request.LessonId}. Proceeding without lesson context.");
            }
        }

        // Resolve subject for the prompt builder (falls back to 0 when unknown — prompt builder handles unsupported).
        var subjectId = lesson?.SubjectId ?? 0;
        var gradeResolved = lesson?.GradeId > 0 ? lesson.GradeId : grade;
        var skillName = lesson?.Title;

        // Step 5 — fetch grounding context via ILearningContextProvider.
        LearningContext learningCtx;
        try
        {
            learningCtx = await _learningContext.GetContextAsync(
                studentId.Value,
                request.SkillId ?? 0,
                questionId,
                wrongAnswer: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ExplainConceptCommandHandler: ILearningContextProvider failed for studentId={studentId}, skillId={skillId}.");
            // Treat retrieval failure as no-context → redirect.
            learningCtx = new LearningContext(
                Chunks: Array.Empty<ChunkDto>(),
                QuestionText: null,
                WrongAnswer: null,
                SkillId: skillId,
                QuestionId: questionId,
                GradeId: gradeResolved,
                SubjectId: subjectId,
                Language: language);
        }

        // Step 6 — AC-3 scope guard: empty chunks → refuse-and-redirect. Do NOT call the LLM.
        if (learningCtx.Chunks.Count == 0)
        {
            _logger.LogInfo($"ExplainConceptCommandHandler: no context for studentId={studentId}, skillId={skillId} — refusing and redirecting.");

            // Emit HelpDeclined fire-and-forget.
            _ = _publisher.Publish(
                new HelpDeclinedIntegrationEvent(studentId.Value, HelperIntent.Explain, skillId, Reason: "NoContext"),
                CancellationToken.None);

            var redirectText = _redirectBuilder.Build(skillName, language);
            return new ExplainResult.Redirect(redirectText, TargetSkillId: request.SkillId);
        }

        // Step 7 — build prompt via IPromptBuilder.
        // Map subjectId to the Subject enum; default to Subject.Math when unknown (handled gracefully
        // by TemplateSelector returning UnsupportedSubjectResult).
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
            _logger.LogWarn($"ExplainConceptCommandHandler: unsupported subject {unsupported.Subject} for studentId={studentId}.");
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.ExplainConceptUnsupportedSubject),
                _localizer[SharedResourcesKey.ExplainConceptUnsupportedSubject]);
        }

        var aiRequest = ((PromptBuilderResult.Success)promptResult).Request;

        // ── DEFERRED (P3-04-BE-9): cache-first lookup goes here. ────────────────────────
        // Before calling the gateway, compute:
        //   CacheKey = SHA256(ConceptId, GradeId, Language, Difficulty, PromptVersion, CurriculumVersion)
        // and call IAiResponseCacheRepository.GetApprovedAsync(cacheKey, ct).
        // On hit (ReviewStatus=Approved, InvalidatedAt=null): return Streamed(cachedContent),
        //   emit HelpDelivered{ContextSource="Cache"} and skip the safety call.
        // On miss: proceed to the safety call below.
        // This path requires IAiResponseCacheRepository (P3-04-BE-8) which is deferred.
        // ─────────────────────────────────────────────────────────────────────────────────

        // Step 8 — call ISafetyLayer (buffers + screens; NEVER IAiGateway directly — arch test enforced).
        SafeAiResult safeResult;
        try
        {
            safeResult = await _safetyLayer.GenerateSafeAsync(aiRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ExplainConceptCommandHandler: ISafetyLayer threw for studentId={studentId}. Returning typed error.");
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.AiServiceUnavailable),
                _localizer[SharedResourcesKey.AiServiceUnavailable]);
        }

        // Step 9 — safety verdict.
        if (!safeResult.Allowed)
        {
            _logger.LogWarn($"ExplainConceptCommandHandler: safety blocked for studentId={studentId}, verdict={safeResult.Verdict}.");
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.ExplainConceptSafetyBlocked),
                _localizer[SharedResourcesKey.ExplainConceptSafetyBlocked]);
        }

        // ── DEFERRED (P3-04-BE-9): cache-write of approved response goes here. ──────────
        // After safety passes, fire-and-forget upsert to IAiResponseCacheRepository:
        //   ReviewStatus = Confidence >= autoApprovalThreshold ? Approved : PendingReview
        // QuestionId = null for Explain type (Decision 4).
        // This path requires IAiResponseCacheRepository (P3-04-BE-8) which is deferred.
        // ─────────────────────────────────────────────────────────────────────────────────

        // Step 10 — emit HelpDelivered fire-and-forget.
        _ = _publisher.Publish(
            new HelpDeliveredIntegrationEvent(
                studentId.Value,
                HelperIntent.Explain,
                skillId,
                questionId,
                ModelUsed: ExplainModelId,
                ContextSource: ContextSourceLive),
            CancellationToken.None);

        return new ExplainResult.Streamed(safeResult.Content ?? string.Empty);
    }

    /// <summary>
    /// Attempts to resolve grade, age, and language from the authenticated student's JWT claims.
    /// Returns false when any required claim is absent or unparseable.
    /// </summary>
    private bool TryResolveProfile(out int grade, out int age, out TutorLanguage language)
    {
        grade = 0;
        age = 0;
        language = TutorLanguage.Ar;

        var gradeStr = _currentUser.GetClaimValue(GradeClaim);
        var ageStr = _currentUser.GetClaimValue(AgeClaim);
        var languageStr = _currentUser.GetClaimValue(LanguageClaim);

        if (!int.TryParse(gradeStr, out grade) || grade <= 0)
            grade = 4; // Safe default — documented in IChildLearningProfileQuery stub.

        if (!int.TryParse(ageStr, out age) || age <= 0)
            age = 10; // Safe default.

        if (string.Equals(languageStr, "en", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(languageStr, "en-US", StringComparison.OrdinalIgnoreCase))
            language = TutorLanguage.En;
        else
            language = TutorLanguage.Ar; // Default to Arabic.

        // Language claim is always resolvable (defaults to Ar) — no hard failure here.
        return true;
    }
}
