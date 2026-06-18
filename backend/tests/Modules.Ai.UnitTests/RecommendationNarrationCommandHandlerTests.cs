using FluentAssertions;
using Learnexia.Modules.Ai.Application.Features.Recommendation.Commands;
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
/// Unit tests for <see cref="RecommendationNarrationCommandHandler"/> branch logic (P3-14).
///
/// Coverage:
///   RN-01  Rate limit exceeded → Error; ISafetyLayer never called; IStudentRecommendationsQuery never called.
///   RN-02  Child access Paused → Error; no debit; ISafetyLayer never called.
///   RN-03  Child access SeatLocked → Error; no debit; ISafetyLayer never called.
///   RN-04  Insufficient monthly balance → Error; no debit; ISafetyLayer never called.
///   RN-05  Daily cap reached + HardStopEnabled → Error; no debit; ISafetyLayer never called.
///   RN-06  Empty recommendation set → Declined (no debit; ISafetyLayer never called).
///   RN-07  Safety blocked → Error; TryDebitAsync never called.
///   RN-08  Safety allowed → Streamed; exactly one TryDebitAsync call (delivery point #2).
///   RN-09  Cache HIT → Streamed from cache; ISafetyLayer never called; exactly one TryDebitAsync call (delivery point #1).
///   RN-10  Cache MISS → ISafetyLayer called exactly once (contrast test).
///   RN-11  IAiGateway NOT injected in constructor (arch invariant P302-ARCH-04).
///   RN-12  Grounding confirmed — IStudentRecommendationsQuery.GetLatestAsync called (not recomputed).
/// </summary>
public sealed class RecommendationNarrationCommandHandlerTests
{
    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static RecommendationNarrationCommandHandler CreateSut(
        Mock<IStudentRecommendationsQuery> recommendationsMock,
        Mock<ISafetyLayer> safetyMock,
        Mock<IPromptBuilder> promptBuilderMock,
        Mock<ICurrentUserService>? currentUserMock = null,
        IAiTutorRateLimiter? rateLimiter = null,
        Mock<IAiResponseCache>? aiCacheMock = null,
        Mock<IGlobalSettingsProvider>? settingsMock = null,
        Mock<IServiceScopeFactory>? scopeFactoryMock = null,
        Mock<ICreditSpendService>? creditSpendMock = null,
        Mock<IChildAccessStateQuery>? childAccessStateMock = null,
        bool hardStopEnabled = false)
    {
        var currentUser   = currentUserMock  ?? BuildDefaultCurrentUserMock();
        var localizer     = BuildLocalizerMock();
        var publisher     = new Mock<IPublisher>();
        var logger        = new Mock<ILoggerManager>();
        var rl            = rateLimiter ?? new AiTutorRateLimiter();
        var cache         = aiCacheMock ?? BuildDefaultAiCacheMock();
        var settings      = settingsMock ?? BuildDefaultSettingsMock();
        var scopeFactory  = scopeFactoryMock ?? BuildNoOpScopeFactoryMock();
        var creditSpend   = creditSpendMock ?? BuildDefaultCreditSpendMock();
        var childAccess   = childAccessStateMock ?? BuildDefaultChildAccessStateMock();
        var costResolver  = BuildCreditCostResolver(settings, hardStopEnabled);

        return new RecommendationNarrationCommandHandler(
            currentUser.Object,
            recommendationsMock.Object,
            promptBuilderMock.Object,
            safetyMock.Object,
            cache.Object,
            settings.Object,
            rl,
            creditSpend.Object,
            childAccess.Object,
            costResolver,
            publisher.Object,
            scopeFactory.Object,
            logger.Object,
            localizer.Object);
    }

