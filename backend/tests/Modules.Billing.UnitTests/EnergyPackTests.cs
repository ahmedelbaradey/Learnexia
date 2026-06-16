using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Payments.Commands.HandleProviderWebhook;
using Learnexia.Modules.Billing.Application.Features.Payments.Commands.StartPackCheckout;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Providers;
using Learnexia.Modules.Billing.Infrastructure.Services;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Settings;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Moq;
using Resources;
using Xunit;

#pragma warning disable CA1707

namespace Modules.Billing.UnitTests;

/// <summary>
/// P10-07 unit tests — Buy an energy pack (never-expires purchased credits).
///
/// Test layers:
/// <list type="bullet">
///   <item><strong>Domain-level</strong> (PACK-03/04/06): verify <see cref="CreditAccount.ApplyPurchase"/>,
///         <see cref="CreditAccount.Debit"/> and the <see cref="CreditTransaction"/> ledger row — no EF,
///         no SQLite, pure in-memory.</item>
///   <item><strong>Service-level with SQLite</strong> (PACK-01/02): <see cref="EnergyPackService.StartPackCheckoutAsync"/>
///         does not touch <see cref="CreditAccount"/>, so the Npgsql-only <c>xmin</c> row-version
///         constraint is never triggered.</item>
///   <item><strong>Webhook handler with mocked IEnergyPackService</strong> (PACK-05/07/E2E):
///         tests the <see cref="HandleProviderWebhookCommandHandler"/> routing; the real
///         EnergyPackService is NOT used so there is no SQLite/xmin issue. The end-to-end
///         behavioral contract is verified: forged sig → no credit; subscription branch unaffected;
///         valid pack webhook → handler calls CreditPurchasedPackAsync exactly once.</item>
///   <item><strong>Reflection</strong> (PACK-08): confirms <see cref="StartPackCheckoutCommandHandler"/>
///         holds no <c>IBillingDbContext</c> / EF field (Option C proof).</item>
/// </list>
///
/// The <c>xmin</c> column (Npgsql optimistic-concurrency token) is not emulated in SQLite —
/// tests that would need to UPDATE a <c>CreditAccount</c> row via EF use the domain-entity
/// approach (no DB) instead. Integration tests against a live Postgres cover the full DB path.
/// </summary>
public sealed class EnergyPackTests
{
    private const string TestSecret = "test-hmac-secret-P10-07";
    private const int DefaultPackSize = 1000;
    private const decimal DefaultPackPrice = 50m;

    // ── SQLite helpers (for tests that don't touch CreditAccount) ─────────────────────────

    private static BillingDbContext BuildInMemoryDb()
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

