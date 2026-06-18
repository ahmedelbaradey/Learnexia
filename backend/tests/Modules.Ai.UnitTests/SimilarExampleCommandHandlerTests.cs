using FluentAssertions;
using Learnexia.Modules.Ai.Application.Features.SimilarExample.Commands;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Contracts.Billing;
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
/// Unit tests for <see cref="SimilarExampleCommandHandler"/> branch logic (P3-06-BE-10).
///
/// Coverage:
///   SE-01  Empty <see cref="LearningContext.Chunks"/> → <see cref="SimilarExampleResult.Redirect"/>
///           (refuse-and-redirect; no LLM call; AC-7 scope guard).
///   SE-02  Non-empty chunks, <see cref="ISafetyLayer"/> returns Allowed=false
///           → <see cref="SimilarExampleResult.Error"/> (AC-9 safe-error).
///   SE-03  Non-empty chunks, <see cref="ISafetyLayer"/> returns Allowed=true
///           → <see cref="SimilarExampleResult.Streamed"/> with approved content (AC-9/FR-AI-6).
///   SE-04  Rate limit exhausted → <see cref="SimilarExampleResult.Error"/>;
///           <see cref="ISafetyLayer"/> never called (cost guard, BE-11).
///   SE-05  Constructor reflection confirms <see cref="IAiGateway"/> is NOT injected
///           (architecture invariant P302-ARCH-04).
///
/// Mocking strategy:
///   - <see cref="ICurrentUserService"/>: returns studentId=99, grade=6, age=12, language=ar.
///   - <see cref="ILearningContextProvider"/>: returns controlled context (empty vs populated chunks).
///   - <see cref="IPromptBuilder"/>: returns a fixed <see cref="AiRequest"/> on success paths.
///   - <see cref="ISafetyLayer"/>: returns controlled <see cref="SafeAiResult"/> (allowed vs blocked).
///   - <see cref="IAiResponseCache"/>: Moq no-op (cache miss by default — returns null).
///   - <see cref="IGlobalSettingsProvider"/>: Moq returning sensible defaults.
///   - <see cref="IServiceScopeFactory"/>: Moq no-op (fire-and-forget publish never awaited in tests).
///   - <see cref="ILoggerManager"/>: Moq no-op.
///   - <see cref="IStringLocalizer{SharedResources}"/>: returns key as value (test isolation).
///   - <see cref="IAiTutorRateLimiter"/>: real <see cref="AiTutorRateLimiter"/> with fresh state per test.
/// </summary>
public sealed class SimilarExampleCommandHandlerTests
{
    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static SimilarExampleCommandHandler CreateSut(
        Mock<ILearningContextProvider> contextProviderMock,
        Mock<ISafetyLayer> safetyMock,
        Mock<IPromptBuilder> promptBuilderMock,
        Mock<ICurrentUserService>? currentUserMock = null,
        IAiTutorRateLimiter? rateLimiter = null,
        Mock<IAiResponseCache>? aiCacheMock = null,
        Mock<IGlobalSettingsProvider>? settingsMock = null,
        Mock<ICreditSpendService>? creditSpendMock = null,
        Mock<IChildAccessStateQuery>? childAccessStateMock = null,
        bool hardStopEnabled = false)
    {
        var currentUser = currentUserMock ?? BuildDefaultCurrentUserMock();
        var localizer = BuildLocalizerMock();
        var logger = new Mock<ILoggerManager>();
        var redirectBuilder = new RedirectResponseBuilder(localizer.Object);
        var rl = rateLimiter ?? new AiTutorRateLimiter();
        var cache = aiCacheMock ?? BuildDefaultAiCacheMock();
        var settings = settingsMock ?? BuildDefaultSettingsMock();
        var creditSpend = creditSpendMock ?? BuildDefaultCreditSpendMock();
        var childAccess = childAccessStateMock ?? BuildDefaultChildAccessStateMock();
        var costResolver = BuildCreditCostResolver(settings, hardStopEnabled);

        // IServiceScopeFactory: no-op stub — fire-and-forget events are not asserted in unit tests.
        // The test process completes before the background Task.Run can resolve the scope.
        var scopeFactory = BuildNoOpScopeFactoryMock();

        return new SimilarExampleCommandHandler(
            currentUser.Object,
            contextProviderMock.Object,
            promptBuilderMock.Object,
            safetyMock.Object,
            cache.Object,
            settings.Object,
            redirectBuilder,
            rl,
            creditSpend.Object,
            childAccess.Object,
            costResolver,
            scopeFactory.Object,
            logger.Object,
            localizer.Object);
    }

