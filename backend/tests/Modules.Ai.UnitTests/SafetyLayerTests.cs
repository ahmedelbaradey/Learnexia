using FluentAssertions;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Modules.Ai.Application.Safety;
using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Modules.Ai.Domain.Safety;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using Resources;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for <see cref="SafetyLayer"/>.
///
/// Coverage (BE-6 gate — all four mandatory paths):
///   SL-01  Toxic output → <see cref="SafetyVerdict.Blocked"/>, SafetyEvent written, fallback returned.
///   SL-02  Safe output → <see cref="SafetyVerdict.Allowed"/>, safe content returned, no SafetyEvent.
///   SL-03  A check throws → <see cref="SafetyVerdict.Blocked"/> (fail-closed guarantee).
///   SL-04  Regeneration path: first output blocked, regenerated output safe → safe returned,
///          bounded by MaxRegenerationAttempts.
///   SL-05  Gateway failure → <see cref="SafetyVerdict.Fallback"/>.
///   SL-06  All checks disabled via config (edge case) → allowed without screening (logged as violation).
///
/// Mocking strategy:
///   - <see cref="IAiGateway"/>: Moq mock. Returns controlled <see cref="AiResult"/> per test.
///   - <see cref="IToxicityCheck"/>, <see cref="IAgeAppropriatenessCheck"/>, <see cref="IHallucinationCheck"/>:
///     Moq mocks. Return controlled <see cref="CheckVerdict"/> per test.
///   - <see cref="IAiSafetyEventStore"/>: Moq mock. Captures AppendAsync calls for assertion.
///   - <see cref="AiDbContext"/>: NOT used directly; the store mock handles persistence.
///   - <see cref="IStringLocalizer{SharedResources}"/>: Moq mock returning keys as values.
///   - <see cref="ILoggerManager"/>: Moq mock (no-op).
/// </summary>
public sealed class SafetyLayerTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static SafetyLayer CreateSut(
        Mock<IAiGateway>               gatewayMock,
        Mock<IToxicityCheck>           toxicityMock,
        Mock<IAgeAppropriatenessCheck> ageMock,
        Mock<IHallucinationCheck>      hallucMock,
        Mock<IAiSafetyEventStore>      storeMock,
        SafetyOptions?                 options = null,
        Mock<IGlobalSettingsProvider>? settingsMock = null)
    {
        var opts = options ?? new SafetyOptions
        {
            EnableToxicityCheck       = true,
            EnableAgeCheck            = true,
            EnableHallucinationCheck  = true,
            MaxRegenerationAttempts   = 2,
        };

        var localizerMock = new Mock<IStringLocalizer<SharedResources>>();
        localizerMock
            .Setup(l => l[It.IsAny<string>()])
            .Returns<string>(key => new LocalizedString(key, key));

        var loggerMock = new Mock<ILoggerManager>();

        // Default settings: return caller-supplied defaults (pass-through behaviour).
        var settings = settingsMock ?? BuildDefaultSettingsMock();

        return new SafetyLayer(
            gatewayMock.Object,
            toxicityMock.Object,
            ageMock.Object,
            hallucMock.Object,
            storeMock.Object,
            Options.Create(opts),
            settings.Object,
            localizerMock.Object,
            loggerMock.Object);
    }

    /// <summary>Default settings mock: returns caller-supplied fallback for every key.</summary>
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

    private static AiRequest MakeRequest(string prompt = "Explain photosynthesis.") =>
        new AiRequest { Prompt = prompt, Task = AiTaskKind.Explain };

    // ── SL-01: Toxic output → Blocked + SafetyEvent written + fallback ─────────

    [Fact(DisplayName = "P302-SL-01 Toxic AI output is blocked, fallback returned, SafetyEvent written")]
    public async Task ToxicOutput_ReturnsBlocked_WithFallback_AndPersistsEvent()
    {
        // Arrange
        var gatewayMock   = new Mock<IAiGateway>();
        var toxicityMock  = new Mock<IToxicityCheck>();
        var ageMock       = new Mock<IAgeAppropriatenessCheck>();
        var hallucMock    = new Mock<IHallucinationCheck>();
        var storeMock     = new Mock<IAiSafetyEventStore>();

        // Gateway returns toxic content.
        gatewayMock.Setup(g => g.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Ok("toxic content here", new AiUsage { Provider = "Claude", ModelId = "haiku" }));

        // Input screen passes (clean prompt).
        toxicityMock.SetupSequence(t => t.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass())           // input screen → pass
            .ReturnsAsync(CheckVerdict.Block(ReasonCodes.ToxicityHigh, 0.97)); // output check → block

        ageMock.Setup(a => a.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        hallucMock.Setup(h => h.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        storeMock.Setup(s => s.AppendAsync(It.IsAny<SafetyEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(gatewayMock, toxicityMock, ageMock, hallucMock, storeMock);

        // Act
        var result = await sut.GenerateSafeAsync(MakeRequest());

        // Assert — verdict
        result.Allowed.Should().BeFalse("toxic content must be blocked");
        result.Verdict.Should().Be(SafetyVerdict.Blocked);
        result.Content.Should().Be(SharedResourcesKey.AiContentBlocked,
            "fallback key is returned as content (localizer mock echoes key)");

        // Assert — content is NOT the toxic original
        result.Content.Should().NotBe("toxic content here",
            "unsafe content must never be returned to the student");

        // Confidence must be null on blocked results (never set on any failure path).
        result.Confidence.Should().BeNull(
            "Confidence must be null for blocked results — only set on genuine safety-pass path");

        // Assert — SafetyEvent was written
        storeMock.Verify(s => s.AppendAsync(
            It.Is<SafetyEvent>(e =>
                e.ReasonCodes.Contains(ReasonCodes.ToxicityHigh) &&
                e.FailedChecks.Contains("ToxicityCheck")),
            It.IsAny<CancellationToken>()), Times.Once,
            "a SafetyEvent must be persisted on every block");
    }

    // ── SL-02: Safe output → Allowed, content returned ─────────────────────────

    [Fact(DisplayName = "P302-SL-02 Safe AI output passes through with Allowed verdict")]
    public async Task SafeOutput_ReturnsAllowed_WithContent()
    {
        // Arrange
        var gatewayMock  = new Mock<IAiGateway>();
        var toxicityMock = new Mock<IToxicityCheck>();
        var ageMock      = new Mock<IAgeAppropriatenessCheck>();
        var hallucMock   = new Mock<IHallucinationCheck>();
        var storeMock    = new Mock<IAiSafetyEventStore>();

        const string safeContent = "Photosynthesis is the process by which plants make food using sunlight.";

        gatewayMock.Setup(g => g.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Ok(safeContent, new AiUsage { Provider = "Claude", ModelId = "sonnet" }));

        toxicityMock.Setup(t => t.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        ageMock.Setup(a => a.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        hallucMock.Setup(h => h.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        var sut = CreateSut(gatewayMock, toxicityMock, ageMock, hallucMock, storeMock);

        // Act
        var result = await sut.GenerateSafeAsync(MakeRequest());

        // Assert
        result.Allowed.Should().BeTrue("all checks passed");
        result.Verdict.Should().Be(SafetyVerdict.Allowed);
        result.Content.Should().Be(safeContent, "safe content must be returned unchanged");

        // Confidence must be populated on the pass path (AI Cache Activation requirement).
        result.Confidence.Should().NotBeNull(
            "SafetyLayer must populate Confidence on the pass path so the auto-approve gate can fire");
        result.Confidence.Should().BeGreaterThan(0m,
            "safety-pass confidence must be a positive decimal (config-driven, default 0.90)");

        // No SafetyEvent on pass.
        storeMock.Verify(s => s.AppendAsync(It.IsAny<SafetyEvent>(), It.IsAny<CancellationToken>()),
            Times.Never, "SafetyEvent must NOT be written when content is safe");
    }

    // ── SL-03: Check throws → Blocked (fail-closed) ────────────────────────────

    [Fact(DisplayName = "P302-SL-03 Check exception causes fail-closed Block (never pass on error)")]
    public async Task CheckThrows_ReturnsBlocked_FailClosed()
    {
        // Arrange
        var gatewayMock  = new Mock<IAiGateway>();
        var toxicityMock = new Mock<IToxicityCheck>();
        var ageMock      = new Mock<IAgeAppropriatenessCheck>();
        var hallucMock   = new Mock<IHallucinationCheck>();
        var storeMock    = new Mock<IAiSafetyEventStore>();

        gatewayMock.Setup(g => g.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Ok("some content", new AiUsage { Provider = "Claude", ModelId = "haiku" }));

        // Input screen passes.
        toxicityMock.SetupSequence(t => t.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass())  // input screen → pass
            .ThrowsAsync(new InvalidOperationException("simulated check failure")); // output check → throws

        ageMock.Setup(a => a.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        hallucMock.Setup(h => h.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        storeMock.Setup(s => s.AppendAsync(It.IsAny<SafetyEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(gatewayMock, toxicityMock, ageMock, hallucMock, storeMock);

        // Act
        var result = await sut.GenerateSafeAsync(MakeRequest());

        // Assert
        result.Allowed.Should().BeFalse("a check exception must fail closed → blocked");
        result.Verdict.Should().Be(SafetyVerdict.Blocked,
            "an exception during a check must result in Blocked, never Allowed");
        result.Content.Should().NotBe("some content",
            "unscreened content must never be returned on check error");
    }

    // ── SL-04: Regeneration — first blocked, regenerated output safe ──────────

    [Fact(DisplayName = "P302-SL-04 Regeneration: first output blocked, regenerated safe output returned")]
    public async Task Regeneration_FirstBlocked_RegeneratedSafe_ReturnsSafe()
    {
        // Arrange
        var gatewayMock  = new Mock<IAiGateway>();
        var toxicityMock = new Mock<IToxicityCheck>();
        var ageMock      = new Mock<IAgeAppropriatenessCheck>();
        var hallucMock   = new Mock<IHallucinationCheck>();
        var storeMock    = new Mock<IAiSafetyEventStore>();

        const string safeRegen = "Photosynthesis: plants convert CO2 + H2O + light into glucose.";

        // First gateway call returns borderline content; second call returns safe content.
        gatewayMock.SetupSequence(g => g.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Ok("borderline content", new AiUsage { Provider = "Claude", ModelId = "haiku" }))
            .ReturnsAsync(AiResult.Ok(safeRegen,            new AiUsage { Provider = "Claude", ModelId = "haiku" }));

        // Input screen passes.
        toxicityMock.Setup(t => t.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        // Age check: returns NeedsRegen on first output, Pass on second.
        ageMock.SetupSequence(a => a.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.NeedsRegen(ReasonCodes.AgeBorderline))
            .ReturnsAsync(CheckVerdict.Pass());

        hallucMock.Setup(h => h.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        storeMock.Setup(s => s.AppendAsync(It.IsAny<SafetyEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(gatewayMock, toxicityMock, ageMock, hallucMock, storeMock,
            new SafetyOptions { MaxRegenerationAttempts = 2 });

        // Act
        var result = await sut.GenerateSafeAsync(MakeRequest());

        // Assert
        result.Allowed.Should().BeTrue("regenerated safe content must be allowed");
        result.Verdict.Should().Be(SafetyVerdict.Allowed);
        result.Content.Should().Be(safeRegen, "the safe regenerated content must be returned");

        // Regeneration event was written.
        storeMock.Verify(s => s.AppendAsync(
            It.Is<SafetyEvent>(e => e.ActionTaken == "Regenerated"),
            It.IsAny<CancellationToken>()), Times.Once,
            "a Regenerated SafetyEvent must be written when regen is triggered");

        // Gateway was called exactly twice (initial + one regen).
        gatewayMock.Verify(g => g.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "gateway must be called once initially and once for regeneration");
    }

    // ── SL-04b: Regeneration bounded — still blocked after max attempts ────────

    [Fact(DisplayName = "P302-SL-04b Regeneration bounded: still blocked after max attempts exhausted")]
    public async Task Regeneration_ExceedsMaxAttempts_ReturnsBlocked()
    {
        // Arrange
        var gatewayMock  = new Mock<IAiGateway>();
        var toxicityMock = new Mock<IToxicityCheck>();
        var ageMock      = new Mock<IAgeAppropriatenessCheck>();
        var hallucMock   = new Mock<IHallucinationCheck>();
        var storeMock    = new Mock<IAiSafetyEventStore>();

        // Gateway always returns borderline content.
        gatewayMock.Setup(g => g.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Ok("always borderline", new AiUsage { Provider = "Claude", ModelId = "haiku" }));

        // Input screen passes.
        toxicityMock.Setup(t => t.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        // Age check always returns NeedsRegen.
        ageMock.Setup(a => a.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.NeedsRegen(ReasonCodes.AgeBorderline));

        hallucMock.Setup(h => h.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        storeMock.Setup(s => s.AppendAsync(It.IsAny<SafetyEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        const int maxAttempts = 1;
        var sut = CreateSut(gatewayMock, toxicityMock, ageMock, hallucMock, storeMock,
            new SafetyOptions { MaxRegenerationAttempts = maxAttempts });

        // Act
        var result = await sut.GenerateSafeAsync(MakeRequest());

        // Assert
        result.Allowed.Should().BeFalse("content must be blocked when max regen attempts exhausted");
        result.Verdict.Should().Be(SafetyVerdict.Blocked);
        result.Content.Should().NotBe("always borderline",
            "borderline content must not be returned after max regen exhausted");

        // Gateway called MaxAttempts+1 times (initial + regen attempts).
        gatewayMock.Verify(g => g.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(maxAttempts + 1),
            $"gateway should be called {maxAttempts + 1} times total (1 initial + {maxAttempts} regen)");
    }

    // ── SL-05: Gateway failure → Fallback ─────────────────────────────────────

    [Fact(DisplayName = "P302-SL-05 Gateway failure returns Fallback verdict (fail-closed)")]
    public async Task GatewayFailure_ReturnsFallback()
    {
        // Arrange
        var gatewayMock  = new Mock<IAiGateway>();
        var toxicityMock = new Mock<IToxicityCheck>();
        var ageMock      = new Mock<IAgeAppropriatenessCheck>();
        var hallucMock   = new Mock<IHallucinationCheck>();
        var storeMock    = new Mock<IAiSafetyEventStore>();

        // Input screen passes.
        toxicityMock.Setup(t => t.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckVerdict.Pass());

        // Gateway fails.
        gatewayMock.Setup(g => g.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Fail(new AiError(AiErrorKind.Unavailable, "service unavailable")));

        var sut = CreateSut(gatewayMock, toxicityMock, ageMock, hallucMock, storeMock);

        // Act
        var result = await sut.GenerateSafeAsync(MakeRequest());

        // Assert
        result.Allowed.Should().BeFalse("gateway failure must result in a non-allowed result");
        result.Verdict.Should().Be(SafetyVerdict.Fallback);
        result.Content.Should().Be(SharedResourcesKey.AiServiceUnavailable,
            "gateway fallback key must be returned as content");
    }
}
