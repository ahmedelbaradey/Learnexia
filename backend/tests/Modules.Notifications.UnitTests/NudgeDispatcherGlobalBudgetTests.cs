using FluentAssertions;
using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Services;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Modules.Notifications.Infrastructure.Reengagement;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Modules.Notifications.UnitTests;

/// <summary>
/// Dispatcher-level unit tests verifying that the P9-07 global daily push budget gate
/// is enforced inside <see cref="NudgeDispatcher"/> — the single choke point for all handler paths.
///
/// Budget resolution was previously split: 4 handlers computed it and passed it via
/// <c>NudgeMessage.GlobalDailyPushBudget</c>; 7 legacy handlers passed null and fell back to
/// the config default (ignoring the parent's actual per-child budget). The fix moves resolution
/// entirely into the dispatcher via
/// <see cref="IChildReengagementPreferenceService.GetGlobalDailyPushBudgetAsync"/>, so the
/// parent's configured budget is honoured uniformly for EVERY nudge.
///
/// Coverage:
///   D1  Budget exhausted → push suppressed, in-app inbox row still written.
///   D2  Budget under limit → push sent (arbiter grants).
///   D3  Arbiter throws (Redis outage) → fail-open: push still attempted, inbox still written.
///   D4  ShouldPush=false → arbiter not consulted (parent pref already suppresses push), inbox written.
///   D5  Dispatcher reads per-child budget from prefs service and passes it to the arbiter.
///   D6  Service returns null budget → dispatcher falls back to config default (4).
///   D7  Legacy-handler nudge (no pre-computed budget) is gated by per-child budget:
///       child budget=1, one prior push today → arbiter suppresses; inbox written, push skipped.
///   D8  Dispatcher skips prefs service lookup when ShouldPush=false (no unnecessary DB read).
///
/// Each test uses an EF InMemory DbContext (no Postgres required) + Moq for all interfaces.
/// The <see cref="INudgeArbiter"/> is mocked to return a predetermined grant/suppress result,
/// keeping these tests pure unit tests of the dispatcher's gate logic.
/// </summary>
public sealed class NudgeDispatcherGlobalBudgetTests : IDisposable
{
    private readonly NotificationsDbContext _db;
    private readonly Mock<IChildReengagementPreferenceService> _preferenceService = new();
    private readonly Mock<IDeviceTokenService>                 _deviceTokenService = new();
    private readonly Mock<IPushSender>                         _pushSender         = new();
    private readonly Mock<INudgeArbiter>                       _arbiter            = new();
    private readonly Mock<IGlobalSettingsProvider>             _settings           = new();
    private readonly Mock<ISystemClock>                        _clock              = new();
    private readonly Mock<IPublisher>                          _publisher          = new();
    private readonly Mock<ILoggerManager>                      _logger             = new();

    private const int ChildId   = 42;
    private const int ParentId  = 1;
    private const int ConfigDefault = 4;

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