    /// <summary>Default access state: Allowed — existing tests are unaffected by the new gate.</summary>
    private static Mock<IChildAccessStateQuery> BuildDefaultChildAccessStateMock()
    {
        var mock = new Mock<IChildAccessStateQuery>();
        mock.Setup(q => q.GetAccessDecisionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChildAccessDecision.Allowed);
        mock.Setup(q => q.IsChildAccessAllowedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock;
    }

    /// <summary>Default credit-spend stub: balance=100 (always sufficient), debit returns Charged.</summary>
    private static Mock<ICreditSpendService> BuildDefaultCreditSpendMock()
    {
        var mock = new Mock<ICreditSpendService>();
        mock.Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null));
        mock.Setup(c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DebitResult(true, 5, 0, 95, DebitOutcome.Charged));
        return mock;
    }

    /// <summary>Builds a <see cref="CreditCostResolver"/> backed by the given settings mock + a minimal config.</summary>
    private static CreditCostResolver BuildCreditCostResolver(
        Mock<IGlobalSettingsProvider>? settingsMock = null,
        bool hardStopEnabled = false)
    {
        var settings = (settingsMock ?? BuildDefaultSettingsMock()).Object;
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Billing:HardStopEnabled"])
            .Returns(hardStopEnabled ? "true" : "false");
        return new CreditCostResolver(settings, configMock.Object);
    }

    /// <summary>Stub user: studentId=99, grade=6, age=12, language=ar.</summary>
    private static Mock<ICurrentUserService> BuildDefaultCurrentUserMock()
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(99);
        mock.Setup(u => u.GetClaimValue("Grade")).Returns("6");
        mock.Setup(u => u.GetClaimValue("Age")).Returns("12");
        mock.Setup(u => u.GetClaimValue("Language")).Returns("ar");
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

    /// <summary>
    /// Returns a no-op <see cref="IServiceScopeFactory"/> so fire-and-forget event publishes
    /// and cache writes don't throw during the synchronous test path.
    /// Resolves both <see cref="IPublisher"/> and <see cref="IAiResponseCache"/> (DEFECT-3 fix:
    /// the handler now resolves IAiResponseCache from a fresh scope for the cache write).
    /// </summary>
    private static Mock<IServiceScopeFactory> BuildNoOpScopeFactoryMock()
    {
        var publisherMock = new Mock<IPublisher>();
        publisherMock
            .Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cacheMock = new Mock<IAiResponseCache>();
        cacheMock
            .Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var spMock = new Mock<IServiceProvider>();
        spMock
            .Setup(sp => sp.GetService(typeof(IPublisher)))
            .Returns(publisherMock.Object);
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

    private static LearningContext MakePopulatedContext() =>
        new LearningContext(
            Chunks: new[] { new ChunkDto("chunk-1", "A similar example of the Pythagorean theorem: in a 3-4-5 triangle.") },
            QuestionText: null,
            WrongAnswer: null,
            SkillId: 20,
            QuestionId: null,
            GradeId: 6,
            SubjectId: (int)Subject.Math,
            Language: TutorLanguage.Ar);

    private static LearningContext MakeEmptyContext() =>
        new LearningContext(
            Chunks: Array.Empty<ChunkDto>(),
            QuestionText: null,
            WrongAnswer: null,
            SkillId: 20,
            QuestionId: null,
            GradeId: 6,
            SubjectId: (int)Subject.Math,
            Language: TutorLanguage.Ar);

    // ── SE-01: Empty context → Redirect ─────────────────────────────────────────

    [Fact(DisplayName = "P306-SE-01 Empty context → Redirect; ISafetyLayer never called (AC-7 scope guard)")]
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
        var command = new SimilarExampleCommand(SkillId: 20, QuestionId: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SimilarExampleResult.Redirect>(
            "empty context must return a redirect, never call the LLM (AC-7 refuse-and-redirect)");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer must NOT be called when context is empty (no-grounding = no-answer invariant)");

        promptMock.Verify(
            p => p.Build(It.IsAny<PromptContext>()),
            Times.Never,
            "IPromptBuilder must NOT be called when context is empty");
    }

    // ── SE-02: Safety blocked → Error ───────────────────────────────────────────

    [Fact(DisplayName = "P306-SE-02 Safety blocked → Error result with safety code (AC-9)")]
    public async Task Handle_SafetyBlocked_ReturnsError()
    {
        // Arrange — context has chunks; safety layer blocks the response.
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
                new AiRequest { Prompt = "Give me a similar math example", Task = AiTaskKind.Explain }));

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: false,
                Content: null,
                Verdict: SafetyVerdict.Blocked,
                Results: Array.Empty<CheckResult>()));

        var sut = CreateSut(contextProvider, safetyMock, promptMock);
        var command = new SimilarExampleCommand(SkillId: 20, QuestionId: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SimilarExampleResult.Error>(
            "a safety block must surface as a typed Error result (AC-9 safe-error)");

        var error = (SimilarExampleResult.Error)result;
        // The handler sets Code = nameof(SharedResourcesKey.SimilarExampleSafetyBlocked)
        // = "SimilarExampleSafetyBlocked" — which contains "Safety".
        error.Code.Should().Contain("Safety",
            "error code must identify the safety block for the SSE wire event: error frame");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "safety layer must be called exactly once when context is non-empty");
    }

    // ── SE-03: Safety allowed → Streamed content ─────────────────────────────────

    [Fact(DisplayName = "P306-SE-03 Safety allowed → Streamed result with approved content (AC-9/FR-AI-6)")]
    public async Task Handle_SafetyAllowed_ReturnsStreamedWithApprovedContent()
    {
        // Arrange — context has chunks; safety approves the response.
        const string approvedContent = "Here is a similar example: if 3² + 4² = 5², then a 3-4-5 triangle is right-angled.";

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
                new AiRequest { Prompt = "Give me a similar math example", Task = AiTaskKind.Explain }));

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: true,
                Content: approvedContent,
                Verdict: SafetyVerdict.Allowed,
                Results: Array.Empty<CheckResult>()));

        var sut = CreateSut(contextProvider, safetyMock, promptMock);
        var command = new SimilarExampleCommand(SkillId: 20, QuestionId: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SimilarExampleResult.Streamed>(
            "a safety-approved response must surface as Streamed content (FR-AI-6 content-only)");

        var streamed = (SimilarExampleResult.Streamed)result;
        streamed.Content.Should().Be(approvedContent,
            "handler must return the exact approved content from ISafetyLayer — no bypass (AC-9)");
    }

    // ── SE-04: Rate limit exceeded → Error ──────────────────────────────────────

    [Fact(DisplayName = "P306-SE-04 Rate limit exhausted → Error; ISafetyLayer and context provider never called (BE-11)")]
    public async Task Handle_RateLimitExceeded_ReturnsErrorAndShortCircuits()
    {
        // Arrange — exhaust the 10-request window for studentId=99.
        var rateLimiter = new AiTutorRateLimiter();
        for (var i = 0; i < 10; i++)
            rateLimiter.TryAllow(99); // Consume all 10 allowed slots.

        var contextProvider = new Mock<ILearningContextProvider>();
        var safetyMock = new Mock<ISafetyLayer>();
        var promptMock = new Mock<IPromptBuilder>();

        var sut = CreateSut(contextProvider, safetyMock, promptMock, rateLimiter: rateLimiter);
        var command = new SimilarExampleCommand(SkillId: 20, QuestionId: null);

        // Act — the 11th call exceeds the window and must be refused immediately.
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SimilarExampleResult.Error>(
            "rate limit exceeded must surface as Error (BE-11 cost/abuse guard)");

        var error = (SimilarExampleResult.Error)result;
        // Handler sets Code = nameof(SharedResourcesKey.SimilarExampleRateLimitExceeded)
        // = "SimilarExampleRateLimitExceeded" — which contains "RateLimit".
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

    // ── SE-06: Cache HIT → Streamed from cache; ISafetyLayer never called ───────

    [Fact(DisplayName = "P306-SE-06 Cache HIT → Streamed with cached content; ISafetyLayer never called (WI-B4 AC-B1)")]
    public async Task Handle_CacheHit_ReturnsStreamedWithCachedContentAndNeverCallsSafetyLayer()
    {
        // Arrange — cache returns an approved response; safety layer must NOT be invoked.
        const string cachedContent = "Here is a cached similar example: a 3-4-5 right triangle.";

        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePopulatedContext());

        var promptMock = new Mock<IPromptBuilder>();
        // Prompt builder might not be called on HIT (handler calls it before cache in SE handler),
        // so we set it up permissively but don't assert on it.
        promptMock
            .Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Give a similar math example", Task = AiTaskKind.Explain }));

        var safetyMock = new Mock<ISafetyLayer>();
        // No setup — any call would yield default null / throw, caught by Times.Never below.

        // HIT: cache returns the pre-approved content string.
        var aiCacheMock = new Mock<IAiResponseCache>();
        aiCacheMock
            .Setup(c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedContent);
        aiCacheMock
            .Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(contextProvider, safetyMock, promptMock, aiCacheMock: aiCacheMock);
        var command = new SimilarExampleCommand(SkillId: 20, QuestionId: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert — content is the exact cached string.
        result.Should().BeOfType<SimilarExampleResult.Streamed>(
            "a cache HIT must return Streamed content immediately (WI-B4 AC-B1)");

        var streamed = (SimilarExampleResult.Streamed)result;
        streamed.Content.Should().Be(cachedContent,
            "handler must return the exact cached content — not re-generate it");

        // The central guarantee: zero AI/safety invocations on a cache HIT.
        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer.GenerateSafeAsync must NOT be called on a cache HIT — " +
            "the cached response is pre-approved; invoking the safety layer would be a zero-cost bypass violation");
    }

    // ── SE-07: Cache MISS → safety layer IS called (contrast test) ──────────────

    [Fact(DisplayName = "P306-SE-07 Cache MISS → ISafetyLayer called; establishes MISS-vs-HIT contrast (WI-B4)")]
    public async Task Handle_CacheMiss_SafetyLayerIsCalled()
    {
        // Arrange — default MISS cache; safety approves.
        const string approvedContent = "Similar example: if 3² + 4² = 5², then a 3-4-5 triangle is right-angled.";

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
                new AiRequest { Prompt = "Give a similar math example", Task = AiTaskKind.Explain }));

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
        var command = new SimilarExampleCommand(SkillId: 20, QuestionId: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert — on a MISS the safety layer MUST be invoked (contrast to SE-06).
        result.Should().BeOfType<SimilarExampleResult.Streamed>(
            "a cache MISS with safety-allowed response must return Streamed content");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "ISafetyLayer must be called exactly once on a cache MISS");
    }

    // ── SE-05: IAiGateway must NOT be injected ───────────────────────────────────

    [Fact(DisplayName = "P306-SE-05 Handler constructor injects ISafetyLayer, never IAiGateway (P302-ARCH-04)")]
    public void Handler_ConstructorDoesNotInject_IAiGateway()
    {
        // Mirrors architecture test P302-ARCH-04 at unit-test level.
        var ctorParams = typeof(SimilarExampleCommandHandler)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        ctorParams.Should().NotContain(
            typeof(IAiGateway),
            "SimilarExampleCommandHandler must NEVER inject IAiGateway directly — " +
            "ISafetyLayer is the sole AI façade (AC-9, P302-ARCH-04)");

        ctorParams.Should().Contain(
            typeof(ISafetyLayer),
            "SimilarExampleCommandHandler must inject ISafetyLayer");
    }

    // ── P10-03-BE-5: No-charge cases for SimilarExample ──────────────────────────

    [Fact(DisplayName = "P1003-SE-P10-01 Insufficient monthly balance → Error; TryDebitAsync never called (P10-03-BE-5)")]
    public async Task Handle_InsufficientMonthlyBalance_ReturnsErrorAndNeverDebits()
    {
        // Arrange — balance=4, cost for SimilarExample=5 → pre-auth blocks.
        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(4, 0, 4, null));

        var contextProvider = new Mock<ILearningContextProvider>();
        var safetyMock = new Mock<ISafetyLayer>();
        var promptMock = new Mock<IPromptBuilder>();

        var sut = CreateSut(contextProvider, safetyMock, promptMock, creditSpendMock: creditSpendMock);
        var command = new SimilarExampleCommand(SkillId: 20, QuestionId: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SimilarExampleResult.Error>(
            "monthly hard-exhausted must surface as a typed Error — locked economy model P10-03");

        var error = (SimilarExampleResult.Error)result;
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

    [Fact(DisplayName = "P1003-SE-P10-02 Safety block → Error; TryDebitAsync never called (P10-03-BE-5 no-charge on block)")]
    public async Task Handle_SafetyBlock_NeverDebits()
    {
        // Arrange — sufficient balance; safety blocks.
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
                new AiRequest { Prompt = "Give a similar math example", Task = AiTaskKind.Explain }));

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: false, Content: null, Verdict: SafetyVerdict.Blocked, Results: Array.Empty<CheckResult>()));

        var sut = CreateSut(contextProvider, safetyMock, promptMock, creditSpendMock: creditSpendMock);
        var command = new SimilarExampleCommand(SkillId: 20, QuestionId: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SimilarExampleResult.Error>("safety block must return Error (AC-9)");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "TryDebitAsync must NOT be called when safety blocks the response (no delivery = no charge)");
    }

    [Fact(DisplayName = "P1003-SE-P10-03 Refuse/redirect (empty chunks) → TryDebitAsync never called (P10-03-BE-5)")]
    public async Task Handle_EmptyContext_NeverDebits()
    {
        // Arrange — empty context → refuse-and-redirect before debit.
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
        var command = new SimilarExampleCommand(SkillId: 20, QuestionId: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SimilarExampleResult.Redirect>("empty context must return Redirect (AC-7)");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "TryDebitAsync must NOT be called on refuse-and-redirect (no delivery = no charge)");
    }

    [Fact(DisplayName = "P1003-SE-P10-04 Cache HIT → exactly one TryDebitAsync call (P10-03-BE-3 delivery point #1)")]
    public async Task Handle_CacheHit_DebitCalledExactlyOnce()
    {
        // Arrange — cache HIT; debit must occur at the cache-HIT delivery point.
        const string cachedContent = "3-4-5 triangle (cached).";

        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null));
        creditSpendMock
            .Setup(c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DebitResult(true, 5, 0, 95, DebitOutcome.Charged));

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
                new AiRequest { Prompt = "Give a similar math example", Task = AiTaskKind.Explain }));

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
        var command = new SimilarExampleCommand(SkillId: 20, QuestionId: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<SimilarExampleResult.Streamed>("cache HIT must return Streamed");

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
}
