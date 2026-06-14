using FluentAssertions;
using Learnexia.Modules.Ai.Application.Features.Hint.Commands;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using Resources;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for <see cref="GetHintCommandHandler"/> branch logic (P3-05 BE-5).
///
/// Coverage:
///   P305-HH-01  Empty <see cref="LearningContext.Chunks"/> → <see cref="HintResult.Redirect"/>;
///               <see cref="ISafetyLayer"/> never called (AC-3 scope guard).
///   P305-HH-02  Hint intent: generated content contains <c>CorrectAnswer</c>
///               → <see cref="HintResult.Error"/> with NoReveal code (OQ-4 post-generation check).
///   P305-HH-03  Non-empty chunks, <see cref="ISafetyLayer"/> blocks
///               → <see cref="HintResult.Error"/> (AC-6 safe-error).
///   P305-HH-04  Hint intent allowed → <see cref="HintResult.Streamed"/> with CurrentHintLevel
///               and NextHintLevel metadata (AC-1/4 + SSE preamble data).
///   P305-HH-05  WhyWrong intent allowed → <see cref="HintResult.Streamed"/> with null level fields
///               (no hint-level metadata for WhyWrong, AC-2).
///   P305-HH-06  Constructor reflection confirms <see cref="IAiGateway"/> is NOT injected
///               (architecture invariant P302-ARCH-04).
///
/// Mocking strategy:
///   - <see cref="ICurrentUserService"/>: studentId=42, grade=5, age=11, ar.
///   - <see cref="IQuestionAnswerContract"/>: returns CorrectAnswer="4", CurrentHintLevel=1.
///   - <see cref="ILearningContextProvider"/>: controlled context (empty vs populated).
///   - <see cref="IPromptBuilder"/>: returns a fixed <see cref="AiRequest"/> on success paths.
///   - <see cref="ISafetyLayer"/>: controlled <see cref="SafeAiResult"/> (allowed vs blocked).
///   - <see cref="IPublisher"/>: Moq no-op (fire-and-forget; publish count not asserted).
///   - <see cref="ILoggerManager"/>: Moq no-op.
///   - <see cref="IStringLocalizer{SharedResources}"/>: returns key as value (test isolation).
///   - <see cref="AiTutorRateLimiter"/>: real instance with fresh state per test.
///   - <see cref="IOptions{HintOptions}"/>: MaxHintLevels=3 (default).
/// </summary>
public sealed class GetHintCommandHandlerTests
{
    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static GetHintCommandHandler CreateSut(
        Mock<ILearningContextProvider> contextProviderMock,
        Mock<ISafetyLayer> safetyMock,
        Mock<IPromptBuilder> promptBuilderMock,
        Mock<ICurrentUserService>? currentUserMock = null,
        Mock<IQuestionAnswerContract>? questionAnswerMock = null,
        AiTutorRateLimiter? rateLimiter = null,
        int maxHintLevels = 3)
    {
        var currentUser   = currentUserMock   ?? BuildDefaultCurrentUserMock();
        var questionAnswer = questionAnswerMock ?? BuildDefaultQuestionAnswerMock();
        var localizer     = BuildLocalizerMock();
        var publisher     = new Mock<IPublisher>();
        var scopeFactory  = new Mock<IServiceScopeFactory>();
        var logger        = new Mock<ILoggerManager>();
        var redirectBuilder = new RedirectResponseBuilder(localizer.Object);
        var rl = rateLimiter ?? new AiTutorRateLimiter();
        var hintOptions   = Options.Create(new HintOptions { MaxHintLevels = maxHintLevels });

        // Wire up the scope factory mock so fire-and-forget Task.Run bodies do not throw
        // NullReferenceException in unit tests. The returned scope resolves a no-op IPublisher.
        // NOTE: CreateAsyncScope() is an extension method on IServiceScopeFactory that calls
        // CreateScope() internally, so we mock the interface method CreateScope() instead.
        var innerPublisher = new Mock<IPublisher>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(sp => sp.GetService(typeof(IPublisher)))
            .Returns(innerPublisher.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);
        scopeFactory
            .Setup(f => f.CreateScope())
            .Returns(scope.Object);

        return new GetHintCommandHandler(
            currentUser.Object,
            questionAnswer.Object,
            contextProviderMock.Object,
            promptBuilderMock.Object,
            safetyMock.Object,
            redirectBuilder,
            rl,
            publisher.Object,
            scopeFactory.Object,
            logger.Object,
            localizer.Object,
            hintOptions);
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

    /// <summary>
    /// Default stub: CorrectAnswer="4", CurrentHintLevel=1.
    /// Tests that need to trigger the no-reveal check must override CorrectAnswer.
    /// </summary>
    private static Mock<IQuestionAnswerContract> BuildDefaultQuestionAnswerMock(
        string correctAnswer = "4",
        int currentHintLevel = 1)
    {
        var mock = new Mock<IQuestionAnswerContract>();
        mock.Setup(q => q.GetQuestionAnswerAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuestionAnswerDto(correctAnswer, currentHintLevel));
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

    private static LearningContext MakePopulatedContext(string? wrongAnswer = null) =>
        new LearningContext(
            Chunks: new[] { new ChunkDto("chunk-1", "If 2+2=? think about adding two groups of two.") },
            QuestionText: "What is 2 + 2?",
            WrongAnswer: wrongAnswer,
            SkillId: 10,
            QuestionId: 1,
            GradeId: 5,
            SubjectId: (int)Subject.Math,
            Language: TutorLanguage.Ar);

    private static LearningContext MakeEmptyContext() =>
        new LearningContext(
            Chunks: Array.Empty<ChunkDto>(),
            QuestionText: null,
            WrongAnswer: null,
            SkillId: 10,
            QuestionId: 1,
            GradeId: 5,
            SubjectId: (int)Subject.Math,
            Language: TutorLanguage.Ar);

    // ── P305-HH-01: Empty context → Redirect ─────────────────────────────────────

    [Fact(DisplayName = "P305-HH-01 Empty context → Redirect; ISafetyLayer never called (AC-3 scope guard)")]
    public async Task Handle_EmptyContext_ReturnsRedirectAndNeverCallsSafety()
    {
        // Arrange
        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeEmptyContext());

        var safetyMock  = new Mock<ISafetyLayer>();
        var promptMock  = new Mock<IPromptBuilder>();

        var sut = CreateSut(contextProvider, safetyMock, promptMock);
        var command = new GetHintCommand(
            QuestionId: 1, AttemptId: 5,
            Intent: HelperIntent.Hint, HintLevel: null, WrongAnswer: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<HintResult.Redirect>(
            "empty context must return redirect — no LLM call (AC-3)");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer must NOT be called when context chunks are empty");

        promptMock.Verify(
            p => p.Build(It.IsAny<PromptContext>()),
            Times.Never,
            "IPromptBuilder must NOT be called when context chunks are empty");
    }

    // ── P305-HH-02: No-reveal violation → Error ─────────────────────────────────

    [Fact(DisplayName = "P305-HH-02 Hint content contains CorrectAnswer → NoRevealViolation Error (OQ-4)")]
    public async Task Handle_HintContentContainsCorrectAnswer_ReturnsNoRevealError()
    {
        // Arrange — CorrectAnswer = "4"; hint will include "The answer is 4"
        const string correctAnswer  = "4";
        const string hintWithAnswer = "Think about adding: the answer is 4.";

        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePopulatedContext());

        var questionAnswerMock = BuildDefaultQuestionAnswerMock(correctAnswer, currentHintLevel: 1);

        var promptMock = new Mock<IPromptBuilder>();
        promptMock
            .Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Give a hint", Task = AiTaskKind.Hint }));

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: true,
                Content: hintWithAnswer,        // Contains the correct answer "4".
                Verdict: SafetyVerdict.Allowed,
                Results: Array.Empty<CheckResult>()));

