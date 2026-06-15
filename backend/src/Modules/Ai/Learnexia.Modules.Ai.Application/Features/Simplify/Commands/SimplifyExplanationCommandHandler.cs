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

namespace Learnexia.Modules.Ai.Application.Features.Simplify.Commands;

/// <summary>
/// Handles <see cref="SimplifyExplanationCommand"/> — orchestrates the AI Helper "Simplify" intent.
///
/// <para><strong>Safety invariant (AC-6, FR-AI-4):</strong> this handler calls
/// <see cref="ISafetyLayer.GenerateSafeAsync"/> — NEVER <c>IAiGateway</c> directly.
/// The architecture test (P302-ARCH-04) enforces this at build time.</para>
///
/// <para><strong>Cache-first (WI-B4):</strong> Simplify is keyed identically to Explain
/// (ConceptId/LessonId, GradeId, Language, Difficulty=0, PromptVersion, CurriculumVersion).
/// On HIT → returns cached content with zero gateway calls (AC-B1).
/// On MISS → runs the normal path and writes to cache on safety-PASS.</para>
///
/// <para><strong>Charge-on-delivery (P10-03):</strong> energy credits are debited ONLY
/// on successful delivery — both the cache-HIT early-return and the post-safety-success
/// path debit via <see cref="ICreditSpendService"/>. No charge on refuse/safety-block/error/
/// pre-auth insufficient. Uses <c>ai_cost.deep_explanation</c> (same as Explain intent).</para>
/// </summary>
public sealed class SimplifyExplanationCommandHandler : ICommandHandler<SimplifyExplanationCommand, SimplifyResult>
{
    private const string GradeClaim    = "Grade";
    private const string AgeClaim      = "Age";
    private const string LanguageClaim = "Language";
    private const string SimplifyModelId = "claude-sonnet-4-6";
    private const string ContextSourceLive  = "Live";
    private const string ContextSourceCache = "Cache";
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
    private readonly CreditCostResolver _costResolver;
    private readonly IPublisher _publisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SimplifyExplanationCommandHandler(
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
        CreditCostResolver costResolver,
        IPublisher publisher,
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser     = currentUser;
        _lessonContext   = lessonContext;
        _learningContext = learningContext;
        _promptBuilder   = promptBuilder;
        _safetyLayer     = safetyLayer;
        _aiCache         = aiCache;
        _settings        = settings;
        _redirectBuilder = redirectBuilder;
        _rateLimiter     = rateLimiter;
        _creditSpend     = creditSpend;
        _costResolver    = costResolver;
        _publisher       = publisher;
        _scopeFactory    = scopeFactory;
        _logger          = logger;
        _localizer       = localizer;
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

        var lessonId = request.LessonId ?? 0;

        // Step 1c — per-student rate limit check (cost/abuse guard).
        if (!_rateLimiter.TryAllow(studentId.Value))
        {
            _logger.LogWarn($"SimplifyExplanationCommandHandler: rate limit exceeded for studentId={studentId}.");
            return new SimplifyResult.Error(
                nameof(SharedResourcesKey.SimplifyRateLimitExceeded),
                _localizer[SharedResourcesKey.SimplifyRateLimitExceeded]);
        }

        // ── P10-03: resolve cost + generate per-request idempotency key ──────────────
        // Simplify shares the deep_explanation cost with Explain (locked economy model).
        var childId = studentId.Value;
        var cost = _costResolver.ResolveCost(HelperIntent.Explain);
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var reasonCode = CreditCostResolver.ResolveReasonCode(HelperIntent.Explain);

        // ── P10-03-BE-2: pre-authorize (monthly hard-limit + optional daily hard-stop) ──
        EnergyBalance balance;
        try
        {
            balance = await _creditSpend.GetBalanceAsync(childId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarn($"SimplifyExplanationCommandHandler: GetBalanceAsync failed for childId={childId}. Proceeding. {ex.GetType().Name}");
            balance = new EnergyBalance(int.MaxValue, 0, int.MaxValue, null);
        }

        if (balance.TotalBalance < cost)
        {
            _logger.LogInfo($"SimplifyExplanationCommandHandler: insufficient energy for childId={childId}, balance={balance.TotalBalance}, cost={cost}.");
            return new SimplifyResult.Error(
                nameof(SharedResourcesKey.AiInsufficientEnergy),
                _localizer[SharedResourcesKey.AiInsufficientEnergy]);
        }

        if (_costResolver.IsHardStopEnabled && balance.DailyCapReached)
        {
            _logger.LogInfo($"SimplifyExplanationCommandHandler: daily cap reached for childId={childId}. HardStop=true.");
            return new SimplifyResult.Error(
                nameof(SharedResourcesKey.AiDailyCapReached),
                _localizer[SharedResourcesKey.AiDailyCapReached]);
        }
        // ─────────────────────────────────────────────────────────────────────────────────

        // Step 2 — emit HelpRequested fire-and-forget (before any LLM call).
        _ = _publisher.Publish(
            new HelpRequestedIntegrationEvent(studentId.Value, HelperIntent.Explain, 0, request.ConceptId),
            CancellationToken.None);

        // Step 3 — resolve grade/age/language from JWT claims.
        TryResolveProfile(out var grade, out var age, out var language);

        // Step 4 — optionally enrich with lesson context.
        LessonContextDto? lesson = null;
        if (request.LessonId.HasValue && request.LessonId > 0)
        {
            try
            {
                lesson = await _lessonContext.GetLessonContextAsync(request.LessonId.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"SimplifyExplanationCommandHandler: ILessonContextContract failed for lessonId={request.LessonId}. Proceeding without lesson context.");
            }
        }

        var subjectId    = lesson?.SubjectId ?? 0;
        var gradeResolved = lesson?.GradeId > 0 ? lesson.GradeId : grade;
        var skillName    = lesson?.Title;

        // Step 5 — fetch grounding context.
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

        // Step 6 — AC-3 scope guard: empty chunks → refuse-and-redirect. NO debit.
        if (learningCtx.Chunks.Count == 0)
        {
            _logger.LogInfo(
                $"SimplifyExplanationCommandHandler: no context for studentId={studentId}, lessonId={lessonId} — refusing and redirecting.");

            _ = _publisher.Publish(
                new HelpDeclinedIntegrationEvent(studentId.Value, HelperIntent.Explain, skillId, Reason: "NoContext"),
                CancellationToken.None);

            var redirectText = _redirectBuilder.Build(skillName, language);
            return new SimplifyResult.Redirect(redirectText, TargetSkillId: request.LessonId);
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
            _logger.LogWarn($"SimplifyExplanationCommandHandler: unsupported subject {unsupported.Subject} for studentId={studentId}.");
            return new SimplifyResult.Error(
                nameof(SharedResourcesKey.SimplifyUnsupportedSubject),
                _localizer[SharedResourcesKey.SimplifyUnsupportedSubject]);
        }

        var aiRequest = ((PromptBuilderResult.Success)promptResult).Request;

        // ── WI-B4: cache-first lookup (AC-B1) ───────────────────────────────────────
        // Simplify shares the Explain cache key space (same tuple dimensions).
        // Security: jwtGrade (from JWT claim, server-trusted) is used for the key's age dimension
        // rather than gradeResolved (which can be the spoofable lesson/context grade).
        var cacheKey = AiCacheKeyBuilder.ForExplain(
            subjectId:         subjectId,
            conceptId:         request.ConceptId ?? 0,
            jwtGrade:          grade,      // JWT-claim grade — server-trusted, not spoofable.
            language:          language,
            difficulty:        0,
            promptVersion:     PromptVersionV1,
            curriculumVersion: CurriculumVersionMvp);

        var cachedContent = await _aiCache.GetApprovedAsync(cacheKey, cancellationToken);
        if (cachedContent is not null)
        {
            _logger.LogInfo($"SimplifyExplanationCommandHandler: cache HIT for studentId={studentId}, key={cacheKey[..8]}…");

            // ── P10-03-BE-3 DELIVERY POINT #1: cache HIT → debit ────────────────────
            await DebitEnergyAsync(childId, cost, reasonCode, idempotencyKey, cancellationToken);
            // ─────────────────────────────────────────────────────────────────────────

            _ = _publisher.Publish(
                new HelpDeliveredIntegrationEvent(
                    studentId.Value, HelperIntent.Explain, skillId, request.ConceptId,
                    SimplifyModelId, ContextSourceCache),
                CancellationToken.None);

            return new SimplifyResult.Streamed(cachedContent);
        }
        // ─────────────────────────────────────────────────────────────────────────────

        // Step 8 — call ISafetyLayer.
        SafeAiResult safeResult;
        try
        {
            safeResult = await _safetyLayer.GenerateSafeAsync(aiRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"SimplifyExplanationCommandHandler: ISafetyLayer threw for studentId={studentId}. Returning typed error.");
            // NO debit on generation error.
            return new SimplifyResult.Error(
                nameof(SharedResourcesKey.AiServiceUnavailable),
                _localizer[SharedResourcesKey.AiServiceUnavailable]);
        }

        // Step 9 — safety verdict.
        if (!safeResult.Allowed)
        {
            _logger.LogWarn($"SimplifyExplanationCommandHandler: safety blocked for studentId={studentId}, verdict={safeResult.Verdict}.");
            // Safety-FAILED: DO NOT cache. NO debit.
            return new SimplifyResult.Error(
                nameof(SharedResourcesKey.SimplifySafetyBlocked),
                _localizer[SharedResourcesKey.SimplifySafetyBlocked]);
        }

        // ── WI-B4: cache-write of approved response (fire-and-forget) ───────────
        var approvalThreshold = _settings.GetDecimal("ai.cache.autoApprovalConfidence", 0.85m);
        var reviewStatus      = (safeResult.Confidence >= approvalThreshold)
            ? AiCacheReviewStatusDto.Approved
            : AiCacheReviewStatusDto.PendingReview;

        var writeEntry = new AiCacheWriteEntry(
            CacheKey:          cacheKey,
            Response:          safeResult.Content ?? string.Empty,
            Type:              AiCacheEntryTypeDto.Explain,
            SkillKey:          skillId.ToString(),
            QuestionId:        null,
            CurriculumVersion: CurriculumVersionMvp,
            PromptVersion:     PromptVersionV1,
            ModelVersion:      SimplifyModelId,
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
                _logger.LogWarn($"SimplifyExplanationCommandHandler: cache write fire-and-forget failed. {ex.GetType().Name}");
            }
        });
        // ─────────────────────────────────────────────────────────────────────────────

