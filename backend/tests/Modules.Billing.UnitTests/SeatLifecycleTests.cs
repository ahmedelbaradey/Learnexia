using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Options;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Infrastructure.Services;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Parent;
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
/// Focused unit tests for P10-15 seat lifecycle correctness.
///
/// All services are built under Option-C: the real <see cref="BillingDbContext"/> (cast to
/// <see cref="IBillingDbContext"/>) runs against SQLite in-memory — no Postgres required.
///
/// Coverage:
///   SL-01  Grace idempotency: re-triggering StartPaymentFailureGraceAsync within an open
///          window does NOT extend or shorten GraceEndsAt.
///   SL-02  Grace opens correctly on first call (null → set, GraceEndsAt = now + graceDays).
///   SL-03  Enforcement tie-break: 3 seats present, paidActiveSeats=2 → oldest 2 kept (Active),
///          newest one locked (NoSeatLocked).
///   SL-04  Enforcement appends SeatLedgerEntry{Locked} for each locked child.
///   SL-05  Enforcement does NOT delete ChildEnergyAllocation rows for kept children.
///   SL-06  Enforcement removes ChildEnergyAllocation for the locked child (no future entitlement).
///   SL-07  CreditSpendService seat-state gate: child with SeatState=NoSeatLocked → DebitOutcome.SeatLocked,
///          no write to ChildEnergyAllocation or CreditTransaction.
///   SL-08  SeatStateQuery returns true for Active reservation, false for NoSeatLocked reservation.
///   SL-09  Enforcement is idempotent: calling EnforceAsync twice on the same subscription locks
///          each over-limit seat only once (no duplicate SeatLedgerEntry rows).
///   SL-10  RemovalScheduledAt past → enforcement marks seat Released, flips SeatState=NoSeatLocked,
///          appends SeatLedgerEntry{RemovedAtRenewal}, removes ChildEnergyAllocation.
/// </summary>
public sealed class SeatLifecycleTests
{
    // ── SQLite helpers ────────────────────────────────────────────────────────────────────

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

    // ── Service builders ──────────────────────────────────────────────────────────────────

    private static Mock<Learnexia.Shared.Kernel.Abstractions.ICurrentUserService> BuildUserMock(int userId = 0)
    {
        var m = new Mock<Learnexia.Shared.Kernel.Abstractions.ICurrentUserService>();
        m.Setup(c => c.UserId).Returns(userId);
        return m;
    }

    private static Mock<Learnexia.Shared.Kernel.Abstractions.ILoggerManager> BuildLoggerMock()
        => new();

    private static Mock<IGlobalSettingsProvider> BuildSettingsMock(
        int graceDays       = 7,
        int includedFree    = 1,
        int maxSeats        = 5,
        int packSize        = 500)
    {
        var m = new Mock<IGlobalSettingsProvider>();
        m.Setup(s => s.GetInt(GlobalSettingKeys.SeatsGraceDays,     It.IsAny<int>())).Returns(graceDays);
        m.Setup(s => s.GetInt(GlobalSettingKeys.SeatsIncludedFree,  It.IsAny<int>())).Returns(includedFree);
        m.Setup(s => s.GetInt(GlobalSettingKeys.SeatsMax,           It.IsAny<int>())).Returns(maxSeats);
        m.Setup(s => s.GetInt(GlobalSettingKeys.PackSize,           It.IsAny<int>())).Returns(packSize);
        m.Setup(s => s.GetInt(GlobalSettingKeys.FreeDailyCap,       It.IsAny<int>())).Returns(10);
        return m;
    }

    private static SeatGraceService BuildGraceService(BillingDbContext db, int graceDays = 7)
    {
        var settings = BuildSettingsMock(graceDays: graceDays);
        return new SeatGraceService(db, settings.Object, BuildUserMock().Object, BuildLoggerMock().Object);
    }

    private static SeatEnforcementService BuildEnforcementService(
        BillingDbContext db,
        int includedFree = 1,
        int maxSeats     = 5)
    {
        var settings = BuildSettingsMock(includedFree: includedFree, maxSeats: maxSeats);
        return new SeatEnforcementService(db, settings.Object, BuildUserMock().Object, BuildLoggerMock().Object);
    }

    private static (CreditSpendService Service, BillingDbContext Db) BuildSpendService(
        BillingDbContext db,
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
        clockMock.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);

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

    private static async Task<Subscription> SeedSubscriptionAsync(
        BillingDbContext db,
        int parentUserId    = 1,
        int purchasedExtra  = 0,
        DateTime? graceEndsAt = null)
    {
        var sub = new Subscription
        {
            ParentUserId       = parentUserId,
            PlanCode           = PlanCode.Free,
            BillingPeriod      = BillingPeriod.Monthly,
            Status             = SubscriptionStatus.Active,
            GraceEndsAt        = graceEndsAt,
            PurchasedExtraSeats = purchasedExtra,
            CurrentCycleStart  = DateTime.UtcNow.AddDays(-15),
            CurrentCycleEnd    = DateTime.UtcNow.AddDays(15),
            CreatedAt          = DateTime.UtcNow.AddMonths(-1),
            CreatedBy          = 0,
        };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();
        return sub;
    }

