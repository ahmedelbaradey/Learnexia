using FluentAssertions;
using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Services;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Modules.Notifications.Infrastructure.Reengagement;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Modules.Notifications.UnitTests;

/// <summary>
/// Dispatcher-level integration tests verifying that the P9-07 global daily push budget gate
/// is enforced inside <see cref="NudgeDispatcher"/> — the single choke point for all 11 handler paths.
///
/// These tests verify:
///   D1  Budget exhausted → push suppressed, in-app inbox row still written.
///   D2  Budget under limit → push sent (arbiter grants).
///   D3  Arbiter throws (Redis outage) → fail-open: push still attempted, inbox still written.
///   D4  ShouldPush=false → arbiter not consulted (parent pref already suppresses push), inbox written.
///
/// Each test uses an EF InMemory DbContext (no Postgres required) + Moq for all interfaces.
/// The <see cref="INudgeArbiter"/> is mocked to return a predetermined grant/suppress result,
/// keeping these tests pure unit tests of the dispatcher's gate logic rather than full-stack.
///
/// Coverage confirms ALL handler paths (legacy + new) are now gated because they all flow
/// through <see cref="NudgeDispatcher.DispatchAsync"/>.
/// </summary>
public sealed class NudgeDispatcherGlobalBudgetTests : IDisposable
{
    private readonly NotificationsDbContext _db;
    private readonly Mock<IDeviceTokenService>     _deviceTokenService = new();
    private readonly Mock<IPushSender>             _pushSender         = new();
    private readonly Mock<INudgeArbiter>           _arbiter            = new();
    private readonly Mock<IGlobalSettingsProvider> _settings           = new();
    private readonly Mock<ISystemClock>            _clock              = new();
    private readonly Mock<ILoggerManager>          _logger             = new();

    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public NudgeDispatcherGlobalBudgetTests()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // isolated per test
            .Options;

        _db = new NotificationsDbContext(options);
        _clock.Setup(c => c.UtcNow).Returns(UtcNow);

        // Default settings: config budget = 4.
        _settings
            .Setup(s => s.GetInt(It.IsAny<string>(), It.IsAny<int>()))
            .Returns((string _, int def) => def);