        // ── P10-03-BE-3 DELIVERY POINT #2: post-safety success → debit ──────────────
        var debitResult = await DebitEnergyAsync(childId, cost, reasonCode, idempotencyKey, cancellationToken);
        if (debitResult is { Charged: false, Outcome: DebitOutcome.InsufficientBalance })
        {
            _logger.LogInfo($"SimplifyExplanationCommandHandler: concurrent balance drain for childId={childId} — graceful decline post-safety.");
            return new SimplifyResult.Error(
                nameof(SharedResourcesKey.AiInsufficientEnergy),
                _localizer[SharedResourcesKey.AiInsufficientEnergy]);
        }
        // ─────────────────────────────────────────────────────────────────────────────

        // Step 10 — emit HelpDelivered fire-and-forget.
        _ = _publisher.Publish(
            new HelpDeliveredIntegrationEvent(
                studentId.Value, HelperIntent.Explain, skillId, request.ConceptId,
                SimplifyModelId, ContextSourceLive),
            CancellationToken.None);

        return new SimplifyResult.Streamed(safeResult.Content ?? string.Empty);
    }

    /// <summary>Fail-soft debit — a billing outage never hard-blocks a student.</summary>
    private async Task<DebitResult?> DebitEnergyAsync(
        int childId, int cost, string reasonCode, string idempotencyKey, CancellationToken ct)
    {
        try
        {
            var result = await _creditSpend.TryDebitAsync(childId, cost, reasonCode, idempotencyKey, ct);
            if (result.Outcome == DebitOutcome.DuplicateIdempotent)
                _logger.LogInfo($"SimplifyExplanationCommandHandler: idempotent debit (retry) for childId={childId}.");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarn($"SimplifyExplanationCommandHandler: TryDebitAsync failed for childId={childId}. Fail-soft. {ex.GetType().Name}");
            return null;
        }
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
