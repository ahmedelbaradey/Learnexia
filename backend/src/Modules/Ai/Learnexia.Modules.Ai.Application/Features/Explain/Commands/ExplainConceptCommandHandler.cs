using Learnexia.Modules.Ai.Application.Cache;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Settings;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
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
/// <para><strong>Cache-first path (WI-B4, AC-B1):</strong> before calling the Safety Layer,
/// the handler computes the canonical cache key and checks <see cref="IAiResponseCache"/>.
/// On a HIT (Approved + non-invalidated), returns cached content IMMEDIATELY — zero
/// <c>IAiGateway</c> calls. On a MISS, runs the normal buffer→safety→emit path and
/// fire-and-forgets the write to cache on safety PASS.</para>
///
/// <para><strong>No progression side effects (AC-5, FR-AI-6):</strong> this handler
/// writes no mastery, XP, or unlock state — it generates content only.</para>
///
/// <para><strong>Charge-on-delivery (P10-03):</strong> energy credits are debited ONLY
/// on successful delivery — both the cache-HIT early-return and the post-safety-success
/// path debit via <see cref="ICreditSpendService"/>. No charge on refuse/safety-block/error/
/// pre-auth insufficient. Monthly hard-exhausted and (when HardStopEnabled) daily-cap-reached
/// trigger a graceful decline without a gateway call.</para>
/// </summary>
public sealed class ExplainConceptCommandHandler : ICommandHandler<ExplainConceptCommand, ExplainResult>
{
    // Claim types used to resolve grade/age/language from the student JWT.
    private const string GradeClaim    = "Grade";
    private const string AgeClaim      = "Age";
    private const string LanguageClaim = "Language";
    // Model used for runtime Explain calls (Sonnet — NOT Opus).
    private const string ExplainModelId = "claude-sonnet-4-6";
    // ContextSource labels for HelpDelivered events.
    private const string ContextSourceLive  = "Live";
    private const string ContextSourceCache = "Cache";
    // Fixed prompt/curriculum version tokens for the MVP slice (P10-12 will inject via IGlobalSettingsProvider).
    private const string PromptVersionV1      = "v1";
    private const string CurriculumVersionMvp = "mvp";