        // Default: prefs service returns null budget (no per-child setting stored).
        _preferenceService
            .Setup(p => p.GetGlobalDailyPushBudgetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
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
        var message = BuildMessage(shouldPush: true, shouldInApp: true);

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
        var message = BuildMessage(shouldPush: true, shouldInApp: true);

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
        var message = BuildMessage(shouldPush: true, shouldInApp: true);

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
        var message = BuildMessage(shouldPush: false, shouldInApp: true);

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
    // D5 — Dispatcher reads per-child budget from prefs service → arbiter receives it
    // =========================================================================

    [Fact(DisplayName = "D5 Prefs service returns per-child budget → arbiter receives that budget, not config default")]
    public async Task PrefsService_ReturnsPerChildBudget_ArbiterReceivesIt()
    {
        const int perChildBudget = 2; // tighter than config default of 4

        // Service returns per-child budget for this child.
        _preferenceService
            .Setup(p => p.GetGlobalDailyPushBudgetAsync(ParentId, ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)perChildBudget);

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
        var message = BuildMessage(shouldPush: true, shouldInApp: true);

        await dispatcher.DispatchAsync(message);

        capturedBudget.Should().Be(perChildBudget,
            "dispatcher must pass the per-child budget from the prefs service to the arbiter");
    }

    // =========================================================================
    // D6 — Prefs service returns null budget → dispatcher falls back to config default
    // =========================================================================

    [Fact(DisplayName = "D6 Prefs service returns null (no per-child setting) → dispatcher falls back to config default (4)")]
    public async Task PrefsServiceReturnsNull_FallsBackToConfigDefault()
    {
        // Service returns null (no per-child budget stored).
        _preferenceService
            .Setup(p => p.GetGlobalDailyPushBudgetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        // Settings returns 4 for the budget key.
        _settings
            .Setup(s => s.GetInt("Notifications:GlobalDailyPushBudget", It.IsAny<int>()))
            .Returns(ConfigDefault);

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
        var message = BuildMessage(shouldPush: true, shouldInApp: true);

        await dispatcher.DispatchAsync(message);

        capturedBudget.Should().Be(ConfigDefault,
            "null prefs-service result must fall back to config default");
    }

    // =========================================================================
    // D7 — Legacy handler push gated by per-child budget (the core regression test)
    //
    // Scenario: parent sets child budget = 1, one push already sent today.
    // A legacy handler dispatches a nudge with ShouldPush=true (but no budget in message —
    // it never set one, because budget used to be a handler concern).
    // The dispatcher must look up the budget from the prefs service → arbiter receives 1,
    // sees 1 push already sent → suppresses push. Inbox row must still be written.
    // =========================================================================

    [Fact(DisplayName = "D7 Legacy handler path: per-child budget=1, 1 prior push today → arbiter suppresses; inbox written")]
    public async Task LegacyHandler_PerChildBudgetOne_OnePriorPush_PushSuppressed_InboxWritten()
    {
        const int perChildBudget = 1;

        // Parent configured budget = 1 for this child.
        _preferenceService
            .Setup(p => p.GetGlobalDailyPushBudgetAsync(ParentId, ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)perChildBudget);

        // Arbiter returns suppress: budget exhausted (1 push already sent, budget = 1).
        _arbiter
            .Setup(a => a.ArbitrateAsync(
                ChildId,
                It.IsAny<NotificationCategory>(),
                It.IsAny<string>(),
                perChildBudget,         // dispatcher must pass the per-child budget, not config default
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReengagementEvaluator.ArbiterResult(
                false,
                ReengagementEvaluator.NotEligibleReason.GlobalBudgetExhausted));

        var dispatcher = BuildDispatcher();

        // Simulate a legacy handler: dispatch NudgeMessage with ShouldPush=true.
        // A legacy handler never pre-computed the budget — the dispatcher must own resolution.
        var message = BuildMessage(shouldPush: true, shouldInApp: true);

        await dispatcher.DispatchAsync(message);

        // Inbox row must be written (in-app is always durable).
        var rows = await _db.Notifications.ToListAsync();
        rows.Should().HaveCount(1, "inbox row must always be written");

        // Push bit must NOT be set: budget exhausted with per-child budget=1.
        (rows[0].DeliveredChannels & 2).Should().Be(0,
            "push suppressed by per-child budget=1 (1 prior push already sent today)");

        // InApp bit must be set.
        (rows[0].DeliveredChannels & 4).Should().Be(4, "in-app inbox is always written");

        // Push sender must NOT have been called.
        _pushSender.Verify(
            p => p.SendAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(),
                             It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "push sender must not be called when per-child budget is exhausted");

        // Verify the prefs service was called with the correct parentId + childId (not any defaults).
        _preferenceService.Verify(
            p => p.GetGlobalDailyPushBudgetAsync(ParentId, ChildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "dispatcher must look up the per-child budget from the prefs service");
    }

    // =========================================================================
    // D8 — ShouldPush=false → prefs service NOT called (no unnecessary DB read)
    // =========================================================================

    [Fact(DisplayName = "D8 ShouldPush=false → prefs service not called (budget resolution skipped)")]
    public async Task ShouldPushFalse_PrefsServiceNotCalled()
    {
        var dispatcher = BuildDispatcher();
        var message = BuildMessage(shouldPush: false, shouldInApp: true);

        await dispatcher.DispatchAsync(message);

        // Prefs service must NOT have been called — no point reading budget when push is off.
        _preferenceService.Verify(
            p => p.GetGlobalDailyPushBudgetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "budget should not be resolved when ShouldPush=false");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private NudgeDispatcher BuildDispatcher()
        => new(
            _db,
            _preferenceService.Object,
            _deviceTokenService.Object,
            _pushSender.Object,
            _arbiter.Object,
            _settings.Object,
            _clock.Object,
            _publisher.Object,
            _logger.Object);

    private static NudgeMessage BuildMessage(
        bool shouldPush,
        bool shouldInApp)
        => new(
            RecipientChildUserId:  ChildId,
            ParentId:              ParentId,
            Category:              NotificationCategory.StreakAtRisk,
            Code:                  "STREAK_AT_RISK",
            Title:                 "Test title",
            Body:                  "Test body",
            DataJson:              null,
            ShouldPush:            shouldPush,
            ShouldInApp:           shouldInApp);
}
