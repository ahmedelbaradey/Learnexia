using Learnexia.Modules.Ai.Application.Cache;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Settings;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Application.Features.SimilarExample.Commands;

/// <summary>
/// Handles <see cref="SimilarExampleCommand"/> — orchestrates the AI Helper "Similar Example" intent.
///
/// <para><strong>Safety invariant (AC-9, FR-AI-4):</strong> this handler calls
/// <see cref="ISafetyLayer.GenerateSafeAsync"/> — NEVER <c>IAiGateway</c> directly.
/// The architecture test (P302-ARCH-04) enforces this at build time.</para>
///
/// <para><strong>Cache-first (WI-B4, Practice pool):</strong> selects a random VariationIndex
/// in [0, practicePoolSize-1] (from <see cref="IGlobalSettingsProvider"/>), computes the
/// Practice cache key, and checks <see cref="IAiResponseCache"/>. HIT → returns cached
/// content with zero gateway calls (AC-B1). MISS → runs normal path + writes to cache.</para>
/// </summary>
public sealed class SimilarExampleCommandHandler : ICommandHandler<SimilarExampleCommand, SimilarExampleResult>
{
    private const string GradeClaim    = "Grade";
    private const string AgeClaim      = "Age";
    private const string LanguageClaim = "Language";
    private const string SimilarExampleModelId = "claude-sonnet-4-6";
    private const string ContextSourceLive  = "Live";
    private const string ContextSourceCache = "Cache";
    private const string PromptVersionV1      = "v1";
    private const string CurriculumVersionMvp = "mvp";

    private readonly ICurrentUserService _currentUser;
    private readonly ILearningContextProvider _learningContext;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ISafetyLayer _safetyLayer;
    private readonly IAiResponseCache _aiCache;
    private readonly IGlobalSettingsProvider _settings;
    private readonly RedirectResponseBuilder _redirectBuilder;
    private readonly IAiTutorRateLimiter _rateLimiter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SimilarExampleCommandHandler(
        ICurrentUserService currentUser,
        ILearningContextProvider learningContext,
        IPromptBuilder promptBuilder,
        ISafetyLayer safetyLayer,
        IAiResponseCache aiCache,
        IGlobalSettingsProvider settings,
        RedirectResponseBuilder redirectBuilder,
        IAiTutorRateLimiter rateLimiter,
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser     = currentUser;
        _learningContext = learningContext;
        _promptBuilder   = promptBuilder;
        _safetyLayer     = safetyLayer;
        _aiCache         = aiCache;
        _settings        = settings;
        _redirectBuilder = redirectBuilder;
        _rateLimiter     = rateLimiter;
        _scopeFactory    = scopeFactory;
        _logger          = logger;
        _localizer       = localizer;
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

        // Step 2 — emit HelpRequested fire-and-forget.
        var helpRequestedEvent = new HelpRequestedIntegrationEvent(
            studentId.Value, HelperIntent.SimilarExample, request.SkillId, request.QuestionId);
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

        // Step 5 — fetch grounding context via ILearningContextProvider.
        // NOTE: context is fetched BEFORE the cache lookup so that subjectId is available
        // for the cache key (security fix — Finding #1: subjectId must be in the key to
        // prevent cross-subject cache collisions when skill IDs are not globally unique across subjects).
        LearningContext learningCtx;
        try
        {
            learningCtx = await _learningContext.GetContextAsync(
                studentId.Value, request.SkillId, request.QuestionId, wrongAnswer: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"SimilarExampleCommandHandler: ILearningContextProvider failed for studentId={studentId}, skillId={request.SkillId}.");
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

        // Step 6 — AC-7 scope guard: empty chunks → refuse-and-redirect.
        if (learningCtx.Chunks.Count == 0)
        {
            _logger.LogInfo(
                $"SimilarExampleCommandHandler: no context for studentId={studentId}, skillId={request.SkillId} — refusing and redirecting.");

            var helpDeclinedEvent = new HelpDeclinedIntegrationEvent(
                studentId.Value, HelperIntent.SimilarExample, request.SkillId, Reason: "NoContext");
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

        // ── WI-B4: Practice pool cache lookup (AC-B1) ───────────────────────────
        // Select a random VariationIndex in [0, practicePoolSize-1] to rotate the pool.
        // Security: subjectId (from learningCtx) and jwtGrade (from JWT claim, server-trusted)
        // are included in the key to prevent cross-subject and cross-cohort cache collisions.
        var practicePoolSize   = _settings.GetInt("ai.cache.practicePoolSize", 5);
        var variationIndex     = practicePoolSize > 0
            ? Random.Shared.Next(0, practicePoolSize)
            : 0;
        var skillKey = request.SkillId.ToString();
        var cacheKey = AiCacheKeyBuilder.ForPractice(
            subjectId:         learningCtx.SubjectId,
            skillKey:          skillKey,
            variationIndex:    variationIndex,
            jwtGrade:          grade,      // JWT-claim grade — server-trusted, not spoofable.
            language:          language,
            promptVersion:     PromptVersionV1,
            curriculumVersion: CurriculumVersionMvp);

        var cachedContent = await _aiCache.GetApprovedAsync(cacheKey, cancellationToken);
        if (cachedContent is not null)
        {
            _logger.LogInfo($"SimilarExampleCommandHandler: cache HIT (vi={variationIndex}) for studentId={studentId}, key={cacheKey[..8]}…");

            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<MediatR.IPublisher>();
                    await publisher.Publish(
                        new HelpDeliveredIntegrationEvent(
                            studentId.Value, HelperIntent.SimilarExample, request.SkillId, request.QuestionId,
                            SimilarExampleModelId, ContextSourceCache),
                        CancellationToken.None);
                }
                catch (Exception ex) { _logger.LogError(ex, "SimilarExampleCommandHandler: HelpDelivered (cache) fire-and-forget failed."); }
            });

            return new SimilarExampleResult.Streamed(cachedContent);
        }
        // ─────────────────────────────────────────────────────────────────────────

