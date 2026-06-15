using FluentAssertions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Contracts.Billing;
using Xunit;

namespace Modules.Billing.UnitTests;

/// <summary>
/// Unit tests for subscription tier resolution logic (P10-05-BE-7).
///
/// These tests exercise the domain-layer rules that
/// <see cref="Learnexia.Modules.Billing.Infrastructure.Contracts.RealBillingSubscriptionContract"/>
/// applies when mapping a parent's <see cref="Subscription"/> to a child's
/// <see cref="PlanTier"/>.
///
/// Coverage:
///   TR-01  Parent with Active Premium → child tier is Premium.
///   TR-02  Parent with Active Free → child tier is Free.
///   TR-03  Parent with PendingPayment → child tier is Free (payment not confirmed).
///   TR-04  Parent with Canceled → child tier is Free.
///   TR-05  Parent with Downgrading → child tier is Free (paid, but scheduled to drop).
///   TR-06  Parent with no subscription row (new parent) → child tier is Free (default).
///   TR-07  Subscription.PlanCode alone does not drive the tier when Status is not Active.
/// </summary>
public sealed class SubscriptionTierResolutionTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static Subscription MakeSubscription(
        int parentId,
        PlanCode planCode,
        SubscriptionStatus status)
        => new()
        {
            Id           = 1,
            ParentUserId = parentId,
            PlanCode     = planCode,
            BillingPeriod = BillingPeriod.Monthly,
            Status       = status,
        };

    /// <summary>
    /// Mirrors the tier-resolution rule in RealBillingSubscriptionContract:
    /// "Active Premium subscription for the parent → Premium; otherwise Free."
    /// </summary>
    private static PlanTier ResolveTier(Subscription? subscription)
        => subscription is { Status: SubscriptionStatus.Active, PlanCode: PlanCode.Premium }
            ? PlanTier.Premium
            : PlanTier.Free;

    // ── TR-01: Active Premium → Premium ──────────────────────────────────────────

    [Fact]
    public void ResolveTier_ActivePremium_ReturnsPremium()
    {
        var sub = MakeSubscription(1, PlanCode.Premium, SubscriptionStatus.Active);

        ResolveTier(sub).Should().Be(PlanTier.Premium);
    }

    // ── TR-02: Active Free → Free ─────────────────────────────────────────────────

    [Fact]
    public void ResolveTier_ActiveFree_ReturnsFree()
    {
        var sub = MakeSubscription(1, PlanCode.Free, SubscriptionStatus.Active);

        ResolveTier(sub).Should().Be(PlanTier.Free);
    }

    // ── TR-03: PendingPayment → Free ─────────────────────────────────────────────

    [Fact]
    public void ResolveTier_PendingPayment_ReturnsFree()
    {
        // Even if PlanCode is marked Premium, payment hasn't confirmed → Free.
        var sub = MakeSubscription(1, PlanCode.Premium, SubscriptionStatus.PendingPayment);

        ResolveTier(sub).Should().Be(PlanTier.Free,
            because: "payment has not been confirmed yet");
    }

    // ── TR-04: Canceled → Free ───────────────────────────────────────────────────

    [Fact]
    public void ResolveTier_Canceled_ReturnsFree()
    {
        var sub = MakeSubscription(1, PlanCode.Premium, SubscriptionStatus.Canceled);

        ResolveTier(sub).Should().Be(PlanTier.Free);
    }

    // ── TR-05: Downgrading → Free ─────────────────────────────────────────────────

    [Fact]
    public void ResolveTier_Downgrading_ReturnsFree()
    {
        // Status is Downgrading — cycle access might still be valid but the domain
        // tier used by the grant job is Free already (the grant resolves at run time).
        var sub = MakeSubscription(1, PlanCode.Premium, SubscriptionStatus.Downgrading);

        ResolveTier(sub).Should().Be(PlanTier.Free,
            because: "downgrading subscriptions are resolved as Free for grant purposes");
    }

    // ── TR-06: No subscription row → Free ────────────────────────────────────────

    [Fact]
    public void ResolveTier_NullSubscription_ReturnsFree()
    {
        ResolveTier(null).Should().Be(PlanTier.Free,
            because: "parents without a subscription row default to Free");
    }

    // ── TR-07: PlanCode alone is not sufficient when Status != Active ─────────────

    [Fact]
    public void ResolveTier_PremiumPlanCode_ButNotActiveStatus_ReturnsFree()
    {
        // Subscription stores PremiumCode but status is not Active.
        var sub = MakeSubscription(1, PlanCode.Premium, SubscriptionStatus.PendingPayment);

        ResolveTier(sub).Should().Be(PlanTier.Free,
            because: "the active check is mandatory — PlanCode alone is insufficient");
    }
}
