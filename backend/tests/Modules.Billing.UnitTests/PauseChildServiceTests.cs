using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Options;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Services;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
#pragma warning disable CA1707

namespace Modules.Billing.UnitTests;

/// <summary>
/// Focused unit tests for P10-18 PauseChildService correctness.
///
/// All services run against SQLite in-memory — no PostgreSQL required.
///
/// Coverage:
///   PC-01  Pause: family-scope rejection (IDOR) when child does not belong to parent.
///   PC-02  Pause: idempotent no-op when child is already Paused (no ledger entry).
///   PC-03  Pause: transitions Active → Paused and writes one ParentPause ledger entry.
///   PC-04  Unpause: idempotent no-op when child is already Active (no ledger entry).
///   PC-05  Unpause: transitions Paused → Active and writes one ParentUnpause ledger entry.
///   PC-06  Pause is access-only: SeatState, ChildEnergyAllocation, and FamilyWallet are
///          untouched by pause AND unpause.
///   PC-07  Dual-gate independence: NoSeatLocked child blocked by seat gate even when not paused.
///   PC-08  Dual-gate independence: Active-seat but Paused child blocked by pause gate.
///   PC-09  IsChildAccessAllowedAsync returns true only when SeatState=Active AND pause=Active.
///   PC-10  GetChildAccessStatusAsync: IDOR rejection when parent doesn't own child.
/// </summary>
public sealed class PauseChildServiceTests
{
    // ── SQLite helpers ────────────────────────────────────────────────────────────────────
    //
    // BillingTestDbContext is used (from FamilyAllocationTransferTests.cs) to supply HasDefaultValue(1u)
    // for the xmin/Version columns on FamilyEnergyAccount and ChildEnergyAllocation. Without this,
    // SQLite raises NOT NULL on those columns (they are server-generated on PostgreSQL).

    private static BillingTestDbContext BuildDb()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new BillingTestDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static Mock<Learnexia.Shared.Kernel.Abstractions.ICurrentUserService> BuildUserMock(int userId = 1)
    {
        var m = new Mock<Learnexia.Shared.Kernel.Abstractions.ICurrentUserService>();
        m.Setup(c => c.UserId).Returns(userId);
        return m;
    }

    private static Mock<Learnexia.Shared.Kernel.Abstractions.ILoggerManager> BuildLoggerMock()
        => new();

    private static PauseChildService BuildPauseService(BillingTestDbContext db, int userId = 1)
        => new PauseChildService(db, BuildUserMock(userId).Object, BuildLoggerMock().Object);

    private static ChildAccessStateQuery BuildChildAccessStateQuery(BillingTestDbContext db)
        => new ChildAccessStateQuery(db);

    private static SeatStateQuery BuildSeatStateQuery(BillingTestDbContext db)
        => new SeatStateQuery(db);

    private static Mock<IGlobalSettingsProvider> BuildSettingsMock()
    {
        var m = new Mock<IGlobalSettingsProvider>();
        m.Setup(s => s.GetInt(It.IsAny<string>(), It.IsAny<int>())).Returns(10);
        return m;
    }

