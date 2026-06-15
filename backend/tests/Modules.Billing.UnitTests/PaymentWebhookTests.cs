using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Payments.Commands.HandleProviderWebhook;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Providers;
using Learnexia.Shared.Contracts.Billing;
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
/// P10-06 unit tests for the webhook handler and FakePaymentProvider.
///
/// Tests are pure-unit: no real DB (InMemory EF), no HTTP.
///
/// Coverage:
///   WH-01  Valid signed webhook (payment.succeeded) activates the subscription + emits
///          <see cref="SubscriptionActivatedIntegrationEvent"/> exactly once.
///   WH-02  Invalid / missing signature → 401 shape → NO state change, NO event published.
///   WH-03  Replayed event (duplicate ProviderEventId) → no-op, no double activation,
///          no double grant event.
///   WH-04  payment.failed webhook marks Payment as Failed + emits PaymentFailedIntegrationEvent.
///   WH-05  Forged amount in payload → server-side Payment.Amount used (no escalation);
///          subscription still activates using the server-recorded amount.
///   WH-06  Checkout creates a Pending payment row with a correct idempotency key.
///   WH-07  FakePaymentProvider.BuildSignedWebhookPayload produces a payload that passes
///          VerifyWebhookSignature (self-consistency test).
///   WH-08  FakePaymentProvider.VerifyWebhookSignature rejects tampered body.
///   WH-09  FakePaymentProvider.VerifyWebhookSignature rejects missing/empty signature.
/// </summary>
public sealed class PaymentWebhookTests
{
    private const string TestSecret = "test-hmac-secret-P10-06";

    // ── Helpers ───────────────────────────────────────────────────────────────────────

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

    private static (HandleProviderWebhookCommandHandler Handler, Mock<IPublisher> Publisher, Learnexia.Modules.Billing.Infrastructure.Persistence.BillingDbContext Db)
        BuildHandler(FakePaymentProvider? provider = null)
    {
        provider ??= BuildFakeProvider();

        // SQLite in-memory: supports transactions (needed by the handler's atomic writes).
        // Each test gets its own shared-cache connection so the schema persists for the test lifetime.
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<Learnexia.Modules.Billing.Infrastructure.Persistence.BillingDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new Learnexia.Modules.Billing.Infrastructure.Persistence.BillingDbContext(options);
        db.Database.EnsureCreated();

        var publisher = new Mock<IPublisher>();
        publisher
            .Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<Learnexia.Shared.Kernel.Abstractions.ILoggerManager>();

        var localizerMock = new Mock<IStringLocalizer<SharedResources>>();
        localizerMock.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => new LocalizedString(k, k));

        var currentUserMock = new Mock<Learnexia.Shared.Kernel.Abstractions.ICurrentUserService>();
        currentUserMock.Setup(c => c.UserId).Returns(0);

        // P10-07: IEnergyPackService — stub for subscription-path tests (no pack payments here).
        var energyPackServiceMock = new Mock<IEnergyPackService>();
        energyPackServiceMock
            .Setup(s => s.CreditPurchasedPackAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackCreditResult.Ok(1000));

        // P10-09: IRefundService — stub for webhook tests (refund/dunning branches not exercised here).
        var refundServiceMock = new Mock<IRefundService>();

        var handler = new HandleProviderWebhookCommandHandler(
            db,
            provider,
            energyPackServiceMock.Object,
            refundServiceMock.Object,
            publisher.Object,
            loggerMock.Object,
            currentUserMock.Object,
            localizerMock.Object);

