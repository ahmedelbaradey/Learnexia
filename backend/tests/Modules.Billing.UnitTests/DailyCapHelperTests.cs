using FluentAssertions;
using Learnexia.Modules.Billing.Application.Services;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Moq;
using Xunit;

namespace Modules.Billing.UnitTests;

/// <summary>
/// Unit tests for <see cref="DailyCapHelper"/>.
///
/// Coverage:
///   DC-01  Today() returns correct local date for Africa/Cairo (UTC+2 in summer).
///   DC-02  Today() falls back to UTC when timezone is null or empty.
///   DC-03  Today() falls back to UTC when timezone id is invalid.
///   DC-04  IsStale() returns false when stored date = today.
///   DC-05  IsStale() returns true when stored date is yesterday.
///   DC-06  IsStale() returns true when stored date is null.
///   DC-07  Daily reset does NOT block spend (soft warning — balance check gates hard-stop).
/// </summary>
public sealed class DailyCapHelperTests
{
    // ── DC-01: Today in Africa/Cairo ─────────────────────────────────────────────

    [Fact]
    public void Today_ReturnsCairoLocalDate_WhenUtcIsNearMidnight()
    {
        // UTC 22:30 on 2026-06-14 = Africa/Cairo 00:30 on 2026-06-15 (UTC+2).
        var clock = MockClock(new DateTime(2026, 6, 14, 22, 30, 0, DateTimeKind.Utc));

        var result = DailyCapHelper.Today("Africa/Cairo", clock);

        result.Should().Be("2026-06-15");
    }

    // ── DC-02: Falls back to UTC when timezone is null/empty ─────────────────────

    [Fact]
    public void Today_FallsBackToUtc_WhenTimezoneIdIsNull()
    {
        var clock = MockClock(new DateTime(2026, 6, 14, 22, 30, 0, DateTimeKind.Utc));

        var result = DailyCapHelper.Today(null, clock);

        result.Should().Be("2026-06-14");
    }

    [Fact]
    public void Today_FallsBackToUtc_WhenTimezoneIdIsEmpty()
    {
        var clock = MockClock(new DateTime(2026, 6, 14, 22, 30, 0, DateTimeKind.Utc));

        var result = DailyCapHelper.Today(string.Empty, clock);

        result.Should().Be("2026-06-14");
    }

    // ── DC-03: Falls back to UTC on invalid timezone ──────────────────────────────

    [Fact]
    public void Today_FallsBackToUtc_WhenTimezoneIdIsInvalid()
    {
        var clock = MockClock(new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc));

        var result = DailyCapHelper.Today("NotAValidTZ/Unknown", clock);

        result.Should().Be("2026-06-15");
    }

    // ── DC-04: IsStale false when stored date = today ─────────────────────────────

    [Fact]
    public void IsStale_ReturnsFalse_WhenStoredDateMatchesToday()
    {
        var clock = MockClock(new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc));

        var stale = DailyCapHelper.IsStale("2026-06-15", "UTC", clock);

        stale.Should().BeFalse();
    }

    // ── DC-05: IsStale true when stored date is yesterday ────────────────────────

    [Fact]
    public void IsStale_ReturnsTrue_WhenStoredDateIsYesterday()
    {
        var clock = MockClock(new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc));

        var stale = DailyCapHelper.IsStale("2026-06-14", "UTC", clock);

        stale.Should().BeTrue();
    }

    // ── DC-06: IsStale true when stored date is null ──────────────────────────────

    [Fact]
    public void IsStale_ReturnsTrue_WhenStoredDateIsNull()
    {
        var clock = MockClock(new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc));

        var stale = DailyCapHelper.IsStale(null, "UTC", clock);

        stale.Should().BeTrue();
    }

    // ── DC-07: Daily cap is soft — domain ledger does not enforce it ──────────────

    [Fact]
    public void DailyCap_IsSoftByDefault_DebitNotBlockedByDomain()
    {
        // The domain layer (CreditAccount) has no knowledge of daily caps.
        // The cap warning is surfaced by EnergyStatusQuery. This test documents the design.
        var account = CreditAccount.CreateEmpty(childId: 99);
        account.ApplyGrant(200, DateTime.UtcNow.AddDays(31), CreditReasonCode.MonthlyGrantFree, "grant:99:202606");

        // Spend 15 credits — even if a daily cap were 10, the domain allows it.
        var tx = account.Debit(fromGranted: 15, fromPurchased: 0, CreditReasonCode.AiHint, "spend:99:001");

        account.GrantedBalance.Should().Be(185, "domain does not enforce daily cap");
        tx.Amount.Should().Be(15);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static ISystemClock MockClock(DateTime utcNow)
    {
        var mock = new Mock<ISystemClock>();
        mock.Setup(c => c.UtcNow).Returns(utcNow);
        return mock.Object;
    }
}