    private static (CreditSpendService Service, BillingTestDbContext Db) BuildSpendService(
        BillingTestDbContext db,
        ISeatStateQuery seatStateQuery)
    {
        var concurrencyOptions = Options.Create(new BillingConcurrencyOptions
        {
            MaxRetries  = 0,
            BaseDelayMs = 0,
            MaxDelayMs  = 0,
            JitterMs    = 0,
        });
        var clockMock = new Mock<ISystemClock>();
        var now = DateTime.UtcNow;
        clockMock.Setup(c => c.UtcNow).Returns(now);

        var service = new CreditSpendService(
            db,
            BuildUserMock().Object,
            BuildLoggerMock().Object,
            BuildSettingsMock().Object,
            clockMock.Object,
            concurrencyOptions,
            seatStateQuery);

        return (service, db);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────────────────

    private static async Task<Subscription> SeedSubscriptionAsync(BillingTestDbContext db, int parentUserId = 1)
    {
        var sub = new Subscription
        {
            ParentUserId       = parentUserId,
            PlanCode           = PlanCode.Free,
            BillingPeriod      = BillingPeriod.Monthly,
            Status             = SubscriptionStatus.Active,
            PurchasedExtraSeats = 0,
            CurrentCycleStart  = DateTime.UtcNow.AddDays(-15),
            CurrentCycleEnd    = DateTime.UtcNow.AddDays(15),
            CreatedAt          = DateTime.UtcNow.AddMonths(-1),
            CreatedBy          = 0,
        };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();
        return sub;
    }

    private static async Task<SeatReservation> SeedSeatAsync(
        BillingTestDbContext db,
        int subscriptionId,
        int parentUserId,
        int childId,
        SeatState seatState           = SeatState.Active,
        ParentPauseState pauseState   = ParentPauseState.Active,
        SeatStatus status             = SeatStatus.Active)
    {
        var seat = new SeatReservation
        {
            SubscriptionId         = subscriptionId,
            ChildId                = childId,
            ParentUserId           = parentUserId,
            Status                 = status,
            SeatState              = seatState,
            ParentPauseState       = pauseState,
            ParentPauseStateChangedAt = null,
            ReservedAt             = DateTime.UtcNow.AddDays(-10),
            CreatedAt              = DateTime.UtcNow.AddDays(-10),
            CreatedBy              = 0,
        };
        db.SeatReservations.Add(seat);
        await db.SaveChangesAsync();
        return seat;
    }

    // ── PC-01: IDOR rejection — child not owned by parent ────────────────────────

    [Fact]
    public async Task PauseChildAsync_ChildNotOwned_ReturnsChildNotOwnedByParent()
    {
        var db      = BuildDb();
        var service = BuildPauseService(db, userId: 99);
        var sub     = await SeedSubscriptionAsync(db, parentUserId: 1);

        // Child belongs to parent 1, but caller is parent 99.
        await SeedSeatAsync(db, sub.Id, parentUserId: 1, childId: 42);

        var result = await service.PauseChildAsync(parentUserId: 99, childId: 42);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(PauseFailureReason.ChildNotOwnedByParent);
    }

    // ── PC-02: Idempotent no-op when already Paused ───────────────────────────────

    [Fact]
    public async Task PauseChildAsync_AlreadyPaused_ReturnsNoOpSuccess_NoLedgerEntry()
    {
        var db      = BuildDb();
        var service = BuildPauseService(db, userId: 1);
        var sub     = await SeedSubscriptionAsync(db, parentUserId: 1);
        await SeedSeatAsync(db, sub.Id, parentUserId: 1, childId: 7, pauseState: ParentPauseState.Paused);

        var ledgerCountBefore = await db.SeatLedgerEntries.CountAsync();

        var result = await service.PauseChildAsync(parentUserId: 1, childId: 7);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.WasNoOp.Should().BeTrue();
        result.Data.ParentPauseState.Should().Be(ParentPauseState.Paused);

        var ledgerCountAfter = await db.SeatLedgerEntries.CountAsync();
        ledgerCountAfter.Should().Be(ledgerCountBefore, "no ledger entry for a no-op");
    }

    // ── PC-03: Pause transitions Active → Paused, appends ledger entry ──────────

    [Fact]
    public async Task PauseChildAsync_ActiveChild_TransitionsToPaused_AppendsLedgerEntry()
    {
        var db      = BuildDb();
        var service = BuildPauseService(db, userId: 1);
        var sub     = await SeedSubscriptionAsync(db, parentUserId: 1);
        await SeedSeatAsync(db, sub.Id, parentUserId: 1, childId: 10, pauseState: ParentPauseState.Active);

        var result = await service.PauseChildAsync(parentUserId: 1, childId: 10);

        result.Succeeded.Should().BeTrue();
        result.Data!.WasNoOp.Should().BeFalse();
        result.Data.ParentPauseState.Should().Be(ParentPauseState.Paused);

        // Verify DB state.
        var seat = await db.SeatReservations.FirstAsync(r => r.ChildId == 10);
        seat.ParentPauseState.Should().Be(ParentPauseState.Paused);
        seat.ParentPauseStateChangedAt.Should().NotBeNull();

        // Verify exactly one ledger entry was appended.
        var entries = await db.SeatLedgerEntries.ToListAsync();
        entries.Should().HaveCount(1);
        entries[0].EventType.Should().Be(SeatLedgerEventType.ParentPause);
        entries[0].ChildId.Should().Be(10);
    }

    // ── PC-04: Unpause idempotent when already Active ────────────────────────────

    [Fact]
    public async Task UnpauseChildAsync_AlreadyActive_ReturnsNoOpSuccess_NoLedgerEntry()
    {
        var db      = BuildDb();
        var service = BuildPauseService(db, userId: 1);
        var sub     = await SeedSubscriptionAsync(db, parentUserId: 1);
        await SeedSeatAsync(db, sub.Id, parentUserId: 1, childId: 5, pauseState: ParentPauseState.Active);

        var ledgerCountBefore = await db.SeatLedgerEntries.CountAsync();

        var result = await service.UnpauseChildAsync(parentUserId: 1, childId: 5);

        result.Succeeded.Should().BeTrue();
        result.Data!.WasNoOp.Should().BeTrue();

        var ledgerCountAfter = await db.SeatLedgerEntries.CountAsync();
        ledgerCountAfter.Should().Be(ledgerCountBefore, "no ledger entry for a no-op");
    }

    // ── PC-05: Unpause transitions Paused → Active, appends ledger entry ─────────

    [Fact]
    public async Task UnpauseChildAsync_PausedChild_TransitionsToActive_AppendsLedgerEntry()
    {
        var db      = BuildDb();
        var service = BuildPauseService(db, userId: 1);
        var sub     = await SeedSubscriptionAsync(db, parentUserId: 1);
        await SeedSeatAsync(db, sub.Id, parentUserId: 1, childId: 11, pauseState: ParentPauseState.Paused);

        var result = await service.UnpauseChildAsync(parentUserId: 1, childId: 11);

        result.Succeeded.Should().BeTrue();
        result.Data!.WasNoOp.Should().BeFalse();
        result.Data.ParentPauseState.Should().Be(ParentPauseState.Active);

        var seat = await db.SeatReservations.FirstAsync(r => r.ChildId == 11);
        seat.ParentPauseState.Should().Be(ParentPauseState.Active);
        seat.ParentPauseStateChangedAt.Should().NotBeNull();

        var entries = await db.SeatLedgerEntries.ToListAsync();
        entries.Should().HaveCount(1);
        entries[0].EventType.Should().Be(SeatLedgerEventType.ParentUnpause);
        entries[0].ChildId.Should().Be(11);
    }

    // ── PC-06: Pause is access-only — zero energy/seat side-effects ──────────────

    [Fact]
    public async Task PauseAndUnpause_DoNotTouchSeatState_Or_EnergyAllocations()
    {
        var db      = BuildDb();
        var service = BuildPauseService(db, userId: 1);
        var sub     = await SeedSubscriptionAsync(db, parentUserId: 1);
        var seat    = await SeedSeatAsync(db, sub.Id, parentUserId: 1, childId: 20,
                                          seatState: SeatState.Active, pauseState: ParentPauseState.Active);

        // Add a wallet + allocation via raw SQL (xmin column requires raw SQL on SQLite).
        var now        = DateTime.UtcNow.ToString("o");
        var cycleStart = DateTime.UtcNow.AddDays(-15).ToString("o");
        var cycleEnd   = DateTime.UtcNow.AddDays(15).ToString("o");

        await db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "FamilyEnergyAccounts"
                ("ParentUserId","SubscriptionBalance","PurchasedBalance","xmin","CreatedAt","CreatedBy")
            VALUES
                (1, 0, 100, 1, '{now}', 0)
            """);

        var walletId = await db.FamilyEnergyAccounts
            .AsNoTracking()
            .Where(w => w.ParentUserId == 1)
            .Select(w => w.Id)
            .FirstAsync();

        await db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "ChildEnergyAllocations"
                ("FamilyEnergyAccountId","ChildId","CycleStartUtc","CycleEndUtc",
                 "AllocatedAmount","SpentAmount","xmin","CreatedAt","CreatedBy")
            VALUES
                ({walletId}, 20, '{cycleStart}', '{cycleEnd}', 50, 0, 1, '{now}', 0)
            """);

        // Pause then unpause.
        await service.PauseChildAsync(parentUserId: 1, childId: 20);
        await service.UnpauseChildAsync(parentUserId: 1, childId: 20);

        // Seat state must still be Active.
        var updatedSeat = await db.SeatReservations.FirstAsync(r => r.ChildId == 20);
        updatedSeat.SeatState.Should().Be(SeatState.Active, "pause must never touch SeatState");

        // Allocation must be unchanged.
        var updatedAlloc = await db.ChildEnergyAllocations.FirstAsync(a => a.ChildId == 20);
        updatedAlloc.AllocatedAmount.Should().Be(50, "pause must never touch energy allocation");
        updatedAlloc.SpentAmount.Should().Be(0, "pause must never touch energy spent amount");

        // Wallet must be unchanged.
        var updatedWallet = await db.FamilyEnergyAccounts.FirstAsync(w => w.ParentUserId == 1);
        updatedWallet.PurchasedBalance.Should().Be(100, "pause must never touch purchased balance");

        // CreditTransactions must be zero (no energy writes).
        (await db.CreditTransactions.CountAsync()).Should().Be(0, "no energy ledger writes on pause/unpause");

        // Two ledger entries (one ParentPause + one ParentUnpause) — both seat-domain only.
        var entries = await db.SeatLedgerEntries.ToListAsync();
        entries.Should().HaveCount(2);
        entries.Should().Contain(e => e.EventType == SeatLedgerEventType.ParentPause);
        entries.Should().Contain(e => e.EventType == SeatLedgerEventType.ParentUnpause);
    }

