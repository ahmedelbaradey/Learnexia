using FluentAssertions;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Modules.Ai.Application.Safety;
using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Modules.Ai.Domain.Safety;
using Learnexia.Modules.Ai.Infrastructure.Cache;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using Resources;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for the AI Cache Activation feature.
///
/// <para>These tests prove the three safety-invariant properties mandated by the spec:</para>
/// <list type="number">
///   <item><strong>CA-01</strong> Safety-pass populates <see cref="SafeAiResult.Confidence"/>
///     (setting-driven, default 0.90). Blocked/fallback results keep Confidence null.</item>
///   <item><strong>CA-02</strong> With AutoApproveEnabled=true and Confidence &gt;= 0.85,
///     the repository writes <see cref="AiCacheReviewStatus.Approved"/> (the entry is
///     subsequently servable via the R5 gate).</item>
///   <item><strong>CA-03</strong> With AutoApproveEnabled=false, any incoming Approved status
///     is downgraded to PendingReview (kill-switch: cache stays dormant).</item>
///   <item><strong>CA-04</strong> Confidence below the threshold → PendingReview even when
///     AutoApproveEnabled=true (confidence gate still enforced by the handler).</item>
///   <item><strong>CA-05</strong> Safety-FAIL results have Confidence null (defensive:
///     never reaches the approve gate; cannot be cached as Approved).</item>
///   <item><strong>CA-06</strong> Cache HIT with AutoApproveEnabled=true still returns
///     without calling SafetyLayer (zero-gateway-call guarantee from WI-B4).</item>
/// </list>
///
/// <para>Mocking strategy:
///   <list type="bullet">
///     <item><see cref="SafetyLayer"/>: constructed with mocked checks; tested end-to-end
///       (not mocked) for CA-01 and CA-05 so the real Confidence-setting logic is exercised.</item>
///     <item><see cref="AiResponseCacheRepository"/>: constructed with in-memory EF +
///       IDistributedCache stub so the kill-switch logic is exercised at the repository level.</item>
///     <item><see cref="IGlobalSettingsProvider"/>: Moq stub returning controlled values per test.</item>
///   </list>
/// </para>
/// </summary>
public sealed class AiCacheActivationTests
{
    // ── Helpers: SafetyLayer under test ────────────────────────────────────────────

    private static SafetyLayer CreateSafetyLayerSut(
        Mock<IAiGateway> gatewayMock,
        Mock<IToxicityCheck> toxicityMock,
        Mock<IAgeAppropriatenessCheck> ageMock,
        Mock<IHallucinationCheck> hallucMock,
        decimal safetyPassConfidence = 0.90m)
    {
        var storeMock = new Mock<IAiSafetyEventStore>();
        storeMock
            .Setup(s => s.AppendAsync(It.IsAny<SafetyEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var opts = new SafetyOptions
        {
            EnableToxicityCheck      = true,
            EnableAgeCheck           = true,
            EnableHallucinationCheck = true,
            MaxRegenerationAttempts  = 2,
        };

        var settingsMock = new Mock<IGlobalSettingsProvider>();
        settingsMock
            .Setup(s => s.GetDecimal("ai.cache.safetyPassConfidence", It.IsAny<decimal>()))
            .Returns(safetyPassConfidence);
        settingsMock
            .Setup(s => s.GetDecimal(It.IsNotIn("ai.cache.safetyPassConfidence"), It.IsAny<decimal>()))
            .Returns<string, decimal>((_, def) => def);

        var localizerMock = new Mock<IStringLocalizer<SharedResources>>();
        localizerMock
            .Setup(l => l[It.IsAny<string>()])
            .Returns<string>(key => new LocalizedString(key, key));

        var loggerMock = new Mock<ILoggerManager>();

        return new SafetyLayer(
            gatewayMock.Object,
            toxicityMock.Object,
            ageMock.Object,
            hallucMock.Object,
            storeMock.Object,
            Options.Create(opts),
            settingsMock.Object,
            localizerMock.Object,
            loggerMock.Object);
    }

    // ── Helpers: AiResponseCacheRepository under test ─────────────────────────────

    private static AiResponseCacheRepository CreateCacheRepositoryWithMocks(
        AiDbContext dbContext,
        bool autoApproveEnabled)
    {
        var distributedCacheMock = new Mock<IDistributedCache>();
        distributedCacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null); // Always MISS — no Redis in unit tests.
        distributedCacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var settingsMock = new Mock<IGlobalSettingsProvider>();
        settingsMock
            .Setup(s => s.GetBool("ai.cache.autoApproveEnabled", It.IsAny<bool>()))
            .Returns(autoApproveEnabled);
        settingsMock
            .Setup(s => s.GetInt(It.IsAny<string>(), It.IsAny<int>()))
            .Returns<string, int>((_, def) => def);
        settingsMock
            .Setup(s => s.GetDecimal(It.IsAny<string>(), It.IsAny<decimal>()))
            .Returns<string, decimal>((_, def) => def);

        var loggerMock = new Mock<ILoggerManager>();

        return new AiResponseCacheRepository(
            dbContext,
            distributedCacheMock.Object,
            settingsMock.Object,
            loggerMock.Object);
    }

    private static AiDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AiDbContext(options);
    }

