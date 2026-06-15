using FluentAssertions;
using Learnexia.Modules.Ai.Application.Features.Explain.Commands;
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
using Moq;
using Resources;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for <see cref="ExplainConceptCommandHandler"/> branch logic (P3-04 BE-3 + P10-03-BE-5).
///
/// Coverage:
///   EH-01  Empty <see cref="LearningContext.Chunks"/> → <see cref="ExplainResult.Redirect"/>
///           (refuse-and-redirect; no LLM call; AC-3 scope guard).
///   EH-02  Non-empty chunks, <see cref="ISafetyLayer"/> returns Allowed=false
///           → <see cref="ExplainResult.Error"/> (AC-6 safe-error).
///   EH-03  Non-empty chunks, <see cref="ISafetyLayer"/> returns Allowed=true
///           → <see cref="ExplainResult.Streamed"/> with approved content (AC-2/4).
///   EH-04  Rate limit exhausted → <see cref="ExplainResult.Error"/>;
///           <see cref="ISafetyLayer"/> never called (cost guard, BE-5).
///   EH-05  Constructor reflection confirms <see cref="IAiGateway"/> is NOT injected
///           (architecture invariant P302-ARCH-04).
///   EH-P10-01  Insufficient monthly balance → Error, no debit, no gateway call (P10-03-BE-5).
///   EH-P10-02  Daily cap reached + HardStopEnabled → Error, no debit, no gateway call (P10-03-BE-5).
///   EH-P10-03  Safety block → Error; TryDebitAsync never called (P10-03-BE-5 no-charge on block).
///   EH-P10-04  Refuse/redirect (empty chunks) → TryDebitAsync never called (P10-03-BE-5).
///   EH-P10-05  Cache HIT → exactly one TryDebitAsync call (P10-03-BE-3 delivery point #1).
///   EH-P10-06  Cache MISS + safety-allowed → exactly one TryDebitAsync call (P10-03-BE-3 delivery point #2).
///
/// Mocking strategy:
///   - <see cref="ICurrentUserService"/>: returns studentId=42, grade=5, age=11, language=ar.
///   - <see cref="ILessonContextContract"/>: returns null (no lesson enrichment needed).
///   - <see cref="ILearningContextProvider"/>: returns controlled context (empty vs populated chunks).
///   - <see cref="IPromptBuilder"/>: returns a fixed <see cref="AiRequest"/> on success paths.
///   - <see cref="ISafetyLayer"/>: returns controlled <see cref="SafeAiResult"/> (allowed vs blocked).
///   - <see cref="IAiResponseCache"/>: Moq no-op (cache miss by default — returns null).
///   - <see cref="IGlobalSettingsProvider"/>: Moq returning sensible defaults.
///   - <see cref="ICreditSpendService"/>: Moq; default stub has balance=100 (always sufficient), debit=Charged.
///   - <see cref="IPublisher"/>: Moq no-op (fire-and-forget; publish count not asserted).
///   - <see cref="ILoggerManager"/>: Moq no-op.
///   - <see cref="IStringLocalizer{SharedResources}"/>: returns key as value (test isolation).
///   - <see cref="IAiTutorRateLimiter"/>: real <see cref="AiTutorRateLimiter"/> with fresh state per test.
/// </summary>
public sealed class ExplainConceptCommandHandlerTests
{
    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static ExplainConceptCommandHandler CreateSut(
        Mock<ILearningContextProvider> contextProviderMock,
        Mock<ISafetyLayer> safetyMock,
        Mock<IPromptBuilder> promptBuilderMock,
        Mock<ICurrentUserService>? currentUserMock = null,
        Mock<ILessonContextContract>? lessonContextMock = null,
        IAiTutorRateLimiter? rateLimiter = null,
        Mock<IAiResponseCache>? aiCacheMock = null,
        Mock<IGlobalSettingsProvider>? settingsMock = null,
        Mock<IServiceScopeFactory>? scopeFactoryMock = null,
        Mock<ICreditSpendService>? creditSpendMock = null,
        bool hardStopEnabled = false)
    {
        var currentUser = currentUserMock ?? BuildDefaultCurrentUserMock();
        var lessonCtx = lessonContextMock ?? BuildDefaultLessonContextMock();
        var localizer = BuildLocalizerMock();
        var publisher = new Mock<IPublisher>();
        var logger = new Mock<ILoggerManager>();
        var redirectBuilder = new RedirectResponseBuilder(localizer.Object);
        var rl = rateLimiter ?? new AiTutorRateLimiter();
        var cache = aiCacheMock ?? BuildDefaultAiCacheMock();
        var settings = settingsMock ?? BuildDefaultSettingsMock();
        var scopeFactory = scopeFactoryMock ?? BuildNoOpScopeFactoryMock();
        var creditSpend = creditSpendMock ?? BuildDefaultCreditSpendMock();
        var costResolver = BuildCreditCostResolver(settings, hardStopEnabled);

        return new ExplainConceptCommandHandler(
            currentUser.Object,
            lessonCtx.Object,
            contextProviderMock.Object,
            promptBuilderMock.Object,
            safetyMock.Object,
            cache.Object,
            settings.Object,
            redirectBuilder,
            rl,
            creditSpend.Object,
            costResolver,
            publisher.Object,
            scopeFactory.Object,
            logger.Object,
            localizer.Object);
    }