        // Step 7 — build prompt via IPromptBuilder.
        var subject      = (Subject)learningCtx.SubjectId;
        var gradeResolved = learningCtx.GradeId > 0 ? learningCtx.GradeId : grade;

        var promptContext = new PromptContext(
            StudentId: studentId.Value,
            Intent: HelperIntent.SimilarExample,
            Subject: subject,
            Grade: gradeResolved,
            Age: age,
            Language: language,
            WeakAreas: null,
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

        // Step 8 — call ISafetyLayer.
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
            // Safety-FAILED: DO NOT cache.
            return new SimilarExampleResult.Error(
                nameof(SharedResourcesKey.SimilarExampleSafetyBlocked),
                _localizer[SharedResourcesKey.SimilarExampleSafetyBlocked]);
        }

        // ── WI-B4: cache-write (fire-and-forget) ────────────────────────────────
        var approvalThreshold = _settings.GetDecimal("ai.cache.autoApprovalConfidence", 0.85m);
        var reviewStatus      = (safeResult.Confidence >= approvalThreshold)
            ? AiCacheReviewStatusDto.Approved
            : AiCacheReviewStatusDto.PendingReview;

        var writeEntry = new AiCacheWriteEntry(
            CacheKey:          cacheKey,
            Response:          safeResult.Content ?? string.Empty,
            Type:              AiCacheEntryTypeDto.Practice,
            SkillKey:          skillKey,
            QuestionId:        null,   // Decision 4: null for Practice type.
            CurriculumVersion: CurriculumVersionMvp,
            PromptVersion:     PromptVersionV1,
            ModelVersion:      SimilarExampleModelId,
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
                _logger.LogWarn($"SimilarExampleCommandHandler: cache write fire-and-forget failed. {ex.GetType().Name}");
            }
        });
        // ─────────────────────────────────────────────────────────────────────────

        // Step 11 — emit HelpDelivered fire-and-forget.
        var helpDeliveredEvent = new HelpDeliveredIntegrationEvent(
            studentId.Value, HelperIntent.SimilarExample, request.SkillId, request.QuestionId,
            SimilarExampleModelId, ContextSourceLive);
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
