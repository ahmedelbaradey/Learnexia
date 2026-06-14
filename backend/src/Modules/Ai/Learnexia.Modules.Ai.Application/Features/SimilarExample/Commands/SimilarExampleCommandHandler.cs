using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Application.Features.SimilarExample.Commands;

/// <summary>
/// Handles <see cref="SimilarExampleCommand"/> — orchestrates the AI Helper "Similar Example" intent
/// (AI Helper intent #4 — "اديني مثال مشابه"). Part B MVP slice (P3-06-BE-10).
///
/// <para><strong>Safety invariant (AC-9, FR-AI-4):</strong> this handler calls
/// <see cref="ISafetyLayer.GenerateSafeAsync"/> — NEVER <c>IAiGateway</c> directly.
/// The architecture test (P302-ARCH-04) enforces this at build time.</para>
///
/// <para><strong>Buffer → Safety → Emit pattern (OQ-2a):</strong> the full response is
/// buffered inside <see cref="ISafetyLayer"/>, safety-screened, then returned.
/// The SSE controller emits only the approved buffer. No unscreened token ever reaches
/// the student — lead-approved (SSE rule-8 exception).</para>
///
/// <para><strong>No progression side effects (FR-AI-6):</strong> this handler writes
/// no mastery, XP, or unlock state — it generates content only.</para>
///
/// Orchestration steps (P3-06-BE-10, MVP — no cache; steps (1)(2)(3)(5)(6)(7)(8)(10)(11)):
/// <list type="number">
///   <item>Resolve student id + rate-limit check (BE-11 abuse guard).</item>
///   <item>Emit <c>HelpRequested { Intent=SimilarExample }</c> fire-and-forget via fresh scope.</item>
///   <item>Resolve grade/age/language from JWT claims via <c>ICurrentUserService</c>.</item>
///   <item>[DEFERRED (BE-13): Practice-pool cache lookup/write]</item>
///   <item>Fetch grounding context via <c>ILearningContextProvider.GetContextAsync(studentId, skillId, questionId?, wrongAnswer:null, ct)</c>.</item>
///   <item>Empty <c>Chunks</c> → refuse-and-redirect + emit <c>HelpDeclined { Reason=NoContext }</c> via fresh scope. No LLM call (AC-7).</item>
///   <item>Build prompt via <c>IPromptBuilder</c> with <c>HelperIntent.SimilarExample</c>.</item>
///   <item>Call <c>ISafetyLayer.GenerateSafeAsync</c> (buffers + screens; NEVER IAiGateway).</item>
///   <item>Blocked → return <see cref="SimilarExampleResult.Error"/>.</item>
///   <item>Allowed → return <see cref="SimilarExampleResult.Streamed"/>.</item>
///   <item>Emit <c>HelpDelivered { Intent=SimilarExample, ContextSource }</c> fire-and-forget via fresh scope.</item>
/// </list>
/// </summary>
public sealed class SimilarExampleCommandHandler : ICommandHandler<SimilarExampleCommand, SimilarExampleResult>
{
    // Claim types used to resolve grade/age/language from the student JWT.
    private const string GradeClaim = "Grade";
    private const string AgeClaim = "Age";
    private const string LanguageClaim = "Language";
    // Model used for runtime SimilarExample calls (Sonnet — NOT Opus, runtime path).
    private const string SimilarExampleModelId = "claude-sonnet-4-6";
    // ContextSource label for the HelpDelivered event on a live generation.
    private const string ContextSourceLive = "SeededCorpus";

