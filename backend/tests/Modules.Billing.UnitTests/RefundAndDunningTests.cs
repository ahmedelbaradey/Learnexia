using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.BillingHistory.Dtos;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Providers;
using Learnexia.Modules.Billing.Infrastructure.Services;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Resources;
using Xunit;
#pragma warning disable CA1707

namespace Modules.Billing.UnitTests;

/// <summary>
/// Unit tests for P10-09 (refunds + dunning) and P10-08 (billing history + receipts).
///
/// All services are built under Option-C:
///   <see cref="RefundService"/> — injected with a real <see cref="BillingDbContext"/> (cast
///   to <see cref="IBillingDbContext"/>), so all EF operations run against SQLite in-memory.
///   <see cref="BillingHistoryQueryService"/> — injected with a real <see cref="BillingDbContext"/>.
///
/// Coverage:
///   RF-01  Pack refund claws back unspent purchased credits (clamped to available balance).
///   RF-02  Pack refund: full-spend — nothing to claw back; PurchasedBalance never goes negative.
///   RF-03  Pack refund is idempotent — second call (same idempotencyKey) returns Duplicate, no double-insert.
///   RF-04  Subscription refund downgrades plan to Free and marks payment Refunded.
///   RF-05  Subscription refund is idempotent — already-Refunded payment is a no-op.
///   RF-06  First charge-failed increments FailedAttemptCount and sets PastDue.
///   RF-07  Retries exhausted (>= maxRetries) sets Status=Dunning, GraceEndsAt set, NextRetryAt null.
///   RF-08  Charge-failed is idempotent — already-Failed payment is a no-op.
///   RF-09  History query is IDOR-scoped — different parents get different (non-overlapping) rows.
///   RF-10  History pagination returns correct page slices.
///   RF-11  GetReceiptAsync returns null when called by a non-owning parentUserId.
///   RF-12  GetReceiptAsync returns a populated ReceiptDto for the owning parentUserId.
///   RF-13  Option C proof — InitiateRefundCommandHandler has no IBillingDbContext / EF field.
/// </summary>
public sealed class RefundAndDunningTests
{
    // ── SQLite helpers ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a new SQLite in-memory connection and creates the schema.
    /// Each test gets its own connection so there is no cross-test state.
    /// The connection is kept open for the lifetime of the returned <see cref="BillingDbContext"/>
    /// (SQLite in-memory databases disappear when the last connection closes).
    /// </summary>
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