    private static async Task<SeatReservation> SeedReservationAsync(
        BillingDbContext db,
        int subscriptionId,
        int childId,
        SeatState seatState         = SeatState.Active,
        SeatStatus status           = SeatStatus.Active,
        DateTime? reservedAt        = null,
        DateTime? removalScheduledAt = null)
    {
        var r = new SeatReservation
        {
            SubscriptionId     = subscriptionId,
            ChildId            = childId,
            ParentUserId       = 1,
            Status             = status,
            SeatState          = seatState,
            ReservedAt         = reservedAt ?? DateTime.UtcNow,
            RemovalScheduledAt = removalScheduledAt,
            CreatedAt          = DateTime.UtcNow,
            CreatedBy          = 0,
        };
        db.SeatReservations.Add(r);
        await db.SaveChangesAsync();
        return r;
    }

    /// <summary>
    /// Seeds a ChildEnergyAllocation row via raw SQL.
    /// Required because EF's IsRowVersion()+ValueGeneratedOnAddOrUpdate on the xmin column
    /// prevents EF from writing the value at insert time under SQLite (Postgres-specific behaviour).
    /// Raw SQL bypasses the row-version mapping and supplies xmin=1 directly.
    /// </summary>
    private static async Task<int> SeedAllocationAsync(
        BillingDbContext db,
        int childId,
        int familyEnergyAccountId = 1,
        int allocated             = 100)
    {
        var now     = DateTime.UtcNow.ToString("o");
        var cycleStart = DateTime.UtcNow.AddDays(-15).ToString("o");
        var cycleEnd   = DateTime.UtcNow.AddDays(15).ToString("o");

        // SQLite does not auto-generate the xmin column so we insert it explicitly as integer 1.
        // No schema prefix — SQLite ignores HasDefaultSchema; tables are created without a prefix.
        await db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "ChildEnergyAllocations"
                ("FamilyEnergyAccountId","ChildId","CycleStartUtc","CycleEndUtc",
                 "AllocatedAmount","SpentAmount","xmin",
                 "CreatedAt","CreatedBy")
            VALUES
                ({familyEnergyAccountId},{childId},'{cycleStart}','{cycleEnd}',
                 {allocated},0,1,
                 '{now}',0)
            """);

        return childId;
    }

    /// <summary>
    /// Seeds a FamilyEnergyAccount row via raw SQL for the same reason as <see cref="SeedAllocationAsync"/>.
    /// </summary>
    private static async Task<int> SeedWalletAsync(BillingDbContext db, int parentUserId = 1, int purchased = 0)
    {
        var now = DateTime.UtcNow.ToString("o");
        // No schema prefix — SQLite ignores HasDefaultSchema; tables are created without a prefix.
        await db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "FamilyEnergyAccounts"
                ("ParentUserId","SubscriptionBalance","PurchasedBalance","xmin",
                 "CreatedAt","CreatedBy")
            VALUES
                ({parentUserId},0,{purchased},1,
                 '{now}',0)
            """);