    private readonly ICurrentUserService _currentUser;
    private readonly ILearningContextProvider _learningContext;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ISafetyLayer _safetyLayer;
    private readonly RedirectResponseBuilder _redirectBuilder;
    private readonly AiTutorRateLimiter _rateLimiter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SimilarExampleCommandHandler(
        ICurrentUserService currentUser,
        ILearningContextProvider learningContext,
        IPromptBuilder promptBuilder,
        ISafetyLayer safetyLayer,
        RedirectResponseBuilder redirectBuilder,
        AiTutorRateLimiter rateLimiter,
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser = currentUser;
        _learningContext = learningContext;
        _promptBuilder = promptBuilder;
        _safetyLayer = safetyLayer;
        _redirectBuilder = redirectBuilder;
        _rateLimiter = rateLimiter;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<SimilarExampleResult> Handle(
        SimilarExampleCommand request,
        CancellationToken cancellationToken)
    {
        // Step 1 — resolve student id from JWT.
        var studentId = _currentUser.UserId;
        if (studentId is null)
        {
            _logger.LogWarn("SimilarExampleCommandHandler: authenticated student id is null — rejecting.");
            return new SimilarExampleResult.Error(
                nameof(SharedResourcesKey.SimilarExampleMissingProfile),
                _localizer[SharedResourcesKey.SimilarExampleMissingProfile]);
        }

        // Step 1b — per-student rate limit check (BE-11 cost/abuse guard).
        if (!_rateLimiter.TryAllow(studentId.Value))
        {
            _logger.LogWarn($"SimilarExampleCommandHandler: rate limit exceeded for studentId={studentId}.");
            return new SimilarExampleResult.Error(
                nameof(SharedResourcesKey.SimilarExampleRateLimitExceeded),
                _localizer[SharedResourcesKey.SimilarExampleRateLimitExceeded]);
        }

        // Step 2 — emit HelpRequested fire-and-forget (instrumentation, AC-8).
        // Fresh scope: the handler may be Scoped; publishing into a disposed request scope
        // would cause ObjectDisposedException in any Scoped notification handler
        // (mirrors the P3-05 fix so it doesn't use the disposed request scope).
        var helpRequestedEvent = new HelpRequestedIntegrationEvent(
            studentId.Value,
            HelperIntent.SimilarExample,
            request.SkillId,
            request.QuestionId);
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var publisher = scope.ServiceProvider.GetRequiredService<MediatR.IPublisher>();
                await publisher.Publish(helpRequestedEvent, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SimilarExampleCommandHandler: HelpRequested fire-and-forget failed.");
            }
        });

        // Step 3 — resolve grade/age/language from JWT claims.
        TryResolveProfile(out var grade, out var age, out var language);

        // ── DEFERRED (BE-13): Practice-pool cache lookup/write ─────────────────────────────
        // Before calling the gateway, this is where:
        //   (1) A random VariationIndex in [0, practicePoolSize-1] would be selected.
        //   (2) CacheKey = SHA256(SkillKey, VariationIndex, Language, PromptVersion, CurriculumVersion)
        //   (3) IAiResponseCacheRepository.GetApprovedAsync(cacheKey, Type=Practice) would be called.
        //   On hit (ReviewStatus=Approved, InvalidatedAt=null): return Streamed(cachedContent),
        //   emit HelpDelivered{ContextSource="Cache"} and skip the safety call (zero LLM cost).
        //   On miss: proceed to context fetch + safety call below.
        //   After safety pass: compute Confidence; store in AiResponseCache (Type=Practice,
        //   SkillKey + VariationIndex, QuestionId=null) with ReviewStatus = Approved or PendingReview.
        //   Both practicePoolSize and autoApprovalThreshold are read via IGlobalSettingsProvider (injected)
        //   at call time; 5 and 0.85 are the bootstrap defaults.
        //   This path requires IAiResponseCacheRepository (P3-04-BE-8) and IGlobalSettingsProvider (P10-12).
        // ─────────────────────────────────────────────────────────────────────────────────────

        // Step 5 — fetch grounding context via ILearningContextProvider.
        LearningContext learningCtx;
        try
        {
            learningCtx = await _learningContext.GetContextAsync(
                studentId.Value,
                request.SkillId,
                request.QuestionId,
                wrongAnswer: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"SimilarExampleCommandHandler: ILearningContextProvider failed for studentId={studentId}, skillId={request.SkillId}.");
            // Treat retrieval failure as no-context → redirect.
            learningCtx = new LearningContext(
                Chunks: Array.Empty<ChunkDto>(),
                QuestionText: null,
                WrongAnswer: null,
                SkillId: request.SkillId,
                QuestionId: request.QuestionId,
                GradeId: grade,
                SubjectId: 0,
                Language: language);
        }