    // ── PC-07: Dual-gate independence — NoSeatLocked child blocked by check 1 ─────

    [Fact]
    public async Task SpendGate_NoSeatLocked_BlockedBySeatCheck_EvenIfNotPaused()
    {
        var db   = BuildDb();
        var sub  = await SeedSubscriptionAsync(db, parentUserId: 1);
        var seat = await SeedSeatAsync(db, sub.Id, parentUserId: 1, childId: 30,
                                       seatState: SeatState.NoSeatLocked,
                                       pauseState: ParentPauseState.Active);  // NOT paused

        var now30       = DateTime.UtcNow.ToString("o");
        var cycleEnd30  = DateTime.UtcNow.AddDays(30).ToString("o");
        var cycleStart30 = DateTime.UtcNow.AddDays(-1).ToString("o");

        await db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "FamilyEnergyAccounts"
                ("ParentUserId","SubscriptionBalance","PurchasedBalance","xmin","CreatedAt","CreatedBy")
            VALUES (1, 0, 500, 1, '{now30}', 0)
            """);
        var walletId30 = await db.FamilyEnergyAccounts.AsNoTracking().Where(w => w.ParentUserId == 1).Select(w => w.Id).FirstAsync();
        await db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "ChildEnergyAllocations"
                ("FamilyEnergyAccountId","ChildId","CycleStartUtc","CycleEndUtc","AllocatedAmount","SpentAmount","xmin","CreatedAt","CreatedBy")
            VALUES ({walletId30}, 30, '{cycleStart30}', '{cycleEnd30}', 100, 0, 1, '{now30}', 0)
            """);

