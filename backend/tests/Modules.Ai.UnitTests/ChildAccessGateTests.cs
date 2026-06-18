using FluentAssertions;
using Learnexia.Modules.Ai.Application.Features.Explain.Commands;
using Learnexia.Modules.Ai.Application.Features.Hint.Commands;
using Learnexia.Modules.Ai.Application.Features.SimilarExample.Commands;
using Learnexia.Modules.Ai.Application.Features.Simplify.Commands;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using Resources;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for the P10-18/P10-15 child-access gate wired into the 4 AI handlers
/// (GetHint, ExplainConcept, SimplifyExplanation, SimilarExample).
///
/// AG-01  Hint / Paused / cache-MISS  → ChildAccessPausedByParent, no content, no charge.
/// AG-02  Hint / SeatLocked / cache-MISS → ChildSeatLockedNoEnergy, no content, no charge.
/// AG-03  Hint / Paused / cache-HIT   → ChildAccessPausedByParent; cache never queried.
///         (Verifies the gate fires BEFORE the cache lookup — P10-18 pre-cache guard.)
/// AG-04  Hint / Allowed / cache-MISS → Streamed (positive regression; gate does not over-block).
/// AG-05  Explain / Paused            → ChildAccessPausedByParent, no content, no charge.
/// AG-06  Explain / SeatLocked        → ChildSeatLockedNoEnergy, no content, no charge.
/// AG-07  Simplify / Paused           → ChildAccessPausedByParent, no content, no charge.
/// AG-08  SimilarExample / Paused     → ChildAccessPausedByParent, no content, no charge.
/// AG-09  SimilarExample / SeatLocked → ChildSeatLockedNoEnergy, no content, no charge.
/// AG-10  Hint / GetAccessDecisionAsync throws → fail-soft; Streamed returned.
///         (Billing outage must never hard-block a student's learning session.)
/// </summary>
public sealed class ChildAccessGateTests
{
    // ── Shared helpers ────────────────────────────────────────────────────────────