        // Step 6 — AC-7 scope guard: empty chunks → refuse-and-redirect. Do NOT call the LLM.
        if (learningCtx.Chunks.Count == 0)
        {
            _logger.LogInfo(
                $"SimilarExampleCommandHandler: no context for studentId={studentId}, skillId={request.SkillId} — refusing and redirecting.");

            // Emit HelpDeclined fire-and-forget (AC-8 instrumentation).
            // Fresh scope: ensures notification handlers with Scoped dependencies run with a live DI scope.
            var helpDeclinedEvent = new HelpDeclinedIntegrationEvent(
                studentId.Value,
                HelperIntent.SimilarExample,
                request.SkillId,
                Reason: "NoContext");
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<MediatR.IPublisher>();
                    await publisher.Publish(helpDeclinedEvent, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SimilarExampleCommandHandler: HelpDeclined fire-and-forget failed.");
                }
            });

            var redirectText = _redirectBuilder.Build(skillName: null, language);
            return new SimilarExampleResult.Redirect(redirectText, TargetSkillId: request.SkillId);
        }

        // Step 7 — build prompt via IPromptBuilder with HelperIntent.SimilarExample.
        var subject = (Subject)learningCtx.SubjectId;
        var gradeResolved = learningCtx.GradeId > 0 ? learningCtx.GradeId : grade;

        var promptContext = new PromptContext(
            StudentId: studentId.Value,
            Intent: HelperIntent.SimilarExample,
            Subject: subject,
            Grade: gradeResolved,
            Age: age,
            Language: language,
            WeakAreas: null,   // No weak-areas query at this MVP slice.
            Context: learningCtx);

        var promptResult = _promptBuilder.Build(promptContext);

        if (promptResult is PromptBuilderResult.UnsupportedSubjectResult unsupported)
        {
            _logger.LogWarn(
                $"SimilarExampleCommandHandler: unsupported subject {unsupported.Subject} for studentId={studentId}.");
            return new SimilarExampleResult.Error(
                nameof(SharedResourcesKey.SimilarExampleUnsupportedSubject),
                _localizer[SharedResourcesKey.SimilarExampleUnsupportedSubject]);
        }

        var aiRequest = ((PromptBuilderResult.Success)promptResult).Request;

        // Step 8 — call ISafetyLayer (buffers + screens; NEVER IAiGateway directly — arch test enforced).
        SafeAiResult safeResult;
        try
        {
            safeResult = await _safetyLayer.GenerateSafeAsync(aiRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"SimilarExampleCommandHandler: ISafetyLayer threw for studentId={studentId}. Returning typed error.");
            return new SimilarExampleResult.Error(
                nameof(SharedResourcesKey.AiServiceUnavailable),
                _localizer[SharedResourcesKey.AiServiceUnavailable]);
        }

        // Step 9 — safety verdict.
        if (!safeResult.Allowed)
        {
            _logger.LogWarn(
                $"SimilarExampleCommandHandler: safety blocked for studentId={studentId}, verdict={safeResult.Verdict}.");
            return new SimilarExampleResult.Error(
                nameof(SharedResourcesKey.SimilarExampleSafetyBlocked),
                _localizer[SharedResourcesKey.SimilarExampleSafetyBlocked]);
        }

        // Step 11 — emit HelpDelivered fire-and-forget (AC-8 instrumentation).
        // Fresh scope: ensures notification handlers with Scoped dependencies run with a live DI scope.
        var helpDeliveredEvent = new HelpDeliveredIntegrationEvent(
            studentId.Value,
            HelperIntent.SimilarExample,
            request.SkillId,
            request.QuestionId,
            ModelUsed: SimilarExampleModelId,
            ContextSource: ContextSourceLive);
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var publisher = scope.ServiceProvider.GetRequiredService<MediatR.IPublisher>();
                await publisher.Publish(helpDeliveredEvent, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SimilarExampleCommandHandler: HelpDelivered fire-and-forget failed.");
            }
        });

        return new SimilarExampleResult.Streamed(safeResult.Content ?? string.Empty);
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