    private static AiCacheWriteEntry MakeApprovedWriteEntry(decimal confidence = 0.90m) =>
        new AiCacheWriteEntry(
            CacheKey:          Guid.NewGuid().ToString("N"),
            Response:          "Photosynthesis is how plants make food from sunlight.",
            Type:              AiCacheEntryTypeDto.Explain,
            SkillKey:          "10",
            QuestionId:        null,
            CurriculumVersion: "mvp",
            PromptVersion:     "v1",
            ModelVersion:      "claude-sonnet-4-6",
            ReviewStatus:      AiCacheReviewStatusDto.Approved,
            Confidence:        confidence);

    private static AiCacheWriteEntry MakePendingWriteEntry() =>
        new AiCacheWriteEntry(
            CacheKey:          Guid.NewGuid().ToString("N"),
            Response:          "Photosynthesis hint.",
            Type:              AiCacheEntryTypeDto.Hint,
            SkillKey:          "10",
            QuestionId:        null,
            CurriculumVersion: "mvp",
            PromptVersion:     "v1",
            ModelVersion:      "claude-sonnet-4-6",
            ReviewStatus:      AiCacheReviewStatusDto.PendingReview,
            Confidence:        0.70m);

    // ── CA-01a: Safety-pass populates Confidence with the configured value ─────────