        var sut = CreateSut(contextProvider, safetyMock, promptMock, questionAnswerMock: questionAnswerMock);
        var command = new GetHintCommand(
            QuestionId: 1, AttemptId: 5,
            Intent: HelperIntent.Hint, HintLevel: null, WrongAnswer: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<HintResult.Error>(
            "a hint containing the correct answer must be blocked by the no-reveal post-check (OQ-4)");

        var error = (HintResult.Error)result;
        error.Code.Should().Contain("NoReveal",
            "error code must identify the no-reveal violation so P3-12 can show a distinct message");
    }

    // ── P305-HH-03: Safety blocked → Error ──────────────────────────────────────

    [Fact(DisplayName = "P305-HH-03 Safety blocked → Error result with safety code (AC-6)")]
    public async Task Handle_SafetyBlocked_ReturnsError()
    {
        // Arrange
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
                new AiRequest { Prompt = "Give a hint", Task = AiTaskKind.Hint }));

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: false,
                Content: null,
                Verdict: SafetyVerdict.Blocked,
                Results: Array.Empty<CheckResult>()));

        var sut = CreateSut(contextProvider, safetyMock, promptMock);
        var command = new GetHintCommand(
            QuestionId: 1, AttemptId: 5,
            Intent: HelperIntent.Hint, HintLevel: null, WrongAnswer: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<HintResult.Error>(
            "a safety block must surface as a typed Error result (AC-6)");

        var error = (HintResult.Error)result;
        // Handler sets Code = nameof(SharedResourcesKey.ExplainConceptSafetyBlocked)
        // (reused key for safety-blocked events)  — contains "Safety".
        error.Code.Should().Contain("Safety",
            "error code must identify the safety block for the SSE wire event: error frame");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "ISafetyLayer must be called once when context is non-empty");
    }

    // ── P305-HH-04: Hint allowed → Streamed with level metadata ─────────────────

    [Fact(DisplayName = "P305-HH-04 Hint intent allowed → Streamed with CurrentHintLevel + NextHintLevel (AC-1/4)")]
    public async Task Handle_HintAllowed_ReturnsStreamedWithLevelMetadata()
    {
        // Arrange — approved hint content that does NOT contain the correct answer.
        const string approvedHint = "Think about adding two equal groups.";
        const string correctAnswer = "4";       // "4" is not in approvedHint.
        const int currentLevel = 1;

        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePopulatedContext());

        var questionAnswerMock = BuildDefaultQuestionAnswerMock(correctAnswer, currentLevel);

        var promptMock = new Mock<IPromptBuilder>();
        promptMock
            .Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Give a hint", Task = AiTaskKind.Hint }));

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: true,
                Content: approvedHint,
                Verdict: SafetyVerdict.Allowed,
                Results: Array.Empty<CheckResult>()));

        var sut = CreateSut(contextProvider, safetyMock, promptMock,
            questionAnswerMock: questionAnswerMock, maxHintLevels: 3);

        var command = new GetHintCommand(
            QuestionId: 1, AttemptId: 5,
            Intent: HelperIntent.Hint, HintLevel: null, WrongAnswer: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<HintResult.Streamed>(
            "an approved hint with no reveal must return Streamed content (AC-1)");

        var streamed = (HintResult.Streamed)result;
        streamed.Content.Should().Be(approvedHint,
            "handler must return the exact approved content from ISafetyLayer");
        streamed.CurrentHintLevel.Should().Be(currentLevel,
            "CurrentHintLevel must reflect the server-derived level (OQ-2b)");
        streamed.NextHintLevel.Should().Be(currentLevel + 1,
            "NextHintLevel must point to the next escalation step (AC-4, SSE preamble)");
    }

    // ── P305-HH-05: WhyWrong → Streamed without level metadata ─────────────────

    [Fact(DisplayName = "P305-HH-05 WhyWrong intent allowed → Streamed with null hint-level fields (AC-2)")]
    public async Task Handle_WhyWrongAllowed_ReturnsStreamedWithoutLevelMetadata()
    {
        // Arrange
        const string approvedWhyWrong = "Your answer 3 missed the second group — try counting again.";

        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePopulatedContext(wrongAnswer: "3"));

        var promptMock = new Mock<IPromptBuilder>();
        promptMock
            .Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Why is 3 wrong for 2+2?", Task = AiTaskKind.Hint }));

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: true,
                Content: approvedWhyWrong,
                Verdict: SafetyVerdict.Allowed,
                Results: Array.Empty<CheckResult>()));

        var sut = CreateSut(contextProvider, safetyMock, promptMock);
        var command = new GetHintCommand(
            QuestionId: 1, AttemptId: 5,
            Intent: HelperIntent.WhyWrong, HintLevel: null, WrongAnswer: "3");

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<HintResult.Streamed>(
            "an approved WhyWrong response must return Streamed content (AC-2)");

        var streamed = (HintResult.Streamed)result;
        streamed.Content.Should().Be(approvedWhyWrong,
            "handler must return the exact approved content from ISafetyLayer");
        streamed.CurrentHintLevel.Should().BeNull(
            "WhyWrong intent must NOT carry hint-level metadata (AC-2 — no escalation ladder)");
        streamed.NextHintLevel.Should().BeNull(
            "WhyWrong intent must NOT carry NextHintLevel (no preamble frame for WhyWrong)");
    }

    // ── P305-HH-06: IAiGateway must NOT be injected ─────────────────────────────

    [Fact(DisplayName = "P305-HH-06 Handler constructor injects ISafetyLayer, never IAiGateway (P302-ARCH-04)")]
    public void Handler_ConstructorDoesNotInject_IAiGateway()
    {
        // Mirrors architecture test P302-ARCH-04 at unit-test level — separately catchable.
        var ctorParams = typeof(GetHintCommandHandler)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        ctorParams.Should().NotContain(
            typeof(IAiGateway),
            "GetHintCommandHandler must NEVER inject IAiGateway directly — ISafetyLayer is the sole AI facade (P302-ARCH-04)");

        ctorParams.Should().Contain(
            typeof(ISafetyLayer),
            "GetHintCommandHandler must inject ISafetyLayer (safety invariant)");
    }
}