        var id = await db.FamilyEnergyAccounts
            .AsNoTracking()
            .Where(w => w.ParentUserId == parentUserId)
            .Select(w => w.Id)
            .FirstAsync();
        return id;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-01: Grace idempotency — re-trigger within open window does NOT extend GraceEndsAt
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SL01_StartGrace_WithinOpenWindow_DoesNotExtendGraceEndsAt()
    {
        using var db = BuildDb();
        var originalGrace = DateTime.UtcNow.AddDays(5); // window still open
        var sub = await SeedSubscriptionAsync(db, graceEndsAt: originalGrace);

        var graceService = BuildGraceService(db, graceDays: 7);

        // Act: call again within the open window.
        await graceService.StartPaymentFailureGraceAsync(sub.Id, CancellationToken.None);

        // Assert: GraceEndsAt is unchanged (no extension / shortening).
        var reloaded = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == sub.Id);
        reloaded.GraceEndsAt.Should().BeCloseTo(originalGrace, TimeSpan.FromSeconds(1));
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-02: Grace opens correctly on first call (GraceEndsAt was null)
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SL02_StartGrace_WhenNoWindow_SetsGraceEndsAtToNowPlusGraceDays()
    {
        using var db = BuildDb();
        var sub = await SeedSubscriptionAsync(db, graceEndsAt: null);

        var before = DateTime.UtcNow;
        var graceService = BuildGraceService(db, graceDays: 7);
        await graceService.StartPaymentFailureGraceAsync(sub.Id, CancellationToken.None);
        var after = DateTime.UtcNow;

        var reloaded = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == sub.Id);
        reloaded.GraceEndsAt.Should().NotBeNull();
        reloaded.GraceEndsAt!.Value.Should().BeOnOrAfter(before.AddDays(7).AddSeconds(-1));
        reloaded.GraceEndsAt!.Value.Should().BeOnOrBefore(after.AddDays(7).AddSeconds(1));
        reloaded.SeatGraceReason.Should().Be(SeatGraceReason.PaymentFailure);
        reloaded.SeatGraceStartedAt.Should().NotBeNull();
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-03: Enforcement tie-break: 3 seats, paidActiveSeats=1 → oldest kept, 2 newest locked
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SL03_Enforcement_TiebBreakEarliestReservedKept_NewestLocked()
    {
        using var db = BuildDb();
        // Free plan (Id=1, IncludedSeats=1) is already seeded by EnsureCreated() via PlanConfig.HasData.
        var sub = await SeedSubscriptionAsync(db, purchasedExtra: 0);

        var now = DateTime.UtcNow;
        // 3 active seats; ordered oldest → newest by ReservedAt.
        var r1 = await SeedReservationAsync(db, sub.Id, childId: 10, reservedAt: now.AddDays(-30)); // oldest → kept
        var r2 = await SeedReservationAsync(db, sub.Id, childId: 20, reservedAt: now.AddDays(-10)); // mid → locked
        var r3 = await SeedReservationAsync(db, sub.Id, childId: 30, reservedAt: now.AddDays(-5));  // newest → locked

        var svc = BuildEnforcementService(db, includedFree: 1);
        var lockedCount = await svc.EnforceAsync(sub.Id, CancellationToken.None);

        lockedCount.Should().Be(2);

        var seats = await db.SeatReservations.AsNoTracking()
            .Where(r => r.SubscriptionId == sub.Id)
            .OrderBy(r => r.ReservedAt)
            .ToListAsync();

        seats[0].ChildId.Should().Be(10);
        seats[0].SeatState.Should().Be(SeatState.Active,   "oldest seat must be kept");

        seats[1].ChildId.Should().Be(20);
        seats[1].SeatState.Should().Be(SeatState.NoSeatLocked, "middle seat must be locked");

        seats[2].ChildId.Should().Be(30);
        seats[2].SeatState.Should().Be(SeatState.NoSeatLocked, "newest seat must be locked");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-04: Enforcement appends SeatLedgerEntry{Locked} for each locked child
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SL04_Enforcement_AppendsLockedLedgerEntries()
    {
        using var db = BuildDb();
        // Free plan (Id=1, IncludedSeats=1) is already seeded by EnsureCreated() via PlanConfig.HasData.
        var sub = await SeedSubscriptionAsync(db, purchasedExtra: 0);

        var now = DateTime.UtcNow;
        await SeedReservationAsync(db, sub.Id, childId: 10, reservedAt: now.AddDays(-30));
        await SeedReservationAsync(db, sub.Id, childId: 20, reservedAt: now.AddDays(-10));
        await SeedReservationAsync(db, sub.Id, childId: 30, reservedAt: now.AddDays(-5));

        var svc = BuildEnforcementService(db, includedFree: 1);
        await svc.EnforceAsync(sub.Id, CancellationToken.None);

        var ledgerEntries = await db.SeatLedgerEntries.AsNoTracking()
            .Where(e => e.SubscriptionId == sub.Id && e.EventType == SeatLedgerEventType.Locked)
            .ToListAsync();

        ledgerEntries.Should().HaveCount(2);
        ledgerEntries.Select(e => e.ChildId).Should().BeEquivalentTo(new[] { 20, 30 });
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-05: Enforcement does NOT remove ChildEnergyAllocation for kept children
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SL05_Enforcement_DoesNotRemoveAllocation_ForKeptChildren()
    {
        using var db = BuildDb();
        // Free plan (Id=1, IncludedSeats=1) is already seeded by EnsureCreated() via PlanConfig.HasData.
        var sub   = await SeedSubscriptionAsync(db, purchasedExtra: 0);
        var walletId = await SeedWalletAsync(db);

        var now = DateTime.UtcNow;
        await SeedReservationAsync(db, sub.Id, childId: 10, reservedAt: now.AddDays(-30)); // kept
        await SeedReservationAsync(db, sub.Id, childId: 20, reservedAt: now.AddDays(-5));  // locked

        // Both children have allocations.
        await SeedAllocationAsync(db, childId: 10, familyEnergyAccountId: walletId);
        await SeedAllocationAsync(db, childId: 20, familyEnergyAccountId: walletId);

        var svc = BuildEnforcementService(db, includedFree: 1);
        await svc.EnforceAsync(sub.Id, CancellationToken.None);

        // Kept child's allocation must still exist.
        var keptAlloc = await db.ChildEnergyAllocations.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ChildId == 10);
        keptAlloc.Should().NotBeNull("allocation for kept child must be preserved");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-06: Enforcement removes ChildEnergyAllocation for locked child
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SL06_Enforcement_RemovesAllocation_ForLockedChild()
    {
        using var db = BuildDb();
        // Free plan (Id=1, IncludedSeats=1) is already seeded by EnsureCreated() via PlanConfig.HasData.
        var sub   = await SeedSubscriptionAsync(db, purchasedExtra: 0);
        var walletId = await SeedWalletAsync(db);

        var now = DateTime.UtcNow;
        await SeedReservationAsync(db, sub.Id, childId: 10, reservedAt: now.AddDays(-30)); // kept
        await SeedReservationAsync(db, sub.Id, childId: 20, reservedAt: now.AddDays(-5));  // locked

        await SeedAllocationAsync(db, childId: 10, familyEnergyAccountId: walletId);
        await SeedAllocationAsync(db, childId: 20, familyEnergyAccountId: walletId);

        var svc = BuildEnforcementService(db, includedFree: 1);
        await svc.EnforceAsync(sub.Id, CancellationToken.None);

        // Locked child's allocation must have been removed.
        var lockedAlloc = await db.ChildEnergyAllocations.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ChildId == 20);
        lockedAlloc.Should().BeNull("allocation for locked child must be removed");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-07: CreditSpendService seat-state gate: NoSeatLocked → SeatLocked outcome, no write
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SL07_SpendGate_NoSeatLocked_ReturnsSeatLockedWithoutWriting()
    {
        using var db = BuildDb();

        // Child 99 has a NoSeatLocked reservation.
        var sub = await SeedSubscriptionAsync(db);
        await SeedReservationAsync(db, sub.Id, childId: 99, seatState: SeatState.NoSeatLocked, status: SeatStatus.Active);

        // Also set up a wallet + allocation for child 99 to confirm they are untouched.
        var walletId = await SeedWalletAsync(db, purchased: 0);
        await SeedAllocationAsync(db, childId: 99, familyEnergyAccountId: walletId, allocated: 50);

        var txBefore = await db.CreditTransactions.AsNoTracking().CountAsync();

        var seatStateQuery = new SeatStateQuery(db);
        var (spendSvc, _) = BuildSpendService(db, seatStateQuery);

        var result = await spendSvc.TryDebitAsync(
            childId      : 99,
            amount       : 5,
            reasonCode   : "AiHint",
            idempotencyKey: "test-spend:99:001",
            ct           : CancellationToken.None);

        result.Outcome.Should().Be(DebitOutcome.SeatLocked);
        result.Charged.Should().BeFalse();
        result.FromGranted.Should().Be(0);
        result.FromPurchased.Should().Be(0);

        // No CreditTransaction rows must have been written.
        var txAfter = await db.CreditTransactions.AsNoTracking().CountAsync();
        txAfter.Should().Be(txBefore, "no ledger writes allowed on SeatLocked gate");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-08: SeatStateQuery: Active reservation → true; NoSeatLocked reservation → false
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SL08_SeatStateQuery_ActiveReservation_ReturnsTrue()
    {
        using var db = BuildDb();
        var sub = await SeedSubscriptionAsync(db);
        await SeedReservationAsync(db, sub.Id, childId: 11, seatState: SeatState.Active, status: SeatStatus.Active);

        var query = new SeatStateQuery(db);
        var isActive = await query.IsChildSeatActiveAsync(11, CancellationToken.None);

        isActive.Should().BeTrue();
    }

    [Fact]
    public async Task SL08b_SeatStateQuery_NoSeatLockedReservation_ReturnsFalse()
    {
        using var db = BuildDb();
        var sub = await SeedSubscriptionAsync(db);
        await SeedReservationAsync(db, sub.Id, childId: 12, seatState: SeatState.NoSeatLocked, status: SeatStatus.Active);

        var query = new SeatStateQuery(db);
        var isActive = await query.IsChildSeatActiveAsync(12, CancellationToken.None);

        isActive.Should().BeFalse();
    }

    [Fact]
    public async Task SL08c_SeatStateQuery_NoReservation_ReturnsFalse()
    {
        using var db = BuildDb();
        var query = new SeatStateQuery(db);
        var isActive = await query.IsChildSeatActiveAsync(999, CancellationToken.None);
        isActive.Should().BeFalse();
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-09: Enforcement idempotency — calling twice does NOT double-lock
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SL09_Enforcement_Idempotent_DoesNotDoubleLock()
    {
        using var db = BuildDb();
        // Free plan (Id=1, IncludedSeats=1) is already seeded by EnsureCreated() via PlanConfig.HasData.
        var sub = await SeedSubscriptionAsync(db, purchasedExtra: 0);

        var now = DateTime.UtcNow;
        await SeedReservationAsync(db, sub.Id, childId: 10, reservedAt: now.AddDays(-30)); // kept
        await SeedReservationAsync(db, sub.Id, childId: 20, reservedAt: now.AddDays(-5));  // locked

        var svc = BuildEnforcementService(db, includedFree: 1);

        // First run.
        var firstCount = await svc.EnforceAsync(sub.Id, CancellationToken.None);
        firstCount.Should().Be(1);

        // Second run (idempotent).
        var secondCount = await svc.EnforceAsync(sub.Id, CancellationToken.None);
        secondCount.Should().Be(0, "already-locked seat must be skipped on second run");

        // Only one Locked ledger entry for child 20.
        var lockedEntries = await db.SeatLedgerEntries.AsNoTracking()
            .Where(e => e.ChildId == 20 && e.EventType == SeatLedgerEventType.Locked)
            .ToListAsync();
        lockedEntries.Should().HaveCount(1, "no duplicate ledger entry on idempotent enforcement");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-10: RemovalScheduledAt past → Released + NoSeatLocked + RemovedAtRenewal ledger
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SL10_Enforcement_RemovalScheduled_Past_ReleasesAndAppendsRemovedAtRenewal()
    {
        using var db = BuildDb();
        // Free plan (Id=1, IncludedSeats=1) is already seeded by EnsureCreated() via PlanConfig.HasData.
        var sub   = await SeedSubscriptionAsync(db, purchasedExtra: 0);
        var walletId = await SeedWalletAsync(db);

        // Only one reservation but its RemovalScheduledAt has ALREADY passed.
        var pastRemoval = DateTime.UtcNow.AddDays(-1);
        await SeedReservationAsync(db, sub.Id, childId: 50, reservedAt: DateTime.UtcNow.AddDays(-30),
            removalScheduledAt: pastRemoval);
        await SeedAllocationAsync(db, childId: 50, familyEnergyAccountId: walletId);

        var svc = BuildEnforcementService(db, includedFree: 1);
        await svc.EnforceAsync(sub.Id, CancellationToken.None);

        var seat = await db.SeatReservations.AsNoTracking()
            .FirstAsync(r => r.ChildId == 50);
        seat.Status.Should().Be(SeatStatus.Released, "scheduled-removal seat must be Released");
        seat.SeatState.Should().Be(SeatState.NoSeatLocked);

        var ledgerEntry = await db.SeatLedgerEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.ChildId == 50 && e.EventType == SeatLedgerEventType.RemovedAtRenewal);
        ledgerEntry.Should().NotBeNull("RemovedAtRenewal ledger entry must exist");

        var allocation = await db.ChildEnergyAllocations.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ChildId == 50);
        allocation.Should().BeNull("allocation for removed seat must be deleted");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-11 (Finding 1 — BE-10): ActivateSeatAsync resolves by reservationKey when supplied,
    //        stamping the real childId only on the matching placeholder row.
    // ══════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Two placeholder reservations (childId=0) exist for the same subscription — simulating
    /// two concurrent add-child calls. ActivateSeatAsync with a specific reservationKey must
    /// activate ONLY the matching row and leave the other placeholder untouched.
    /// </summary>
    [Fact]
    public async Task SL11_ActivateSeat_WithReservationKey_ActivatesExactRow_LeavesOtherUntouched()
    {
        // This test verifies that ActivateSeatAsync resolves by IdempotencyKey rather than
        // by "most-recent childId=0", which is the root of the concurrent-reservation race (BE-10).
        //
        // SQLite note: the Postgres filtered unique index on (SubscriptionId, ChildId) WHERE
        // Status IN (0,1) allows two rows with ChildId=0 at the same time (each from a concurrent
        // add-child call). SQLite's EnsureCreated() creates it as a full unique index, so two rows
        // with the same ChildId are not storable. We use distinct synthetic placeholder ChildIds
        // (0 for the first, 99 for the second) to model the "two concurrent calls in-flight" state
        // while still testing the key-based resolution property: given two Reserved rows with
        // distinct keys, ActivateSeat(key=key1) must mutate ONLY the key1 row.
        using var db  = BuildDb();
        var sub       = await SeedSubscriptionAsync(db, purchasedExtra: 1); // 2 seats total

        var key1 = "add-child-seat:1:aabbcc112233";
        var key2 = "add-child-seat:1:ddeeff445566";

        // Row 1: childId=0 placeholder, key1 (call 1's reservation).
        var r1 = new SeatReservation
        {
            SubscriptionId = sub.Id,
            ChildId        = 0,
            ParentUserId   = 1,
            Status         = SeatStatus.Reserved,
            SeatState      = SeatState.Active,
            IdempotencyKey = key1,
            ReservedAt     = DateTime.UtcNow.AddSeconds(-2),
            CreatedAt      = DateTime.UtcNow.AddSeconds(-2),
            CreatedBy      = 0,
        };
        // Row 2: childId=99 placeholder (distinct from 0 to satisfy SQLite full unique index),
        // key2 (call 2's reservation). On Postgres this row would also have ChildId=0 but that
        // distinction doesn't affect the key-resolution property being tested here.
        var r2 = new SeatReservation
        {
            SubscriptionId = sub.Id,
            ChildId        = 99,
            ParentUserId   = 1,
            Status         = SeatStatus.Reserved,
            SeatState      = SeatState.Active,
            IdempotencyKey = key2,
            ReservedAt     = DateTime.UtcNow.AddSeconds(-1),
            CreatedAt      = DateTime.UtcNow.AddSeconds(-1),
            CreatedBy      = 0,
        };
        db.SeatReservations.AddRange(r1, r2);
        await db.SaveChangesAsync();

        var settingsMock         = BuildSettingsMock();
        var currentUserMock      = BuildUserMock(userId: 1);
        var loggerMock           = BuildLoggerMock();
        var paymentProviderMock  = new Mock<IPaymentProvider>();
        var parentChildQueryMock = new Mock<IParentChildQuery>();

        var seatService = new SeatService(
            db,
            settingsMock.Object,
            paymentProviderMock.Object,
            currentUserMock.Object,
            loggerMock.Object,
            parentChildQueryMock.Object);

        // Activate the first call's row by key — must stamp childId=101 only on r1.
        await seatService.ActivateSeatAsync(parentUserId: 1, childId: 101, ct: CancellationToken.None,
            reservationKey: key1);

        var reloaded1 = await db.SeatReservations.AsNoTracking().FirstAsync(r => r.Id == r1.Id);
        var reloaded2 = await db.SeatReservations.AsNoTracking().FirstAsync(r => r.Id == r2.Id);

        reloaded1.Status.Should().Be(SeatStatus.Active,
            "SL-11: key1 row must be flipped to Active after ActivateSeat with key1");
        reloaded1.ChildId.Should().Be(101,
            "SL-11: key1 row must have childId stamped to 101");

        reloaded2.Status.Should().Be(SeatStatus.Reserved,
            "SL-11: key2 row must remain Reserved (must not be activated by the key1 call)");
        reloaded2.ChildId.Should().Be(99,
            "SL-11: key2 row childId must be unchanged (99) — not mutated by the key1 ActivateSeat call");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-12 (Finding 1 — BE-10): ReleaseSeatAsync resolves by reservationKey when supplied,
    //        releasing ONLY the matching placeholder row.
    // ══════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Two placeholder reservations exist for the same subscription. ReleaseSeatAsync with a
    /// specific reservationKey must release ONLY the matching row and leave the other intact.
    /// </summary>
    [Fact]
    public async Task SL12_ReleaseSeat_WithReservationKey_ReleasesExactRow_LeavesOtherReserved()
    {
        // SQLite note: same workaround as SL-11 — use distinct ChildId values (0 and 99)
        // to avoid the full unique index on (SubscriptionId, ChildId) that SQLite enforces.
        // The property under test is key-based row resolution, not ChildId uniqueness.
        using var db  = BuildDb();
        var sub       = await SeedSubscriptionAsync(db, purchasedExtra: 1);

        var key1 = "add-child-seat:1:aaaa11112222";
        var key2 = "add-child-seat:1:bbbb33334444";

        var r1 = new SeatReservation
        {
            SubscriptionId = sub.Id,
            ChildId        = 0,
            ParentUserId   = 1,
            Status         = SeatStatus.Reserved,
            SeatState      = SeatState.Active,
            IdempotencyKey = key1,
            ReservedAt     = DateTime.UtcNow.AddSeconds(-2),
            CreatedAt      = DateTime.UtcNow.AddSeconds(-2),
            CreatedBy      = 0,
        };
        var r2 = new SeatReservation
        {
            SubscriptionId = sub.Id,
            ChildId        = 99,
            ParentUserId   = 1,
            Status         = SeatStatus.Reserved,
            SeatState      = SeatState.Active,
            IdempotencyKey = key2,
            ReservedAt     = DateTime.UtcNow.AddSeconds(-1),
            CreatedAt      = DateTime.UtcNow.AddSeconds(-1),
            CreatedBy      = 0,
        };
        db.SeatReservations.AddRange(r1, r2);
        await db.SaveChangesAsync();

        var settingsMock         = BuildSettingsMock();
        var currentUserMock      = BuildUserMock(userId: 1);
        var loggerMock           = BuildLoggerMock();
        var paymentProviderMock  = new Mock<IPaymentProvider>();
        var parentChildQueryMock = new Mock<IParentChildQuery>();

        var seatService = new SeatService(
            db,
            settingsMock.Object,
            paymentProviderMock.Object,
            currentUserMock.Object,
            loggerMock.Object,
            parentChildQueryMock.Object);

        // Compensation path for key1: must release only r1.
        await seatService.ReleaseSeatAsync(parentUserId: 1, childId: 0, ct: CancellationToken.None,
            reservationKey: key1);

        var reloaded1 = await db.SeatReservations.AsNoTracking().FirstAsync(r => r.Id == r1.Id);
        var reloaded2 = await db.SeatReservations.AsNoTracking().FirstAsync(r => r.Id == r2.Id);

        reloaded1.Status.Should().Be(SeatStatus.Released,
            "SL-12: key1 row must be Released after ReleaseSeat with key1");
        reloaded2.Status.Should().Be(SeatStatus.Reserved,
            "SL-12: key2 row must remain Reserved (must not be released by the key1 compensation call)");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-13 (Finding 2): EnforceAsync applies the voluntary-downgrade cancel marker when
    //        ExtraSeatCancelEffectiveAt <= nowUtc, decrementing PurchasedExtraSeats and
    //        clearing the marker before computing paidActiveSeats.
    // ══════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parent buys 1 extra seat (total 2 paid = 1 included + 1 purchased), has 2 active children,
    /// schedules the extra-seat cancel with ExtraSeatCancelEffectiveAt in the PAST.
    /// Enforcement runs: PurchasedExtraSeats decremented to 0, marker cleared,
    /// over-limit child locked, SeatLedgerEntry{RemovedAtRenewal} written for the subscription
    /// reduction AND SeatLedgerEntry{Locked} for the over-limit child.
    /// </summary>
    [Fact]
    public async Task SL13_Enforcement_AppliesCancelMarker_LocksOverLimitChild()
    {
        using var db  = BuildDb();

        // Free plan (1 included seat) + 1 purchased extra = 2 total initially.
        var sub = new Subscription
        {
            ParentUserId              = 1,
            PlanCode                  = PlanCode.Free,
            BillingPeriod             = BillingPeriod.Monthly,
            Status                    = SubscriptionStatus.Active,
            PurchasedExtraSeats       = 1,
            PendingExtraSeatRemovals  = 1,
            // Marker: effective at yesterday = in the past → must be applied by EnforceAsync.
            ExtraSeatCancelEffectiveAt = DateTime.UtcNow.AddDays(-1),
            CurrentCycleStart         = DateTime.UtcNow.AddDays(-30),
            CurrentCycleEnd           = DateTime.UtcNow.AddDays(-1),
            CreatedAt                 = DateTime.UtcNow.AddMonths(-1),
            CreatedBy                 = 0,
        };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        // Two children; child 10 is oldest (kept), child 20 is newest (over-limit after marker applied).
        await SeedReservationAsync(db, sub.Id, childId: 10, reservedAt: now.AddDays(-20));
        await SeedReservationAsync(db, sub.Id, childId: 20, reservedAt: now.AddDays(-10));

        var svc    = BuildEnforcementService(db, includedFree: 1, maxSeats: 5);
        var locked = await svc.EnforceAsync(sub.Id, CancellationToken.None);

        // Cancel marker must be consumed.
        var reloadedSub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == sub.Id);
        reloadedSub.PurchasedExtraSeats.Should().Be(0,
            "SL-13: PurchasedExtraSeats must be decremented by PendingExtraSeatRemovals at renewal");
        reloadedSub.PendingExtraSeatRemovals.Should().Be(0,
            "SL-13: PendingExtraSeatRemovals must be reset to 0 after applying cancel marker");
        reloadedSub.ExtraSeatCancelEffectiveAt.Should().BeNull(
            "SL-13: ExtraSeatCancelEffectiveAt must be cleared after applying cancel marker");

        // With paidSeats=1, child 20 is over-limit.
        locked.Should().Be(1,
            "SL-13: exactly 1 child must be locked after cancel marker reduces paid seats to 1");

        var r20 = await db.SeatReservations.AsNoTracking().FirstAsync(r => r.ChildId == 20);
        r20.SeatState.Should().Be(SeatState.NoSeatLocked,
            "SL-13: over-limit child must be NoSeatLocked after enforcement with reduced seat count");

        // SeatLedgerEntry{RemovedAtRenewal} must exist for the subscription-level reduction.
        var renewalEntry = await db.SeatLedgerEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SubscriptionId == sub.Id
                                   && e.EventType == SeatLedgerEventType.RemovedAtRenewal
                                   && e.ChildId == null);
        renewalEntry.Should().NotBeNull(
            "SL-13: SeatLedgerEntry{{RemovedAtRenewal}} must be written for the cancel-marker reduction");
        renewalEntry!.Quantity.Should().Be(-1,
            "SL-13: Quantity must be -1 for a single-seat reduction; actual={0}", renewalEntry.Quantity);
        renewalEntry!.IdempotencyKey.Should().NotBeNullOrEmpty(
            "SL-13: RemovedAtRenewal ledger entry must have a deterministic IdempotencyKey (Finding 3)");
        renewalEntry!.IdempotencyKey.Should().StartWith("removed:",
            "SL-13: IdempotencyKey must follow 'removed:{subscriptionId}:{cycleKey}' format");

        // SeatLedgerEntry{Locked} must exist for the over-limit child.
        var lockedEntry = await db.SeatLedgerEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.ChildId == 20 && e.EventType == SeatLedgerEventType.Locked);
        lockedEntry.Should().NotBeNull("SL-13: SeatLedgerEntry{{Locked}} must be written for the locked child");
        lockedEntry!.IdempotencyKey.Should().NotBeNullOrEmpty(
            "SL-13: Locked ledger entry must have a deterministic IdempotencyKey (Finding 3)");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-14 (Finding 3): Enforcement SeatLedgerEntry{Locked} rows carry deterministic
    //        IdempotencyKey values; a second enforcement run cannot re-insert them because
    //        the state guard + key guard both block it.
    // ══════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Run enforcement twice. First run locks 1 child and writes a Locked ledger entry with a
    /// deterministic IdempotencyKey. Second run: the SeatState guard prevents re-locking, so
    /// no second Locked ledger entry is written. The ledger count must not grow.
    /// </summary>
    [Fact]
    public async Task SL14_Enforcement_LockedLedger_HasIdempotencyKey_NoDuplicateOnSecondRun()
    {
        using var db  = BuildDb();
        var sub       = await SeedSubscriptionAsync(db, purchasedExtra: 0); // 1 included seat

        var now = DateTime.UtcNow;
        await SeedReservationAsync(db, sub.Id, childId: 10, reservedAt: now.AddDays(-30)); // kept
        await SeedReservationAsync(db, sub.Id, childId: 20, reservedAt: now.AddDays(-10)); // locked

        var svc = BuildEnforcementService(db, includedFree: 1);

        // First run.
        await svc.EnforceAsync(sub.Id, CancellationToken.None);

        var lockedEntries = await db.SeatLedgerEntries.AsNoTracking()
            .Where(e => e.EventType == SeatLedgerEventType.Locked && e.ChildId == 20)
            .ToListAsync();

        lockedEntries.Should().HaveCount(1, "SL-14: exactly one Locked entry after first run");
        lockedEntries[0].IdempotencyKey.Should().NotBeNullOrEmpty(
            "SL-14: Locked ledger entry must carry a deterministic IdempotencyKey");
        lockedEntries[0].IdempotencyKey.Should().StartWith("lock:",
            "SL-14: IdempotencyKey must follow the 'lock:subscriptionId:childId:cycleKey' format");

        var ledgerCountAfterFirst = await db.SeatLedgerEntries.CountAsync();

        // Second run (idempotent: SeatState guard blocks re-locking, no new Locked row).
        await svc.EnforceAsync(sub.Id, CancellationToken.None);

        var ledgerCountAfterSecond = await db.SeatLedgerEntries.CountAsync();
        ledgerCountAfterSecond.Should().Be(ledgerCountAfterFirst,
            "SL-14: second enforcement run must not write additional ledger rows; " +
            "before={0}, after={1}", ledgerCountAfterFirst, ledgerCountAfterSecond);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // SL-15: Stale-placeholder sweep — orphaned Reserved placeholders are released; fresh
    //        placeholders and real (positive-ChildId) reservations are NOT touched.
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SL15_SweepStalePlaceholders_ReleasesOrphans_KeepsFreshAndRealReservations()
    {
        using var db = BuildDb();
        var sub = await SeedSubscriptionAsync(db, purchasedExtra: 0);
        var now = DateTime.UtcNow;

        // (a) Stale placeholder: negative ChildId, Reserved, reserved long ago → must be released.
        var stale = await SeedReservationAsync(db, sub.Id, childId: -111111,
            status: SeatStatus.Reserved, reservedAt: now.AddHours(-2));

        // (b) Fresh placeholder: negative ChildId, Reserved, reserved just now → must be kept
        //     (an in-flight add-child still completing within the safety window).
        var fresh = await SeedReservationAsync(db, sub.Id, childId: -222222,
            status: SeatStatus.Reserved, reservedAt: now);

        // (c) Real active reservation (positive ChildId) → must be kept regardless of age.
        var real = await SeedReservationAsync(db, sub.Id, childId: 30,
            status: SeatStatus.Active, reservedAt: now.AddDays(-5));

        var svc = BuildEnforcementService(db);

        // Cutoff = 30 min ago → only the 2-hours-old placeholder is past the cutoff.
        var released = await svc.SweepStalePlaceholdersAsync(now.AddMinutes(-30), CancellationToken.None);

        released.Should().Be(1, "only the orphaned 2-hour-old placeholder is past the cutoff");

        var staleRow = await db.SeatReservations.AsNoTracking().FirstAsync(r => r.Id == stale.Id);
        staleRow.Status.Should().Be(SeatStatus.Released, "the orphaned placeholder must be released");
        staleRow.ReleasedAt.Should().NotBeNull();

        var freshRow = await db.SeatReservations.AsNoTracking().FirstAsync(r => r.Id == fresh.Id);
        freshRow.Status.Should().Be(SeatStatus.Reserved, "an in-flight placeholder must NOT be swept");

        var realRow = await db.SeatReservations.AsNoTracking().FirstAsync(r => r.Id == real.Id);
        realRow.Status.Should().Be(SeatStatus.Active, "a real reservation must NOT be swept");

        // Idempotent: a second sweep finds nothing new (released rows are no longer Reserved).
        var secondPass = await svc.SweepStalePlaceholdersAsync(now.AddMinutes(-30), CancellationToken.None);
        secondPass.Should().Be(0, "re-running the sweep is a no-op once orphans are released");
    }
}
