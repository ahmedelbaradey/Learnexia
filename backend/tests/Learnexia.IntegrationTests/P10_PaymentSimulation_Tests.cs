// P10 — Mock Payment Simulation integration tests
//
// Covers POST /api/Billing/Webhooks/Simulate (DevWebhookController, [Authorize(Policy=AdminOnly)])
// against the triple-gate (Provider==Fake + AllowSimulation==true + AdminOnly JWT).
//
// Test scenarios:
//   TC-SIM-01  Enabled happy path — payment.succeeded → 200 + Payment=Succeeded + Subscription Active+Premium
//   TC-SIM-02  Idempotent replay — same simulate twice → second returns "Duplicate", grant not double-applied
//   TC-SIM-03  payment.failed → Payment=Failed state
//   TC-SIM-04  refund.succeeded on a succeeded payment → refund fires (clawback/refund ledger row)
//   TC-SIM-05  Gate off by default — AllowSimulation absent/false → 404
//   TC-SIM-06a Auth — anonymous → 401 (gate on)
//   TC-SIM-06b Auth — parent JWT (non-admin) → 403 (gate on)
//   TC-SIM-07a Validation — paymentId <= 0 → 422
//   TC-SIM-07b Validation — unknown (unsupported) eventType → 422
//   TC-SIM-07c Validation — unknown paymentId (gate on) → 404

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// Integration tests for POST /api/Billing/Webhooks/Simulate (DevWebhookController).
///
/// All enabled-gate tests use a derived factory that overrides
/// <c>Billing:PaymentProvider:AllowSimulation=true</c>.
/// The gate-off test (TC-SIM-05) uses the default factory (AllowSimulation absent/false).
///
/// Pattern mirrors P10_W3_SubscriptionPayment_IntegrationTests:
///   shared LearnexiaWebAppFactory for DB + migrations; per-test derived factory for config overrides.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P10_PaymentSimulation_Tests : IAsyncLifetime
{
    // ── URL constants ──────────────────────────────────────────────────────────
    private const string SignInUrl              = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl      = "api/Users/Authentication/Register-Parent";
    private const string SubscriptionUpgradeUrl = "api/Billing/Subscription/Upgrade";
    private const string SubscriptionCheckoutUrl= "api/Billing/Subscription/Checkout";
    private const string WebhookSimulateUrl     = "api/Billing/Webhooks/Simulate";

    // Seeded admin credentials
    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";

    // Same signing secret used in LearnexiaWebAppFactory (referenced for the enabled-gate config override)
    private const string TestSigningSecret = P10_W3_SubscriptionPayment_IntegrationTests.TestSigningSecret;

    private readonly LearnexiaWebAppFactory _factory;

    // Enabled-gate client: AllowSimulation=true added via WithWebHostBuilder.
    // Created once for the test class because WebHostBuilder mutations are cheap and
    // the DB state is shared across all tests anyway (same container).
    private readonly HttpClient _clientEnabled;

    // Default-gate client: uses the base factory (AllowSimulation absent → false).
    private readonly HttpClient _clientDefault;

    public P10_PaymentSimulation_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;

        // ── Enabled-gate factory (AllowSimulation=true) ───────────────────────
        // Both Billing:PaymentProvider:Provider and FakeSigningSecret are already
        // wired in LearnexiaWebAppFactory.ConfigureAppConfiguration.
        // We only add AllowSimulation=true on top.
        var enabledFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Billing:PaymentProvider:AllowSimulation"] = "true",
                    ["Billing:PaymentProvider:Provider"]        = "Fake",
                    ["Billing:PaymentProvider:FakeSigningSecret"] = TestSigningSecret,
                })));

        _clientEnabled = enabledFactory.CreateClient();
        _clientDefault = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ══════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static string UniqueEmail(string tag = "")
        => $"sim_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@simtest.test";

    /// <summary>Case-insensitive JSON property lookup.</summary>
    private static bool TryProp(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (el.TryGetProperty(pascal, out value)) return true;
        foreach (var p in el.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private async Task<(HttpResponseMessage Resp, JsonElement Root, string Body)>
        SendAsync(HttpClient client, HttpMethod method, string url,
                  object? body = null, string? bearer = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null)
            req.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearer is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        var resp    = await client.SendAsync(req);
        var bodyStr = await resp.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
            try { root = JsonDocument.Parse(bodyStr).RootElement; } catch { }
        return (resp, root, bodyStr);
    }

    private async Task<string> GetAdminTokenAsync(HttpClient? client = null)
    {
        var (resp, root, body) = await SendAsync(
            client ?? _clientEnabled, HttpMethod.Post, SignInUrl,
            new { UserName = AdminUserName, Password = AdminPassword });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"admin sign-in must succeed; body: {body}");
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        return tok.GetString()!;
    }

    private async Task<string> RegisterParentAndGetTokenAsync(HttpClient? client = null)
    {
        var (resp, root, body) = await SendAsync(
            client ?? _clientEnabled, HttpMethod.Post, RegisterParentUrl,
            new { Email = UniqueEmail("par"), Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"parent registration must succeed; body: {body}");
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        return tok.GetString()!;
    }

    /// <summary>
    /// Full subscription setup: register parent → upgrade → checkout.
    /// Returns (paymentId, providerRef, parentToken).
    /// Uses the enabled-gate client so the subsequent Simulate call shares the same DB context.
    /// </summary>
    private async Task<(int PaymentId, string ProviderRef, string ParentToken)>
        SetupSubscriptionPaymentAsync(string tag = "")
    {
        var parentToken = await RegisterParentAndGetTokenAsync(_clientEnabled);

        var (upResp, _, upBody) = await SendAsync(_clientEnabled, HttpMethod.Post,
            SubscriptionUpgradeUrl, new { BillingPeriod = (int)BillingPeriod.Monthly }, parentToken);
        upResp.StatusCode.Should().Be(HttpStatusCode.OK, $"Upgrade; body: {upBody}");

        var (chResp, chRoot, chBody) = await SendAsync(_clientEnabled, HttpMethod.Post,
            SubscriptionCheckoutUrl, null, parentToken);
        chResp.StatusCode.Should().Be(HttpStatusCode.OK, $"Checkout; body: {chBody}");

        TryProp(chRoot, "data", out var data);
        TryProp(data, "paymentId", out var pidProp);
        TryProp(data, "providerPaymentRef", out var refProp);

        return (pidProp.GetInt32(), refProp.GetString()!, parentToken);
    }

    private async Task<(PaymentStatus Status, decimal Amount)> GetPaymentFromDbAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var p  = await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentId);
        return p is null ? (PaymentStatus.Initiated, 0m) : (p.Status, p.Amount);
    }

    private async Task<(PlanCode Plan, SubscriptionStatus Status)>
        GetSubscriptionFromDbAsync(string parentToken)
    {
        // Decode parentId from JWT payload
        var parentId = DecodeUserIdFromJwt(parentToken);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var s  = await db.Subscriptions.AsNoTracking()
            .Where(x => x.ParentUserId == parentId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        return s is null ? (PlanCode.Free, SubscriptionStatus.Active) : (s.PlanCode, s.Status);
    }

    private async Task<int> CountWebhookEventsAsync(string providerEventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        return await db.WebhookEvents.AsNoTracking()
            .CountAsync(w => w.ProviderEventId == providerEventId);
    }

    private static int DecodeUserIdFromJwt(string jwt)
    {
        var parts   = jwt.Split('.');
        var payload = parts[1];
        var padded  = payload + new string('=', (4 - payload.Length % 4) % 4);
        var decoded = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        var json    = Encoding.UTF8.GetString(decoded);
        var doc     = JsonDocument.Parse(json).RootElement;
        if (doc.TryGetProperty("Id", out var idProp))
        {
            var raw = idProp.ValueKind == JsonValueKind.Number
                ? idProp.GetInt32().ToString() : idProp.GetString() ?? "";
            if (int.TryParse(raw, out var id)) return id;
        }
        throw new InvalidOperationException($"Cannot parse userId from JWT payload: {json}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-SIM-01  Happy path — payment.succeeded activates subscription
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-SIM-01 HappyPath: payment.succeeded → 200 + Payment=Succeeded + Subscription Active+Premium")]
    public async Task TC_SIM_01_HappyPath_PaymentSucceeded_ActivatesSubscription()
    {
        // ── Setup ─────────────────────────────────────────────────────────────
        var adminToken = await GetAdminTokenAsync();
        var (paymentId, _, parentToken) = await SetupSubscriptionPaymentAsync("sim01");

        // ── POST Simulate (AllowSimulation=true factory) ──────────────────────
        var (resp, root, body) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = paymentId, EventType = "payment.succeeded" },
            adminToken);

        // ── HTTP status + envelope ────────────────────────────────────────────
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Simulate payment.succeeded must return 200 when gate is on; body: {body}");

        TryProp(root, "successed", out var suc).Should().BeTrue($"body: {body}");
        suc.GetBoolean().Should().BeTrue($"successed must be true; body: {body}");

        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue($"body: {body}");
        statusCodeProp.GetInt32().Should().Be(200, $"body: {body}");

        TryProp(root, "data", out var data).Should().BeTrue($"body: {body}");
        data.ValueKind.Should().NotBe(JsonValueKind.Null, $"data must not be null; body: {body}");

        // ── SimulatePaymentResultDto fields ───────────────────────────────────
        TryProp(data, "paymentId", out var pidProp).Should().BeTrue($"data.paymentId; body: {body}");
        pidProp.GetInt32().Should().Be(paymentId, $"paymentId in response must match request; body: {body}");

        TryProp(data, "simulatedEventId", out var evtProp).Should().BeTrue($"data.simulatedEventId; body: {body}");
        evtProp.GetString().Should().StartWith("sim-",
            $"simulatedEventId must follow the deterministic pattern sim-{{paymentId}}-{{eventType}}; body: {body}");
        evtProp.GetString().Should().Be($"sim-{paymentId}-payment.succeeded",
            $"simulatedEventId must be deterministic; body: {body}");

        TryProp(data, "eventType", out var etProp).Should().BeTrue($"data.eventType; body: {body}");
        etProp.GetString().Should().Be("payment.succeeded", $"body: {body}");

        TryProp(data, "outcome", out var outcomeProp).Should().BeTrue($"data.outcome; body: {body}");
        outcomeProp.GetString().Should().Be("PaymentSucceeded",
            $"Simulate must reuse the real webhook state machine — outcome must be 'PaymentSucceeded'; body: {body}");

        // ── DB: Payment = Succeeded ────────────────────────────────────────────
        var (dbPayStatus, _) = await GetPaymentFromDbAsync(paymentId);
        dbPayStatus.Should().Be(PaymentStatus.Succeeded,
            $"DEFECT: Payment must be Succeeded in DB after simulated payment.succeeded; actual: {dbPayStatus}");

        // ── DB: Subscription = Active + Premium ───────────────────────────────
        var (dbPlan, dbSubStatus) = await GetSubscriptionFromDbAsync(parentToken);
        dbPlan.Should().Be(PlanCode.Premium,
            $"DEFECT: Subscription PlanCode must be Premium after activation; actual: {dbPlan}");
        dbSubStatus.Should().Be(SubscriptionStatus.Active,
            $"DEFECT: Subscription Status must be Active after activation; actual: {dbSubStatus}");

        // ── WebhookEvent row created ───────────────────────────────────────────
        var wEvtCount = await CountWebhookEventsAsync($"sim-{paymentId}-payment.succeeded");
        wEvtCount.Should().Be(1,
            $"Exactly one WebhookEvent row must be created for the simulated event; found: {wEvtCount}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-SIM-02  Idempotent replay — same simulate twice
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-SIM-02 Idempotency: simulate same payment.succeeded twice → second returns Duplicate, no double-grant")]
    public async Task TC_SIM_02_Idempotency_ReplaySameSimulate_SecondIsDuplicate_NoDoubleGrant()
    {
        // ── Setup ─────────────────────────────────────────────────────────────
        var adminToken = await GetAdminTokenAsync();
        var (paymentId, _, parentToken) = await SetupSubscriptionPaymentAsync("sim02");

        var requestBody = new { PaymentId = paymentId, EventType = "payment.succeeded" };
        var eventId     = $"sim-{paymentId}-payment.succeeded";

        // ── First call (processes the event) ──────────────────────────────────
        var (resp1, root1, body1) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl, requestBody, adminToken);

        resp1.StatusCode.Should().Be(HttpStatusCode.OK,
            $"First simulate call must succeed; body: {body1}");

        TryProp(root1, "data", out var data1);
        TryProp(data1, "outcome", out var outcome1);
        outcome1.GetString().Should().Be("PaymentSucceeded",
            $"First simulate must be processed as PaymentSucceeded; body: {body1}");

        // ── Second call (replay — same deterministic eventId) ─────────────────
        var (resp2, root2, body2) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl, requestBody, adminToken);

        resp2.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Second (replay) simulate call must return 200 no-op; body: {body2}");

        TryProp(root2, "data", out var data2);
        TryProp(data2, "outcome", out var outcome2);
        outcome2.GetString().Should().Be("Duplicate",
            $"IDEMPOTENCY DEFECT: second simulate of the same (paymentId, eventType) must return " +
            $"outcome='Duplicate'. The deterministic eventId sim-{{paymentId}}-{{eventType}} makes it " +
            $"idempotent. Got: '{outcome2.GetString()}'; body: {body2}");

        // ── DB: exactly ONE WebhookEvent row for the event id ─────────────────
        var wEvtCount = await CountWebhookEventsAsync(eventId);
        wEvtCount.Should().Be(1,
            $"IDEMPOTENCY DEFECT: exactly one WebhookEvent row must exist for eventId={eventId}. " +
            $"Found: {wEvtCount}. If 2, the replay wrote a second row (no deduplication).");

        // ── DB: Subscription activated exactly once ────────────────────────────
        // It must still be Active+Premium — not rolled back by a duplicate.
        var (dbPlan, dbSubStatus) = await GetSubscriptionFromDbAsync(parentToken);
        dbPlan.Should().Be(PlanCode.Premium,
            $"Subscription must remain Premium after replay; actual: {dbPlan}");
        dbSubStatus.Should().Be(SubscriptionStatus.Active,
            $"Subscription must remain Active after replay; actual: {dbSubStatus}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-SIM-03  payment.failed → Payment=Failed state
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-SIM-03 PaymentFailed: simulate payment.failed → 200 + Payment=Failed")]
    public async Task TC_SIM_03_PaymentFailed_SimulatesFailedState()
    {
        // ── Setup ─────────────────────────────────────────────────────────────
        var adminToken = await GetAdminTokenAsync();
        var (paymentId, _, _) = await SetupSubscriptionPaymentAsync("sim03");

        // ── Simulate payment.failed ────────────────────────────────────────────
        var (resp, root, body) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = paymentId, EventType = "payment.failed" },
            adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Simulate payment.failed must return 200; body: {body}");

        TryProp(root, "successed", out var suc).Should().BeTrue($"body: {body}");
        suc.GetBoolean().Should().BeTrue($"successed must be true on simulation success; body: {body}");

        TryProp(root, "data", out var data).Should().BeTrue($"body: {body}");
        TryProp(data, "outcome", out var outcomeProp).Should().BeTrue($"data.outcome; body: {body}");
        outcomeProp.GetString().Should().Be("PaymentFailed",
            $"Simulate must route through the real webhook state machine — outcome must be 'PaymentFailed'; body: {body}");

        TryProp(data, "simulatedEventId", out var evtId).Should().BeTrue($"body: {body}");
        evtId.GetString().Should().Be($"sim-{paymentId}-payment.failed",
            $"deterministic eventId must be sim-{{paymentId}}-payment.failed; body: {body}");

        // ── DB: Payment = Failed ──────────────────────────────────────────────
        var (dbPayStatus, _) = await GetPaymentFromDbAsync(paymentId);
        dbPayStatus.Should().Be(PaymentStatus.Failed,
            $"DEFECT: Payment must be Failed in DB after simulated payment.failed; actual: {dbPayStatus}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-SIM-04  refund.succeeded — fires refund/clawback via real webhook path
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-SIM-04 RefundSucceeded: simulate refund.succeeded on a succeeded payment → refund fires via real webhook path")]
    public async Task TC_SIM_04_RefundSucceeded_FiresRealWebhookPath()
    {
        // ── Setup: activate payment first via real webhook ────────────────────
        var adminToken  = await GetAdminTokenAsync();
        var parentToken = await RegisterParentAndGetTokenAsync(_clientEnabled);

        // Upgrade → checkout
        var (upResp, _, upBody) = await SendAsync(_clientEnabled, HttpMethod.Post,
            SubscriptionUpgradeUrl, new { BillingPeriod = (int)BillingPeriod.Monthly }, parentToken);
        upResp.StatusCode.Should().Be(HttpStatusCode.OK, $"Upgrade; body: {upBody}");

        var (chResp, chRoot, chBody) = await SendAsync(_clientEnabled, HttpMethod.Post,
            SubscriptionCheckoutUrl, null, parentToken);
        chResp.StatusCode.Should().Be(HttpStatusCode.OK, $"Checkout; body: {chBody}");
        TryProp(chRoot, "data", out var chData);
        TryProp(chData, "paymentId", out var pidProp);
        var paymentId = pidProp.GetInt32();

        // ── Activate via simulate payment.succeeded first ─────────────────────
        var (succResp, succRoot, succBody) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = paymentId, EventType = "payment.succeeded" },
            adminToken);
        succResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"payment.succeeded simulate must succeed as prerequisite; body: {succBody}");

        TryProp(succRoot, "data", out var succData);
        TryProp(succData, "outcome", out var succOutcome);
        succOutcome.GetString().Should().Be("PaymentSucceeded",
            $"prerequisite payment.succeeded must process correctly; body: {succBody}");

        // Verify payment is succeeded before refund
        var (preRefundPayStatus, _) = await GetPaymentFromDbAsync(paymentId);
        preRefundPayStatus.Should().Be(PaymentStatus.Succeeded,
            $"Payment must be Succeeded before we can simulate a refund; actual: {preRefundPayStatus}");

        // ── Simulate refund.succeeded ──────────────────────────────────────────
        var (refResp, refRoot, refBody) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = paymentId, EventType = "refund.succeeded" },
            adminToken);

        refResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Simulate refund.succeeded must return 200; body: {refBody}");

        TryProp(refRoot, "successed", out var refSuc).Should().BeTrue($"body: {refBody}");
        refSuc.GetBoolean().Should().BeTrue($"successed must be true for refund simulation; body: {refBody}");

        TryProp(refRoot, "data", out var refData).Should().BeTrue($"body: {refBody}");
        TryProp(refData, "outcome", out var refOutcome).Should().BeTrue($"data.outcome; body: {refBody}");
        // The outcome from the real webhook state machine is either "SubscriptionRefunded"
        // (first time) or "SubscriptionRefundDuplicate" (if the idempotency guard fires).
        // On the first call it must be the settled outcome.
        refOutcome.GetString().Should().BeOneOf(
            "SubscriptionRefunded", "RefundSucceeded",
            $"Simulate refund.succeeded must route through the real webhook state machine; " +
            $"outcome must be 'SubscriptionRefunded' (or 'RefundSucceeded' if mapped differently); body: {refBody}");

        TryProp(refData, "simulatedEventId", out var refEvtId).Should().BeTrue($"body: {refBody}");
        refEvtId.GetString().Should().Be($"sim-{paymentId}-refund.succeeded",
            $"deterministic eventId for refund; body: {refBody}");

        // ── DB: Payment = Refunded (ProcessSubscriptionRefundAsync sets Status=Refunded) ──────
        var (postRefundPayStatus, _) = await GetPaymentFromDbAsync(paymentId);
        postRefundPayStatus.Should().Be(PaymentStatus.Refunded,
            $"DEFECT: Payment.Status must be Refunded after refund.succeeded simulation; actual: {postRefundPayStatus}");

        // ── DB: Subscription downgraded to Free (subscription refund policy) ─────────────────
        var (postRefundPlan, postRefundSubStatus) = await GetSubscriptionFromDbAsync(parentToken);
        postRefundPlan.Should().Be(PlanCode.Free,
            $"DEFECT: Subscription must be downgraded to Free after refund; actual planCode: {postRefundPlan}");
        postRefundSubStatus.Should().Be(SubscriptionStatus.Active,
            $"DEFECT: Subscription must be Active (Free plan) after refund; actual status: {postRefundSubStatus}");

        // ── Distinct event types produce distinct WebhookEvent rows ─────────────────────────
        var succEvtCount   = await CountWebhookEventsAsync($"sim-{paymentId}-payment.succeeded");
        var refundEvtCount = await CountWebhookEventsAsync($"sim-{paymentId}-refund.succeeded");
        succEvtCount.Should().Be(1,
            $"payment.succeeded WebhookEvent row must still be 1; found: {succEvtCount}");
        refundEvtCount.Should().Be(1,
            $"refund.succeeded WebhookEvent row must be 1; found: {refundEvtCount}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-SIM-05  Gate off by default — AllowSimulation absent/false → 404
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-SIM-05 GateOff: AllowSimulation absent/false → 404 (existence not disclosed)")]
    public async Task TC_SIM_05_GateOff_AllowSimulationFalse_Returns404()
    {
        // Use the DEFAULT factory client (LearnexiaWebAppFactory does NOT set AllowSimulation=true).
        // The appsettings.json default is AllowSimulation=false — the gate must refuse with 404.
        var adminToken = await GetAdminTokenAsync(_clientDefault);

        // We need a valid paymentId to prove the 404 is from the gate, not from payment not found.
        // But for the gate-off test, paymentId doesn't matter — the gate fires first in the handler.
        // Use a plausible positive paymentId (9999999 — won't exist, but gate fires before lookup).
        var (resp, root, body) = await SendAsync(
            _clientDefault, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = 9999999, EventType = "payment.succeeded" },
            adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            $"SECURITY DEFECT: Simulate must return 404 when AllowSimulation=false (gate off). " +
            $"If this returns 200, the simulation endpoint is active in production by default. " +
            $"body: {body}");

        TryProp(root, "successed", out var suc).Should().BeTrue($"body: {body}");
        suc.GetBoolean().Should().BeFalse(
            $"successed must be false on gate-off 404; body: {body}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-SIM-06a Auth — anonymous → 401
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-SIM-06a Auth: anonymous (no JWT) → 401 even when gate is on")]
    public async Task TC_SIM_06a_Auth_Anonymous_Returns401()
    {
        // No bearer token — [Authorize(Policy=AdminOnly)] fires at the middleware level.
        var (resp, _, body) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = 1, EventType = "payment.succeeded" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            $"Simulate is [Authorize(Policy=AdminOnly)]; anon → 401. body: {body}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-SIM-06b Auth — parent JWT (non-admin) → 403
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-SIM-06b Auth: parent JWT (non-admin) → 403 Forbidden")]
    public async Task TC_SIM_06b_Auth_ParentJwt_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(_clientEnabled);

        var (resp, _, body) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = 1, EventType = "payment.succeeded" },
            parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"Simulate is AdminOnly; Parent JWT → 403. body: {body}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-SIM-07a Validation — paymentId <= 0 → 422
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-SIM-07a Validation: paymentId <= 0 → 422 UnprocessableEntity")]
    public async Task TC_SIM_07a_Validation_PaymentIdZero_Returns422()
    {
        var adminToken = await GetAdminTokenAsync();

        var (resp, root, body) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = 0, EventType = "payment.succeeded" },
            adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            $"PaymentId=0 must fail FluentValidation (GreaterThan(0)) → 422; body: {body}");

        TryProp(root, "successed", out var suc).Should().BeTrue($"body: {body}");
        suc.GetBoolean().Should().BeFalse($"successed must be false for validation error; body: {body}");

        TryProp(root, "errors", out var errors).Should().BeTrue($"body: {body}");
        errors.EnumerateArray().ToList().Should().NotBeEmpty(
            $"Errors[] must contain the PaymentId validation message; body: {body}");
    }

    [Fact(DisplayName = "TC-SIM-07a-neg: paymentId = -1 → 422 UnprocessableEntity")]
    public async Task TC_SIM_07a_Validation_PaymentIdNegative_Returns422()
    {
        var adminToken = await GetAdminTokenAsync();

        var (resp, root, body) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = -1, EventType = "payment.succeeded" },
            adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            $"PaymentId=-1 must fail validation → 422; body: {body}");

        TryProp(root, "successed", out var suc).Should().BeTrue($"body: {body}");
        suc.GetBoolean().Should().BeFalse($"body: {body}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-SIM-07b Validation — unknown/unsupported eventType → 422
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-SIM-07b Validation: unknown eventType → 422 UnprocessableEntity")]
    public async Task TC_SIM_07b_Validation_UnknownEventType_Returns422()
    {
        var adminToken = await GetAdminTokenAsync();

        var (resp, root, body) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = 1, EventType = "bogus.event.type" },
            adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            $"Unsupported eventType must fail FluentValidation → 422; body: {body}");

        TryProp(root, "successed", out var suc).Should().BeTrue($"body: {body}");
        suc.GetBoolean().Should().BeFalse($"successed must be false; body: {body}");

        TryProp(root, "errors", out var errors).Should().BeTrue($"body: {body}");
        errors.EnumerateArray().ToList().Should().NotBeEmpty(
            $"Errors[] must contain the eventType validation message; body: {body}");
    }

    [Fact(DisplayName = "TC-SIM-07b-empty: empty eventType → 422 UnprocessableEntity")]
    public async Task TC_SIM_07b_Validation_EmptyEventType_Returns422()
    {
        var adminToken = await GetAdminTokenAsync();

        var (resp, root, body) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = 1, EventType = "" },
            adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            $"Empty eventType must fail NotEmpty validation → 422; body: {body}");

        TryProp(root, "successed", out var suc).Should().BeTrue($"body: {body}");
        suc.GetBoolean().Should().BeFalse($"body: {body}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-SIM-07c Validation — unknown paymentId (gate on) → 404
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-SIM-07c NotFound: unknown paymentId (gate on, admin JWT) → 404")]
    public async Task TC_SIM_07c_UnknownPaymentId_Returns404()
    {
        var adminToken = await GetAdminTokenAsync();

        // Use an extremely large paymentId that will never exist
        var (resp, root, body) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = int.MaxValue, EventType = "payment.succeeded" },
            adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            $"A paymentId that does not exist must return 404 (PaymentNotFound path); body: {body}");

        TryProp(root, "successed", out var suc).Should().BeTrue($"body: {body}");
        suc.GetBoolean().Should().BeFalse(
            $"successed must be false when payment not found; body: {body}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-SIM-08  Envelope shape verification
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-SIM-08 Envelope: Simulate success response has all 5 BaseResponse keys")]
    public async Task TC_SIM_08_Envelope_HasAllBaseResponseKeys()
    {
        var adminToken = await GetAdminTokenAsync();
        var (paymentId, _, _) = await SetupSubscriptionPaymentAsync("sim08");

        var (resp, root, body) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = paymentId, EventType = "payment.succeeded" },
            adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body: {body}");

        TryProp(root, "statusCode", out _).Should().BeTrue(
            $"envelope must contain 'statusCode'; body: {body}");
        TryProp(root, "successed", out _).Should().BeTrue(
            $"envelope must contain 'successed' (sic — C# spelling); body: {body}");
        TryProp(root, "message", out _).Should().BeTrue(
            $"envelope must contain 'message'; body: {body}");
        TryProp(root, "data", out _).Should().BeTrue(
            $"envelope must contain 'data'; body: {body}");
        TryProp(root, "errors", out _).Should().BeTrue(
            $"envelope must contain 'errors'; body: {body}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TC-SIM-09  Distinct eventTypes on same payment get distinct deterministic event ids
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-SIM-09 DeterministicEventId: distinct eventTypes on same payment produce distinct sim-{id}-{type} ids")]
    public async Task TC_SIM_09_DeterministicEventId_DistinctEventTypes_DistinctIds()
    {
        var adminToken = await GetAdminTokenAsync();
        var (paymentId, _, _) = await SetupSubscriptionPaymentAsync("sim09");

        // Simulate payment.succeeded
        var (r1, root1, b1) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = paymentId, EventType = "payment.succeeded" },
            adminToken);
        r1.StatusCode.Should().Be(HttpStatusCode.OK, $"payment.succeeded; body: {b1}");
        TryProp(root1, "data", out var d1);
        TryProp(d1, "simulatedEventId", out var evtId1);

        // Simulate refund.succeeded (different eventType → different id)
        var (r2, root2, b2) = await SendAsync(
            _clientEnabled, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = paymentId, EventType = "refund.succeeded" },
            adminToken);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, $"refund.succeeded; body: {b2}");
        TryProp(root2, "data", out var d2);
        TryProp(d2, "simulatedEventId", out var evtId2);

        // Assert distinct deterministic ids
        var id1 = evtId1.GetString()!;
        var id2 = evtId2.GetString()!;

        id1.Should().Be($"sim-{paymentId}-payment.succeeded",
            $"eventId for payment.succeeded must follow pattern; got: {id1}");
        id2.Should().Be($"sim-{paymentId}-refund.succeeded",
            $"eventId for refund.succeeded must follow pattern; got: {id2}");
        id1.Should().NotBe(id2,
            "distinct eventTypes on the same payment must produce distinct event ids");

        // Both are present as separate WebhookEvent rows in the DB
        var count1 = await CountWebhookEventsAsync(id1);
        var count2 = await CountWebhookEventsAsync(id2);
        count1.Should().Be(1, $"payment.succeeded WebhookEvent row must be 1; found: {count1}");
        count2.Should().Be(1, $"refund.succeeded WebhookEvent row must be 1; found: {count2}");
    }
}