    private static FakePaymentProvider BuildFakeProvider(string? secret = TestSecret)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(secret is null
                ? Array.Empty<KeyValuePair<string, string?>>()
                : new[] { new KeyValuePair<string, string?>("Billing:PaymentProvider:FakeSigningSecret", secret) })
            .Build();
        var loggerMock = new Mock<Learnexia.Shared.Kernel.Abstractions.ILoggerManager>();
        return new FakePaymentProvider(config, loggerMock.Object);
    }

    private static IGlobalSettingsProvider BuildSettings(int packSize = DefaultPackSize, decimal packPrice = DefaultPackPrice)
    {
        var settingsMock = new Mock<IGlobalSettingsProvider>();
        settingsMock.Setup(s => s.GetInt(GlobalSettingKeys.PackSize, It.IsAny<int>())).Returns(packSize);
        settingsMock.Setup(s => s.GetDecimal(GlobalSettingKeys.PackPriceEgp, It.IsAny<decimal>())).Returns(packPrice);
        return settingsMock.Object;
    }

    private static EnergyPackService BuildEnergyPackService(
        BillingDbContext db,
        bool parentOwnsChild = true,
        int packSize = DefaultPackSize,
        decimal packPrice = DefaultPackPrice,
        FakePaymentProvider? provider = null)
    {
        provider ??= BuildFakeProvider();
        var settings = BuildSettings(packSize, packPrice);

        var parentChildMock = new Mock<IParentChildQuery>();
        parentChildMock
            .Setup(p => p.IsParentOfChildAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentOwnsChild);

        var currentUserMock = new Mock<Learnexia.Shared.Kernel.Abstractions.ICurrentUserService>();
        currentUserMock.Setup(c => c.UserId).Returns(1);

        var loggerMock = new Mock<Learnexia.Shared.Kernel.Abstractions.ILoggerManager>();

        return new EnergyPackService(db, provider, settings, parentChildMock.Object, currentUserMock.Object, loggerMock.Object);
    }

    /// <summary>
    /// Builds the webhook handler, wiring up a MOCKED <see cref="IEnergyPackService"/> via the real
    /// <see cref="WebhookEventService"/> (Option C: handler injects IWebhookEventService; real service
    /// does all the EF writes). The mock pack service is returned so callers can verify interactions.
    /// </summary>
    private static (HandleProviderWebhookCommandHandler Handler, Mock<IPublisher> Publisher,
                    Mock<IEnergyPackService> PackService, BillingDbContext Db)
        BuildWebhookHandlerWithMocks(
            FakePaymentProvider? provider = null,
            PackCreditResult? packCreditResult = null)
    {
        provider ??= BuildFakeProvider();
        var db = BuildInMemoryDb();

        var publisherMock = new Mock<IPublisher>();
        publisherMock
            .Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var packServiceMock = new Mock<IEnergyPackService>();
        packServiceMock
            .Setup(s => s.CreditPurchasedPackAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(packCreditResult ?? PackCreditResult.Ok(DefaultPackSize));

        // P10-09: IRefundService stub — not exercised by pack tests; mock routes to no-op.
        var refundServiceMock = new Mock<IRefundService>();

        var loggerMock = new Mock<Learnexia.Shared.Kernel.Abstractions.ILoggerManager>();
        var localizerMock = new Mock<IStringLocalizer<SharedResources>>();
        localizerMock.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => new LocalizedString(k, k));

        // Option C: build the real WebhookEventService (owns EF/transaction/event logic).
        var webhookEventService = new WebhookEventService(
            db,
            packServiceMock.Object,
            refundServiceMock.Object,
            publisherMock.Object,
            loggerMock.Object);

        var handler = new HandleProviderWebhookCommandHandler(
            provider,
            webhookEventService,
            loggerMock.Object,
            localizerMock.Object);

        return (handler, publisherMock, packServiceMock, db);
    }

    /// <summary>Seeds a Payment{Kind=Pack} row and returns it.</summary>
    private static async Task<Payment> SeedPackPaymentAsync(
        BillingDbContext db,
        int parentId,
        int childId,
        string providerRef,
        decimal amount = DefaultPackPrice)
    {
        var payment = new Payment
        {
            ParentUserId       = parentId,
            TargetChildId      = childId,
            ProviderPaymentRef = providerRef,
            Amount             = amount,
            Currency           = PaymentCurrency.EGP,
            Status             = PaymentStatus.Initiated,
            Kind               = PaymentKind.Pack,
            IdempotencyKey     = $"pack-checkout:{parentId}:{childId}:{Guid.NewGuid():N}",
            CreatedAt          = DateTime.UtcNow,
            CreatedBy          = 0,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return payment;
    }

    // ── PACK-01: Checkout creates Kind=Pack Initiated payment ───────────────────────────────

    [Fact]
    public async Task PACK01_Checkout_CreatesPackPaymentRow_WithCorrectKind()
    {
        using var db = BuildInMemoryDb();
        var service = BuildEnergyPackService(db, parentOwnsChild: true);

        var result = await service.StartPackCheckoutAsync(
            parentId       : 1,
            childId        : 42,
            idempotencyKey : $"pack-checkout:1:42:{Guid.NewGuid():N}",
            ct             : CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.PaymentId.Should().BeGreaterThan(0);
        result.RedirectUrl.Should().NotBeNullOrEmpty();

        var payment = await db.Payments.FindAsync(result.PaymentId);
        payment.Should().NotBeNull();
        payment!.Kind.Should().Be(PaymentKind.Pack);
        payment.TargetChildId.Should().Be(42);
        payment.ParentUserId.Should().Be(1);
        payment.Amount.Should().Be(DefaultPackPrice);
        payment.Status.Should().Be(PaymentStatus.Initiated);
    }

    // ── PACK-02: Checkout rejects when parent does not own child ─────────────────────────────

    [Fact]
    public async Task PACK02_Checkout_RejectsWhenParentNotOwner_NoPaymentRowCreated()
    {
        using var db = BuildInMemoryDb();
        var service = BuildEnergyPackService(db, parentOwnsChild: false);

        var result = await service.StartPackCheckoutAsync(
            parentId       : 1,
            childId        : 999,
            idempotencyKey : $"pack-checkout:1:999:{Guid.NewGuid():N}",
            ct             : CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(PackCheckoutFailureReason.ChildNotOwnedByParent);
        db.Payments.Should().BeEmpty();
    }

    // ── PACK-03: CreditAccount.ApplyPurchase credits exact pack amount to PurchasedBalance ─────

    /// <summary>
    /// Domain-level test: verifies that <see cref="CreditAccount.ApplyPurchase"/> increments
    /// <see cref="CreditAccount.PurchasedBalance"/> by exactly the pack size and returns a
    /// <see cref="CreditTransaction"/> with the correct fields (Type=Purchase, Pool=Purchased).
    /// This is the core ledger primitive that <see cref="EnergyPackService.CreditPurchasedPackAsync"/>
    /// delegates to after the DB/transaction plumbing.
    /// </summary>
    [Fact]
    public void PACK03_ApplyPurchase_CreditsExactPackAmount_ToPurchasedBalance()
    {
        const int packSize = 1000;
        var account = CreditAccount.CreateEmpty(childId: 42);
        var idempotencyKey = $"pack-credit:7:{Guid.NewGuid():N}";

        var tx = account.ApplyPurchase(
            amount           : packSize,
            idempotencyKey   : idempotencyKey,
            relatedPaymentId : "7");

        // Balance checks.
        account.PurchasedBalance.Should().Be(packSize);
        account.GrantedBalance.Should().Be(0);
        account.TotalBalance.Should().Be(packSize);

        // Ledger row checks.
        tx.Type.Should().Be(CreditTransactionType.Purchase);
        tx.Pool.Should().Be(CreditPool.Purchased);
        tx.Amount.Should().Be(packSize);
        tx.ReasonCode.Should().Be(CreditReasonCode.PackPurchase);
        tx.IdempotencyKey.Should().Be(idempotencyKey);
        tx.RelatedPaymentId.Should().Be("7");

        // Resulting balance snapshot.
        tx.ResultingPurchasedBalance.Should().Be(packSize);
        tx.ResultingGrantedBalance.Should().Be(0);
    }

    // ── PACK-04: Second ApplyPurchase with same key is guarded by DB unique constraint ─────────

    /// <summary>
    /// Domain-level test: verifies that calling <see cref="CreditAccount.ApplyPurchase"/> twice
    /// (domain mutator) DOES accumulate. The idempotency guard lives at the DB level
    /// (<c>UX_CreditTransactions_IdempotencyKey</c>) and in <see cref="EnergyPackService"/>'s
    /// pre-check. The domain mutator itself is idempotent in intent (identical key) but the
    /// guard is enforced above it — this test documents that contract.
    /// </summary>
    [Fact]
    public void PACK04_ApplyPurchase_IdempotencyKey_IsPassedToTransaction_ForDbGuard()
    {
        const int packSize = 1000;
        var account = CreditAccount.CreateEmpty(childId: 42);
        var idempotencyKey = $"pack-credit:7:{Guid.NewGuid():N}";

        var tx = account.ApplyPurchase(packSize, idempotencyKey, "7");

        // The idempotency key surfaces on the transaction so the DB unique constraint
        // can reject a concurrent duplicate INSERT.
        tx.IdempotencyKey.Should().Be(idempotencyKey);

        // Verifying domain: two ApplyPurchase calls accumulate (the DB guard prevents second call
        // via EnergyPackService's pre-check; this is the domain-only behavior).
        var tx2 = account.ApplyPurchase(packSize, $"pack-credit:7:different-key", "7");
        account.PurchasedBalance.Should().Be(packSize * 2,
            because: "the DB-level guard (not the domain mutator) is what prevents double credit");
        tx2.Should().NotBeNull();
    }

    // ── PACK-05: Forged / invalid-sig webhook credits nothing ───────────────────────────────────

    [Fact]
    public async Task PACK05_ForgedWebhook_CreditsNothing_PackServiceNeverCalled()
    {
        var fakeRef = $"ref-pack-{Guid.NewGuid():N}";
        const int childId = 42;

        var (handler, _, packServiceMock, db) = BuildWebhookHandlerWithMocks();

        // Seed a pack payment so there is something to "target".
        var payment = await SeedPackPaymentAsync(db, parentId: 1, childId: childId, providerRef: fakeRef);

        // Build payload signed with the WRONG secret; pass a non-matching signature header.
        var (rawBody, _) = FakePaymentProvider.BuildSignedWebhookPayload(
            Guid.NewGuid().ToString(),
            WebhookEventTypes.PaymentSucceeded,
            fakeRef,
            DefaultPackPrice,
            signingSecret: "WRONG-SECRET");

        var command = new HandleProviderWebhookCommand
        {
            RawBody         = rawBody,
            SignatureHeader = "sha256=forged-value",
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Successed.Should().BeFalse();

        // IEnergyPackService.CreditPurchasedPackAsync must never have been called.
        packServiceMock.Verify(
            s => s.CreditPurchasedPackAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Payment should remain Initiated (no status flip without a valid webhook).
        var updatedPayment = await db.Payments.FindAsync(payment.Id);
        updatedPayment!.Status.Should().Be(PaymentStatus.Initiated);
    }

    // ── PACK-06: Credited energy is spendable (domain-level proof) ──────────────────────────────

    /// <summary>
    /// Domain-level test: after <see cref="CreditAccount.ApplyPurchase"/> the balance can be
    /// reduced by <see cref="CreditAccount.Debit"/> (purchased pool).
    /// </summary>
    [Fact]
    public void PACK06_CreditedEnergy_IsSpendable_ViaLedgerDebit()
    {
        const int packSize = 1000;
        const int spendAmt = 5;

        var account = CreditAccount.CreateEmpty(childId: 42);

        // Credit the pack.
        account.ApplyPurchase(packSize, $"pack-credit:7:{Guid.NewGuid():N}", "7");
        account.TotalBalance.Should().Be(packSize);

        // Spend from the purchased pool.
        var spendTx = account.Debit(
            fromGranted   : 0,
            fromPurchased : spendAmt,
            reasonCode    : CreditReasonCode.AiHint,
            idempotencyKey: $"spend:{Guid.NewGuid():N}");

        account.PurchasedBalance.Should().Be(packSize - spendAmt);
        account.GrantedBalance.Should().Be(0);
        account.TotalBalance.Should().Be(packSize - spendAmt);

        spendTx.Type.Should().Be(CreditTransactionType.Spend);
        spendTx.Pool.Should().Be(CreditPool.Purchased);
        spendTx.Amount.Should().Be(spendAmt);
        spendTx.FromPurchased.Should().Be(spendAmt);
        spendTx.FromGranted.Should().Be(0);
    }

    // ── PACK-07: Subscription webhook path is unaffected by the Pack branch (regression) ─────────

    [Fact]
    public async Task PACK07_SubscriptionWebhook_UnaffectedByPackBranch_StillActivates()
    {
        var (handler, publisherMock, packServiceMock, db) = BuildWebhookHandlerWithMocks();

        var sub = new Subscription
        {
            ParentUserId  = 1,
            PlanCode      = PlanCode.Free,
            BillingPeriod = BillingPeriod.Monthly,
            Status        = SubscriptionStatus.PendingPayment,
            CreatedAt     = DateTime.UtcNow,
            CreatedBy     = 0,
        };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var fakeRef = $"ref-sub-{Guid.NewGuid():N}";
        var subPayment = new Payment
        {
            SubscriptionId     = sub.Id,
            ParentUserId       = 1,
            ProviderPaymentRef = fakeRef,
            Amount             = 199m,
            Currency           = PaymentCurrency.EGP,
            Status             = PaymentStatus.Initiated,
            Kind               = PaymentKind.Subscription,
            IdempotencyKey     = $"sub-checkout:1:{sub.Id}:{Guid.NewGuid():N}",
            CreatedAt          = DateTime.UtcNow,
            CreatedBy          = 0,
        };
        db.Payments.Add(subPayment);
        await db.SaveChangesAsync();

        var eventId = Guid.NewGuid().ToString();
        var (rawBody, sigHeader) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, WebhookEventTypes.PaymentSucceeded, fakeRef, 199m, TestSecret);

        var result = await handler.Handle(
            new HandleProviderWebhookCommand { RawBody = rawBody, SignatureHeader = sigHeader },
            CancellationToken.None);

        result.Successed.Should().BeTrue();

        // Subscription → Active Premium.
        var updatedSub = await db.Subscriptions.FindAsync(sub.Id);
        updatedSub!.Status.Should().Be(SubscriptionStatus.Active);
        updatedSub.PlanCode.Should().Be(PlanCode.Premium);

        // SubscriptionActivatedIntegrationEvent published exactly once.
        publisherMock.Verify(
            p => p.Publish(It.IsAny<SubscriptionActivatedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // IEnergyPackService.CreditPurchasedPackAsync must NOT have been called for a Subscription payment.
        packServiceMock.Verify(
            s => s.CreditPurchasedPackAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── PACK-08: Option C — StartPackCheckoutCommandHandler is EF-free ──────────────────────────

    [Fact]
    public void PACK08_StartPackCheckoutHandler_IsEfFree_NoIBillingDbContextOrDbSet()
    {
        var handlerType = typeof(StartPackCheckoutCommandHandler);

        var fields = handlerType.GetFields(
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        var dbContextFields = fields
            .Where(f => f.FieldType.Name.Contains("DbContext")
                     || f.FieldType.Name.Contains("DbSet")
                     || f.FieldType.FullName?.Contains("EntityFrameworkCore") == true)
            .ToList();

        dbContextFields.Should().BeEmpty(because:
            "StartPackCheckoutCommandHandler must be EF-free per Option C — " +
            "all EF access lives in IEnergyPackService (Infrastructure layer).");
    }

    // ── PACK-E2E: Valid signed pack webhook → handler calls CreditPurchasedPackAsync once ─────────

    /// <summary>
    /// End-to-end handler path test (mocked IEnergyPackService):
    /// verifies the Pack branch routes correctly — the handler calls
    /// <see cref="IEnergyPackService.CreditPurchasedPackAsync"/> exactly once with the right
    /// paymentId/childId/providerEventId, and returns the correct outcome.
    /// Also verifies the WebhookEvent idempotency row is persisted.
    /// </summary>
    [Fact]
    public async Task PACK_E2E_ValidPackWebhook_Handler_CallsPackService_Once()
    {
        const int childId  = 42;
        var fakeRef        = $"ref-pack-e2e-{Guid.NewGuid():N}";

        var (handler, _, packServiceMock, db) = BuildWebhookHandlerWithMocks(
            packCreditResult: PackCreditResult.Ok(DefaultPackSize));

        var payment = await SeedPackPaymentAsync(db, parentId: 1, childId: childId, providerRef: fakeRef);

        var eventId = Guid.NewGuid().ToString();
        var (rawBody, sigHeader) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, WebhookEventTypes.PaymentSucceeded, fakeRef, DefaultPackPrice, TestSecret);

        var result = await handler.Handle(
            new HandleProviderWebhookCommand { RawBody = rawBody, SignatureHeader = sigHeader },
            CancellationToken.None);

        result.Successed.Should().BeTrue();
        result.Data!.Outcome.Should().Be("PackCredited");

        // IEnergyPackService.CreditPurchasedPackAsync called exactly once with the correct args.
        packServiceMock.Verify(
            s => s.CreditPurchasedPackAsync(
                payment.Id,
                childId,
                eventId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // WebhookEvent idempotency row committed BEFORE the service call.
        var webhookRow = await db.WebhookEvents.FirstAsync(w => w.ProviderEventId == eventId);
        webhookRow.Should().NotBeNull();
        webhookRow.Succeeded.Should().BeTrue();
    }

    // ── PACK-E2E-Replay: Webhook replay (duplicate ProviderEventId) → service not called ──────────

    [Fact]
    public async Task PACK_E2E_Replay_WebhookHandler_IsIdempotent_PackServiceNotCalledTwice()
    {
        const int childId = 42;
        var fakeRef       = $"ref-pack-e2e-replay-{Guid.NewGuid():N}";

        var (handler, _, packServiceMock, db) = BuildWebhookHandlerWithMocks(
            packCreditResult: PackCreditResult.Ok(DefaultPackSize));

        await SeedPackPaymentAsync(db, parentId: 1, childId: childId, providerRef: fakeRef);

        var eventId = Guid.NewGuid().ToString();
        var (rawBody, sigHeader) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, WebhookEventTypes.PaymentSucceeded, fakeRef, DefaultPackPrice, TestSecret);

        var command = new HandleProviderWebhookCommand { RawBody = rawBody, SignatureHeader = sigHeader };

        // First delivery.
        var first = await handler.Handle(command, CancellationToken.None);
        first.Successed.Should().BeTrue();
        first.Data!.Outcome.Should().Be("PackCredited");

        // Replay with the SAME eventId — outer WebhookEvent dedupe short-circuits.
        var second = await handler.Handle(command, CancellationToken.None);
        second.Successed.Should().BeTrue();
        second.Data!.Outcome.Should().Be("Duplicate");

        // Pack service must have been called ONLY once (not on replay).
        packServiceMock.Verify(
            s => s.CreditPurchasedPackAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