        var seatStateQuery = BuildSeatStateQuery(db);
        var (spendService, _) = BuildSpendService(db, seatStateQuery);

        var result = await spendService.TryDebitAsync(30, 5, "AiHint", "key:seat-check-30", CancellationToken.None);

        result.Outcome.Should().Be(DebitOutcome.SeatLocked,
            "NoSeatLocked child must be blocked by the seat gate regardless of pause state");
        result.Charged.Should().BeFalse();
        (await db.CreditTransactions.CountAsync()).Should().Be(0, "no energy charge on SeatLocked block");
    }

    // ── PC-08: Dual-gate independence — Active-seat + Paused child blocked by check 2

    [Fact]
    public async Task SpendGate_ActiveSeat_ButParentPaused_BlockedByPauseCheck()
    {
        var db   = BuildDb();
        var sub  = await SeedSubscriptionAsync(db, parentUserId: 1);
        await SeedSeatAsync(db, sub.Id, parentUserId: 1, childId: 31,
                            seatState: SeatState.Active,
                            pauseState: ParentPauseState.Paused);  // seat Active but PAUSED

        var now31        = DateTime.UtcNow.ToString("o");
        var cycleStart31 = DateTime.UtcNow.AddDays(-1).ToString("o");
        var cycleEnd31   = DateTime.UtcNow.AddDays(30).ToString("o");

        await db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "FamilyEnergyAccounts"
                ("ParentUserId","SubscriptionBalance","PurchasedBalance","xmin","CreatedAt","CreatedBy")
            VALUES (1, 0, 500, 1, '{now31}', 0)
            """);
        var walletId31 = await db.FamilyEnergyAccounts.AsNoTracking().Where(w => w.ParentUserId == 1).Select(w => w.Id).FirstAsync();
        await db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "ChildEnergyAllocations"
                ("FamilyEnergyAccountId","ChildId","CycleStartUtc","CycleEndUtc","AllocatedAmount","SpentAmount","xmin","CreatedAt","CreatedBy")
            VALUES ({walletId31}, 31, '{cycleStart31}', '{cycleEnd31}', 100, 0, 1, '{now31}', 0)
            """);

        var seatStateQuery = BuildSeatStateQuery(db);
        var (spendService, _) = BuildSpendService(db, seatStateQuery);

        var result = await spendService.TryDebitAsync(31, 5, "AiHint", "key:pause-check-31", CancellationToken.None);

        result.Outcome.Should().Be(DebitOutcome.ParentPaused,
            "Active-seat child who is parent-paused must be blocked by the pause gate");
        result.Charged.Should().BeFalse();
        (await db.CreditTransactions.CountAsync()).Should().Be(0, "no energy charge on ParentPaused block");
    }

    // ── PC-09: IsChildAccessAllowedAsync — both Active → true; either Paused/NoSeat → false

    [Fact]
    public async Task IsChildAccessAllowedAsync_BothActive_ReturnsTrue()
    {
        var db   = BuildDb();
        var sub  = await SeedSubscriptionAsync(db, parentUserId: 1);
        await SeedSeatAsync(db, sub.Id, parentUserId: 1, childId: 50,
                            seatState: SeatState.Active, pauseState: ParentPauseState.Active);

        var query  = BuildChildAccessStateQuery(db);
        var result = await query.IsChildAccessAllowedAsync(50);

        result.Should().BeTrue("access is allowed when both SeatState and ParentPauseState are Active");
    }

    [Fact]
    public async Task IsChildAccessAllowedAsync_NoSeatLocked_ReturnsFalse()
    {
        var db  = BuildDb();
        var sub = await SeedSubscriptionAsync(db, parentUserId: 1);
        await SeedSeatAsync(db, sub.Id, parentUserId: 1, childId: 51,
                            seatState: SeatState.NoSeatLocked, pauseState: ParentPauseState.Active);

        var query  = BuildChildAccessStateQuery(db);
        var result = await query.IsChildAccessAllowedAsync(51);

        result.Should().BeFalse("access is blocked when SeatState is NoSeatLocked");
    }

    [Fact]
    public async Task IsChildAccessAllowedAsync_Paused_ReturnsFalse()
    {
        var db  = BuildDb();
        var sub = await SeedSubscriptionAsync(db, parentUserId: 1);
        await SeedSeatAsync(db, sub.Id, parentUserId: 1, childId: 52,
                            seatState: SeatState.Active, pauseState: ParentPauseState.Paused);

        var query  = BuildChildAccessStateQuery(db);
        var result = await query.IsChildAccessAllowedAsync(52);

        result.Should().BeFalse("access is blocked when ParentPauseState is Paused");
    }

    [Fact]
    public async Task IsChildAccessAllowedAsync_BothNoSeatAndPaused_ReturnsFalse()
    {
        var db  = BuildDb();
        var sub = await SeedSubscriptionAsync(db, parentUserId: 1);
        await SeedSeatAsync(db, sub.Id, parentUserId: 1, childId: 53,
                            seatState: SeatState.NoSeatLocked, pauseState: ParentPauseState.Paused);

        var query  = BuildChildAccessStateQuery(db);
        var result = await query.IsChildAccessAllowedAsync(53);

        result.Should().BeFalse("access is blocked when both states are non-Active");
    }

    // ── PC-10: GetChildAccessStatusAsync IDOR rejection ──────────────────────────

    [Fact]
    public async Task GetChildAccessStatusAsync_ChildNotOwned_ReturnsFailure()
    {
        var db      = BuildDb();
        var service = BuildPauseService(db, userId: 200);
        var sub     = await SeedSubscriptionAsync(db, parentUserId: 1);
        await SeedSeatAsync(db, sub.Id, parentUserId: 1, childId: 99);

        // Caller is parent 200, child belongs to parent 1.
        var result = await service.GetChildAccessStatusAsync(parentUserId: 200, childId: 99);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(PauseFailureReason.ChildNotOwnedByParent);
    }
}