        // Default: no active device tokens (can be overridden per test).
        _deviceTokenService
            .Setup(d => d.GetActiveTokensAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    public void Dispose() => _db.Dispose();

    // =========================================================================
    // D1 — Budget exhausted → push suppressed; inbox still written
    // =========================================================================

    [Fact(DisplayName = "D1 Budget exhausted → arbiter suppresses push; inbox row is still written")]
    public async Task BudgetExhausted_PushSuppressed_InboxStillWritten()
    {
        // Arrange: arbiter says suppress (budget exhausted).
        _arbiter
            .Setup(a => a.ArbitrateAsync(
                It.IsAny<int>(), It.IsAny<NotificationCategory>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReengagementEvaluator.ArbiterResult(false, ReengagementEvaluator.NotEligibleReason.GlobalBudgetExhausted));

        var dispatcher = BuildDispatcher();
        var message = BuildMessage(shouldPush: true, shouldInApp: true, globalBudget: 4);

        // Act
        await dispatcher.DispatchAsync(message);

        // Assert: one inbox row written.
        var rows = await _db.Notifications.ToListAsync();
        rows.Should().HaveCount(1, "dispatcher always writes inbox row");

        // Push bit must NOT be set because arbiter suppressed it.
        (rows[0].DeliveredChannels & 2).Should().Be(0, "push suppressed = push bitmask not set");

        // InApp bit must be set.
        (rows[0].DeliveredChannels & 4).Should().Be(4, "inApp was enabled");

        // IPushSender must NOT have been called.
        _pushSender.Verify(
            p => p.SendAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(),
                             It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "push must not be attempted when arbiter suppresses");
    }

    // =========================================================================
    // D2 — Budget under limit → arbiter grants; push sent
    // =========================================================================

    [Fact(DisplayName = "D2 Budget under limit → arbiter grants; push attempted (one token)")]
    public async Task BudgetUnderLimit_ArbiterGrants_PushSent()
    {
        // Arrange: arbiter grants push.
        _arbiter
            .Setup(a => a.ArbitrateAsync(
                It.IsAny<int>(), It.IsAny<NotificationCategory>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReengagementEvaluator.ArbiterResult(true, ReengagementEvaluator.NotEligibleReason.None));

        // One active device token.
        _deviceTokenService
            .Setup(d => d.GetActiveTokensAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ActiveTokenInfo(Id: 1, ExpoPushToken: "ExponentPushToken[test]")]);

        // Push sender returns success.
        _pushSender
            .Setup(p => p.SendAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(),
                                    It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushSendResult(Sent: 1, Failed: 0, InvalidTokens: []));

        var dispatcher = BuildDispatcher();
        var message = BuildMessage(shouldPush: true, shouldInApp: true, globalBudget: 4);

        // Act
        await dispatcher.DispatchAsync(message);

        // Assert: inbox row written.
        var rows = await _db.Notifications.ToListAsync();
        rows.Should().HaveCount(1);

        // Both push (2) and inApp (4) bits set.
        (rows[0].DeliveredChannels & 2).Should().Be(2, "push was granted and sent");
        (rows[0].DeliveredChannels & 4).Should().Be(4, "inApp was enabled");

        // Push sender was called once.
        _pushSender.Verify(
            p => p.SendAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(),
                             It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "push sender must be called when arbiter grants");
    }

    // =========================================================================
    // D3 — Arbiter throws → fail-open: push still attempted; inbox still written
    // =========================================================================

    [Fact(DisplayName = "D3 Arbiter throws (Redis outage) → fail-open; push attempted; inbox still written")]
    public async Task ArbiterThrows_FailOpen_PushAttempted_InboxWritten()
    {
        // Arrange: arbiter throws (simulates Redis outage).
        _arbiter
            .Setup(a => a.ArbitrateAsync(
                It.IsAny<int>(), It.IsAny<NotificationCategory>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));

        // One active device token.
        _deviceTokenService
            .Setup(d => d.GetActiveTokensAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ActiveTokenInfo(Id: 1, ExpoPushToken: "ExponentPushToken[test]")]);

        _pushSender
            .Setup(p => p.SendAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(),
                                    It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushSendResult(Sent: 1, Failed: 0, InvalidTokens: []));

        var dispatcher = BuildDispatcher();
        var message = BuildMessage(shouldPush: true, shouldInApp: true, globalBudget: 4);

        // Act
        await dispatcher.DispatchAsync(message);

        // Assert: inbox row written.
        var rows = await _db.Notifications.ToListAsync();
        rows.Should().HaveCount(1, "fail-open must not break inbox write");

        // Push should have been attempted (fail-open = allow through).
        _pushSender.Verify(
            p => p.SendAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(),
                             It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "fail-open: arbiter outage must not silently drop push");
    }

    // =========================================================================
    // D4 — ShouldPush=false → arbiter NOT consulted; inbox written
    // =========================================================================

    [Fact(DisplayName = "D4 ShouldPush=false (parent pref off) → arbiter not called; inbox row written")]
    public async Task ShouldPushFalse_ArbiterNotCalled_InboxWritten()
    {
        var dispatcher = BuildDispatcher();
        var message = BuildMessage(shouldPush: false, shouldInApp: true, globalBudget: 4);

        // Act
        await dispatcher.DispatchAsync(message);

        // Assert: inbox row written.
        var rows = await _db.Notifications.ToListAsync();
        rows.Should().HaveCount(1);

        // Push bit NOT set.
        (rows[0].DeliveredChannels & 2).Should().Be(0, "push was suppressed by parent pref");

        // Arbiter must NOT have been called (no point consulting budget when prefs say no push).
        _arbiter.Verify(
            a => a.ArbitrateAsync(It.IsAny<int>(), It.IsAny<NotificationCategory>(), It.IsAny<string>(),
                                  It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "arbiter should not be consulted when parent pref already suppresses push");
    }

    // =========================================================================
    // D5 — Budget carried from message (per-child column) → arbiter receives it
    // =========================================================================

    [Fact(DisplayName = "D5 Message carries per-child budget → arbiter receives that budget, not config default")]
    public async Task PerChildBudget_InMessage_PassedToArbiter()
    {
        const int perChildBudget = 2; // tighter than config default of 4

        int capturedBudget = -1;
        _arbiter
            .Setup(a => a.ArbitrateAsync(
                It.IsAny<int>(), It.IsAny<NotificationCategory>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<int, NotificationCategory, string, int, DateTime, CancellationToken>(
                (_, _, _, budget, _, _) => capturedBudget = budget)
            .ReturnsAsync(new ReengagementEvaluator.ArbiterResult(true, ReengagementEvaluator.NotEligibleReason.None));

        // No active tokens — push will be attempted but find nothing (still proves arbiter was called with right budget).
        _deviceTokenService
            .Setup(d => d.GetActiveTokensAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var dispatcher = BuildDispatcher();
        var message = BuildMessage(shouldPush: true, shouldInApp: true, globalBudget: perChildBudget);

        await dispatcher.DispatchAsync(message);

        capturedBudget.Should().Be(perChildBudget,
            "dispatcher must pass the per-child budget carried in the message to the arbiter");
    }

    // =========================================================================
    // D6 — Null budget in message → dispatcher falls back to config default
    // =========================================================================

    [Fact(DisplayName = "D6 Null GlobalDailyPushBudget in message → dispatcher uses config default (4)")]
    public async Task NullBudgetInMessage_FallsBackToConfigDefault()
    {
        const int configDefault = 4;

        // Settings returns 4 for the budget key.
        _settings
            .Setup(s => s.GetInt("Notifications:GlobalDailyPushBudget", It.IsAny<int>()))
            .Returns(configDefault);

        int capturedBudget = -1;
        _arbiter
            .Setup(a => a.ArbitrateAsync(
                It.IsAny<int>(), It.IsAny<NotificationCategory>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<int, NotificationCategory, string, int, DateTime, CancellationToken>(
                (_, _, _, budget, _, _) => capturedBudget = budget)
            .ReturnsAsync(new ReengagementEvaluator.ArbiterResult(true, ReengagementEvaluator.NotEligibleReason.None));

        _deviceTokenService
            .Setup(d => d.GetActiveTokensAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var dispatcher = BuildDispatcher();
        // Message carries null budget (legacy handler path).
        var message = BuildMessage(shouldPush: true, shouldInApp: true, globalBudget: null);

        await dispatcher.DispatchAsync(message);

        capturedBudget.Should().Be(configDefault,
            "null GlobalDailyPushBudget in message must fall back to config default");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private NudgeDispatcher BuildDispatcher()
        => new(
            _db,
            _deviceTokenService.Object,
            _pushSender.Object,
            _arbiter.Object,
            _settings.Object,
            _clock.Object,
            _logger.Object);

    private static NudgeMessage BuildMessage(
        bool shouldPush,
        bool shouldInApp,
        int? globalBudget)
        => new(
            RecipientChildUserId:  42,
            ParentId:              1,
            Category:              NotificationCategory.StreakAtRisk,
            Code:                  "STREAK_AT_RISK",
            Title:                 "Test title",
            Body:                  "Test body",
            DataJson:              null,
            ShouldPush:            shouldPush,
            ShouldInApp:           shouldInApp,
            GlobalDailyPushBudget: globalBudget);
}

