using FluentAssertions;
using Learnexia.Modules.Ai.Application.Features.SimilarExample.Commands;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
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
///   - <see cref="IServiceScopeFactory"/>: Moq no-op (fire-and-forget publish never awaited in tests).
///   - <see cref="ILoggerManager"/>: Moq no-op.
///   - <see cref="IStringLocalizer{SharedResources}"/>: returns key as value (test isolation).
///   - <see cref="AiTutorRateLimiter"/>: real instance with fresh state per test.
/// </summary>
public sealed class SimilarExampleCommandHandlerTests
{
    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static SimilarExampleCommandHandler CreateSut(
        Mock<ILearningContextProvider> contextProviderMock,
        Mock<ISafetyLayer> safetyMock,
        Mock<IPromptBuilder> promptBuilderMock,
        Mock<ICurrentUserService>? currentUserMock = null,
        AiTutorRateLimiter? rateLimiter = null)
    {
        var currentUser = currentUserMock ?? BuildDefaultCurrentUserMock();
        var localizer = BuildLocalizerMock();
        var logger = new Mock<ILoggerManager>();
        var redirectBuilder = new RedirectResponseBuilder(localizer.Object);
        var rl = rateLimiter ?? new AiTutorRateLimiter();

        // IServiceScopeFactory: no-op stub — fire-and-forget events are not asserted in unit tests.
        // The test process completes before the background Task.Run can resolve the scope.
        var scopeFactory = BuildNoOpScopeFactoryMock();

        return new SimilarExampleCommandHandler(
            currentUser.Object,
            contextProviderMock.Object,
            promptBuilderMock.Object,
            safetyMock.Object,
            redirectBuilder,
            rl,
            scopeFactory.Object,
            logger.Object,
            localizer.Object);
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

    /// <summary>
    /// Returns a no-op <see cref="IServiceScopeFactory"/> so fire-and-forget event publishes
    /// don't throw during the synchronous test path.
    /// </summary>
    private static Mock<IServiceScopeFactory> BuildNoOpScopeFactoryMock()
    {
        var publisherMock = new Mock<IPublisher>();
        publisherMock
            .Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var spMock = new Mock<IServiceProvider>();
        spMock
            .Setup(sp => sp.GetService(typeof(IPublisher)))
            .Returns(publisherMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);

        var asyncScopeMock = new Mock<IAsyncDisposable>();

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
}
