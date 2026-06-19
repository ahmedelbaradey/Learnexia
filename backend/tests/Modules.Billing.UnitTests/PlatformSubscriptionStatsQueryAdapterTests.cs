using FluentAssertions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Queries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
#pragma warning disable CA1707

namespace Modules.Billing.UnitTests;

/// <summary>
/// Unit tests for <see cref="PlatformSubscriptionStatsQueryAdapter"/> (P7-10).
/// Runs against SQLite in-memory — mirrors the ChildEnergyUsageQueryTests pattern.
///
/// Coverage:
///   PS-01  EmptyDb_ReturnsZeroActiveSubscriptions
///   PS-02  ActiveSubscriptions_CountsByPlanCode
///   PS-03  NonActiveSubscriptions_NotCounted
///   PS-04  RevenueNaReason_IsNotNull
///   PS-05  TierBreakdown_SumsPerPlanCode
///   PS-06  FreeAndPremiumMix_CorrectBreakdown
/// </summary>
public sealed class PlatformSubscriptionStatsQueryAdapterTests
{
    // ── SQLite helper ─────────────────────────────────────────────────────────────

    private static BillingDbContext BuildDb()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new BillingDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static PlatformSubscriptionStatsQueryAdapter BuildSut(BillingDbContext db)
        => new(db);

    private static Subscription MakeSubscription(int id, int parentUserId, PlanCode plan, SubscriptionStatus status)
        => new Subscription
        {
            Id            = id,
            ParentUserId  = parentUserId,
            PlanCode      = plan,
            Status        = status,
            BillingPeriod = BillingPeriod.Monthly,
        };

    // ── PS-01: empty database → zero active subscriptions ─────────────────────

    [Fact]
    public async Task PS_01_EmptyDb_ReturnsZeroActiveSubscriptions()
    {
        await using var db  = BuildDb();
        var sut = BuildSut(db);

        var result = await sut.GetPlatformAsync();

        result.TotalActiveSubscriptions.Should().Be(0);
        result.ByTier.Should().BeEmpty();
    }

    // ── PS-02: active subscriptions counted ───────────────────────────────────

    [Fact]
    public async Task PS_02_ActiveSubscriptions_CountsByPlanCode()
    {
        await using var db = BuildDb();

        db.Subscriptions.AddRange(
            MakeSubscription(1, 101, PlanCode.Free,    SubscriptionStatus.Active),
            MakeSubscription(2, 102, PlanCode.Free,    SubscriptionStatus.Active),
            MakeSubscription(3, 103, PlanCode.Premium, SubscriptionStatus.Active));
        await db.SaveChangesAsync();

        var sut    = BuildSut(db);
        var result = await sut.GetPlatformAsync();

        result.TotalActiveSubscriptions.Should().Be(3);
    }

    // ── PS-03: non-active subscriptions not counted ───────────────────────────

    [Fact]
    public async Task PS_03_NonActiveSubscriptions_NotCounted()
    {
        await using var db = BuildDb();

        db.Subscriptions.AddRange(
            MakeSubscription(1, 101, PlanCode.Free,    SubscriptionStatus.Active),
            MakeSubscription(2, 102, PlanCode.Premium, SubscriptionStatus.PendingPayment),
            MakeSubscription(3, 103, PlanCode.Premium, SubscriptionStatus.Canceled),
            MakeSubscription(4, 104, PlanCode.Free,    SubscriptionStatus.Dunning));
        await db.SaveChangesAsync();

        var sut    = BuildSut(db);
        var result = await sut.GetPlatformAsync();

        result.TotalActiveSubscriptions.Should().Be(1);
    }

    // ── PS-04: revenue N/A reason is set ──────────────────────────────────────

    [Fact]
    public async Task PS_04_RevenueNaReason_IsNotNull()
    {
        await using var db = BuildDb();
        var sut = BuildSut(db);

        var result = await sut.GetPlatformAsync();

        result.RevenueNaReason.Should().NotBeNullOrWhiteSpace()
            .And.Contain("N/A");
    }

    // ── PS-05: tier breakdown sums per plan code ───────────────────────────────

    [Fact]
    public async Task PS_05_TierBreakdown_SumsPerPlanCode()
    {
        await using var db = BuildDb();

        db.Subscriptions.AddRange(
            MakeSubscription(1, 101, PlanCode.Free,    SubscriptionStatus.Active),
            MakeSubscription(2, 102, PlanCode.Free,    SubscriptionStatus.Active),
            MakeSubscription(3, 103, PlanCode.Premium, SubscriptionStatus.Active));
        await db.SaveChangesAsync();

        var sut    = BuildSut(db);
        var result = await sut.GetPlatformAsync();

        result.ByTier.Should().HaveCount(2);

        var freeRow = result.ByTier.Single(t => t.PlanCode == nameof(PlanCode.Free));
        freeRow.Count.Should().Be(2);

        var premRow = result.ByTier.Single(t => t.PlanCode == nameof(PlanCode.Premium));
        premRow.Count.Should().Be(1);
    }

    // ── PS-06: free + premium mix correct breakdown ────────────────────────────

    [Fact]
    public async Task PS_06_FreeAndPremiumMix_CorrectBreakdown()
    {
        await using var db = BuildDb();

        // 3 Free active, 2 Premium active, 1 Premium canceled (not counted)
        db.Subscriptions.AddRange(
            MakeSubscription(1, 101, PlanCode.Free,    SubscriptionStatus.Active),
            MakeSubscription(2, 102, PlanCode.Free,    SubscriptionStatus.Active),
            MakeSubscription(3, 103, PlanCode.Free,    SubscriptionStatus.Active),
            MakeSubscription(4, 104, PlanCode.Premium, SubscriptionStatus.Active),
            MakeSubscription(5, 105, PlanCode.Premium, SubscriptionStatus.Active),
            MakeSubscription(6, 106, PlanCode.Premium, SubscriptionStatus.Canceled));
        await db.SaveChangesAsync();

        var sut    = BuildSut(db);
        var result = await sut.GetPlatformAsync();

        result.TotalActiveSubscriptions.Should().Be(5);

        var freeRow = result.ByTier.Single(t => t.PlanCode == nameof(PlanCode.Free));
        freeRow.Count.Should().Be(3);

        var premRow = result.ByTier.Single(t => t.PlanCode == nameof(PlanCode.Premium));
        premRow.Count.Should().Be(2);
    }
}