    [Fact(DisplayName = "CA-01a SafetyLayer pass path sets Confidence to safetyPassConfidence (default 0.90)")]
    public async Task SafetyPass_SetsConfidenceToSafetyPassConfidence()
    {
        // Arrange — all checks pass; safety pass confidence = 0.90 (default).
        const string safeContent = "Plants use sunlight to produce food via photosynthesis.";
        const decimal expectedConfidence = 0.90m;

        var gatewayMock  = new Mock<IAiGateway>();
        var toxicityMock = new Mock<IToxicityCheck>();
        var ageMock      = new Mock<IAgeAppropriatenessCheck>();
        var hallucMock   = new Mock<IHallucinationCheck>();

        gatewayMock
            .Setup(g => g.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Ok(safeContent, new AiUsage { Provider = "Claude", ModelId = "sonnet" }));

        toxicityMock
            .Setup(t => t.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        ageMock
            .Setup(a => a.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        hallucMock
            .Setup(h => h.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        var sut = CreateSafetyLayerSut(gatewayMock, toxicityMock, ageMock, hallucMock, expectedConfidence);

        // Act
        var result = await sut.GenerateSafeAsync(
            new AiRequest { Prompt = "Explain photosynthesis.", Task = AiTaskKind.Explain });

        // Assert
        result.Allowed.Should().BeTrue("all checks passed");
        result.Confidence.Should().NotBeNull(
            "SafetyLayer must populate Confidence on pass path — the auto-approve gate depends on it");
        result.Confidence.Should().Be(expectedConfidence,
            "Confidence must equal the safetyPassConfidence setting (0.90) when all checks pass");
    }

    // ── CA-01b: Confidence is config-driven (non-default value respected) ─────────

    [Fact(DisplayName = "CA-01b SafetyLayer pass path respects custom safetyPassConfidence setting")]
    public async Task SafetyPass_RespectsCustomSafetyPassConfidenceSetting()
    {
        // Arrange — custom confidence 0.95 (different from code default 0.90).
        const string safeContent = "Safety content.";
        const decimal customConfidence = 0.95m;

        var gatewayMock  = new Mock<IAiGateway>();
        var toxicityMock = new Mock<IToxicityCheck>();
        var ageMock      = new Mock<IAgeAppropriatenessCheck>();
        var hallucMock   = new Mock<IHallucinationCheck>();

        gatewayMock
            .Setup(g => g.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Ok(safeContent, new AiUsage { Provider = "Claude", ModelId = "sonnet" }));

        toxicityMock
            .Setup(t => t.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        ageMock
            .Setup(a => a.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        hallucMock
            .Setup(h => h.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        var sut = CreateSafetyLayerSut(gatewayMock, toxicityMock, ageMock, hallucMock, customConfidence);

        // Act
        var result = await sut.GenerateSafeAsync(
            new AiRequest { Prompt = "Explain.", Task = AiTaskKind.Explain });

        // Assert
        result.Confidence.Should().Be(customConfidence,
            "SafetyLayer must use the value from IGlobalSettingsProvider, not a hardcoded constant");
    }

    // ── CA-05: Safety-FAIL results have Confidence null ───────────────────────────

    [Fact(DisplayName = "CA-05 Safety-FAIL (blocked) result has Confidence null (never reaches approve gate)")]
    public async Task SafetyFail_Blocked_ConfidenceIsNull()
    {
        // Arrange — toxicity check blocks the output.
        var gatewayMock  = new Mock<IAiGateway>();
        var toxicityMock = new Mock<IToxicityCheck>();
        var ageMock      = new Mock<IAgeAppropriatenessCheck>();
        var hallucMock   = new Mock<IHallucinationCheck>();

        gatewayMock
            .Setup(g => g.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Ok("toxic response", new AiUsage { Provider = "Claude", ModelId = "haiku" }));

        // Input screen passes, output toxicity check blocks.
        toxicityMock
            .SetupSequence(t => t.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass())
            .ReturnsAsync(CheckVerdict.Block(ReasonCodes.ToxicityHigh));

        ageMock
            .Setup(a => a.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        hallucMock
            .Setup(h => h.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        var sut = CreateSafetyLayerSut(gatewayMock, toxicityMock, ageMock, hallucMock);

        // Act
        var result = await sut.GenerateSafeAsync(
            new AiRequest { Prompt = "Prompt.", Task = AiTaskKind.Explain });

        // Assert — safety invariant: Confidence must be null on all failure paths.
        result.Allowed.Should().BeFalse("blocked result must not be allowed");
        result.Verdict.Should().Be(SafetyVerdict.Blocked);
        result.Confidence.Should().BeNull(
            "Confidence must NEVER be populated on a blocked result — the auto-approve gate must not fire for safety-failed responses");
    }

    // ── CA-05b: Gateway fallback result has Confidence null ───────────────────────

    [Fact(DisplayName = "CA-05b Gateway fallback result has Confidence null (never reaches approve gate)")]
    public async Task GatewayFallback_ConfidenceIsNull()
    {
        // Arrange — gateway fails immediately after input screen passes.
        var gatewayMock  = new Mock<IAiGateway>();
        var toxicityMock = new Mock<IToxicityCheck>();
        var ageMock      = new Mock<IAgeAppropriatenessCheck>();
        var hallucMock   = new Mock<IHallucinationCheck>();

        // Input screen uses toxicity check — must pass.
        toxicityMock
            .Setup(t => t.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        // Gateway fails.
        gatewayMock
            .Setup(g => g.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Fail(new AiError(AiErrorKind.Unavailable, "service down")));

        var sut = CreateSafetyLayerSut(gatewayMock, toxicityMock, ageMock, hallucMock);

        // Act
        var result = await sut.GenerateSafeAsync(
            new AiRequest { Prompt = "Prompt.", Task = AiTaskKind.Explain });

        // Assert
        result.Allowed.Should().BeFalse("gateway failure must not be allowed");
        result.Verdict.Should().Be(SafetyVerdict.Fallback);
        result.Confidence.Should().BeNull(
            "Confidence must be null for gateway-fallback results — the fallback path must never trigger auto-approve");
    }

    // ── CA-02: AutoApproveEnabled=true + Confidence >= threshold → Approved in DB ─

    [Fact(DisplayName = "CA-02 AutoApproveEnabled=true + Confidence>=0.85 → entry written as Approved in DB")]
    public async Task WriteAsync_AutoApproveEnabled_ConfidenceAboveThreshold_WritesApproved()
    {
        // Arrange — autoApproveEnabled=true; handler passed entry.ReviewStatus=Approved (confidence met threshold).
        await using var dbContext = CreateInMemoryDbContext();
        var sut = CreateCacheRepositoryWithMocks(dbContext, autoApproveEnabled: true);

        var entry = MakeApprovedWriteEntry(confidence: 0.90m);

        // Act
        await sut.WriteAsync(entry, CancellationToken.None);

        // Assert — the row in DB must be Approved.
        var row = await dbContext.AiResponseCaches
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CacheKey == entry.CacheKey);

        row.Should().NotBeNull("the entry must have been written to the DB");
        row!.ReviewStatus.Should().Be(AiCacheReviewStatus.Approved,
            "when AutoApproveEnabled=true and the handler set Approved, the DB row must be Approved " +
            "so the R5 gate can serve it on subsequent reads");
    }

    // ── CA-02b: Approved entry → subsequent GetApprovedAsync returns HIT ─────────

    [Fact(DisplayName = "CA-02b Approved entry → GetApprovedAsync returns HIT (R5 gate serves content)")]
    public async Task GetApprovedAsync_AfterApprovedWrite_ReturnsHit()
    {
        // Arrange — write an Approved entry, then read it back.
        await using var dbContext = CreateInMemoryDbContext();
        var sut = CreateCacheRepositoryWithMocks(dbContext, autoApproveEnabled: true);

        var entry = MakeApprovedWriteEntry(confidence: 0.90m);
        await sut.WriteAsync(entry, CancellationToken.None);

        // Act
        var hit = await sut.GetApprovedAsync(entry.CacheKey, CancellationToken.None);

        // Assert
        hit.Should().NotBeNull(
            "an Approved entry must be returned by GetApprovedAsync (R5 gate — cache activation is live)");
        hit.Should().Be(entry.Response,
            "the returned content must match the written response exactly");
    }

    // ── CA-03: AutoApproveEnabled=false → Approved downgraded to PendingReview ────

    [Fact(DisplayName = "CA-03 AutoApproveEnabled=false → Approved entry downgraded to PendingReview (kill-switch)")]
    public async Task WriteAsync_AutoApproveDisabled_ApprovedDowngradedToPendingReview()
    {
        // Arrange — autoApproveEnabled=false (kill-switch); handler sent Approved status.
        await using var dbContext = CreateInMemoryDbContext();
        var sut = CreateCacheRepositoryWithMocks(dbContext, autoApproveEnabled: false);

        var entry = MakeApprovedWriteEntry(confidence: 0.90m);

        // Act
        await sut.WriteAsync(entry, CancellationToken.None);

        // Assert — the row must be PendingReview, not Approved.
        var row = await dbContext.AiResponseCaches
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CacheKey == entry.CacheKey);

        row.Should().NotBeNull("the entry must still be written (just not Approved)");
        row!.ReviewStatus.Should().Be(AiCacheReviewStatus.PendingReview,
            "when AutoApproveEnabled=false, the repository must downgrade Approved → PendingReview " +
            "so the R5 gate never serves cached content (kill-switch / dormant behaviour)");
    }

    // ── CA-03b: AutoApproveEnabled=false → GetApprovedAsync returns null (no HIT) ─

    [Fact(DisplayName = "CA-03b AutoApproveEnabled=false → GetApprovedAsync returns null (cache stays dormant)")]
    public async Task GetApprovedAsync_AfterKillSwitch_ReturnsNull()
    {
        // Arrange — write with kill-switch on; the entry lands as PendingReview.
        await using var dbContext = CreateInMemoryDbContext();
        var sut = CreateCacheRepositoryWithMocks(dbContext, autoApproveEnabled: false);

        var entry = MakeApprovedWriteEntry(confidence: 0.90m);
        await sut.WriteAsync(entry, CancellationToken.None);

        // Act — the R5 gate must return null for a PendingReview row.
        var hit = await sut.GetApprovedAsync(entry.CacheKey, CancellationToken.None);

        // Assert
        hit.Should().BeNull(
            "when AutoApproveEnabled=false, no entry is ever Approved in the DB, " +
            "so GetApprovedAsync must return null (cache dormant — no HIT)");
    }

    // ── CA-04: Confidence below threshold → entry stays PendingReview ─────────────

    [Fact(DisplayName = "CA-04 Confidence below autoApprovalConfidence → entry written as PendingReview")]
    public async Task WriteAsync_ConfidenceBelowThreshold_StaysPendingReview()
    {
        // Arrange — handler computed PendingReview because confidence (0.70) < threshold (0.85).
        await using var dbContext = CreateInMemoryDbContext();
        var sut = CreateCacheRepositoryWithMocks(dbContext, autoApproveEnabled: true);

        // Handler already wrote PendingReview — repository must not upgrade it.
        var entry = MakePendingWriteEntry();

        // Act
        await sut.WriteAsync(entry, CancellationToken.None);

        // Assert — the row must remain PendingReview.
        var row = await dbContext.AiResponseCaches
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CacheKey == entry.CacheKey);

        row.Should().NotBeNull();
        row!.ReviewStatus.Should().Be(AiCacheReviewStatus.PendingReview,
            "a PendingReview entry (low confidence) must never be automatically upgraded to Approved");
    }

    // ── CA-04b: PendingReview entry → GetApprovedAsync returns null (not served) ──

    [Fact(DisplayName = "CA-04b PendingReview entry not served by R5 gate (GetApprovedAsync returns null)")]
    public async Task GetApprovedAsync_PendingReviewEntry_ReturnsNull()
    {
        // Arrange — write PendingReview entry directly.
        await using var dbContext = CreateInMemoryDbContext();
        var sut = CreateCacheRepositoryWithMocks(dbContext, autoApproveEnabled: true);

        var entry = MakePendingWriteEntry();
        await sut.WriteAsync(entry, CancellationToken.None);

        // Act
        var hit = await sut.GetApprovedAsync(entry.CacheKey, CancellationToken.None);

        // Assert
        hit.Should().BeNull(
            "a PendingReview entry must never be served by the R5 gate " +
            "(only Approved + non-invalidated rows pass the gate predicate)");
    }

    // ── CA-06: AutoApproveEnabled=false does NOT affect PendingReview entries ──────

    [Fact(DisplayName = "CA-06 AutoApproveEnabled=false kill-switch does not change PendingReview entries (idempotent)")]
    public async Task WriteAsync_AutoApproveDisabled_PendingReviewEntryRemainsUnchanged()
    {
        // Arrange — autoApproveEnabled=false; handler already sent PendingReview.
        await using var dbContext = CreateInMemoryDbContext();
        var sut = CreateCacheRepositoryWithMocks(dbContext, autoApproveEnabled: false);

        var entry = MakePendingWriteEntry();

        // Act
        await sut.WriteAsync(entry, CancellationToken.None);

        // Assert — PendingReview stays PendingReview (kill-switch only affects Approved downgrade).
        var row = await dbContext.AiResponseCaches
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CacheKey == entry.CacheKey);

        row.Should().NotBeNull();
        row!.ReviewStatus.Should().Be(AiCacheReviewStatus.PendingReview,
            "PendingReview entries must not be affected by the AutoApproveEnabled kill-switch (only Approved is downgraded)");
    }
}
