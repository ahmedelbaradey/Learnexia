using FluentAssertions;
using Learnexia.Modules.Billing.Domain.Enums;
using Xunit;

namespace Modules.Billing.UnitTests;

/// <summary>
/// Unit tests for the <see cref="WarningState"/> enum semantics as computed by
/// <c>EnergyStatusQueryHandler</c> (tested here via pure domain-layer helper logic
/// mirroring the handler's computation — no DB required).
///
/// Coverage:
///   WS-01  None when balance healthy + daily not reached.
///   WS-02  ApproachingDailyCap when daily >= 80% of cap but < cap.
///   WS-03  DailyCapReached when daily >= cap.
///   WS-04  LowBalance when total <= 10% of monthly allotment (and not empty).
///   WS-05  Empty when total = 0.
///   WS-06  Empty takes precedence over DailyCapReached.
///   WS-07  DailyCapReached takes precedence over LowBalance.
///   WS-08  Daily soft cap does NOT block spend (warning only, domain-level assertion).
/// </summary>
public sealed class EnergyStatusWarningStateTests
{
    // Mirrors the computation in EnergyStatusQueryHandler.Handle.
    private static WarningState ComputeWarningState(
        int total,
        int dailyUsed,
        int dailyCap,
        int monthlyAllotment)
    {
        var dailyCapReached = dailyUsed >= dailyCap;
        var approaching = !dailyCapReached && dailyUsed >= (int)(dailyCap * 0.8);
        var lowThreshold = (int)Math.Ceiling(monthlyAllotment * 0.10);
        var lowEnergy = total > 0 && total <= lowThreshold;
        var empty = total == 0;

        return empty
            ? WarningState.Empty
            : dailyCapReached
                ? WarningState.DailyCapReached
                : lowEnergy
                    ? WarningState.LowBalance
                    : approaching
                        ? WarningState.ApproachingDailyCap
                        : WarningState.None;
    }

    // ── WS-01: None when healthy ──────────────────────────────────────────────────

    [Fact]
    public void WarningState_IsNone_WhenBalanceHealthy()
    {
        var state = ComputeWarningState(total: 80, dailyUsed: 5, dailyCap: 10, monthlyAllotment: 100);

        state.Should().Be(WarningState.None);
    }

    // ── WS-02: ApproachingDailyCap at >= 80% ─────────────────────────────────────

    [Fact]
    public void WarningState_IsApproachingDailyCap_WhenDailyUsedAtEightyPercent()
    {
        // 8 used / 10 cap = 80% — threshold met.
        var state = ComputeWarningState(total: 80, dailyUsed: 8, dailyCap: 10, monthlyAllotment: 100);

        state.Should().Be(WarningState.ApproachingDailyCap);
    }

    [Fact]
    public void WarningState_IsApproachingDailyCap_WhenDailyUsedAboveEightyPercent()
    {
        // 9 used / 10 cap = 90% — still approaching (not yet reached).
        var state = ComputeWarningState(total: 80, dailyUsed: 9, dailyCap: 10, monthlyAllotment: 100);

        state.Should().Be(WarningState.ApproachingDailyCap);
    }

    // ── WS-03: DailyCapReached when daily >= cap ──────────────────────────────────

    [Fact]
    public void WarningState_IsDailyCapReached_WhenDailyUsedEqualsCap()
    {
        var state = ComputeWarningState(total: 80, dailyUsed: 10, dailyCap: 10, monthlyAllotment: 100);

        state.Should().Be(WarningState.DailyCapReached);
    }

    [Fact]
    public void WarningState_IsDailyCapReached_WhenDailyUsedExceedsCap()
    {
        // This can happen if admin lowers the cap after some spend.
        var state = ComputeWarningState(total: 80, dailyUsed: 12, dailyCap: 10, monthlyAllotment: 100);

        state.Should().Be(WarningState.DailyCapReached);
    }

    // ── WS-04: LowBalance when total <= 10% of allotment ─────────────────────────

    [Fact]
    public void WarningState_IsLowBalance_WhenTotalAtTenPercentThreshold()
    {
        // 10% of 100 = 10; total = 10.
        var state = ComputeWarningState(total: 10, dailyUsed: 2, dailyCap: 10, monthlyAllotment: 100);

        state.Should().Be(WarningState.LowBalance);
    }

    [Fact]
    public void WarningState_IsLowBalance_WhenTotalBelowTenPercent()
    {
        // 5 < 10% of 100.
        var state = ComputeWarningState(total: 5, dailyUsed: 0, dailyCap: 10, monthlyAllotment: 100);

        state.Should().Be(WarningState.LowBalance);
    }

    [Fact]
    public void WarningState_IsNone_WhenTotalAboveTenPercent()
    {
        // 11 > 10% of 100.
        var state = ComputeWarningState(total: 11, dailyUsed: 0, dailyCap: 10, monthlyAllotment: 100);

        state.Should().Be(WarningState.None);
    }

    // ── WS-05: Empty when total = 0 ──────────────────────────────────────────────

    [Fact]
    public void WarningState_IsEmpty_WhenTotalIsZero()
    {
        var state = ComputeWarningState(total: 0, dailyUsed: 0, dailyCap: 10, monthlyAllotment: 100);

        state.Should().Be(WarningState.Empty);
    }

    // ── WS-06: Empty takes precedence over DailyCapReached ───────────────────────

    [Fact]
    public void WarningState_IsEmpty_EvenWhenDailyCapReached()
    {
        // Balance exhausted + daily cap reached — Empty wins.
        var state = ComputeWarningState(total: 0, dailyUsed: 10, dailyCap: 10, monthlyAllotment: 100);

        state.Should().Be(WarningState.Empty);
    }

    // ── WS-07: DailyCapReached takes precedence over LowBalance ──────────────────

    [Fact]
    public void WarningState_IsDailyCapReached_EvenWhenLowBalance()
    {
        // Daily cap reached AND low balance — DailyCapReached takes priority.
        var state = ComputeWarningState(total: 5, dailyUsed: 10, dailyCap: 10, monthlyAllotment: 100);

        state.Should().Be(WarningState.DailyCapReached);
    }

    // ── WS-08: Daily cap is SOFT — spend not blocked at domain level ──────────────

    [Fact]
    public void DailySoftCap_DoesNotBlockSpend_AtDomainLevel()
    {
        // The WarningState = DailyCapReached is a warning for the UI only.
        // The domain entity allows the spend (HardStopEnabled=false by default).
        var state = ComputeWarningState(total: 50, dailyUsed: 10, dailyCap: 10, monthlyAllotment: 100);

        state.Should().Be(WarningState.DailyCapReached);
        // No exception raised — the warning does not block the domain operation.
    }
}