    /// <summary>Default credit-spend stub: balance=100 (always sufficient), debit returns Charged.</summary>
    private static Mock<ICreditSpendService> BuildDefaultCreditSpendMock()
    {
        var mock = new Mock<ICreditSpendService>();
        mock.Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null));
        mock.Setup(c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DebitResult(true, 3, 0, 97, DebitOutcome.Charged));
        return mock;
    }

    /// <summary>Builds a <see cref="CreditCostResolver"/> backed by the given settings mock + a minimal config.</summary>
    private static CreditCostResolver BuildCreditCostResolver(
        Mock<IGlobalSettingsProvider>? settingsMock = null,
        bool hardStopEnabled = false)
    {
        var settings = (settingsMock ?? BuildDefaultSettingsMock()).Object;
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c.GetSection("Billing").GetSection("HardStopEnabled").Value)
            .Returns(hardStopEnabled ? "true" : "false");
        // GetValue<bool> extension uses indexer access on the flattened key.
        configMock.Setup(c => c["Billing:HardStopEnabled"])
            .Returns(hardStopEnabled ? "true" : "false");
        return new CreditCostResolver(settings, configMock.Object);
    }

    /// <summary>
    /// Returns a no-op <see cref="IServiceScopeFactory"/> stub that resolves a no-op
    /// <see cref="IAiResponseCache"/> on CreateScope(). Used to prevent fire-and-forget
    /// cache writes from throwing NullReferenceException during unit tests.
    /// </summary>
    private static Mock<IServiceScopeFactory> BuildNoOpScopeFactoryMock()
    {
        var cacheMock = new Mock<IAiResponseCache>();
        cacheMock
            .Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var spMock = new Mock<IServiceProvider>();
        spMock
            .Setup(sp => sp.GetService(typeof(IAiResponseCache)))
            .Returns(cacheMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);

        var factoryMock = new Mock<IServiceScopeFactory>();
        factoryMock
            .Setup(f => f.CreateScope())
            .Returns(scopeMock.Object);

        return factoryMock;
    }

    /// <summary>Stub user: studentId=42, grade=5, age=11, language=ar.</summary>
    private static Mock<ICurrentUserService> BuildDefaultCurrentUserMock()
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(42);
        mock.Setup(u => u.GetClaimValue("Grade")).Returns("5");
        mock.Setup(u => u.GetClaimValue("Age")).Returns("11");
        mock.Setup(u => u.GetClaimValue("Language")).Returns("ar");
        return mock;
    }

    /// <summary>Stub: returns null for any lesson id (no enrichment path exercised).</summary>
    private static Mock<ILessonContextContract> BuildDefaultLessonContextMock()
    {
        var mock = new Mock<ILessonContextContract>();
        mock.Setup(l => l.GetLessonContextAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LessonContextDto?)null);
        return mock;
    }

    /// <summary>Returns the resource key as its own value (no resx file needed in tests).</summary>
    private static Mock<IStringLocalizer<SharedResources>> BuildLocalizerMock()
    {
        var mock = new Mock<IStringLocalizer<SharedResources>>();
        mock.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(key => new LocalizedString(key, key));
        return mock;
    }

    /// <summary>Default cache: always a MISS (returns null) — exercises the live path.</summary>
    private static Mock<IAiResponseCache> BuildDefaultAiCacheMock()
    {
        var mock = new Mock<IAiResponseCache>();
        mock.Setup(c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        mock.Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    /// <summary>Default settings: sensible MVP defaults for autoApprovalConfidence and pool size.</summary>
    private static Mock<IGlobalSettingsProvider> BuildDefaultSettingsMock()
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

    private static LearningContext MakePopulatedContext() =>
        new LearningContext(
            Chunks: new[] { new ChunkDto("chunk-1", "Photosynthesis is the process by which plants use sunlight.") },
            QuestionText: null,
            WrongAnswer: null,
            SkillId: 10,
            QuestionId: null,
            GradeId: 5,
            SubjectId: (int)Subject.Science,
            Language: TutorLanguage.Ar);

    private static LearningContext MakeEmptyContext() =>
        new LearningContext(
            Chunks: Array.Empty<ChunkDto>(),
            QuestionText: null,
            WrongAnswer: null,
            SkillId: 10,
            QuestionId: null,
            GradeId: 5,
            SubjectId: (int)Subject.Science,
            Language: TutorLanguage.Ar);

    // ── EH-01: Empty context → Redirect ─────────────────────────────────────────

    [Fact(DisplayName = "P304-EH-01 Empty context → Redirect; ISafetyLayer never called (AC-3 scope guard)")]
    public async Task Handle_EmptyContext_ReturnsRedirectAndNeverCallsSafety()
    {
        // Arrange
        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeEmptyContext());

        var safetyMock = new Mock<ISafetyLayer>();
        var promptMock = new Mock<IPromptBuilder>();

        var sut = CreateSut(contextProvider, safetyMock, promptMock);
        var command = new ExplainConceptCommand(LessonId: null, ConceptId: null, SkillId: 10, Question: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ExplainResult.Redirect>(
            "empty context must return a redirect, never call the LLM (AC-3)");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer must NOT be called when context is empty");

        promptMock.Verify(
            p => p.Build(It.IsAny<PromptContext>()),
            Times.Never,
            "IPromptBuilder must NOT be called when context is empty");
    }

    // ── EH-02: Safety blocked → Error ───────────────────────────────────────────

    [Fact(DisplayName = "P304-EH-02 Safety blocked → Error result with safety code (AC-6)")]
    public async Task Handle_SafetyBlocked_ReturnsError()
    {
        // Arrange — context has chunks, safety layer blocks the response.
        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePopulatedContext());

        var promptMock = new Mock<IPromptBuilder>();
        promptMock
            .Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Explain photosynthesis", Task = AiTaskKind.Explain }));

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: false,
                Content: "I cannot help with that.",
                Verdict: SafetyVerdict.Blocked,
                Results: Array.Empty<CheckResult>()));

        var sut = CreateSut(contextProvider, safetyMock, promptMock);
        var command = new ExplainConceptCommand(LessonId: null, ConceptId: null, SkillId: 10, Question: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ExplainResult.Error>(
            "a safety block must surface as a typed Error result (AC-6 safe-error)");

        var error = (ExplainResult.Error)result;
        // The handler sets Code = nameof(SharedResourcesKey.ExplainConceptSafetyBlocked)
        // = "ExplainConceptSafetyBlocked" — which contains "Safety".
        error.Code.Should().Contain("Safety",
            "error code must identify the safety block for the SSE wire event: error frame");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "safety layer must be called once when context is non-empty");
    }

    // ── EH-03: Safety allowed → Streamed content ─────────────────────────────────

    [Fact(DisplayName = "P304-EH-03 Safety allowed → Streamed result with approved content (AC-2/4)")]
    public async Task Handle_SafetyAllowed_ReturnsStreamedWithApprovedContent()
    {
        // Arrange — context has chunks, safety approves the response.
        const string approvedContent = "Photosynthesis is a process used by plants to convert sunlight into food.";

        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePopulatedContext());

        var promptMock = new Mock<IPromptBuilder>();
        promptMock
            .Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Explain photosynthesis", Task = AiTaskKind.Explain }));

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: true,
                Content: approvedContent,
                Verdict: SafetyVerdict.Allowed,
                Results: Array.Empty<CheckResult>()));

        var sut = CreateSut(contextProvider, safetyMock, promptMock);
        var command = new ExplainConceptCommand(LessonId: null, ConceptId: null, SkillId: 10, Question: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ExplainResult.Streamed>(
            "a safety-approved response must surface as Streamed content (AC-2)");

        var streamed = (ExplainResult.Streamed)result;
        streamed.Content.Should().Be(approvedContent,
            "handler must return the exact approved content from ISafetyLayer — no bypass (AC-4)");
    }

    // ── EH-04: Rate limit exceeded → Error ──────────────────────────────────────

    [Fact(DisplayName = "P304-EH-04 Rate limit exhausted → Error; ISafetyLayer and context provider never called (BE-5)")]
    public async Task Handle_RateLimitExceeded_ReturnsErrorAndShortCircuits()
    {
        // Arrange — exhaust the 10-request window for studentId=42.
        var rateLimiter = new AiTutorRateLimiter();
        for (var i = 0; i < 10; i++)
            rateLimiter.TryAllow(42); // Consume all 10 allowed slots.

        var contextProvider = new Mock<ILearningContextProvider>();
        var safetyMock = new Mock<ISafetyLayer>();
        var promptMock = new Mock<IPromptBuilder>();

        var sut = CreateSut(contextProvider, safetyMock, promptMock, rateLimiter: rateLimiter);
        var command = new ExplainConceptCommand(LessonId: null, ConceptId: null, SkillId: 10, Question: null);

        // Act — the 11th call exceeds the window and must be refused immediately.
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ExplainResult.Error>(
            "rate limit exceeded must surface as Error (BE-5 cost/abuse guard)");

        var error = (ExplainResult.Error)result;
        // Handler sets Code = nameof(SharedResourcesKey.ExplainConceptRateLimitExceeded)
        // = "ExplainConceptRateLimitExceeded" — which contains "RateLimit".
        error.Code.Should().Contain("RateLimit",
            "error code must identify the rate-limit violation");

        // Rate limit fires before context fetch or LLM — both must be zero.
        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer must NOT be called when rate limit is exceeded");

        contextProvider.Verify(
            p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ILearningContextProvider must NOT be called when rate limit is exceeded");
    }

    // ── EH-06: Cache HIT → Streamed from cache; ISafetyLayer never called ───────

    [Fact(DisplayName = "P304-EH-06 Cache HIT → Streamed with cached content; ISafetyLayer never called (WI-B4 AC-B1)")]
    public async Task Handle_CacheHit_ReturnsStreamedWithCachedContentAndNeverCallsSafetyLayer()
    {
        // Arrange — cache returns an approved cached response; safety layer must NOT be invoked.
        const string cachedContent = "Photosynthesis (cached): plants convert light to glucose using chlorophyll.";

        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePopulatedContext());

        var promptMock = new Mock<IPromptBuilder>();
        promptMock
            .Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Explain photosynthesis", Task = AiTaskKind.Explain }));

        var safetyMock = new Mock<ISafetyLayer>();
        // Safety layer is left with no setup — any call would throw or return null.
        // If it is ever called, the Times.Never verification below will catch it.

        // HIT: cache returns the pre-approved content string.
        var aiCacheMock = new Mock<IAiResponseCache>();
        aiCacheMock
            .Setup(c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedContent);
        aiCacheMock
            .Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(contextProvider, safetyMock, promptMock, aiCacheMock: aiCacheMock);
        var command = new ExplainConceptCommand(LessonId: null, ConceptId: null, SkillId: 10, Question: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert — content is the exact cached string.
        result.Should().BeOfType<ExplainResult.Streamed>(
            "a cache HIT must return Streamed content immediately (WI-B4 AC-B1)");

        var streamed = (ExplainResult.Streamed)result;
        streamed.Content.Should().Be(cachedContent,
            "handler must return the exact cached content — not re-generate it");

        // The central guarantee: zero AI/safety invocations on a cache HIT.
        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer.GenerateSafeAsync must NOT be called on a cache HIT — " +
            "the cached response is pre-approved; invoking the safety layer would be a zero-cost bypass violation");
    }

    // ── EH-07: Cache MISS → safety layer IS called (contrast test) ──────────────

    [Fact(DisplayName = "P304-EH-07 Cache MISS → ISafetyLayer called; establishes MISS-vs-HIT contrast (WI-B4)")]
    public async Task Handle_CacheMiss_SafetyLayerIsCalled()
    {
        // Arrange — default MISS cache; safety approves.
        const string approvedContent = "Photosynthesis: light energy → chemical energy.";

        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePopulatedContext());

        var promptMock = new Mock<IPromptBuilder>();
        promptMock
            .Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Explain photosynthesis", Task = AiTaskKind.Explain }));

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: true,
                Content: approvedContent,
                Verdict: SafetyVerdict.Allowed,
                Results: Array.Empty<CheckResult>()));

        // Default MISS: BuildDefaultAiCacheMock() returns null (no HIT).
        var sut = CreateSut(contextProvider, safetyMock, promptMock);
        var command = new ExplainConceptCommand(LessonId: null, ConceptId: null, SkillId: 10, Question: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert — on a MISS the safety layer MUST be invoked (contrast to EH-06).
        result.Should().BeOfType<ExplainResult.Streamed>(
            "a cache MISS with safety-allowed response must return Streamed content");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "ISafetyLayer must be called exactly once on a cache MISS");
    }

    // ── EH-08: Cache write dispatched on NEW scope (DEFECT-3 fix) ───────────────

    [Fact(DisplayName = "P304-EH-08 Cache write is dispatched on a fresh IServiceScopeFactory scope (DEFECT-3 fix)")]
    public async Task Handle_CacheMiss_SafetyAllowed_CacheWriteUsesNewScope()
    {
        // Arrange — verify that the cache write is resolved from a NEW scope (not the request-scoped
        // IAiResponseCache), so the scoped AiDbContext is independent of the SSE request lifetime.
        // This is the DEFECT-3 regression guard: if the handler uses _ = _aiCache.WriteAsync(...)
        // directly (request-scoped), this test will fail because the scopeFactory-resolved cache
        // is never called. If the fix is in place, the scope-factory is used and WriteAsync is called
        // on the fresh-scope cache.
        const string approvedContent = "Photosynthesis is a process used by plants.";

        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePopulatedContext());

        var promptMock = new Mock<IPromptBuilder>();
        promptMock
            .Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Explain photosynthesis", Task = AiTaskKind.Explain }));

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: true,
                Content: approvedContent,
                Verdict: SafetyVerdict.Allowed,
                Results: Array.Empty<CheckResult>()));

        // Distinct cache mock for the fresh scope — we assert WriteAsync is called on THIS one.
        var freshScopeCacheMock = new Mock<IAiResponseCache>();
        freshScopeCacheMock
            .Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        freshScopeCacheMock
            .Setup(c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var spMock = new Mock<IServiceProvider>();
        spMock
            .Setup(sp => sp.GetService(typeof(IAiResponseCache)))
            .Returns(freshScopeCacheMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Returns(scopeMock.Object);

        // Request-scope cache: always MISS — WriteAsync should NOT be called on this one.
        var requestScopeCacheMock = new Mock<IAiResponseCache>();
        requestScopeCacheMock
            .Setup(c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = CreateSut(
            contextProvider,
            safetyMock,
            promptMock,
            aiCacheMock: requestScopeCacheMock,
            scopeFactoryMock: scopeFactoryMock);

        var command = new ExplainConceptCommand(LessonId: null, ConceptId: null, SkillId: 10, Question: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Allow the fire-and-forget Task.Run to complete before asserting.
        await Task.Delay(100);

        // Assert — response is correct.
        result.Should().BeOfType<ExplainResult.Streamed>(
            "safety-allowed cache-MISS path must return Streamed content");

        // Key assertion: WriteAsync was called on the FRESH-SCOPE cache (scope factory was used).
        freshScopeCacheMock.Verify(
            c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "DEFECT-3 fix: cache write must resolve IAiResponseCache from a NEW scope via IServiceScopeFactory " +
            "so the AiDbContext lifetime is independent of the disposed SSE request scope");

        // Negative: WriteAsync was NOT called on the request-scope cache instance.
        requestScopeCacheMock.Verify(
            c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "DEFECT-3 fix: the request-scoped IAiResponseCache field must NOT be used for the write " +
            "(it would use the disposed request AiDbContext)");
    }

    // ── EH-05: IAiGateway must NOT be injected ───────────────────────────────────

    [Fact(DisplayName = "P304-EH-05 Handler constructor injects ISafetyLayer, never IAiGateway (P302-ARCH-04)")]
    public void Handler_ConstructorDoesNotInject_IAiGateway()
    {
        // Mirrors architecture test P302-ARCH-04 at unit-test level.
        // Ensures the invariant is visible and catchable independently of the arch test runner.
        var ctorParams = typeof(ExplainConceptCommandHandler)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        ctorParams.Should().NotContain(
            typeof(IAiGateway),
            "ExplainConceptCommandHandler must NEVER inject IAiGateway directly — ISafetyLayer is the sole AI façade (AC-2, P302-ARCH-04)");

        ctorParams.Should().Contain(
            typeof(ISafetyLayer),
            "ExplainConceptCommandHandler must inject ISafetyLayer");
    }

    // ── P10-03-BE-5: No-charge cases ──────────────────────────────────────────────

    [Fact(DisplayName = "P1003-EH-P10-01 Insufficient monthly balance → Error; TryDebitAsync never called (P10-03-BE-5)")]
    public async Task Handle_InsufficientMonthlyBalance_ReturnsErrorAndNeverDebits()
    {
        // Arrange — balance=2, cost for Explain=3 (default) → pre-auth blocks.
        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(2, 0, 2, null));

        var contextProvider = new Mock<ILearningContextProvider>();
        var safetyMock = new Mock<ISafetyLayer>();
        var promptMock = new Mock<IPromptBuilder>();

        var sut = CreateSut(contextProvider, safetyMock, promptMock, creditSpendMock: creditSpendMock);
        var command = new ExplainConceptCommand(LessonId: null, ConceptId: null, SkillId: 10, Question: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ExplainResult.Error>(
            "monthly hard-exhausted must surface as a typed Error — locked economy model P10-03");

        var error = (ExplainResult.Error)result;
        error.Code.Should().Contain("Insufficient",
            "error code must identify the insufficient-energy condition");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "TryDebitAsync must NOT be called when monthly balance is insufficient (no-charge rule)");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer must NOT be called when monthly balance is insufficient (no gateway call)");
    }

    [Fact(DisplayName = "P1003-EH-P10-02 Daily cap reached + HardStopEnabled → Error; TryDebitAsync never called (P10-03-BE-5)")]
    public async Task Handle_DailyCapReachedHardStop_ReturnsErrorAndNeverDebits()
    {
        // Arrange — balance=100 (sufficient monthly), daily cap reached, HardStop=true.
        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null, DailyUsed: 10, DailyCap: 10, DailyCapReached: true));

        var contextProvider = new Mock<ILearningContextProvider>();
        var safetyMock = new Mock<ISafetyLayer>();
        var promptMock = new Mock<IPromptBuilder>();

        // hardStopEnabled=true makes the daily cap a hard block.
        var sut = CreateSut(contextProvider, safetyMock, promptMock,
            creditSpendMock: creditSpendMock, hardStopEnabled: true);
        var command = new ExplainConceptCommand(LessonId: null, ConceptId: null, SkillId: 10, Question: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ExplainResult.Error>(
            "daily cap reached + HardStopEnabled must surface as a typed Error");

        var error = (ExplainResult.Error)result;
        error.Code.Should().Contain("DailyCap",
            "error code must identify the daily-cap condition");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "TryDebitAsync must NOT be called when daily cap is hard-stopped (no-charge rule)");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer must NOT be called when daily cap is hard-stopped (no gateway call)");
    }

    [Fact(DisplayName = "P1003-EH-P10-03 Safety block → Error; TryDebitAsync never called (P10-03-BE-5 no-charge on block)")]
    public async Task Handle_SafetyBlock_NeverDebits()
    {
        // Arrange — sufficient balance, but safety blocks response.
        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null));

        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePopulatedContext());

        var promptMock = new Mock<IPromptBuilder>();
        promptMock
            .Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Explain photosynthesis", Task = AiTaskKind.Explain }));

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: false,
                Content: null,
                Verdict: SafetyVerdict.Blocked,
                Results: Array.Empty<CheckResult>()));

        var sut = CreateSut(contextProvider, safetyMock, promptMock, creditSpendMock: creditSpendMock);
        var command = new ExplainConceptCommand(LessonId: null, ConceptId: null, SkillId: 10, Question: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ExplainResult.Error>(
            "safety block must return Error (AC-6)");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "TryDebitAsync must NOT be called when response is safety-blocked (no delivery = no charge)");
    }

    [Fact(DisplayName = "P1003-EH-P10-04 Refuse/redirect (empty chunks) → TryDebitAsync never called (P10-03-BE-5)")]
    public async Task Handle_EmptyContext_NeverDebits()
    {
        // Arrange — empty context triggers refuse-and-redirect before debit.
        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null));

        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeEmptyContext());

        var safetyMock = new Mock<ISafetyLayer>();
        var promptMock = new Mock<IPromptBuilder>();

        var sut = CreateSut(contextProvider, safetyMock, promptMock, creditSpendMock: creditSpendMock);
        var command = new ExplainConceptCommand(LessonId: null, ConceptId: null, SkillId: 10, Question: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ExplainResult.Redirect>(
            "empty context must return a Redirect (AC-3 refuse-and-redirect)");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "TryDebitAsync must NOT be called on refuse-and-redirect (no delivery = no charge)");
    }

    [Fact(DisplayName = "P1003-EH-P10-05 Cache HIT → exactly one TryDebitAsync call (P10-03-BE-3 delivery point #1)")]
    public async Task Handle_CacheHit_DebitCalledExactlyOnce()
    {
        // Arrange — cache HIT; debit must occur at the cache-HIT delivery point.
        const string cachedContent = "Photosynthesis (cached)";

        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null));
        creditSpendMock
            .Setup(c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DebitResult(true, 3, 0, 97, DebitOutcome.Charged));

        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePopulatedContext());

        var promptMock = new Mock<IPromptBuilder>();
        promptMock
            .Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Explain photosynthesis", Task = AiTaskKind.Explain }));

        var aiCacheMock = new Mock<IAiResponseCache>();
        aiCacheMock
            .Setup(c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedContent);
        aiCacheMock
            .Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var safetyMock = new Mock<ISafetyLayer>();

        var sut = CreateSut(contextProvider, safetyMock, promptMock,
            aiCacheMock: aiCacheMock, creditSpendMock: creditSpendMock);
        var command = new ExplainConceptCommand(LessonId: null, ConceptId: null, SkillId: 10, Question: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ExplainResult.Streamed>(
            "cache HIT must return Streamed content");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "TryDebitAsync must be called exactly once on cache HIT (delivery point #1 — P10-03-BE-3)");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer must NOT be called on cache HIT");
    }

    [Fact(DisplayName = "P1003-EH-P10-06 Cache MISS + safety-allowed → exactly one TryDebitAsync call (P10-03-BE-3 delivery point #2)")]
    public async Task Handle_CacheMissSafetyAllowed_DebitCalledExactlyOnce()
    {
        // Arrange — cache MISS, safety approves; debit must occur post-safety.
        const string approvedContent = "Photosynthesis is a process used by plants.";

        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null));
        creditSpendMock
            .Setup(c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DebitResult(true, 3, 0, 97, DebitOutcome.Charged));

        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePopulatedContext());

        var promptMock = new Mock<IPromptBuilder>();
        promptMock
            .Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Explain photosynthesis", Task = AiTaskKind.Explain }));

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: true,
                Content: approvedContent,
                Verdict: SafetyVerdict.Allowed,
                Results: Array.Empty<CheckResult>()));

        // Default MISS cache.
        var sut = CreateSut(contextProvider, safetyMock, promptMock, creditSpendMock: creditSpendMock);
        var command = new ExplainConceptCommand(LessonId: null, ConceptId: null, SkillId: 10, Question: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ExplainResult.Streamed>(
            "cache MISS + safety-allowed must return Streamed content");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "TryDebitAsync must be called exactly once post-safety-success (delivery point #2 — P10-03-BE-3)");
    }
}