    private static Mock<ICurrentUserService> BuildDefaultCurrentUserMock()
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(42);
        mock.Setup(u => u.GetClaimValue("Grade")).Returns("5");
        mock.Setup(u => u.GetClaimValue("Age")).Returns("11");
        mock.Setup(u => u.GetClaimValue("Language")).Returns("ar");
        return mock;
    }

    private static Mock<IChildAccessStateQuery> BuildDefaultChildAccessStateMock()
    {
        var mock = new Mock<IChildAccessStateQuery>();
        mock.Setup(q => q.GetAccessDecisionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChildAccessDecision.Allowed);
        mock.Setup(q => q.IsChildAccessAllowedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock;
    }

    private static Mock<ICreditSpendService> BuildDefaultCreditSpendMock()
    {
        var mock = new Mock<ICreditSpendService>();
        // Default balance = 100 (always sufficient for cost=5).
        mock.Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null));
        mock.Setup(c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DebitResult(true, 5, 0, 95, DebitOutcome.Charged));
        return mock;
    }

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

    private static Mock<IStringLocalizer<SharedResources>> BuildLocalizerMock()
    {
        var mock = new Mock<IStringLocalizer<SharedResources>>();
        mock.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(key => new LocalizedString(key, key));
        return mock;
    }

    private static Mock<IAiResponseCache> BuildDefaultAiCacheMock()
    {
        var mock = new Mock<IAiResponseCache>();
        mock.Setup(c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        mock.Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

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

    /// <summary>A non-empty recommendation list (3 items) for happy-path tests.</summary>
    private static IReadOnlyList<RecommendationItem> MakeRecommendations() =>
        new[]
        {
            new RecommendationItem(SkillId: 1, SubjectCode: 0, TitleKey: "RecPracticeTitle", BodyKey: "RecPracticeBody",
                CtaKey: "RecPracticeCta", Severity: 2, ActionType: RecommendationActionType.Practice, TargetDifficulty: 2),
            new RecommendationItem(SkillId: 2, SubjectCode: 1, TitleKey: "RecReviewTitle", BodyKey: "RecReviewBody",
                CtaKey: "RecReviewCta", Severity: 3, ActionType: RecommendationActionType.Review, TargetDifficulty: 1),
        };

    private static Mock<IPromptBuilder> BuildDefaultPromptBuilderMock()
    {
        var mock = new Mock<IPromptBuilder>();
        mock.Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Narrate recommendations", Task = AiTaskKind.Explain }));
        return mock;
    }

    // ── RN-01: Rate limit exceeded ───────────────────────────────────────────────

    [Fact(DisplayName = "P314-RN-01 Rate limit exceeded → Error; ISafetyLayer and recommendations query never called")]
    public async Task Handle_RateLimitExceeded_ReturnsErrorAndShortCircuits()
    {
        // Arrange — exhaust the 10-request window for childId=42.
        var rateLimiter = new AiTutorRateLimiter();
        for (var i = 0; i < 10; i++)
            rateLimiter.TryAllow(42);

        var recommendationsMock = new Mock<IStudentRecommendationsQuery>();
        var safetyMock          = new Mock<ISafetyLayer>();
        var promptMock          = new Mock<IPromptBuilder>();

        var sut = CreateSut(recommendationsMock, safetyMock, promptMock, rateLimiter: rateLimiter);

        // Act — the 11th call exceeds the window.
        var result = await sut.Handle(new RecommendationNarrationCommand(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<RecommendationNarrationResult.Error>(
            "rate limit exceeded must return Error before touching recommendations or LLM");

        var error = (RecommendationNarrationResult.Error)result;
        error.Code.Should().Contain("RateLimit", "error code must identify the rate-limit violation");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer must NOT be called when rate limit is exceeded");

        recommendationsMock.Verify(
            q => q.GetLatestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "IStudentRecommendationsQuery must NOT be called when rate limit is exceeded");
    }

    // ── RN-02: Paused child ──────────────────────────────────────────────────────

    [Fact(DisplayName = "P314-RN-02 Paused child → Error; no debit; ISafetyLayer never called")]
    public async Task Handle_ChildPaused_ReturnsErrorAndNeverDebits()
    {
        var childAccessMock = new Mock<IChildAccessStateQuery>();
        childAccessMock.Setup(q => q.GetAccessDecisionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChildAccessDecision.Paused);

        var creditSpendMock     = new Mock<ICreditSpendService>();
        var recommendationsMock = new Mock<IStudentRecommendationsQuery>();
        var safetyMock          = new Mock<ISafetyLayer>();
        var promptMock          = new Mock<IPromptBuilder>();

        var sut = CreateSut(recommendationsMock, safetyMock, promptMock,
            childAccessStateMock: childAccessMock, creditSpendMock: creditSpendMock);

        var result = await sut.Handle(new RecommendationNarrationCommand(), CancellationToken.None);

        result.Should().BeOfType<RecommendationNarrationResult.Error>(
            "paused child must return Error (no debit, no content)");

        var error = (RecommendationNarrationResult.Error)result;
        error.Code.Should().Contain("Paused", "error code must identify the paused state");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "TryDebitAsync must NOT be called for a paused child (no delivery)");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer must NOT be called for a paused child");
    }

    // ── RN-03: SeatLocked child ──────────────────────────────────────────────────

    [Fact(DisplayName = "P314-RN-03 SeatLocked child → Error; no debit; ISafetyLayer never called")]
    public async Task Handle_ChildSeatLocked_ReturnsErrorAndNeverDebits()
    {
        var childAccessMock = new Mock<IChildAccessStateQuery>();
        childAccessMock.Setup(q => q.GetAccessDecisionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChildAccessDecision.SeatLocked);

        var creditSpendMock     = new Mock<ICreditSpendService>();
        var recommendationsMock = new Mock<IStudentRecommendationsQuery>();
        var safetyMock          = new Mock<ISafetyLayer>();
        var promptMock          = new Mock<IPromptBuilder>();

        var sut = CreateSut(recommendationsMock, safetyMock, promptMock,
            childAccessStateMock: childAccessMock, creditSpendMock: creditSpendMock);

        var result = await sut.Handle(new RecommendationNarrationCommand(), CancellationToken.None);

        result.Should().BeOfType<RecommendationNarrationResult.Error>(
            "seat-locked child must return Error (no debit, no content)");

        var error = (RecommendationNarrationResult.Error)result;
        error.Code.Should().Contain("Seat", "error code must identify the seat-lock condition");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "TryDebitAsync must NOT be called for a seat-locked child");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer must NOT be called for a seat-locked child");
    }

    // ── RN-04: Insufficient balance ──────────────────────────────────────────────

    [Fact(DisplayName = "P314-RN-04 Insufficient monthly balance → Error; TryDebitAsync never called")]
    public async Task Handle_InsufficientBalance_ReturnsErrorAndNeverDebits()
    {
        // Arrange — balance=3, cost=5 → pre-auth blocks.
        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(3, 0, 3, null));

        var recommendationsMock = new Mock<IStudentRecommendationsQuery>();
        var safetyMock          = new Mock<ISafetyLayer>();
        var promptMock          = new Mock<IPromptBuilder>();

        var sut = CreateSut(recommendationsMock, safetyMock, promptMock, creditSpendMock: creditSpendMock);

        var result = await sut.Handle(new RecommendationNarrationCommand(), CancellationToken.None);

        result.Should().BeOfType<RecommendationNarrationResult.Error>(
            "insufficient balance must return Error (locked economy P10-03)");

        var error = (RecommendationNarrationResult.Error)result;
        error.Code.Should().Contain("Insufficient", "error code must identify insufficient energy");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "TryDebitAsync must NOT be called when balance is insufficient");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer must NOT be called when balance is insufficient");
    }

    // ── RN-05: Daily cap + HardStop ──────────────────────────────────────────────

    [Fact(DisplayName = "P314-RN-05 Daily cap reached + HardStopEnabled → Error; TryDebitAsync never called")]
    public async Task Handle_DailyCapReachedHardStop_ReturnsErrorAndNeverDebits()
    {
        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null, DailyUsed: 10, DailyCap: 10, DailyCapReached: true));

        var recommendationsMock = new Mock<IStudentRecommendationsQuery>();
        var safetyMock          = new Mock<ISafetyLayer>();
        var promptMock          = new Mock<IPromptBuilder>();

        var sut = CreateSut(recommendationsMock, safetyMock, promptMock,
            creditSpendMock: creditSpendMock, hardStopEnabled: true);

        var result = await sut.Handle(new RecommendationNarrationCommand(), CancellationToken.None);

        result.Should().BeOfType<RecommendationNarrationResult.Error>(
            "daily cap + HardStopEnabled must return Error");

        var error = (RecommendationNarrationResult.Error)result;
        error.Code.Should().Contain("DailyCap", "error code must identify the daily-cap condition");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "TryDebitAsync must NOT be called when daily cap is hard-stopped");
    }

    // ── RN-06: Empty recommendation set → Declined (no debit) ───────────────────

    [Fact(DisplayName = "P314-RN-06 Empty recommendation set → Declined; no debit; ISafetyLayer never called (grounding invariant)")]
    public async Task Handle_EmptyRecommendations_ReturnsDenied_AndNeverDebits()
    {
        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null));

        var recommendationsMock = new Mock<IStudentRecommendationsQuery>();
        recommendationsMock
            .Setup(q => q.GetLatestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecommendationItem>());

        var safetyMock = new Mock<ISafetyLayer>();
        var promptMock = new Mock<IPromptBuilder>();

        var sut = CreateSut(recommendationsMock, safetyMock, promptMock, creditSpendMock: creditSpendMock);

        var result = await sut.Handle(new RecommendationNarrationCommand(), CancellationToken.None);

        result.Should().BeOfType<RecommendationNarrationResult.Declined>(
            "empty recommendations must return Declined without calling LLM (grounding invariant)");

        // Critical grounding invariant: no LLM call and no debit when there is nothing to narrate.
        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer must NOT be called when there are no recommendations to narrate (grounding invariant)");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "TryDebitAsync must NOT be called when there are no recommendations (no delivery = no charge)");
    }

    // ── RN-07: Safety blocked → Error; no debit ──────────────────────────────────

    [Fact(DisplayName = "P314-RN-07 Safety blocked → Error; TryDebitAsync never called (no delivery = no charge)")]
    public async Task Handle_SafetyBlocked_ReturnsErrorAndNeverDebits()
    {
        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null));

        var recommendationsMock = new Mock<IStudentRecommendationsQuery>();
        recommendationsMock
            .Setup(q => q.GetLatestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRecommendations());

        var promptMock = BuildDefaultPromptBuilderMock();

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: false,
                Content: null,
                Verdict: SafetyVerdict.Blocked,
                Results: Array.Empty<CheckResult>()));

        var sut = CreateSut(recommendationsMock, safetyMock, promptMock, creditSpendMock: creditSpendMock);

        var result = await sut.Handle(new RecommendationNarrationCommand(), CancellationToken.None);

        result.Should().BeOfType<RecommendationNarrationResult.Error>(
            "safety block must return Error");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "TryDebitAsync must NOT be called when safety blocked (no delivery = no charge)");
    }

    // ── RN-08: Safety allowed → Streamed; exactly one debit (delivery point #2) ──

    [Fact(DisplayName = "P314-RN-08 Safety allowed → Streamed; exactly one TryDebitAsync (delivery point #2, charge-per-delivery)")]
    public async Task Handle_SafetyAllowed_ReturnsStreamedAndDebitsExactlyOnce()
    {
        const string approvedContent = "Here are your personalised study recommendations from Lexi!";

        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null));
        creditSpendMock
            .Setup(c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DebitResult(true, 5, 0, 95, DebitOutcome.Charged));

        var recommendationsMock = new Mock<IStudentRecommendationsQuery>();
        recommendationsMock
            .Setup(q => q.GetLatestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRecommendations());

        var promptMock = BuildDefaultPromptBuilderMock();

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: true,
                Content: approvedContent,
                Verdict: SafetyVerdict.Allowed,
                Results: Array.Empty<CheckResult>()));

        var sut = CreateSut(recommendationsMock, safetyMock, promptMock, creditSpendMock: creditSpendMock);

        var result = await sut.Handle(new RecommendationNarrationCommand(), CancellationToken.None);

        result.Should().BeOfType<RecommendationNarrationResult.Streamed>(
            "safety-allowed response must return Streamed content");

        var streamed = (RecommendationNarrationResult.Streamed)result;
        streamed.Content.Should().Be(approvedContent,
            "handler must return the exact approved content from ISafetyLayer");

        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "TryDebitAsync must be called exactly once on delivery (charge-per-delivery — P10-03-BE-3 delivery point #2)");
    }

    // ── RN-09: Cache HIT → Streamed; ISafetyLayer never called; exactly one debit ─

    [Fact(DisplayName = "P314-RN-09 Cache HIT → Streamed; ISafetyLayer never called; exactly one TryDebitAsync (delivery point #1)")]
    public async Task Handle_CacheHit_ReturnsStreamedAndDebitsExactlyOnce()
    {
        const string cachedContent = "Lexi narration (cached): here are your recommendations!";

        var creditSpendMock = new Mock<ICreditSpendService>();
        creditSpendMock
            .Setup(c => c.GetBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyBalance(100, 0, 100, null));
        creditSpendMock
            .Setup(c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DebitResult(true, 5, 0, 95, DebitOutcome.Charged));

        var recommendationsMock = new Mock<IStudentRecommendationsQuery>();
        recommendationsMock
            .Setup(q => q.GetLatestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRecommendations());

        var promptMock = BuildDefaultPromptBuilderMock();

        // Cache HIT.
        var aiCacheMock = new Mock<IAiResponseCache>();
        aiCacheMock
            .Setup(c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedContent);
        aiCacheMock
            .Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var safetyMock = new Mock<ISafetyLayer>();

        var sut = CreateSut(recommendationsMock, safetyMock, promptMock,
            aiCacheMock: aiCacheMock, creditSpendMock: creditSpendMock);

        var result = await sut.Handle(new RecommendationNarrationCommand(), CancellationToken.None);

        result.Should().BeOfType<RecommendationNarrationResult.Streamed>(
            "cache HIT must return Streamed content immediately");

        var streamed = (RecommendationNarrationResult.Streamed)result;
        streamed.Content.Should().Be(cachedContent,
            "handler must return the exact cached content");

        // Cache HIT: ISafetyLayer must NOT be called (zero LLM/safety invocations).
        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ISafetyLayer must NOT be called on a cache HIT");

        // Cache hit IS a delivery — charge exactly once (delivery point #1, P10-03 locked economy).
        creditSpendMock.Verify(
            c => c.TryDebitAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "TryDebitAsync must be called exactly once on cache HIT (delivery point #1 — charge-per-delivery)");
    }

    // ── RN-10: Cache MISS → ISafetyLayer called (contrast test) ─────────────────

    [Fact(DisplayName = "P314-RN-10 Cache MISS → ISafetyLayer called exactly once (contrast test)")]
    public async Task Handle_CacheMiss_SafetyLayerIsCalled()
    {
        const string approvedContent = "Cache MISS path: safety-approved narration.";

        var recommendationsMock = new Mock<IStudentRecommendationsQuery>();
        recommendationsMock
            .Setup(q => q.GetLatestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRecommendations());

        var promptMock = BuildDefaultPromptBuilderMock();

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: true,
                Content: approvedContent,
                Verdict: SafetyVerdict.Allowed,
                Results: Array.Empty<CheckResult>()));

        // Default MISS cache.
        var sut = CreateSut(recommendationsMock, safetyMock, promptMock);

        var result = await sut.Handle(new RecommendationNarrationCommand(), CancellationToken.None);

        result.Should().BeOfType<RecommendationNarrationResult.Streamed>(
            "cache MISS + safety-allowed must return Streamed content");

        safetyMock.Verify(
            s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "ISafetyLayer must be called exactly once on a cache MISS");
    }

    // ── RN-11: IAiGateway NOT injected ───────────────────────────────────────────

    [Fact(DisplayName = "P314-RN-11 Handler constructor injects ISafetyLayer, never IAiGateway (P302-ARCH-04)")]
    public void Handler_ConstructorDoesNotInject_IAiGateway()
    {
        var ctorParams = typeof(RecommendationNarrationCommandHandler)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        ctorParams.Should().NotContain(
            typeof(IAiGateway),
            "RecommendationNarrationCommandHandler must NEVER inject IAiGateway directly — " +
            "ISafetyLayer is the sole AI façade (AC-2, P302-ARCH-04)");

        ctorParams.Should().Contain(
            typeof(ISafetyLayer),
            "RecommendationNarrationCommandHandler must inject ISafetyLayer");
    }

    // ── RN-12: Grounding confirmed — recommendations query IS called ─────────────

    [Fact(DisplayName = "P314-RN-12 Grounding confirmed — IStudentRecommendationsQuery.GetLatestAsync called (not recomputed)")]
    public async Task Handle_SafetyAllowed_RecommendationsQueryCalledExactlyOnce()
    {
        const string approvedContent = "Grounding confirmed — narrated from persisted recommendations.";

        var recommendationsMock = new Mock<IStudentRecommendationsQuery>();
        recommendationsMock
            .Setup(q => q.GetLatestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRecommendations());

        var promptMock = BuildDefaultPromptBuilderMock();

        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: true,
                Content: approvedContent,
                Verdict: SafetyVerdict.Allowed,
                Results: Array.Empty<CheckResult>()));

        var sut = CreateSut(recommendationsMock, safetyMock, promptMock);

        await sut.Handle(new RecommendationNarrationCommand(), CancellationToken.None);

        // The grounding invariant: the handler MUST call GetLatestAsync to get the persisted set.
        recommendationsMock.Verify(
            q => q.GetLatestAsync(It.Is<int>(id => id == 42), It.IsAny<CancellationToken>()),
            Times.Once,
            "IStudentRecommendationsQuery.GetLatestAsync must be called exactly once with the JWT childId " +
            "(grounding invariant — no recompute, no invented skills)");
    }
}