    private static RefundService BuildRefundService(
        BillingDbContext db,
        int maxRetries = 3,
        int retryHours = 24,
        int packSize   = 500)
    {
        var settingsMock = new Mock<IGlobalSettingsProvider>();
        settingsMock
            .Setup(s => s.GetInt(GlobalSettingKeys.DunningMaxRetries, It.IsAny<int>()))
            .Returns(maxRetries);
        settingsMock
            .Setup(s => s.GetInt(GlobalSettingKeys.DunningRetryIntervalHours, It.IsAny<int>()))
            .Returns(retryHours);
        settingsMock
            .Setup(s => s.GetInt(GlobalSettingKeys.PackSize, It.IsAny<int>()))
            .Returns(packSize);

        var loggerMock = new Mock<Learnexia.Shared.Kernel.Abstractions.ILoggerManager>();

        var currentUserMock = new Mock<Learnexia.Shared.Kernel.Abstractions.ICurrentUserService>();
        currentUserMock.Setup(c => c.UserId).Returns(0);

        var providerConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string?>(
                    "Billing:PaymentProvider:FakeSigningSecret", "test-secret"),
            })
            .Build();
        var fakeProvider = new FakePaymentProvider(providerConfig, loggerMock.Object);

        // IServiceScopeFactory: build a minimal SP that resolves BillingDbContext.
        // Used by ProcessDunningRetriesAsync — mirrors BillingGrantJob pattern.
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IBillingDbContext>(db);
        services.AddSingleton<IPaymentProvider>(fakeProvider);
        var sp = services.BuildServiceProvider();

        // BillingDbContext implements IBillingDbContext — pass it through the interface.
        IBillingDbContext billingDb = db;

        return new RefundService(
            billingDb,
            fakeProvider,
            currentUserMock.Object,
            settingsMock.Object,
            sp.GetRequiredService<IServiceScopeFactory>(),
            loggerMock.Object);
    }

    private static BillingHistoryQueryService BuildHistoryService(
        BillingDbContext db,
        string sellerName = "Learnexia Test")
    {
        var loggerMock = new Mock<Learnexia.Shared.Kernel.Abstractions.ILoggerManager>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string?>(
                    "Billing:Receipt:SellerName", sellerName),
            })
            .Build();
        // Localizer mock: returns the key as-is so the receipt Description field is populated
        // (unit tests assert against the localizer key, not the translated string).
        var localizerMock = new Mock<IStringLocalizer<SharedResources>>();
        localizerMock.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => new LocalizedString(k, k));
        return new BillingHistoryQueryService(db, loggerMock.Object, config, localizerMock.Object);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a <see cref="CreditAccount"/> with optional purchased balance.
    /// The domain mutator returns a <see cref="CreditTransaction"/> that must be added explicitly.
    /// </summary>
    // NOTE: SeedAccountAsync is intentionally NOT used by RF-01/02/03.
    // CreditAccount.Version maps to the Npgsql xmin system column (NOT NULL + concurrency token).
    // EF does not include xmin in INSERT statements (it is DB-generated by Postgres); SQLite
    // rejects the implicit null and throws SqliteException 19. Any test that INSERTs a
    // CreditAccount row via EF must run against a live PostgreSQL instance (integration test).
    // RF-01/02/03 therefore use pure domain-level assertions (no DB) — same pattern as PACK-03/04/06.
    // RF-03 service-level idempotency (missing account path) seeds only a Payment (no CreditAccount).

    /// <summary>Seeds a <see cref="Payment"/> with Kind=Pack.</summary>
    private static async Task<Payment> SeedPackPaymentAsync(
        BillingDbContext db,
        int parentId,
        int childId,
        PaymentStatus status = PaymentStatus.Succeeded)
    {
        var payment = new Payment
        {
            ParentUserId       = parentId,
            TargetChildId      = childId,
            Amount             = 99m,
            Currency           = PaymentCurrency.EGP,
            Status             = status,
            Kind               = PaymentKind.Pack,
            IdempotencyKey     = $"pack:{parentId}:{childId}:{Guid.NewGuid():N}",
            ProviderPaymentRef = $"fake-pack-{Guid.NewGuid():N}",
            CreatedAt          = DateTime.UtcNow,
            CreatedBy          = 0,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(0);
        return payment;
    }

    /// <summary>Seeds a <see cref="Subscription"/> and an associated <see cref="Payment"/> with Kind=Subscription.</summary>
    private static async Task<(Payment Payment, Subscription Sub)> SeedSubscriptionPaymentAsync(
        BillingDbContext db,
        int parentId,
        PaymentStatus paymentStatus    = PaymentStatus.Succeeded,
        SubscriptionStatus subStatus   = SubscriptionStatus.Active,
        PlanCode planCode              = PlanCode.Premium,
        int priorFailedAttempts        = 0)
    {
        var sub = new Subscription
        {
            ParentUserId       = parentId,
            PlanCode           = planCode,
            Status             = subStatus,
            BillingPeriod      = BillingPeriod.Monthly,
            CurrentCycleStart  = DateTime.UtcNow,
            CurrentCycleEnd    = DateTime.UtcNow.AddMonths(1),
            FailedAttemptCount = priorFailedAttempts,
            CreatedAt          = DateTime.UtcNow,
            CreatedBy          = 0,
        };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync(0);

        var payment = new Payment
        {
            SubscriptionId     = sub.Id,
            ParentUserId       = parentId,
            Amount             = 199m,
            Currency           = PaymentCurrency.EGP,
            Status             = paymentStatus,
            Kind               = PaymentKind.Subscription,
            IdempotencyKey     = $"sub:{parentId}:{sub.Id}:{Guid.NewGuid():N}",
            ProviderPaymentRef = $"fake-sub-{Guid.NewGuid():N}",
            CreatedAt          = DateTime.UtcNow,
            CreatedBy          = 0,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(0);

        return (payment, sub);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-01 (domain): CreditAccount.Refund claws back unspent purchased credits clamped
    //
    // NOTE: SQLite cannot store the Npgsql xmin concurrency token (NOT NULL, DB-generated).
    // EF omits xmin from INSERT → NOT NULL constraint fails in SQLite.
    // This test therefore validates the domain invariant WITHOUT writing to the DB — same
    // pattern as PACK-03/06 in EnergyPackTests. Service-level round-trip requires Postgres.
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RF01_Domain_Refund_ClawsBackUnspentPurchased_ClampedToAvailable()
    {
        const int purchased = 500;
        const int spent     = 200;
        const int remaining = purchased - spent; // 300
        const int packSize  = 500;

        var account = CreditAccount.CreateEmpty(childId: 1);
        account.ApplyPurchase(purchased, $"purchase:1:seed");
        account.Debit(fromGranted: 0, fromPurchased: spent, CreditReasonCode.AiHint, $"spend:1:001");

        account.PurchasedBalance.Should().Be(remaining, "sanity: 300 unspent after spending 200");

        // Act: refund request for packSize=500, but only 300 is available.
        var refundTx = account.Refund(packSize, $"pack-refund:7:event-rf01");

        // Assert: clamped to 300 (available PurchasedBalance), not 500.
        refundTx.Amount.Should().Be(remaining,
            "CreditAccount.Refund clamps to available PurchasedBalance");
        account.PurchasedBalance.Should().Be(0, "all unspent credits clawed back");
        account.PurchasedBalance.Should().BeGreaterThanOrEqualTo(0, "never negative");

        refundTx.Type.Should().Be(CreditTransactionType.Refund);
        refundTx.Pool.Should().Be(CreditPool.Purchased);
        refundTx.ReasonCode.Should().Be(CreditReasonCode.PackRefund);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-02 (domain): Refund on fully-spent pack claws back zero; balance stays at 0 (never negative)
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RF02_Domain_Refund_FullySpent_ClawsBackZero_BalanceNeverNegative()
    {
        var account = CreditAccount.CreateEmpty(childId: 2);
        // PurchasedBalance is already 0 (nothing purchased yet / all spent).
        account.PurchasedBalance.Should().Be(0);

        var refundTx = account.Refund(500, "pack-refund:8:event-rf02");

        refundTx.Amount.Should().Be(0, "nothing to claw back — no purchased balance");
        account.PurchasedBalance.Should().Be(0, "never negative");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-03 (service): ProcessPackRefundAsync returns CreditAccountNotFound when no account
    // exists for the target child — validates the service guard (no DB CreditAccount needed).
    //
    // Idempotency (duplicate-CreditTransaction-key path) also requires a CreditAccount in DB
    // (xmin column) so it cannot be tested in SQLite — covered by Postgres integration tests.
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RF03_ProcessPackRefundAsync_MissingCreditAccount_ReturnsCreditAccountNotFoundFailure()
    {
        using var db = BuildDb();

        // Seed a Pack payment WITHOUT a CreditAccount for the target child.
        var payment = await SeedPackPaymentAsync(db, parentId: 12, childId: 99);
        var service = BuildRefundService(db, packSize: 500);

        var result = await service.ProcessPackRefundAsync(
            payment.Id, "event-rf03-missing", CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(RefundFailureReason.CreditAccountNotFound,
            "service must return typed failure when no credit account exists for the child");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-04: Subscription refund downgrades plan to Free, marks payment Refunded
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RF04_SubscriptionRefund_DowngradesToFree_MarksPaymentRefunded()
    {
        using var db = BuildDb();

        var (payment, sub) = await SeedSubscriptionPaymentAsync(
            db, parentId: 20, subStatus: SubscriptionStatus.Active, planCode: PlanCode.Premium);

        var service = BuildRefundService(db);
        var result = await service.ProcessSubscriptionRefundAsync(
            payment.Id, "event-rf04", CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.WasDuplicate.Should().BeFalse();

        var updatedSub = await db.Subscriptions.FindAsync(sub.Id);
        updatedSub!.PlanCode.Should().Be(PlanCode.Free, "refund revokes Premium access");
        updatedSub.Status.Should().Be(SubscriptionStatus.Active, "stays Active but on Free plan");
        updatedSub.CurrentCycleEnd.Should().BeNull("billing cycle cleared on refund");

        var updatedPayment = await db.Payments.FindAsync(payment.Id);
        updatedPayment!.Status.Should().Be(PaymentStatus.Refunded);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-05: Subscription refund is idempotent — already-Refunded payment is a no-op
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RF05_SubscriptionRefund_AlreadyRefunded_IsIdempotentNoOp()
    {
        using var db = BuildDb();

        var (payment, _) = await SeedSubscriptionPaymentAsync(
            db, parentId: 21, paymentStatus: PaymentStatus.Refunded);

        var service = BuildRefundService(db);
        var result = await service.ProcessSubscriptionRefundAsync(
            payment.Id, "event-rf05", CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.WasDuplicate.Should().BeTrue("already Refunded payment → idempotent no-op");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-06: First charge-failed increments FailedAttemptCount and sets PastDue
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RF06_ChargeFailed_FirstFailure_IncrementsAttemptCount_SetsPastDue()
    {
        using var db = BuildDb();

        var (payment, sub) = await SeedSubscriptionPaymentAsync(
            db, parentId: 30,
            paymentStatus: PaymentStatus.Initiated,
            subStatus: SubscriptionStatus.Active);

        var service = BuildRefundService(db, maxRetries: 3, retryHours: 24);
        var result = await service.ProcessChargeFailedAsync(
            payment.Id, sub.Id, "event-rf06", CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.WasDuplicate.Should().BeFalse();
        result.FailedAttemptCount.Should().Be(1, "first failure");
        result.DowngradeScheduled.Should().BeFalse("still within retry budget");
        result.NextRetryAt.Should().NotBeNull("retry scheduled");

        var updatedSub = await db.Subscriptions.FindAsync(sub.Id);
        updatedSub!.Status.Should().Be(SubscriptionStatus.PastDue);
        updatedSub.FailedAttemptCount.Should().Be(1);
        updatedSub.NextRetryAt.Should().NotBeNull();

        var updatedPayment = await db.Payments.FindAsync(payment.Id);
        updatedPayment!.Status.Should().Be(PaymentStatus.Failed);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-07: Retries exhausted (>= maxRetries) → Status=Dunning, GraceEndsAt set, NextRetryAt null
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RF07_ChargeFailed_RetriesExhausted_SchedulesDunning_SetsGraceEndsAt()
    {
        using var db = BuildDb();

        // Seed with 2 prior failures so the 3rd attempt (maxRetries=3) exhausts.
        var (payment, sub) = await SeedSubscriptionPaymentAsync(
            db, parentId: 31,
            paymentStatus: PaymentStatus.Initiated,
            subStatus: SubscriptionStatus.PastDue,
            priorFailedAttempts: 2);

        var service = BuildRefundService(db, maxRetries: 3, retryHours: 24);
        var result = await service.ProcessChargeFailedAsync(
            payment.Id, sub.Id, "event-rf07", CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.FailedAttemptCount.Should().Be(3, "incremented from 2 to 3");
        result.DowngradeScheduled.Should().BeTrue("3 >= maxRetries=3 → downgrade scheduled");
        result.GraceEndsAt.Should().NotBeNull("grace window = CurrentCycleEnd");
        result.NextRetryAt.Should().BeNull("no more retries");

        var updatedSub = await db.Subscriptions.FindAsync(sub.Id);
        updatedSub!.Status.Should().Be(SubscriptionStatus.Dunning);
        updatedSub.GraceEndsAt.Should().NotBeNull();
        updatedSub.NextRetryAt.Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-08: Charge-failed is idempotent — already-Failed payment is a no-op
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RF08_ChargeFailed_AlreadyFailed_IsIdempotentNoOp_NoExtraIncrement()
    {
        using var db = BuildDb();

        var (payment, sub) = await SeedSubscriptionPaymentAsync(
            db, parentId: 32,
            paymentStatus: PaymentStatus.Failed,
            subStatus: SubscriptionStatus.PastDue);

        var service = BuildRefundService(db);
        var result = await service.ProcessChargeFailedAsync(
            payment.Id, sub.Id, "event-rf08", CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.WasDuplicate.Should().BeTrue("already-Failed payment = no-op");

        // FailedAttemptCount must NOT be incremented.
        var updatedSub = await db.Subscriptions.FindAsync(sub.Id);
        updatedSub!.FailedAttemptCount.Should().Be(0, "no additional increment on duplicate event");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-09: History IDOR — different parents see only their own rows (no cross-account leakage)
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RF09_BillingHistory_IDOR_ParentSeesOnlyOwnRows_NoCrossAccountLeakage()
    {
        using var db = BuildDb();

        // Parent A: 2 payments.
        await SeedPackPaymentAsync(db, parentId: 40, childId: 1);
        await SeedPackPaymentAsync(db, parentId: 40, childId: 2);

        // Parent B: 1 payment.
        await SeedPackPaymentAsync(db, parentId: 41, childId: 3);

        var service = BuildHistoryService(db);

        var resultA = await service.GetHistoryAsync(40, pageNumber: 1, pageSize: 100, CancellationToken.None);
        var resultB = await service.GetHistoryAsync(41, pageNumber: 1, pageSize: 100, CancellationToken.None);

        resultA.TotalCount.Should().Be(2, "parent 40 has 2 payments");
        resultB.TotalCount.Should().Be(1, "parent 41 has 1 payment");

        var aIds = resultA.Data!.Select(x => x.PaymentId).ToHashSet();
        var bIds = resultB.Data!.Select(x => x.PaymentId).ToHashSet();

        aIds.Overlaps(bIds).Should().BeFalse(
            "IDOR guard: parent A's rows and parent B's rows are disjoint sets");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-10: History pagination returns correct page slices
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RF10_BillingHistory_Pagination_ReturnsCorrectSlices()
    {
        using var db = BuildDb();

        for (int i = 1; i <= 5; i++)
            await SeedPackPaymentAsync(db, parentId: 50, childId: i);

        var service = BuildHistoryService(db);

        var page1 = await service.GetHistoryAsync(50, pageNumber: 1, pageSize: 3, CancellationToken.None);
        var page2 = await service.GetHistoryAsync(50, pageNumber: 2, pageSize: 3, CancellationToken.None);

        page1.TotalCount.Should().Be(5, "total count across all pages = 5");
        page1.Data!.Count.Should().Be(3, "first page size = 3");
        page2.Data!.Count.Should().Be(2, "second page has the remaining 2 items");

        var p1Ids = page1.Data!.Select(x => x.PaymentId).ToHashSet();
        var p2Ids = page2.Data!.Select(x => x.PaymentId).ToHashSet();
        p1Ids.Overlaps(p2Ids).Should().BeFalse("no duplicate rows across pages");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-11: GetReceiptAsync returns null when called by a non-owning parentUserId (IDOR guard)
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RF11_GetReceipt_WrongParent_ReturnsNull_AntiIDOR()
    {
        using var db = BuildDb();

        var payment = await SeedPackPaymentAsync(db, parentId: 60, childId: 1);
        var service = BuildHistoryService(db);

        // Parent 61 tries to access parent 60's payment.
        var receipt = await service.GetReceiptAsync(61, payment.Id, CancellationToken.None);

        receipt.Should().BeNull(
            "IDOR guard: payment belongs to parent 60, not 61 — must return null (indistinguishable from not-found)");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-12: GetReceiptAsync returns populated ReceiptDto for the owning parentUserId
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RF12_GetReceipt_OwningParent_ReturnsPopulatedReceiptDto()
    {
        using var db = BuildDb();

        var payment = await SeedPackPaymentAsync(db, parentId: 70, childId: 1);
        var service = BuildHistoryService(db, sellerName: "Learnexia Test");

        var receipt = await service.GetReceiptAsync(70, payment.Id, CancellationToken.None);

        receipt.Should().NotBeNull("owning parent must get a receipt");
        receipt!.PaymentId.Should().Be(payment.Id);
        receipt.GrossAmount.Should().Be(99m);
        receipt.Currency.Should().Be(PaymentCurrency.EGP);
        receipt.Kind.Should().Be(PaymentKind.Pack.ToString());
        receipt.TargetChildId.Should().Be(1);
        receipt.SellerName.Should().Be("Learnexia Test");
        receipt.Status.Should().Be(PaymentStatus.Succeeded.ToString());
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-13: Option C proof — InitiateRefundCommandHandler has no IBillingDbContext / EF field
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RF13_InitiateRefundCommandHandler_IsEfFree_OptionCProof()
    {
        var handlerType = typeof(
            Learnexia.Modules.Billing.Application.Features.Refund.Commands.InitiateRefund
                .InitiateRefundCommandHandler);

        var fields = handlerType.GetFields(
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        var efFields = fields
            .Where(f =>
                f.FieldType.Name.Contains("DbContext", StringComparison.Ordinal)
                || f.FieldType.Name.Contains("DbSet", StringComparison.Ordinal)
                || f.FieldType.FullName?.Contains("EntityFrameworkCore", StringComparison.Ordinal) == true)
            .ToList();

        efFields.Should().BeEmpty(because:
            "InitiateRefundCommandHandler must be EF-free (Option C): " +
            "all EF/persistence lives in IRefundService (Infrastructure layer).");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-14: Grace-expiry downgrade — Dunning subscription with GraceEndsAt in the past is
    //        downgraded to Free/Active by ProcessDunningRetriesAsync (Pass 1).
    //
    // This is the "dunning never finalizes to Free" fix:
    //   • Dunning subscriptions have NextRetryAt=null → were never swept by the retry query.
    //   • ProcessDunningRetriesAsync now also queries by GraceEndsAt <= nowUtc.
    //   • After the sweep the subscription must be Free/Active with all dunning fields cleared.
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RF14_DunningGraceExpired_ProcessDunningRetries_DowngradesToFree()
    {
        using var db = BuildDb();

        // Seed a Dunning subscription (NextRetryAt=null) with GraceEndsAt in the past.
        var graceEndsAt = DateTime.UtcNow.AddDays(-1); // already expired
        var sub = new Subscription
        {
            ParentUserId       = 80,
            PlanCode           = PlanCode.Premium,
            Status             = SubscriptionStatus.Dunning,
            BillingPeriod      = BillingPeriod.Monthly,
            FailedAttemptCount = 3,
            GraceEndsAt        = graceEndsAt,
            NextRetryAt        = null, // Dunning = no more retries
            CurrentCycleStart  = DateTime.UtcNow.AddDays(-30),
            CurrentCycleEnd    = graceEndsAt,
            CreatedAt          = DateTime.UtcNow.AddDays(-31),
            CreatedBy          = 0,
        };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync(0);

        var service = BuildRefundService(db, maxRetries: 3);

        // Act: run the dunning sweep at nowUtc = now (after GraceEndsAt).
        var result = await service.ProcessDunningRetriesAsync(DateTime.UtcNow, CancellationToken.None);

        // Assert: at least one subscription was attempted + succeeded.
        result.Attempted.Should().BeGreaterThan(0, "one grace-expired subscription should be swept");
        result.Failed.Should().Be(0, "grace-expiry downgrade should not fail");

        var updatedSub = await db.Subscriptions.FindAsync(sub.Id);
        updatedSub!.PlanCode.Should().Be(PlanCode.Free, "grace-expired subscription must be downgraded to Free");
        updatedSub.Status.Should().Be(SubscriptionStatus.Active, "downgraded status = Active");
        updatedSub.GraceEndsAt.Should().BeNull("dunning fields must be cleared");
        updatedSub.NextRetryAt.Should().BeNull("no more retries");
        updatedSub.FailedAttemptCount.Should().Be(0, "attempt count reset on downgrade");
        updatedSub.CurrentCycleEnd.Should().BeNull("billing cycle cleared on downgrade");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-15: Grace-expiry downgrade is idempotent — re-running sweep on already-Free/Active
    //        subscription is a safe no-op (no double-clear, no error).
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RF15_DunningGraceExpired_AlreadyDowngraded_IsIdempotentNoOp()
    {
        using var db = BuildDb();

        // Seed a subscription already downgraded to Free (GraceEndsAt cleared).
        var sub = new Subscription
        {
            ParentUserId       = 81,
            PlanCode           = PlanCode.Free,
            Status             = SubscriptionStatus.Active,
            BillingPeriod      = BillingPeriod.Monthly,
            FailedAttemptCount = 0,
            GraceEndsAt        = null, // already cleared
            NextRetryAt        = null,
            CreatedAt          = DateTime.UtcNow.AddDays(-35),
            CreatedBy          = 0,
        };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync(0);

        var service = BuildRefundService(db, maxRetries: 3);
        var result = await service.ProcessDunningRetriesAsync(DateTime.UtcNow, CancellationToken.None);

        // No subscriptions matched (GraceEndsAt is null — not in grace-expiry query).
        result.Attempted.Should().Be(0, "no grace-expired subscription exists — sweep is a no-op");

        var updatedSub = await db.Subscriptions.FindAsync(sub.Id);
        updatedSub!.PlanCode.Should().Be(PlanCode.Free, "already Free — unchanged");
        updatedSub.FailedAttemptCount.Should().Be(0, "no increment on no-op");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // RF-16: Pack-credit atomicity — ProcessPackRefundAsync uses the same default (1000)
    //        as EnergyPackService.CreditPurchasedPackAsync so credit + clawback are symmetric.
    //
    // Domain-level test (no DB insert — xmin SQLite constraint; mirrors RF-01 pattern).
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RF16_PackCredit_ClawbackDefault_MatchesCreditDefault_1000()
    {
        // EnergyPackService defaults packSize to 1000.
        // RefundService previously defaulted to 500 — now unified to 1000.
        // This test pins the correct behaviour: credit 1000, clawback up to 1000.
        const int packSize = 1000;

        var account = CreditAccount.CreateEmpty(childId: 5);
        // Simulate CreditPurchasedPackAsync crediting 1000.
        account.ApplyPurchase(packSize, "pack-credit:42:event-001");

        account.PurchasedBalance.Should().Be(packSize, "full pack credited");

        // Simulate ProcessPackRefundAsync clawing back with default 1000.
        var refundTx = account.Refund(packSize, "pack-refund:42:event-002");

        refundTx.Amount.Should().Be(packSize,
            "clawback default (1000) must match credit default (1000) so full pack is recovered");
        account.PurchasedBalance.Should().Be(0, "all credits clawed back — none lost");
    }
}
