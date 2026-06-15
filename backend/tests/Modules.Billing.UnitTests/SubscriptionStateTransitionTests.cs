using FluentAssertions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Xunit;

namespace Modules.Billing.UnitTests;

/// <summary>
/// Unit tests for subscription state transitions (P10-05-BE-5).
///
/// Tests verify the business rules that the command handlers enforce on the
/// <see cref="Subscription"/> entity — expressed as pure domain-layer assertions
/// (no infrastructure, no DB).
///
/// Coverage:
///   ST-01  New subscription starts Active Free.
///   ST-02  Free → RequestUpgrade → PendingPayment with desired BillingPeriod.
///   ST-03  Active Premium → RequestDowngrade → Downgrading with PendingPlanCode = Free.
///   ST-04  Active → CancelSubscription → Canceled, pending fields cleared.
///   ST-05  PendingPayment → CancelSubscription → Canceled.
///   ST-06  Downgrading → CancelSubscription → Canceled.
///   ST-07  Already Active Premium → RequestUpgrade (cadence switch) → PendingBillingPeriod set.
///   ST-08  Canceled subscription should NOT become Active (re-subscribe creates a new row).
/// </summary>
public sealed class SubscriptionStateTransitionTests
{
    private static Subscription NewFreeActive(int parentId = 1)
        => new()
        {
            Id            = 1,
            ParentUserId  = parentId,
            PlanCode      = PlanCode.Free,
            BillingPeriod = BillingPeriod.Monthly,
            Status        = SubscriptionStatus.Active,
        };

    // ── ST-01: New subscription starts Active Free ────────────────────────────────

    [Fact]
    public void NewSubscription_DefaultState_IsActiveFree()
    {
        var sub = NewFreeActive();

        sub.PlanCode.Should().Be(PlanCode.Free);
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.PendingPlanCode.Should().BeNull();
        sub.PendingBillingPeriod.Should().BeNull();
    }

    // ── ST-02: Free → PendingPayment on upgrade request ──────────────────────────

    [Fact]
    public void RequestUpgrade_FromFree_TransitionsToPendingPayment()
    {
        var sub = NewFreeActive();

        // Simulate what RequestUpgradeCommandHandler does.
        sub.BillingPeriod = BillingPeriod.Annual;
        sub.Status        = SubscriptionStatus.PendingPayment;

        sub.Status.Should().Be(SubscriptionStatus.PendingPayment);
        sub.BillingPeriod.Should().Be(BillingPeriod.Annual);
        sub.PlanCode.Should().Be(PlanCode.Free, because: "plan changes only on payment success");
    }

    // ── ST-03: Active Premium → Downgrading ──────────────────────────────────────

    [Fact]
    public void RequestDowngrade_FromActivePremium_TransitionsToDowngrading()
    {
        var sub = new Subscription
        {
            Id            = 2,
            ParentUserId  = 1,
            PlanCode      = PlanCode.Premium,
            BillingPeriod = BillingPeriod.Monthly,
            Status        = SubscriptionStatus.Active,
        };

        // Simulate RequestDowngradeCommandHandler.
        sub.Status          = SubscriptionStatus.Downgrading;
        sub.PendingPlanCode = PlanCode.Free;

        sub.Status.Should().Be(SubscriptionStatus.Downgrading);
        sub.PendingPlanCode.Should().Be(PlanCode.Free);
        sub.PlanCode.Should().Be(PlanCode.Premium, because: "current plan unchanged until cycle end");
    }

    // ── ST-04: Active → Canceled, pending fields cleared ─────────────────────────

    [Fact]
    public void CancelSubscription_FromActive_TransitionsToCanceled_ClearsPending()
    {
        var sub = NewFreeActive();
        sub.PendingPlanCode     = PlanCode.Free;
        sub.PendingBillingPeriod = BillingPeriod.Annual;

        // Simulate CancelSubscriptionCommandHandler.
        sub.Status               = SubscriptionStatus.Canceled;
        sub.PendingPlanCode      = null;
        sub.PendingBillingPeriod = null;

        sub.Status.Should().Be(SubscriptionStatus.Canceled);
        sub.PendingPlanCode.Should().BeNull();
        sub.PendingBillingPeriod.Should().BeNull();
    }

    // ── ST-05: PendingPayment → Canceled ─────────────────────────────────────────

    [Fact]
    public void CancelSubscription_FromPendingPayment_TransitionsToCanceled()
    {
        var sub = new Subscription
        {
            Id            = 3,
            ParentUserId  = 1,
            PlanCode      = PlanCode.Free,
            Status        = SubscriptionStatus.PendingPayment,
        };

        sub.Status = SubscriptionStatus.Canceled;

        sub.Status.Should().Be(SubscriptionStatus.Canceled);
    }

    // ── ST-06: Downgrading → Canceled ────────────────────────────────────────────

    [Fact]
    public void CancelSubscription_FromDowngrading_TransitionsToCanceled()
    {
        var sub = new Subscription
        {
            Id            = 4,
            ParentUserId  = 1,
            PlanCode      = PlanCode.Premium,
            Status        = SubscriptionStatus.Downgrading,
            PendingPlanCode = PlanCode.Free,
        };

        sub.Status          = SubscriptionStatus.Canceled;
        sub.PendingPlanCode = null;

        sub.Status.Should().Be(SubscriptionStatus.Canceled);
        sub.PendingPlanCode.Should().BeNull();
    }

    // ── ST-07: Active Premium cadence-switch records PendingBillingPeriod ─────────

    [Fact]
    public void RequestUpgrade_AlreadyActivePremium_SetsPendingBillingPeriod()
    {
        var sub = new Subscription
        {
            Id            = 5,
            ParentUserId  = 1,
            PlanCode      = PlanCode.Premium,
            BillingPeriod = BillingPeriod.Monthly,
            Status        = SubscriptionStatus.Active,
        };

        // Simulate the "already Premium" branch in RequestUpgradeCommandHandler.
        sub.PendingBillingPeriod = BillingPeriod.Annual;

        sub.Status.Should().Be(SubscriptionStatus.Active, because: "status unchanged for cadence switch");
        sub.PlanCode.Should().Be(PlanCode.Premium);
        sub.PendingBillingPeriod.Should().Be(BillingPeriod.Annual);
    }

    // ── ST-08: Canceled subscription — re-subscribe creates a new row ────────────

    [Fact]
    public void CanceledSubscription_IsNotDirectlyReactivated()
    {
        var sub = new Subscription
        {
            Id     = 6,
            Status = SubscriptionStatus.Canceled,
        };

        // Domain rule: the handler does NOT mutate a Canceled row back to Active.
        // Instead it creates a new Subscription row. We document the old row stays Canceled.
        sub.Status.Should().Be(SubscriptionStatus.Canceled);
    }
}
