using FluentAssertions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Queries;
using Learnexia.Shared.Contracts.Billing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
#pragma warning disable CA1707

namespace Modules.Billing.UnitTests;

/// <summary>
/// Unit tests for <see cref="PostgresChildEnergyUsageQuery"/> (<see cref="IChildEnergyUsageQuery"/>).
/// Runs against SQLite in-memory — mirrors the existing Billing test harness pattern.
///
/// Seed note: <c>FamilyEnergyAccount</c> and <c>ChildEnergyAllocation</c> carry an <c>xmin</c>
/// row-version column configured with <c>IsRowVersion() + ValueGeneratedOnAddOrUpdate</c>.
/// SQLite does not auto-generate this column, so inserts must supply it explicitly via raw SQL
/// (same pattern as <see cref="SeatLifecycleTests.SeedWalletAsync"/>).
///
/// Coverage:
///   EU-01  No allocation row → zeroed <see cref="ChildEnergySnapshot"/>, no error.
///   EU-02  Active allocation → Remaining / Allocated / Spent derived correctly.
///   EU-03  PurchasedBalance from the family wallet flows into the snapshot.
///   EU-04  Most-recent allocation row wins when multiple cycles exist.
///   EU-05  GetUsageByKindAsync → empty window returns all-zero counts (4 kinds).
///   EU-06  GetUsageByKindAsync → AiHint spend counted in "hints" bucket.
///   EU-07  GetUsageByKindAsync → AiWhyWrong counted in "explain", AiDeepExplanation in "deep",
///          AiPracticeGeneration in "practice".
///   EU-08  GetUsageByKindAsync → row outside window not counted.
///   EU-09  GetUsageByKindAsync → row belonging to ANOTHER child not counted (isolation).
///   EU-10  GetSnapshotAsync never writes: ChangeTracker stays clean after a call.
///   EU-11  GetUsageByKindAsync returns exactly 4 EnergyUsageByKind entries (always).
/// </summary>
public sealed class ChildEnergyUsageQueryTests
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

    private static PostgresChildEnergyUsageQuery BuildQuery(BillingDbContext db)
        => new(db);

    // ── Seed helpers (raw SQL required for xmin columns — see class comment) ─────

    /// <summary>
    /// Seeds a FamilyEnergyAccount row via raw SQL.
    /// EF's IsRowVersion()+ValueGeneratedOnAddOrUpdate on the xmin column prevents normal EF
    /// inserts under SQLite (Postgres-specific behaviour). Returns the auto-increment Id.
    /// </summary>
    private static async Task<int> SeedWalletAsync(
        BillingDbContext db,
        int parentUserId        = 1,
        int subscriptionBalance = 0,
        int purchasedBalance    = 0)
    {
        var now = DateTime.UtcNow.ToString("o");
        await db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "FamilyEnergyAccounts"
                ("ParentUserId","SubscriptionBalance","PurchasedBalance","xmin",
                 "CreatedAt","CreatedBy")
            VALUES
                ({parentUserId},{subscriptionBalance},{purchasedBalance},1,
                 '{now}',0)
            """);

        return await db.FamilyEnergyAccounts
            .AsNoTracking()
            .Where(w => w.ParentUserId == parentUserId)
            .Select(w => w.Id)
            .FirstAsync();
    }

    /// <summary>
    /// Seeds a ChildEnergyAllocation row via raw SQL. Same xmin reason as SeedWalletAsync.
    /// Returns the allocation's auto-increment Id.
    /// </summary>
    private static async Task<int> SeedAllocationAsync(
        BillingDbContext db,
        int familyEnergyAccountId,
        int childId,
        int allocated,
        int spent         = 0,
        DateTime? cycleStart = null,
        DateTime? cycleEnd   = null)
    {
        var now  = DateTime.UtcNow.ToString("o");
        var cStart = (cycleStart ?? DateTime.UtcNow.AddDays(-15)).ToString("o");
        var cEnd   = (cycleEnd   ?? DateTime.UtcNow.AddDays(15)).ToString("o");

        await db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "ChildEnergyAllocations"
                ("FamilyEnergyAccountId","ChildId","CycleStartUtc","CycleEndUtc",
                 "AllocatedAmount","SpentAmount","xmin",
                 "CreatedAt","CreatedBy")
            VALUES
                ({familyEnergyAccountId},{childId},'{cStart}','{cEnd}',
                 {allocated},{spent},1,
                 '{now}',0)
            """);

        return await db.ChildEnergyAllocations
            .AsNoTracking()
            .Where(a => a.ChildId == childId && a.FamilyEnergyAccountId == familyEnergyAccountId)
            .OrderByDescending(a => a.Id)
            .Select(a => a.Id)
            .FirstAsync();
    }

    private static async Task SeedSpendTransactionAsync(
        BillingDbContext db,
        int childEnergyAllocationId,
        int familyEnergyAccountId,
        CreditReasonCode reasonCode,
        DateTime occurredAt,
        int amount = 1)
    {
        var tx = new CreditTransaction
        {
            ChildEnergyAllocationId   = childEnergyAllocationId,
            FamilyEnergyAccountId     = familyEnergyAccountId,
            Type                      = CreditTransactionType.Spend,
            Pool                      = CreditPool.Granted,
            SourceBucket              = EnergyBucket.Subscription,
            Amount                    = amount,
            ReasonCode                = reasonCode,
            ResultingGrantedBalance   = 0,
            ResultingPurchasedBalance = 0,
            OccurredAtUtc             = occurredAt,
            IdempotencyKey            = Guid.NewGuid().ToString(),
        };
        db.CreditTransactions.Add(tx);
        await db.SaveChangesAsync();
    }

    // ── EU-01: No allocation → zeroed snapshot ────────────────────────────────────

    [Fact]
    public async Task GetSnapshotAsync_NoAllocation_ReturnsZeroedSnapshot()
    {
        var db    = BuildDb();
        var query = BuildQuery(db);

        var result = await query.GetSnapshotAsync(childId: 99);

        result.Remaining.Should().Be(0);
        result.Allocated.Should().Be(0);
        result.Spent.Should().Be(0);
        result.PurchasedBalance.Should().Be(0);
        result.CycleEndsAtUtc.Should().BeNull();
    }

    // ── EU-02: Active allocation → correct derived fields ─────────────────────────

    [Fact]
    public async Task GetSnapshotAsync_WithAllocation_DerivedFieldsCorrect()
    {
        var db       = BuildDb();
        var walletId = await SeedWalletAsync(db, parentUserId: 1, purchasedBalance: 0);
        await SeedAllocationAsync(db, walletId, childId: 1, allocated: 100, spent: 30);
        var query    = BuildQuery(db);

        var result   = await query.GetSnapshotAsync(childId: 1);

        result.Remaining.Should().Be(70);
        result.Allocated.Should().Be(100);
        result.Spent.Should().Be(30);
        result.CycleEndsAtUtc.Should().NotBeNull();
    }

    // ── EU-03: PurchasedBalance flows through ─────────────────────────────────────

    [Fact]
    public async Task GetSnapshotAsync_FamilyHasPurchasedBalance_PopulatedInSnapshot()
    {
        var db       = BuildDb();
        var walletId = await SeedWalletAsync(db, parentUserId: 2, purchasedBalance: 250);
        await SeedAllocationAsync(db, walletId, childId: 2, allocated: 50, spent: 10);
        var query    = BuildQuery(db);

        var result   = await query.GetSnapshotAsync(childId: 2);

        result.PurchasedBalance.Should().Be(250);
    }

    // ── EU-04: Most-recent allocation wins ────────────────────────────────────────

    [Fact]
    public async Task GetSnapshotAsync_MultiCycle_MostRecentCycleWins()
    {
        var db       = BuildDb();
        var walletId = await SeedWalletAsync(db, parentUserId: 3);
        var now      = DateTime.UtcNow;

        // Old cycle
        await SeedAllocationAsync(db, walletId, childId: 3,
            allocated: 200, spent: 150,
            cycleStart: now.AddDays(-40), cycleEnd: now.AddDays(-10));

        // New cycle
        await SeedAllocationAsync(db, walletId, childId: 3,
            allocated: 100, spent: 5,
            cycleStart: now.AddDays(-5), cycleEnd: now.AddDays(25));

        var query  = BuildQuery(db);
        var result = await query.GetSnapshotAsync(childId: 3);

        result.Allocated.Should().Be(100);
        result.Spent.Should().Be(5);
        result.Remaining.Should().Be(95);
    }

    // ── EU-05: Empty window → all-zero usage ─────────────────────────────────────

    [Fact]
    public async Task GetUsageByKindAsync_EmptyWindow_AllZeroCounts()
    {
        var db    = BuildDb();
        var query = BuildQuery(db);
        var from  = DateTime.UtcNow.AddDays(-7);
        var to    = DateTime.UtcNow;

        var result = await query.GetUsageByKindAsync(childId: 10, from, to);

        result.Should().HaveCount(4);
        result.Should().AllSatisfy(e => e.Count.Should().Be(0));
    }

    // ── EU-06: AiHint counted in "hints" ─────────────────────────────────────────

    [Fact]
    public async Task GetUsageByKindAsync_AiHintSpend_CountedInHintsBucket()
    {
        var db           = BuildDb();
        var walletId     = await SeedWalletAsync(db, parentUserId: 11);
        var allocationId = await SeedAllocationAsync(db, walletId, childId: 5, allocated: 100);
        var within       = DateTime.UtcNow.AddDays(-1);
        await SeedSpendTransactionAsync(db, allocationId, walletId, CreditReasonCode.AiHint, within);
        await SeedSpendTransactionAsync(db, allocationId, walletId, CreditReasonCode.AiHint, within);

        var query  = BuildQuery(db);
        var result = await query.GetUsageByKindAsync(
            childId : 5,
            fromUtc : DateTime.UtcNow.AddDays(-7),
            toUtc   : DateTime.UtcNow.AddHours(1));

        result.Single(e => e.Kind == "hints").Count.Should().Be(2);
    }

    // ── EU-07: All four helper kinds counted ─────────────────────────────────────

    [Fact]
    public async Task GetUsageByKindAsync_AllFourKinds_CountedInCorrectBuckets()
    {
        var db           = BuildDb();
        var walletId     = await SeedWalletAsync(db, parentUserId: 12);
        var allocationId = await SeedAllocationAsync(db, walletId, childId: 6, allocated: 200);
        var within       = DateTime.UtcNow.AddDays(-1);

        await SeedSpendTransactionAsync(db, allocationId, walletId, CreditReasonCode.AiHint,               within);
        await SeedSpendTransactionAsync(db, allocationId, walletId, CreditReasonCode.AiWhyWrong,            within);
        await SeedSpendTransactionAsync(db, allocationId, walletId, CreditReasonCode.AiDeepExplanation,     within);
        await SeedSpendTransactionAsync(db, allocationId, walletId, CreditReasonCode.AiPracticeGeneration,  within);

        var query  = BuildQuery(db);
        var result = await query.GetUsageByKindAsync(
            childId : 6,
            fromUtc : DateTime.UtcNow.AddDays(-7),
            toUtc   : DateTime.UtcNow.AddHours(1));

        result.Single(e => e.Kind == "hints"   ).Count.Should().Be(1);
        result.Single(e => e.Kind == "explain" ).Count.Should().Be(1);
        result.Single(e => e.Kind == "deep"    ).Count.Should().Be(1);
        result.Single(e => e.Kind == "practice").Count.Should().Be(1);
    }

    // ── EU-08: Row outside window not counted ─────────────────────────────────────

    [Fact]
    public async Task GetUsageByKindAsync_TransactionOutsideWindow_NotCounted()
    {
        var db           = BuildDb();
        var walletId     = await SeedWalletAsync(db, parentUserId: 13);
        var allocationId = await SeedAllocationAsync(db, walletId, childId: 7, allocated: 100);

        // Before window (10 days ago; window starts 7 days ago)
        await SeedSpendTransactionAsync(db, allocationId, walletId,
            CreditReasonCode.AiHint,
            DateTime.UtcNow.AddDays(-10));

        var query  = BuildQuery(db);
        var result = await query.GetUsageByKindAsync(
            childId : 7,
            fromUtc : DateTime.UtcNow.AddDays(-7),
            toUtc   : DateTime.UtcNow.AddHours(1));

        result.Single(e => e.Kind == "hints").Count.Should().Be(0);
    }

    // ── EU-09: Another child's rows not counted ───────────────────────────────────

    [Fact]
    public async Task GetUsageByKindAsync_OtherChildTransaction_NotCounted()
    {
        var db            = BuildDb();
        var walletId      = await SeedWalletAsync(db, parentUserId: 14);
        var allocChild8Id = await SeedAllocationAsync(db, walletId, childId: 8, allocated: 100);
        var allocChild9Id = await SeedAllocationAsync(db, walletId, childId: 9, allocated: 100);
        var within        = DateTime.UtcNow.AddDays(-1);

        // Only child 9 has a spend
        await SeedSpendTransactionAsync(db, allocChild9Id, walletId, CreditReasonCode.AiHint, within);

        var query  = BuildQuery(db);
        var result = await query.GetUsageByKindAsync(
            childId : 8,
            fromUtc : DateTime.UtcNow.AddDays(-7),
            toUtc   : DateTime.UtcNow.AddHours(1));

        result.Single(e => e.Kind == "hints").Count.Should().Be(0);
    }

    // ── EU-10: GetSnapshotAsync never writes ──────────────────────────────────────

    [Fact]
    public async Task GetSnapshotAsync_NeverWritesToDatabase()
    {
        var db    = BuildDb();
        var query = BuildQuery(db);

        await query.GetSnapshotAsync(childId: 42);

        // ChangeTracker must have NO added/modified entities after a pure read.
        var addedOrModified = db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .ToList();

        addedOrModified.Should().BeEmpty("GetSnapshotAsync is a pure read — no rows may be written");
    }

    // ── EU-11: Always returns exactly 4 kinds ─────────────────────────────────────

    [Fact]
    public async Task GetUsageByKindAsync_AlwaysReturnsFourEntries()
    {
        var db    = BuildDb();
        var query = BuildQuery(db);

        var result = await query.GetUsageByKindAsync(
            childId : 999,
            fromUtc : DateTime.UtcNow.AddDays(-7),
            toUtc   : DateTime.UtcNow);

        result.Should().HaveCount(4);
        result.Select(e => e.Kind).Should()
            .BeEquivalentTo(["hints", "explain", "deep", "practice"]);
    }
}