    private static Mock<ICurrentUserService> BuildUserMock(int studentId = 42)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(studentId);
        mock.Setup(u => u.GetClaimValue("Grade")).Returns("5");
        mock.Setup(u => u.GetClaimValue("Age")).Returns("11");
        mock.Setup(u => u.GetClaimValue("Language")).Returns("ar");
        return mock;
    }

    private static Mock<IStringLocalizer<SharedResources>> BuildLocalizerMock()
    {
        var mock = new Mock<IStringLocalizer<SharedResources>>();
        mock.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(key => new LocalizedString(key, key));
        return mock;
    }

    private static Mock<IChildAccessStateQuery> BuildAccessMock(ChildAccessDecision decision)
    {
        var mock = new Mock<IChildAccessStateQuery>();
        mock.Setup(q => q.GetAccessDecisionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(decision);
        mock.Setup(q => q.IsChildAccessAllowedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(decision == ChildAccessDecision.Allowed);
        return mock;
    }

    private static Mock<ICreditSpendService> BuildCreditSpendMock()
    {
        var mock = new Mock<ICreditSpendService>();
        mock.Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null));
        mock.Setup(c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DebitResult(true, 1, 0, 99, DebitOutcome.Charged));
        return mock;
    }

    private static Mock<IAiResponseCache> BuildCacheMissMock()
    {
        var mock = new Mock<IAiResponseCache>();
        mock.Setup(c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        mock.Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static Mock<IAiResponseCache> BuildCacheHitMock(string content = "cached answer")
    {
        var mock = new Mock<IAiResponseCache>();
        mock.Setup(c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        mock.Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static Mock<IGlobalSettingsProvider> BuildSettingsMock()
    {
        var mock = new Mock<IGlobalSettingsProvider>();
        mock.Setup(s => s.GetDecimal(It.IsAny<string>(), It.IsAny<decimal>()))
            .Returns<string, decimal>((_, def) => def);
        mock.Setup(s => s.GetInt(It.IsAny<string>(), It.IsAny<int>()))
            .Returns<string, int>((_, def) => def);
        mock.Setup(s => s.GetString(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((_, def) => def);
        mock.Setup(s => s.GetBool(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns<string, bool>((_, def) => def);
        return mock;
    }

    private static CreditCostResolver BuildCostResolver()
    {
        var settings = BuildSettingsMock().Object;
        var config   = new Mock<IConfiguration>();
        config.Setup(c => c["Billing:HardStopEnabled"]).Returns("false");
        return new CreditCostResolver(settings, config.Object);
    }

    /// <summary>
    /// No-op scope factory: fire-and-forget Task.Run bodies resolve a no-op
    /// IPublisher + IAiResponseCache so they never throw during unit tests.
    /// </summary>
    private static Mock<IServiceScopeFactory> BuildNoOpScopeFactory()
    {
        var innerCache = new Mock<IAiResponseCache>();
        innerCache.Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var innerPublisher = new Mock<IPublisher>();

        var sp = new Mock<IServiceProvider>();
        sp.Setup(p => p.GetService(typeof(IAiResponseCache))).Returns(innerCache.Object);
        sp.Setup(p => p.GetService(typeof(IPublisher))).Returns(innerPublisher.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(sp.Object);

        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return factory;
    }

    private static LearningContext MakePopulatedContext() =>
        new LearningContext(
            Chunks: new[] { new ChunkDto("c1", "Math chunk.") },
            QuestionText: "What is 2+2?",
            WrongAnswer: null,
            SkillId: 10,
            QuestionId: 1,
            GradeId: 5,
            SubjectId: (int)Subject.Math,
            Language: TutorLanguage.Ar);

    private static Mock<ILearningContextProvider> BuildContextProviderMock()
    {
        var mock = new Mock<ILearningContextProvider>();
        mock.Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePopulatedContext());
        return mock;
    }

    private static Mock<IPromptBuilder> BuildPromptBuilderMock(AiTaskKind taskKind = AiTaskKind.Hint)
    {
        var mock = new Mock<IPromptBuilder>();
        mock.Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "test prompt", Task = taskKind }));
        return mock;
    }

    private static Mock<ISafetyLayer> BuildSafetyMock(string content = "safe content")
    {
        var mock = new Mock<ISafetyLayer>();
        mock.Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(true, content, SafetyVerdict.Allowed, Array.Empty<CheckResult>()));
        return mock;
    }

    // ── Handler factory: Hint ──────────────────────────────────────────────────────

    private static GetHintCommandHandler BuildHintHandler(
        Mock<IChildAccessStateQuery> childAccess,
        Mock<ICreditSpendService>? creditSpend = null,
        Mock<IAiResponseCache>? cache = null,
        Mock<ISafetyLayer>? safety = null,
        Mock<ILearningContextProvider>? contextProvider = null)
    {
        var localizer    = BuildLocalizerMock();
        var user         = BuildUserMock();
        var logger       = new Mock<ILoggerManager>();
        var settings     = BuildSettingsMock();
        var scopeFactory = BuildNoOpScopeFactory();
        var publisher    = new Mock<IPublisher>();
        var costResolver = BuildCostResolver();
        var hintOptions  = Options.Create(new HintOptions { MaxHintLevels = 3 });

        var qa = new Mock<IQuestionAnswerContract>();
        qa.Setup(q => q.GetQuestionAnswerAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuestionAnswerDto("4", 1));

        var ctx = contextProvider ?? BuildContextProviderMock();
        var pb  = BuildPromptBuilderMock(AiTaskKind.Hint);
        var sl  = safety ?? BuildSafetyMock("approved hint");

        return new GetHintCommandHandler(
            user.Object,
            qa.Object,
            ctx.Object,
            pb.Object,
            sl.Object,
            (cache ?? BuildCacheMissMock()).Object,
            settings.Object,
            new RedirectResponseBuilder(localizer.Object),
            new AiTutorRateLimiter(),
            (creditSpend ?? BuildCreditSpendMock()).Object,
            childAccess.Object,
            costResolver,
            publisher.Object,
            scopeFactory.Object,
            logger.Object,
            localizer.Object,
            hintOptions);
    }

    // ── Handler factory: Explain ──────────────────────────────────────────────────

    private static ExplainConceptCommandHandler BuildExplainHandler(
        Mock<IChildAccessStateQuery> childAccess,
        Mock<ICreditSpendService>? creditSpend = null,
        Mock<IAiResponseCache>? cache = null,
        Mock<ISafetyLayer>? safety = null)
    {
        var localizer    = BuildLocalizerMock();
        var user         = BuildUserMock();
        var logger       = new Mock<ILoggerManager>();
        var settings     = BuildSettingsMock();
        var scopeFactory = BuildNoOpScopeFactory();
        var publisher    = new Mock<IPublisher>();
        var costResolver = BuildCostResolver();

        var lessonCtx = new Mock<ILessonContextContract>();
        lessonCtx.Setup(l => l.GetLessonContextAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LessonContextDto?)null);

        var sl = safety ?? BuildSafetyMock("explanation");

        return new ExplainConceptCommandHandler(
            user.Object,
            lessonCtx.Object,
            BuildContextProviderMock().Object,
            BuildPromptBuilderMock(AiTaskKind.Explain).Object,
            sl.Object,
            (cache ?? BuildCacheMissMock()).Object,
            settings.Object,
            new RedirectResponseBuilder(localizer.Object),
            new AiTutorRateLimiter(),
            (creditSpend ?? BuildCreditSpendMock()).Object,
            childAccess.Object,
            costResolver,
            publisher.Object,
            scopeFactory.Object,
            logger.Object,
            localizer.Object);
    }

    // ── Handler factory: Simplify ─────────────────────────────────────────────────

    private static SimplifyExplanationCommandHandler BuildSimplifyHandler(
        Mock<IChildAccessStateQuery> childAccess,
        Mock<ICreditSpendService>? creditSpend = null)
    {
        var localizer    = BuildLocalizerMock();
        var user         = BuildUserMock();
        var logger       = new Mock<ILoggerManager>();
        var settings     = BuildSettingsMock();
        var scopeFactory = BuildNoOpScopeFactory();
        var publisher    = new Mock<IPublisher>();
        var costResolver = BuildCostResolver();

        var lessonCtx = new Mock<ILessonContextContract>();
        lessonCtx.Setup(l => l.GetLessonContextAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LessonContextDto?)null);

        return new SimplifyExplanationCommandHandler(
            user.Object,
            lessonCtx.Object,
            BuildContextProviderMock().Object,
            BuildPromptBuilderMock(AiTaskKind.Explain).Object,
            BuildSafetyMock("simplified").Object,
            BuildCacheMissMock().Object,
            settings.Object,
            new RedirectResponseBuilder(localizer.Object),
            new AiTutorRateLimiter(),
            (creditSpend ?? BuildCreditSpendMock()).Object,
            childAccess.Object,
            costResolver,
            publisher.Object,
            scopeFactory.Object,
            logger.Object,
            localizer.Object);
    }

    // ── Handler factory: SimilarExample ──────────────────────────────────────────

    private static SimilarExampleCommandHandler BuildSimilarExampleHandler(
        Mock<IChildAccessStateQuery> childAccess,
        Mock<ICreditSpendService>? creditSpend = null)
    {
        var localizer    = BuildLocalizerMock();
        var user         = BuildUserMock();
        var logger       = new Mock<ILoggerManager>();
        var settings     = BuildSettingsMock();
        var scopeFactory = BuildNoOpScopeFactory();
        var costResolver = BuildCostResolver();

        return new SimilarExampleCommandHandler(
            user.Object,
            BuildContextProviderMock().Object,
            BuildPromptBuilderMock(AiTaskKind.Explain).Object,
            BuildSafetyMock("similar example").Object,
            BuildCacheMissMock().Object,
            settings.Object,
            new RedirectResponseBuilder(localizer.Object),
            new AiTutorRateLimiter(),
            (creditSpend ?? BuildCreditSpendMock()).Object,
            childAccess.Object,
            costResolver,
            scopeFactory.Object,
            logger.Object,
            localizer.Object);
    }

    // ── AG-01: Hint / Paused / cache-MISS → ChildAccessPausedByParent ─────────────

    [Fact(DisplayName = "AG-01 Hint/Paused/cache-MISS → ChildAccessPausedByParent, no content, no charge, no LLM call")]
    public async Task Hint_PausedChild_CacheMiss_ReturnsAccessPausedErrorAndNeverChargesOrCallsLlm()
    {
        var childAccess = BuildAccessMock(ChildAccessDecision.Paused);
        var creditSpend = BuildCreditSpendMock();
        var safetyMock  = new Mock<ISafetyLayer>();
        var cacheMock   = BuildCacheMissMock();

        var sut    = BuildHintHandler(childAccess, creditSpend, cacheMock, safetyMock);
        var cmd    = new GetHintCommand(QuestionId: 1, AttemptId: 5, Intent: HelperIntent.Hint, HintLevel: null, WrongAnswer: null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.Should().BeOfType<HintResult.Error>("a paused child must be declined at the pre-flight access gate");
        ((HintResult.Error)result).Code.Should().Contain("Paused",
            "error code must identify the parent-pause condition (SharedResourcesKey.ChildAccessPausedByParent)");

        creditSpend.Verify(
            c => c.TryDebitAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "no energy must be charged — the access gate fires before any delivery");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never, "ISafetyLayer must not be called for a paused child");

        cacheMock.Verify(
            c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "cache must not be queried — the access gate fires before the cache lookup");
    }

    // ── AG-02: Hint / SeatLocked / cache-MISS → ChildSeatLockedNoEnergy ──────────

    [Fact(DisplayName = "AG-02 Hint/SeatLocked/cache-MISS → ChildSeatLockedNoEnergy, no content, no charge")]
    public async Task Hint_SeatLockedChild_CacheMiss_ReturnsSeatLockedErrorAndNeverCharges()
    {
        var childAccess = BuildAccessMock(ChildAccessDecision.SeatLocked);
        var creditSpend = BuildCreditSpendMock();
        var safetyMock  = new Mock<ISafetyLayer>();
        var cacheMock   = BuildCacheMissMock();

        var sut    = BuildHintHandler(childAccess, creditSpend, cacheMock, safetyMock);
        var cmd    = new GetHintCommand(QuestionId: 1, AttemptId: 5, Intent: HelperIntent.Hint, HintLevel: null, WrongAnswer: null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.Should().BeOfType<HintResult.Error>("a seat-locked child must be declined at the access gate");
        ((HintResult.Error)result).Code.Should().Contain("SeatLocked",
            "error code must identify the seat-locked condition (SharedResourcesKey.ChildSeatLockedNoEnergy)");

        creditSpend.Verify(
            c => c.TryDebitAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        cacheMock.Verify(
            c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── AG-03: Hint / Paused / cache-HIT → ChildAccessPausedByParent ─────────────
    // Critical: verifies the gate fires BEFORE the cache lookup, not just before the LLM call.

    [Fact(DisplayName = "AG-03 Hint/Paused/cache-HIT → ChildAccessPausedByParent; cache never queried (P10-18 pre-cache guard)")]
    public async Task Hint_PausedChild_CacheHit_ReturnsAccessPausedErrorAndNeverQueriesCache()
    {
        var childAccess = BuildAccessMock(ChildAccessDecision.Paused);
        var creditSpend = BuildCreditSpendMock();
        var safetyMock  = new Mock<ISafetyLayer>();
        // Cache is set up to HIT — if the gate were placed after the cache lookup, content
        // would be delivered to the paused child (the original security defect).
        var cacheMock = BuildCacheHitMock("cached hint — must NOT be delivered to a paused child");

        var sut    = BuildHintHandler(childAccess, creditSpend, cacheMock, safetyMock);
        var cmd    = new GetHintCommand(QuestionId: 1, AttemptId: 5, Intent: HelperIntent.Hint, HintLevel: null, WrongAnswer: null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.Should().BeOfType<HintResult.Error>(
            "a paused child must be declined EVEN when the cache would hit — gate must fire before the cache lookup");
        ((HintResult.Error)result).Code.Should().Contain("Paused");

        creditSpend.Verify(
            c => c.TryDebitAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "no charge — access gate fires before the cache-HIT delivery point");

        // Core assertion: the cache was never reached because the gate short-circuited first.
        cacheMock.Verify(
            c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "GetApprovedAsync must NOT be called for a paused child — " +
            "if this assertion fails, the access gate is placed AFTER the cache lookup and the paused-child-cache-HIT exploit is active");
    }

    // ── AG-04: Hint / Allowed / cache-MISS → Streamed (positive regression) ───────

    [Fact(DisplayName = "AG-04 Hint/Allowed/cache-MISS → Streamed (active funded child still receives content)")]
    public async Task Hint_AllowedChild_CacheMiss_ReturnsStreamedContent()
    {
        var childAccess = BuildAccessMock(ChildAccessDecision.Allowed);
        var safetyMock  = BuildSafetyMock("approved hint content");

        var sut    = BuildHintHandler(childAccess, safety: safetyMock);
        var cmd    = new GetHintCommand(QuestionId: 1, AttemptId: 5, Intent: HelperIntent.Hint, HintLevel: null, WrongAnswer: null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.Should().BeOfType<HintResult.Streamed>(
            "an active funded child must still receive content — the access gate must not over-block");
        ((HintResult.Streamed)result).Content.Should().Be("approved hint content");
    }

    // ── AG-05: Explain / Paused → ChildAccessPausedByParent ──────────────────────

    [Fact(DisplayName = "AG-05 Explain/Paused → ChildAccessPausedByParent, no content, no charge (P10-18 on ExplainConcept)")]
    public async Task Explain_PausedChild_ReturnsAccessPausedErrorAndNeverCharges()
    {
        var childAccess = BuildAccessMock(ChildAccessDecision.Paused);
        var creditSpend = BuildCreditSpendMock();
        var safetyMock  = new Mock<ISafetyLayer>();

        var sut    = BuildExplainHandler(childAccess, creditSpend, safety: safetyMock);
        var cmd    = new ExplainConceptCommand(LessonId: null, ConceptId: 1, SkillId: 10, Question: null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.Should().BeOfType<ExplainResult.Error>("a paused child must be declined on Explain");
        ((ExplainResult.Error)result).Code.Should().Contain("Paused");

        creditSpend.Verify(
            c => c.TryDebitAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "no charge on Explain for a paused child");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never, "ISafetyLayer must not be called for a paused child on Explain");
    }

    // ── AG-06: Explain / SeatLocked → ChildSeatLockedNoEnergy ────────────────────

    [Fact(DisplayName = "AG-06 Explain/SeatLocked → ChildSeatLockedNoEnergy, no content, no charge (P10-15 message fix on Explain)")]
    public async Task Explain_SeatLockedChild_ReturnsSeatLockedErrorAndNeverCharges()
    {
        var childAccess = BuildAccessMock(ChildAccessDecision.SeatLocked);
        var creditSpend = BuildCreditSpendMock();
        var safetyMock  = new Mock<ISafetyLayer>();

        var sut    = BuildExplainHandler(childAccess, creditSpend, safety: safetyMock);
        var cmd    = new ExplainConceptCommand(LessonId: null, ConceptId: 1, SkillId: 10, Question: null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.Should().BeOfType<ExplainResult.Error>("a seat-locked child must be declined on Explain");
        ((ExplainResult.Error)result).Code.Should().Contain("SeatLocked",
            "seat-locked child must see ChildSeatLockedNoEnergy key, not the generic insufficient-energy message");

        creditSpend.Verify(
            c => c.TryDebitAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── AG-07: Simplify / Paused → ChildAccessPausedByParent ─────────────────────

    [Fact(DisplayName = "AG-07 Simplify/Paused → ChildAccessPausedByParent, no content, no charge (P10-18 on SimplifyExplanation)")]
    public async Task Simplify_PausedChild_ReturnsAccessPausedErrorAndNeverCharges()
    {
        var childAccess = BuildAccessMock(ChildAccessDecision.Paused);
        var creditSpend = BuildCreditSpendMock();

        var sut    = BuildSimplifyHandler(childAccess, creditSpend);
        var cmd    = new SimplifyExplanationCommand(ConceptId: 1, LessonId: null, PreviousExplanationRef: null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.Should().BeOfType<SimplifyResult.Error>("a paused child must be declined on Simplify");
        ((SimplifyResult.Error)result).Code.Should().Contain("Paused");

        creditSpend.Verify(
            c => c.TryDebitAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "no charge on Simplify for a paused child");
    }

    // ── AG-08: SimilarExample / Paused → ChildAccessPausedByParent ───────────────

    [Fact(DisplayName = "AG-08 SimilarExample/Paused → ChildAccessPausedByParent, no content, no charge (P10-18 on SimilarExample)")]
    public async Task SimilarExample_PausedChild_ReturnsAccessPausedErrorAndNeverCharges()
    {
        var childAccess = BuildAccessMock(ChildAccessDecision.Paused);
        var creditSpend = BuildCreditSpendMock();

        var sut    = BuildSimilarExampleHandler(childAccess, creditSpend);
        var cmd    = new SimilarExampleCommand(SkillId: 10, QuestionId: 1);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.Should().BeOfType<SimilarExampleResult.Error>("a paused child must be declined on SimilarExample");
        ((SimilarExampleResult.Error)result).Code.Should().Contain("Paused");

        creditSpend.Verify(
            c => c.TryDebitAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "no charge on SimilarExample for a paused child");
    }

    // ── AG-09: SimilarExample / SeatLocked → ChildSeatLockedNoEnergy ─────────────

    [Fact(DisplayName = "AG-09 SimilarExample/SeatLocked → ChildSeatLockedNoEnergy, no content, no charge (P10-15 message fix on SimilarExample)")]
    public async Task SimilarExample_SeatLockedChild_ReturnsSeatLockedErrorAndNeverCharges()
    {
        var childAccess = BuildAccessMock(ChildAccessDecision.SeatLocked);
        var creditSpend = BuildCreditSpendMock();

        var sut    = BuildSimilarExampleHandler(childAccess, creditSpend);
        var cmd    = new SimilarExampleCommand(SkillId: 10, QuestionId: 1);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.Should().BeOfType<SimilarExampleResult.Error>("a seat-locked child must be declined on SimilarExample");
        ((SimilarExampleResult.Error)result).Code.Should().Contain("SeatLocked");

        creditSpend.Verify(
            c => c.TryDebitAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── AG-10: Hint / GetAccessDecisionAsync throws → fail-soft → Streamed ────────

    [Fact(DisplayName = "AG-10 Hint/access-query-throws → fail-soft; Streamed returned (billing outage must not block learning)")]
    public async Task Hint_AccessQueryThrows_FailSoftProceedsAndReturnsStreamedContent()
    {
        var childAccess = new Mock<IChildAccessStateQuery>();
        childAccess.Setup(q => q.GetAccessDecisionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Billing service unavailable (simulated)"));

        var safetyMock = BuildSafetyMock("approved hint");
        var sut    = BuildHintHandler(childAccess, safety: safetyMock);
        var cmd    = new GetHintCommand(QuestionId: 1, AttemptId: 5, Intent: HelperIntent.Hint, HintLevel: null, WrongAnswer: null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        // Fail-soft: a billing service outage must never hard-block a student's learning session.
        result.Should().BeOfType<HintResult.Streamed>(
            "when GetAccessDecisionAsync throws, the handler must proceed (fail-soft) so billing outage never blocks learning");
    }
}