    private readonly ICurrentUserService _currentUser;
    private readonly ILessonContextContract _lessonContext;
    private readonly ILearningContextProvider _learningContext;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ISafetyLayer _safetyLayer;
    private readonly IAiResponseCache _aiCache;
    private readonly IGlobalSettingsProvider _settings;
    private readonly RedirectResponseBuilder _redirectBuilder;
    private readonly IAiTutorRateLimiter _rateLimiter;
    private readonly ICreditSpendService _creditSpend;
    private readonly IChildAccessStateQuery _childAccessState;
    private readonly CreditCostResolver _costResolver;
    private readonly IPublisher _publisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ExplainConceptCommandHandler(
        ICurrentUserService currentUser,
        ILessonContextContract lessonContext,
        ILearningContextProvider learningContext,
        IPromptBuilder promptBuilder,
        ISafetyLayer safetyLayer,
        IAiResponseCache aiCache,
        IGlobalSettingsProvider settings,
        RedirectResponseBuilder redirectBuilder,
        IAiTutorRateLimiter rateLimiter,
        ICreditSpendService creditSpend,
        IChildAccessStateQuery childAccessState,
        CreditCostResolver costResolver,
        IPublisher publisher,
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser      = currentUser;
        _lessonContext    = lessonContext;
        _learningContext  = learningContext;
        _promptBuilder    = promptBuilder;
        _safetyLayer      = safetyLayer;
        _aiCache          = aiCache;
        _settings         = settings;
        _redirectBuilder  = redirectBuilder;
        _rateLimiter      = rateLimiter;
        _creditSpend      = creditSpend;
        _childAccessState = childAccessState;
        _costResolver     = costResolver;
        _publisher        = publisher;
        _scopeFactory     = scopeFactory;
        _logger           = logger;
        _localizer        = localizer;
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
        var skillId    = request.SkillId ?? 0;
        var questionId = request.ConceptId;

        // Step 1c — per-student rate limit check (BE-5 cost/abuse guard).
        if (!_rateLimiter.TryAllow(studentId.Value))
        {
            _logger.LogWarn($"ExplainConceptCommandHandler: rate limit exceeded for studentId={studentId}.");
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.ExplainConceptRateLimitExceeded),
                _localizer[SharedResourcesKey.ExplainConceptRateLimitExceeded]);
        }

        // ── P10-03: resolve cost + generate per-request idempotency key ──────────────
        // childId ALWAYS from JWT — never from request body (security-critical, P10-03-BE-1).
        var childId = studentId.Value;
        var cost = _costResolver.ResolveCost(HelperIntent.Explain);
        // One GUID per HTTP request — stored here so retries reuse the same key (no double-charge).
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var reasonCode = CreditCostResolver.ResolveReasonCode(HelperIntent.Explain);

        // ── P10-18/P10-15 ACCESS GATE: check pause + seat state BEFORE cache or LLM ─
        // Must run before BOTH the cache-HIT early-return and the cache-MISS LLM path.
        // Fail-soft: a billing-service outage must never hard-block learning — proceed on exception.
        try
        {
            var accessDecision = await _childAccessState.GetAccessDecisionAsync(childId, cancellationToken);
            if (accessDecision == ChildAccessDecision.Paused)
            {
                _logger.LogInfo($"ExplainConceptCommandHandler: child {childId} is paused by parent — declining (no charge, no content).");
                return new ExplainResult.Error(
                    nameof(SharedResourcesKey.ChildAccessPausedByParent),
                    _localizer[SharedResourcesKey.ChildAccessPausedByParent]);
            }
            if (accessDecision == ChildAccessDecision.SeatLocked)
            {
                _logger.LogInfo($"ExplainConceptCommandHandler: child {childId} seat is locked — declining (no charge, no content).");
                return new ExplainResult.Error(
                    nameof(SharedResourcesKey.ChildSeatLockedNoEnergy),
                    _localizer[SharedResourcesKey.ChildSeatLockedNoEnergy]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarn($"ExplainConceptCommandHandler: GetAccessDecisionAsync failed for childId={childId}. Proceeding (fail-soft). {ex.GetType().Name}");
        }
        // ─────────────────────────────────────────────────────────────────────────────

        // ── P10-03-BE-2: pre-authorize (monthly hard-limit + optional daily hard-stop) ──
        EnergyBalance balance;
        try
        {
            balance = await _creditSpend.GetBalanceAsync(childId, cancellationToken);
        }
        catch (Exception ex)
        {
            // Fail-soft: if billing is unavailable, allow the request (never block learning).
            _logger.LogWarn($"ExplainConceptCommandHandler: GetBalanceAsync failed for childId={childId}. Proceeding. {ex.GetType().Name}");
            balance = new EnergyBalance(int.MaxValue, 0, int.MaxValue, null);
        }

        if (balance.TotalBalance < cost)
        {
            _logger.LogInfo($"ExplainConceptCommandHandler: insufficient energy for childId={childId}, balance={balance.TotalBalance}, cost={cost}.");
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.AiInsufficientEnergy),
                _localizer[SharedResourcesKey.AiInsufficientEnergy]);
        }

        if (_costResolver.IsHardStopEnabled && balance.DailyCapReached)
        {
            _logger.LogInfo($"ExplainConceptCommandHandler: daily cap reached for childId={childId}, dailyUsed={balance.DailyUsed}, cap={balance.DailyCap}. HardStop=true.");
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.AiDailyCapReached),
                _localizer[SharedResourcesKey.AiDailyCapReached]);
        }
        // ─────────────────────────────────────────────────────────────────────────────────

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
                _logger.LogError(ex, $"ExplainConceptCommandHandler: ILessonContextContract failed for lessonId={request.LessonId}. Proceeding without lesson context.");
            }
        }

        var subjectId    = lesson?.SubjectId ?? 0;
        var gradeResolved = lesson?.GradeId > 0 ? lesson.GradeId : grade;
        var skillName    = lesson?.Title;

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
        // NO debit on refuse-and-redirect.
        if (learningCtx.Chunks.Count == 0)
        {
            _logger.LogInfo($"ExplainConceptCommandHandler: no context for studentId={studentId}, skillId={skillId} — refusing and redirecting.");

            _ = _publisher.Publish(
                new HelpDeclinedIntegrationEvent(studentId.Value, HelperIntent.Explain, skillId, Reason: "NoContext"),
                CancellationToken.None);

            var redirectText = _redirectBuilder.Build(skillName, language);
            return new ExplainResult.Redirect(redirectText, TargetSkillId: request.SkillId);
        }

        // Step 7 — build prompt via IPromptBuilder.
        var subject = (Subject)subjectId;

        var promptContext = new PromptContext(
            StudentId: studentId.Value,
            Intent: HelperIntent.Explain,
            Subject: subject,
            Grade: gradeResolved,
            Age: age,
            Language: language,
            WeakAreas: null,
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

        // ── WI-B4: cache-first lookup (P3-04-BE-9, AC-B1) ───────────────────────────
        // Compute canonical cache key for Explain:
        //   (SubjectId, ConceptId, AgeBand(jwtGrade), Language, Difficulty=0, PromptVersion, CurriculumVersion).
        // Security: jwtGrade (from JWT claim, server-trusted) is used for the key's age dimension
        // rather than gradeResolved (which can be the spoofable lesson/context grade).
        // gradeResolved is still passed to PromptContext for prompt grounding.
        var cacheKey = AiCacheKeyBuilder.ForExplain(
            subjectId:         subjectId,
            conceptId:         questionId ?? 0,
            jwtGrade:          grade,      // JWT-claim grade — server-trusted, not spoofable.
            language:          language,
            difficulty:        0,          // Difficulty dimension — MVP uses 0 (not yet exposed).
            promptVersion:     PromptVersionV1,
            curriculumVersion: CurriculumVersionMvp);

        var cachedContent = await _aiCache.GetApprovedAsync(cacheKey, cancellationToken);
        if (cachedContent is not null)
        {
            _logger.LogInfo($"ExplainConceptCommandHandler: cache HIT for studentId={studentId}, key={cacheKey[..8]}… — returning cached result (zero gateway calls).");

            // ── P10-03-BE-3 DELIVERY POINT #1: cache HIT → debit ────────────────────
            // Cache hit IS a delivery — energy charged same as live (locked economy, brief §0/D7).
            await DebitEnergyAsync(childId, cost, reasonCode, idempotencyKey, cancellationToken);
            // ─────────────────────────────────────────────────────────────────────────

            // Cache HIT — emit HelpDelivered with ContextSource="Cache" and return immediately.
            _ = _publisher.Publish(
                new HelpDeliveredIntegrationEvent(
                    studentId.Value, HelperIntent.Explain, skillId, questionId,
                    ModelUsed: ExplainModelId, ContextSource: ContextSourceCache),
                CancellationToken.None);

            return new ExplainResult.Streamed(cachedContent);
        }
        // ─────────────────────────────────────────────────────────────────────────────

        // Step 8 — cache MISS: call ISafetyLayer (buffers + screens; NEVER IAiGateway directly — arch test enforced).
        SafeAiResult safeResult;
        try
        {
            safeResult = await _safetyLayer.GenerateSafeAsync(aiRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ExplainConceptCommandHandler: ISafetyLayer threw for studentId={studentId}. Returning typed error.");
            // NO debit on generation error.
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.AiServiceUnavailable),
                _localizer[SharedResourcesKey.AiServiceUnavailable]);
        }

        // Step 9 — safety verdict.
        if (!safeResult.Allowed)
        {
            _logger.LogWarn($"ExplainConceptCommandHandler: safety blocked for studentId={studentId}, verdict={safeResult.Verdict}.");
            // Safety-FAILED: DO NOT cache. NO debit.
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.ExplainConceptSafetyBlocked),
                _localizer[SharedResourcesKey.ExplainConceptSafetyBlocked]);
        }

        // ── WI-B4: cache-write of approved response (fire-and-forget) ───────────
        // Only safety-PASSED responses are cached. R5 gate: auto-approve if Confidence ≥ threshold.
        var approvalThreshold = _settings.GetDecimal("ai.cache.autoApprovalConfidence", 0.85m);
        var reviewStatus      = (safeResult.Confidence >= approvalThreshold)
            ? AiCacheReviewStatusDto.Approved
            : AiCacheReviewStatusDto.PendingReview;

        var writeEntry = new AiCacheWriteEntry(
            CacheKey:          cacheKey,
            Response:          safeResult.Content ?? string.Empty,
            Type:              AiCacheEntryTypeDto.Explain,
            SkillKey:          skillId.ToString(),
            QuestionId:        null,   // Decision 4: null for Explain type.
            CurriculumVersion: CurriculumVersionMvp,
            PromptVersion:     PromptVersionV1,
            ModelVersion:      ExplainModelId,
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
                _logger.LogWarn($"ExplainConceptCommandHandler: cache write fire-and-forget failed. {ex.GetType().Name}");
            }
        });
        // ─────────────────────────────────────────────────────────────────────────────

        // ── P10-03-BE-3 DELIVERY POINT #2: post-safety success → debit ──────────────
        // Debit is the FINAL step of a successful delivery. Cache-write is fire-and-forget above.
        // Debit returns InsufficientBalance on a concurrent-drain race — graceful decline (OQ-AI-3).
        var debitResult = await DebitEnergyAsync(childId, cost, reasonCode, idempotencyKey, cancellationToken);
        if (debitResult is { Charged: false, Outcome: DebitOutcome.InsufficientBalance })
        {
            _logger.LogInfo($"ExplainConceptCommandHandler: concurrent balance drain for childId={childId} — graceful decline post-safety.");
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.AiInsufficientEnergy),
                _localizer[SharedResourcesKey.AiInsufficientEnergy]);
        }
        // Defense-in-depth: access state changed between the pre-flight check and debit.
        if (debitResult is { Charged: false, Outcome: DebitOutcome.ParentPaused })
        {
            _logger.LogInfo($"ExplainConceptCommandHandler: ParentPaused at debit for childId={childId} — graceful decline post-safety.");
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.ChildAccessPausedByParent),
                _localizer[SharedResourcesKey.ChildAccessPausedByParent]);
        }
        if (debitResult is { Charged: false, Outcome: DebitOutcome.SeatLocked })
        {
            _logger.LogInfo($"ExplainConceptCommandHandler: SeatLocked at debit for childId={childId} — graceful decline post-safety.");
            return new ExplainResult.Error(
                nameof(SharedResourcesKey.ChildSeatLockedNoEnergy),
                _localizer[SharedResourcesKey.ChildSeatLockedNoEnergy]);
        }
        // ─────────────────────────────────────────────────────────────────────────────

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
    /// Calls <see cref="ICreditSpendService.TryDebitAsync"/> and returns the result.
    /// On failure (billing unavailable), logs and returns null — fail-soft so a billing
    /// outage never hard-blocks a student from getting an AI response they earned.
    /// </summary>
    private async Task<DebitResult?> DebitEnergyAsync(
        int childId, int cost, string reasonCode, string idempotencyKey, CancellationToken ct)
    {
        try
        {
            var result = await _creditSpend.TryDebitAsync(childId, cost, reasonCode, idempotencyKey, ct);
            if (result.Outcome == DebitOutcome.DuplicateIdempotent)
                _logger.LogInfo($"ExplainConceptCommandHandler: idempotent debit (retry) for childId={childId}, key={idempotencyKey[..8]}…");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarn($"ExplainConceptCommandHandler: TryDebitAsync failed for childId={childId}. Fail-soft. {ex.GetType().Name}");
            return null;
        }
    }

    private bool TryResolveProfile(out int grade, out int age, out TutorLanguage language)
    {
        grade    = 0;
        age      = 0;
        language = TutorLanguage.Ar;

        var gradeStr    = _currentUser.GetClaimValue(GradeClaim);
        var ageStr      = _currentUser.GetClaimValue(AgeClaim);
        var languageStr = _currentUser.GetClaimValue(LanguageClaim);

        if (!int.TryParse(gradeStr, out grade) || grade <= 0)
            grade = 4;

        if (!int.TryParse(ageStr, out age) || age <= 0)
            age = 10;

        if (string.Equals(languageStr, "en", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(languageStr, "en-US", StringComparison.OrdinalIgnoreCase))
            language = TutorLanguage.En;
        else
            language = TutorLanguage.Ar;

        return true;
    }
}