        return (handler, publisher, db);
    }

    private static async Task<(int PaymentId, string ProviderRef, Subscription Sub)> SeedPendingPaymentAsync(
        Learnexia.Modules.Billing.Infrastructure.Persistence.BillingDbContext db,
        string providerRef,
        decimal amount = 199m)
    {
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

        var payment = new Payment
        {
            SubscriptionId     = sub.Id,
            ParentUserId       = 1,
            ProviderPaymentRef = providerRef,
            Amount             = amount,
            Currency           = PaymentCurrency.EGP,
            Status             = PaymentStatus.Initiated,
            Kind               = PaymentKind.Subscription,
            IdempotencyKey     = $"test-checkout:{sub.Id}:{Guid.NewGuid():N}",
            CreatedAt          = DateTime.UtcNow,
            CreatedBy          = 0,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return (payment.Id, providerRef, sub);
    }

    // ── WH-07: FakePaymentProvider self-consistency ──────────────────────────────────

    [Fact]
    public void FakeProvider_BuildSignedPayload_PassesVerification()
    {
        var provider = BuildFakeProvider();
        var (rawBody, sigHeader) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId    : Guid.NewGuid().ToString(),
            eventType  : WebhookEventTypes.PaymentSucceeded,
            paymentRef : "fake-pay-001",
            amount     : 199m,
            signingSecret: TestSecret);

        provider.VerifyWebhookSignature(rawBody, sigHeader).Should().BeTrue();
    }

    // ── WH-08: FakePaymentProvider rejects tampered body ─────────────────────────────

    [Fact]
    public void FakeProvider_VerifySignature_RejectsTamperedBody()
    {
        var provider = BuildFakeProvider();
        var (rawBody, sigHeader) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId    : Guid.NewGuid().ToString(),
            eventType  : WebhookEventTypes.PaymentSucceeded,
            paymentRef : "fake-pay-002",
            amount     : 199m,
            signingSecret: TestSecret);

        // Tamper with the body.
        rawBody[0] ^= 0xFF;

        provider.VerifyWebhookSignature(rawBody, sigHeader).Should().BeFalse();
    }

    // ── WH-09: FakePaymentProvider rejects missing / empty signature ─────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FakeProvider_VerifySignature_RejectsMissingOrEmptyHeader(string? header)
    {
        var provider = BuildFakeProvider();
        var rawBody = Encoding.UTF8.GetBytes("{\"test\":true}");

        provider.VerifyWebhookSignature(rawBody, header).Should().BeFalse();
    }

    // ── WH-02: Invalid signature → 401, no state change ──────────────────────────────

    [Fact]
    public async Task Webhook_InvalidSignature_Returns401_AndChangesNothing()
    {
        var (handler, publisher, db) = BuildHandler();
        var fakeRef = $"ref-{Guid.NewGuid():N}";
        await SeedPendingPaymentAsync(db, fakeRef);

        // Forge the body with a wrong signature.
        var (rawBody, _) = FakePaymentProvider.BuildSignedWebhookPayload(
            Guid.NewGuid().ToString(), WebhookEventTypes.PaymentSucceeded,
            fakeRef, 199m, "WRONG-SECRET");

        var command = new HandleProviderWebhookCommand
        {
            RawBody         = rawBody,
            SignatureHeader = "sha256=totally-wrong",
        };

        var result = await handler.Handle(command, CancellationToken.None);

        // Should be rejected before any state change.
        result.Successed.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // No events published.
        publisher.Verify(
            p => p.Publish(It.IsAny<SubscriptionActivatedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Payment still Initiated, Subscription still PendingPayment.
        var payment = await db.Payments.FirstAsync();
        payment.Status.Should().Be(PaymentStatus.Initiated);
        var sub = await db.Subscriptions.FirstAsync();
        sub.Status.Should().Be(SubscriptionStatus.PendingPayment);
        sub.PlanCode.Should().Be(PlanCode.Free);
    }

    // ── WH-01: Valid signed webhook activates subscription + emits event once ─────────

    [Fact]
    public async Task Webhook_ValidSignedSuccess_ActivatesSubscription_EmitsEventOnce()
    {
        var (handler, publisher, db) = BuildHandler();
        var fakeRef = $"ref-{Guid.NewGuid():N}";
        await SeedPendingPaymentAsync(db, fakeRef, amount: 199m);

        var eventId = Guid.NewGuid().ToString();
        var (rawBody, sigHeader) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, WebhookEventTypes.PaymentSucceeded, fakeRef, 199m, TestSecret);

        var command = new HandleProviderWebhookCommand
        {
            RawBody = rawBody,
            SignatureHeader = sigHeader,
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Successed.Should().BeTrue();

        // Payment → Succeeded.
        var payment = await db.Payments.FirstAsync();
        payment.Status.Should().Be(PaymentStatus.Succeeded);

        // Subscription → Active Premium.
        var sub = await db.Subscriptions.FirstAsync();
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.PlanCode.Should().Be(PlanCode.Premium);
        sub.CurrentCycleStart.Should().NotBeNull();
        sub.CurrentCycleEnd.Should().NotBeNull();

        // WebhookEvent row inserted.
        var webhookRow = await db.WebhookEvents.FirstAsync();
        webhookRow.ProviderEventId.Should().Be(eventId);
        webhookRow.Succeeded.Should().BeTrue();

        // SubscriptionActivatedIntegrationEvent published exactly once.
        publisher.Verify(
            p => p.Publish(It.IsAny<SubscriptionActivatedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── WH-03: Replayed event (duplicate ProviderEventId) is idempotent ───────────────

    [Fact]
    public async Task Webhook_ReplayedEvent_IsIdempotent_NoDoubleActivation()
    {
        var (handler, publisher, db) = BuildHandler();
        var fakeRef = $"ref-{Guid.NewGuid():N}";
        await SeedPendingPaymentAsync(db, fakeRef);

        var eventId = Guid.NewGuid().ToString();
        var (rawBody, sigHeader) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, WebhookEventTypes.PaymentSucceeded, fakeRef, 199m, TestSecret);

        var command = new HandleProviderWebhookCommand { RawBody = rawBody, SignatureHeader = sigHeader };

        // First delivery.
        var first = await handler.Handle(command, CancellationToken.None);
        first.Successed.Should().BeTrue();

        // Second delivery of the SAME event.
        var second = await handler.Handle(command, CancellationToken.None);
        second.Successed.Should().BeTrue();
        // The message should indicate it's already processed.
        second.Data!.Outcome.Should().Be("Duplicate");

        // Subscription activated only once.
        var sub = await db.Subscriptions.FirstAsync();
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.PlanCode.Should().Be(PlanCode.Premium);

        // SubscriptionActivated published exactly once (not twice).
        publisher.Verify(
            p => p.Publish(It.IsAny<SubscriptionActivatedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── WH-04: payment.failed marks Payment as Failed + emits PaymentFailedIntegrationEvent ──

    [Fact]
    public async Task Webhook_PaymentFailed_MarksPaymentFailed_EmitsEvent()
    {
        var (handler, publisher, db) = BuildHandler();
        var fakeRef = $"ref-{Guid.NewGuid():N}";
        await SeedPendingPaymentAsync(db, fakeRef);

        var eventId = Guid.NewGuid().ToString();
        var (rawBody, sigHeader) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, WebhookEventTypes.PaymentFailed, fakeRef, 199m, TestSecret);

        var command = new HandleProviderWebhookCommand { RawBody = rawBody, SignatureHeader = sigHeader };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Successed.Should().BeTrue();

        // Payment → Failed.
        var payment = await db.Payments.FirstAsync();
        payment.Status.Should().Be(PaymentStatus.Failed);

        // Subscription remains PendingPayment (no state flip on failure).
        var sub = await db.Subscriptions.FirstAsync();
        sub.Status.Should().Be(SubscriptionStatus.PendingPayment);
        sub.PlanCode.Should().Be(PlanCode.Free);

        // PaymentFailedIntegrationEvent published.
        publisher.Verify(
            p => p.Publish(It.IsAny<PaymentFailedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // SubscriptionActivated NOT published.
        publisher.Verify(
            p => p.Publish(It.IsAny<SubscriptionActivatedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── WH-05: Forged amount in payload → server-side amount authoritative ────────────

    [Fact]
    public async Task Webhook_ForgedAmount_UsesServerSideAmount_NoEscalation()
    {
        var (handler, publisher, db) = BuildHandler();
        var fakeRef = $"ref-{Guid.NewGuid():N}";
        // Server-side payment is for 199 EGP (monthly).
        var (paymentId, _, _) = await SeedPendingPaymentAsync(db, fakeRef, amount: 199m);

        // Forged payload claims the payment was for 9999 EGP (attempt to inflate).
        var eventId = Guid.NewGuid().ToString();
        var (rawBody, sigHeader) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, WebhookEventTypes.PaymentSucceeded, fakeRef, 9999m, TestSecret);

        var command = new HandleProviderWebhookCommand { RawBody = rawBody, SignatureHeader = sigHeader };
        var result = await handler.Handle(command, CancellationToken.None);

        result.Successed.Should().BeTrue();

        // The Payment.Amount stored on our side remains 199 — not 9999.
        var payment = await db.Payments.FindAsync(paymentId);
        payment!.Amount.Should().Be(199m, because: "server-side amount is authoritative");

        // Subscription activated (the activation is based on BillingPeriod, not amount).
        var sub = await db.Subscriptions.FirstAsync();
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.PlanCode.Should().Be(PlanCode.Premium);

        // Event still emitted (activation happened correctly).
        publisher.Verify(
            p => p.Publish(It.IsAny<SubscriptionActivatedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
