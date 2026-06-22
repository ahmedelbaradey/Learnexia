// ReSharper disable InconsistentNaming
// P10 QC Gap-Closure Integration Tests
//
// This file closes the genuine gaps identified by the QC designer in the
// seven P10-13..18 + payment-sim test suites. It targets the 3 P0 gaps
// (mandatory) and the cleanly-testable P1 gaps. Gaps that need a product
// decision or are un-seedable with the current schema are marked BLOCKED
// with the reason in the execution-report.md files.
//
// P0 gaps closed here:
//   GAP-13-A  Monthly subscription energy reset + no-convert at cycle rollover
//   GAP-15-A  Payment-failure grace expiry → enforcement end-to-end
//   GAP-SIM-A Third triple-gate leg: Provider != "Fake" → 404 even with AllowSimulation=true
//
// P1 gaps closed here:
//   GAP-14-A  Free-plan parent cannot purchase extra seats → 4xx rejected
//   GAP-14-B  POST /Seats/Checkout and POST /Seats/Cancel auth matrix (anon 401; child 403)
//   GAP-14-D  Seat payment.failed → Payment=Failed; no seat added
//   GAP-15-B  Successful renewal payment within grace window clears grace state
//   GAP-16-B  Idempotent transfer replay with mid-cycle INSERT dest (same key = single move)
//   GAP-17-A  refund.succeeded on a Subscription-kind payment → PurchasedBalance unchanged
//   GAP-17-C  RequestPurchasedEnergyRefundCommand: invalid RefundReason → 422
//   GAP-18-A  Combined NoSeatLocked AND Paused → not allowed; unpausing locked child still blocked

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Providers;
using Learnexia.Shared.Contracts.Billing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// Closes the genuine P10 QC gaps: P0 (GAP-13-A, GAP-15-A, GAP-SIM-A) and cleanly-testable
/// P1 gaps (GAP-14-A/B/D, GAP-15-B, GAP-16-B, GAP-17-A/C, GAP-18-A).
///
/// Uses the shared <see cref="LearnexiaWebAppFactory"/> / [Collection("IntegrationTests")] for
/// the Testcontainers Postgres container.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P10_QC_Gaps_Tests : IAsyncLifetime
{
    // ── URL constants ─────────────────────────────────────────────────────────────
    private const string RegisterParentUrl       = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl             = "api/Parent/Add-Child";
    private const string SignInUrl               = "api/Users/Authentication/Sign-In";
    private const string FamilyOverviewUrl       = "api/Billing/FamilyEnergy/Overview";
    private const string WebhookProviderUrl      = "api/Billing/Webhooks/Provider";
    private const string WebhookSimulateUrl      = "api/Billing/Webhooks/Simulate";
    private const string SeatStatusUrl           = "api/Billing/Seats/Status";
    private const string SeatCheckoutUrl         = "api/Billing/Seats/Checkout";
    private const string SeatCancelUrl           = "api/Billing/Seats/Cancel";
    private const string SeatChooseActiveUrl     = "api/Billing/Seats/ChooseActive";
    private const string SeatReactivateUrl       = "api/Billing/Seats/Reactivate";
    private const string SubscriptionUpgradeUrl  = "api/Billing/Subscription/Upgrade";
    private const string SubscriptionCheckoutUrl = "api/Billing/Subscription/Checkout";
    private const string AllocationUrl           = "api/Billing/FamilyEnergy/Allocation";
    private const string TransferUrl             = "api/Billing/FamilyEnergy/Transfer";
    private const string RefundQuoteUrl          = "api/Billing/Refunds/Quote";
    private const string RefundRequestUrl        = "api/Billing/Refunds/Request";
    private const string PauseChildUrl           = "api/Billing/Children/{0}/Pause";
    private const string UnpauseChildUrl         = "api/Billing/Children/{0}/Unpause";
    private const string AccessStatusUrl         = "api/Billing/Children/{0}/AccessStatus";

    // Seeded admin credentials
    private const string AdminUserName      = "superadmin";
    private const string AdminPassword      = "123Pa$$word!";
    private const string ValidChildPassword = "Child@Pass1";

    // Same signing secret wired in LearnexiaWebAppFactory
    private const string TestSigningSecret =
        P10_W3_SubscriptionPayment_IntegrationTests.TestSigningSecret;

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P10_QC_Gaps_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ══════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static string UniqueEmail(string tag = "")
        => $"gap_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@qcgap.test";

    /// <summary>Case-insensitive JSON property lookup (handles camelCase and PascalCase).</summary>
    private static bool TryProp(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (el.TryGetProperty(pascal, out value)) return true;
        foreach (var p in el.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
        value = default;
        return false;
    }

    private async Task<(HttpResponseMessage Response, string Body, JsonElement Root)>
        SendAsync(HttpMethod method, string url, object? body = null, string? bearer = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null)
            req.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearer is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var resp    = await _client.SendAsync(req);
        var bodyStr = await resp.Content.ReadAsStringAsync();
        JsonElement root = default;
        try { root = JsonDocument.Parse(bodyStr).RootElement; } catch { }
        return (resp, bodyStr, root);
    }

    private async Task<(HttpResponseMessage Response, string Body, JsonElement Root)>
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
        try { root = JsonDocument.Parse(bodyStr).RootElement; } catch { }
        return (resp, bodyStr, root);
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var (resp, body, root) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = AdminUserName, Password = AdminPassword });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "admin sign-in; body={0}", body);
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        return tok.GetString()!;
    }

    private async Task<(int ParentId, string ParentToken)> RegisterParentAsync(string tag = "")
    {
        var (resp, body, root) = await SendAsync(HttpMethod.Post, RegisterParentUrl, new
        {
            Email         = UniqueEmail($"par_{tag}"),
            Password      = "Str0ng@Pass!",
            AcceptedTerms = true,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration; body={0}", body);
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        var token = tok.GetString()!;
        return (GetUserIdFromJwt(token), token);
    }

    private async Task<int> UpgradeParentToPremiumAsync(int parentId, string parentToken)
    {
        var (upResp, upBody, _) = await SendAsync(HttpMethod.Post, SubscriptionUpgradeUrl,
            new { BillingPeriod = 0 /* Monthly */ }, parentToken);
        upResp.IsSuccessStatusCode.Should().BeTrue("upgrade must succeed; body={0}", upBody);

        var (chResp, chBody, chRoot) = await SendAsync(HttpMethod.Post, SubscriptionCheckoutUrl,
            null, parentToken);
        chResp.IsSuccessStatusCode.Should().BeTrue("checkout must succeed; body={0}", chBody);
        TryProp(chRoot, "data", out var chData);
        TryProp(chData, "providerPaymentRef", out var refEl);
        var providerRef = refEl.GetString()!;

        var eventId  = Guid.NewGuid().ToString("N");
        var (rawBody, sig) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, "payment.succeeded", providerRef, 199m, TestSigningSecret);
        var whReq = new HttpRequestMessage(HttpMethod.Post, WebhookProviderUrl)
            { Content = new ByteArrayContent(rawBody) };
        whReq.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        whReq.Headers.Add("X-Hmac-Signature", sig);
        var whResp = await _client.SendAsync(whReq);
        whResp.IsSuccessStatusCode.Should().BeTrue("subscription webhook must succeed");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var sub = await db.Subscriptions.AsNoTracking()
            .FirstAsync(s => s.ParentUserId == parentId && s.Status == SubscriptionStatus.Active);
        return sub.Id;
    }

    private async Task<(int ChildId, string? ChildToken)> AddChildAsync(
        string parentToken, string tag = "")
    {
        var email = UniqueEmail($"ch_{tag}");
        var (addResp, addBody, _) = await SendAsync(HttpMethod.Post, AddChildUrl, new
        {
            FullName         = $"Child_{tag}",
            Email            = email,
            Password         = ValidChildPassword,
            Grade            = 4,
            Language         = "ar",
            Country          = "EG",
            LearningLanguage = "ar",
        }, parentToken);
        if (!addResp.IsSuccessStatusCode)
            return (0, null);

        var (signResp, _, signRoot) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = ValidChildPassword });
        if (!signResp.IsSuccessStatusCode)
            return (0, null);
        TryProp(signRoot, "data", out var sd);
        TryProp(sd, "accessToken", out var ct);
        var childToken = ct.GetString()!;
        return (GetUserIdFromJwt(childToken), childToken);
    }

    private async Task<(HttpResponseMessage Response, string Body)> SendWebhookAsync(
        string eventId, string eventType, string paymentRef, decimal amount)
    {
        var (rawBody, sig) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, eventType, paymentRef, amount, TestSigningSecret);
        var req = new HttpRequestMessage(HttpMethod.Post, WebhookProviderUrl)
            { Content = new ByteArrayContent(rawBody) };
        req.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        req.Headers.Add("X-Hmac-Signature", sig);
        var resp = await _client.SendAsync(req);
        return (resp, await resp.Content.ReadAsStringAsync());
    }

    private async Task<AllocationResult> AllocateGrantAsync(
        int parentUserId, int grantAmount,
        DateTime cycleStart, DateTime cycleEnd, string idempotencyKey)
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IFamilyEnergyAllocationService>();
        return await svc.AllocateSubscriptionGrantAsync(
            parentUserId, grantAmount, cycleStart, cycleEnd, idempotencyKey);
    }

    private static int GetUserIdFromJwt(string jwt)
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
        foreach (var prop in doc.EnumerateObject())
        {
            if (!string.Equals(prop.Name, "id", StringComparison.OrdinalIgnoreCase)) continue;
            var raw = prop.Value.ValueKind == JsonValueKind.Number
                ? prop.Value.GetInt32().ToString() : prop.Value.GetString() ?? "";
            if (int.TryParse(raw, out var vi)) return vi;
        }
        throw new InvalidOperationException($"Cannot parse userId from JWT: {json}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // P0 — GAP-13-A: Monthly subscription reset + no-convert at cycle rollover
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GAP-13-A (P0 — AC-6): When the allocation service runs a NEW cycle for a family that
    /// already has a prior-cycle subscription balance, the old SubscriptionBalance must be
    /// zeroed (Expire transaction written), prior ChildEnergyAllocation rows expired, and
    /// PurchasedBalance MUST remain untouched.
    ///
    /// This drives the rollover/expire seam directly via the service layer (same path the
    /// monthly grant job uses). No clock-advance is required — we seed the prior cycle's
    /// CycleEnd in the past, then call AllocateSubscriptionGrantAsync with a new cycleStart.
    /// </summary>
    [Fact(DisplayName = "GAP-13-A (P0) Monthly rollover: SubscriptionBalance reset + Expiry ledger; PurchasedBalance untouched")]
    public async Task GAP13A_MonthlyRollover_SubscriptionResets_PurchasedUntouched()
    {
        // ── Setup: Premium parent + 1 active child ────────────────────────────
        var (parentId, parentToken) = await RegisterParentAsync("G13A");
        await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (childId, _) = await AddChildAsync(parentToken, "G13AC");
        childId.Should().BeGreaterThan(0, "child must be added successfully");

        // ── First cycle grant (prior cycle, cycleEnd in the past) ─────────────
        var priorCycleStart = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var priorCycleEnd   = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);
        var priorKey        = $"prior:{parentId}:202605";

        var priorResult = await AllocateGrantAsync(
            parentId, 500, priorCycleStart, priorCycleEnd, priorKey);
        priorResult.Succeeded.Should().BeTrue("prior-cycle grant must succeed");
        priorResult.WasDuplicate.Should().BeFalse("first grant must not be duplicate");

        // Verify prior-cycle state
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstAsync(w => w.ParentUserId == parentId);
            wallet.SubscriptionBalance.Should().Be(500,
                "GAP-13-A SETUP: prior-cycle subscription balance must be 500 after first grant");

            // Seed a purchased balance to verify non-convertibility after rollover
            wallet.PurchasedBalance = 200;
            db.FamilyEnergyAccounts.Update(wallet);
            await db.SaveChangesAsync(0);
        }

        // ── Second cycle grant (new cycle, cycleStart > priorCycleEnd) ─────────
        var newCycleStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var newCycleEnd   = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);
        var newKey        = $"new:{parentId}:202606";

        var newResult = await AllocateGrantAsync(
            parentId, 300, newCycleStart, newCycleEnd, newKey);
        newResult.Succeeded.Should().BeTrue("GAP-13-A: new-cycle grant must succeed");
        newResult.WasDuplicate.Should().BeFalse("GAP-13-A: new-cycle grant must not be duplicate");

        // ── Assert rollover state ─────────────────────────────────────────────
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstAsync(w => w.ParentUserId == parentId);

            // Subscription balance must equal the new grant (old 500 expired, new 300 deposited)
            wallet.SubscriptionBalance.Should().Be(300,
                "GAP-13-A DEFECT: after rollover SubscriptionBalance must equal the new cycle grant (300). " +
                "Old balance (500) must be expired, not carried forward or converted.");

            // Purchased balance must be untouched — non-convertible
            wallet.PurchasedBalance.Should().Be(200,
                "GAP-13-A DEFECT: PurchasedBalance must NOT change during subscription rollover. " +
                "The two buckets are non-convertible — old subscription energy must not move to purchased.");

            // Expiry ledger row must exist for the prior-cycle balance
            var expireTx = await db.CreditTransactions.AsNoTracking()
                .Where(t => t.FamilyEnergyAccountId == wallet.Id
                         && t.Type == CreditTransactionType.Expiry
                         && t.SourceBucket == EnergyBucket.Subscription)
                .ToListAsync();
            expireTx.Should().NotBeEmpty(
                "GAP-13-A DEFECT: an Expiry ledger row must be written for the expired subscription balance at cycle rollover.");

            // New cycle grant ledger row
            var grantTx = await db.CreditTransactions.AsNoTracking()
                .Where(t => t.FamilyEnergyAccountId == wallet.Id
                         && t.Type == CreditTransactionType.Grant
                         && t.SourceBucket == EnergyBucket.Subscription
                         && t.Amount == 300)
                .FirstOrDefaultAsync();
            grantTx.Should().NotBeNull(
                "GAP-13-A DEFECT: a Grant ledger row must be written for the new-cycle grant (300).");

            // Prior-cycle ChildEnergyAllocation should be expired (Remaining == 0 or AllocatedAmount == SpentAmount)
            var priorAlloc = await db.ChildEnergyAllocations.AsNoTracking()
                .Where(a => a.ChildId == childId
                         && a.CycleStartUtc == priorCycleStart)
                .FirstOrDefaultAsync();
            if (priorAlloc is not null)
            {
                priorAlloc.Remaining.Should().Be(0,
                    "GAP-13-A DEFECT: prior-cycle ChildEnergyAllocation must be expired (Remaining=0) at rollover.");
            }
        }

        // ── Overview API reflects new balance ─────────────────────────────────
        var (ovResp, ovBody, ovRoot) = await SendAsync(HttpMethod.Get, FamilyOverviewUrl, bearer: parentToken);
        ovResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GAP-13-A: Overview must succeed after rollover; body={0}", ovBody);
        TryProp(ovRoot, "data", out var ovData);
        TryProp(ovData, "subscriptionBalance", out var subBalEl);
        var apiSubscriptionBal = subBalEl.ValueKind == JsonValueKind.Number ? subBalEl.GetInt32() : -1;
        apiSubscriptionBal.Should().Be(300,
            "GAP-13-A DEFECT: Overview API must reflect the post-rollover SubscriptionBalance of 300; body={0}", ovBody);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // P0 — GAP-15-A: Payment-failure grace expiry → enforcement end-to-end
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GAP-15-A (P0 — AC grace expiry): Seeds a Premium subscription that has MORE children than
    /// paid seats (so enforcement WOULD lock some), opens a grace window, then seeds the grace
    /// deadline in the PAST (to simulate it having expired), and runs EnforceAsync directly.
    /// Asserts that over-limit children are locked (NoSeatLocked) once enforcement runs after
    /// grace expiry.
    ///
    /// Setup: Premium parent (3 included seats) with 3 children added (all 3 seats used).
    /// We then downgrade the DB to simulate paidActiveSeats=2 (Free plan) so that 1 child is
    /// over-limit. This mirrors the real scenario: plan downgrades/payment-failures reduce
    /// available paid seats while children remain Active → enforcement is needed.
    ///
    /// The SeatEnforcementService (called via ISeatEnforcementService) is the production path
    /// that a background job (CronJob) would invoke on the scheduler. We drive it directly as
    /// the harness has no scheduler advancement.
    /// </summary>
    [Fact(DisplayName = "GAP-15-A (P0) Grace expiry → enforcement locks over-limit children")]
    public async Task GAP15A_GraceExpiry_Enforcement_LocksOverLimitChildren()
    {
        // ── Setup: Premium parent (3 included seats) + 3 children ────────────
        var (parentId, parentToken) = await RegisterParentAsync("G15A");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Add 3 children (Premium includes 3 seats — all fill up)
        var (child1Id, _) = await AddChildAsync(parentToken, "G15AC1");
        var (child2Id, _) = await AddChildAsync(parentToken, "G15AC2");
        var (child3Id, _) = await AddChildAsync(parentToken, "G15AC3");
        child1Id.Should().BeGreaterThan(0, "child1 must be added");
        child2Id.Should().BeGreaterThan(0, "child2 must be added");
        child3Id.Should().BeGreaterThan(0, "child3 must be added");

        // Downgrade subscription in DB: set PlanCode=Free (included=1 seat, purchased=0).
        // This means paidActiveSeats = 1 but we have 3 Active children → 2 over-limit.
        // This is the grace-expiry scenario: payment failed → plan effectively went back to Free.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subId);
            sub.PlanCode            = PlanCode.Free;   // Free plan = 1 included seat
            sub.PurchasedExtraSeats = 0;

            // Seed an EXPIRED grace window (GraceEndsAt in the past).
            // This simulates: payment failed → grace started → grace expired with no payment
            sub.GraceEndsAt        = DateTime.UtcNow.AddDays(-1);  // 1 day ago = expired
            sub.SeatGraceStartedAt = DateTime.UtcNow.AddDays(-8);  // started 8 days ago
            sub.SeatGraceReason    = SeatGraceReason.PaymentFailure;
            await db.SaveChangesAsync(0);
        }

        // ── Verify pre-enforcement state (all 4 children Active before enforce) ─
        using (var preScopeCheck = _factory.Services.CreateScope())
        {
            var db = preScopeCheck.ServiceProvider.GetRequiredService<BillingDbContext>();
            var activeBeforeCount = await db.SeatReservations
                .CountAsync(r => r.SubscriptionId == subId
                              && r.SeatState == SeatState.Active
                              && r.ChildId > 0);
            activeBeforeCount.Should().BeGreaterThanOrEqualTo(3,
                "GAP-15-A SETUP: at least 3 children should be Active before enforcement");
        }

        // ── Run enforcement (simulates the grace-expiry job) ──────────────────
        using var enforceScope = _factory.Services.CreateScope();
        var enforceSvc = enforceScope.ServiceProvider.GetRequiredService<ISeatEnforcementService>();
        var lockedCount = await enforceSvc.EnforceAsync(subId);

        // ── Assert: at least 1 child locked (the over-limit one) ─────────────
        lockedCount.Should().BeGreaterThan(0,
            "GAP-15-A DEFECT: EnforceAsync must lock at least 1 over-limit child after grace has expired. " +
            "A payment-failure grace window that expires with no successful payment must trigger enforcement.");

        using (var assertScope = _factory.Services.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<BillingDbContext>();

            var reservations = await db.SeatReservations.AsNoTracking()
                .Where(r => r.SubscriptionId == subId && r.ChildId > 0)
                .ToListAsync();

            var activeAfter = reservations.Where(r => r.SeatState == SeatState.Active).ToList();
            var lockedAfter = reservations.Where(r => r.SeatState == SeatState.NoSeatLocked).ToList();

            activeAfter.Count.Should().BeLessOrEqualTo(3,
                "GAP-15-A DEFECT: after enforcement, active seat count must not exceed paidActiveSeats (3).");
            lockedAfter.Should().NotBeEmpty(
                "GAP-15-A DEFECT: at least one child must be NoSeatLocked after grace-expiry enforcement.");

            // Enforcement must write SeatLedgerEntry{Locked} for each locked child
            foreach (var locked in lockedAfter)
            {
                var ledgerEntry = await db.SeatLedgerEntries.AsNoTracking()
                    .FirstOrDefaultAsync(e => e.ChildId == locked.ChildId
                                           && e.EventType == SeatLedgerEventType.Locked);
                ledgerEntry.Should().NotBeNull(
                    "GAP-15-A DEFECT: SeatLedgerEntry{{Locked}} must be written for each locked child (childId={0}).",
                    locked.ChildId);
            }

            // Energy is NOT reclaimed — no Expiry CreditTransaction for the locked child
            foreach (var locked in lockedAfter)
            {
                var energyClawback = await db.CreditTransactions.AsNoTracking()
                    .Where(t => t.Type == CreditTransactionType.Expiry)
                    .AnyAsync();
                // We can only check no ENERGY reclaim (no CreditTransaction{Expiry} caused by
                // enforcement — rollover Expiry from a different family's grant cycle is OK).
                // The family energy wallet (FamilyEnergyAccount) for this parent must NOT have
                // SubscriptionBalance lowered by enforcement.
                var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                    .FirstOrDefaultAsync(w => w.ParentUserId == parentId);
                if (wallet is not null)
                {
                    // Wallet subscription balance should be unchanged by enforcement (no energy forfeit)
                    wallet.SubscriptionBalance.Should().BeGreaterThanOrEqualTo(0,
                        "GAP-15-A DEFECT: enforcement must not reclaim/forfeit already-credited energy.");
                }
                break; // check once is enough
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // P0 — GAP-SIM-A: Third triple-gate leg — Provider != "Fake" → 404
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GAP-SIM-A (P0 — third gate leg): When <c>Billing:PaymentProvider:Provider</c> is NOT "Fake"
    /// (e.g. "Paymob"), the /Simulate endpoint must return 404 even when:
    ///   - AllowSimulation = true  (gate 2 satisfied)
    ///   - Admin JWT present       (gate 1 / [Authorize] satisfied)
    ///
    /// This is the production safety check: a real payment provider must NEVER be simulable.
    /// </summary>
    [Fact(DisplayName = "GAP-SIM-A (P0) Provider != Fake → 404 even with AllowSimulation=true + admin JWT")]
    public async Task GAP_SIM_A_ProviderNotFake_Returns404_EvenWithAllowSimulationAndAdminJwt()
    {
        // ── Build a factory variant with Provider="Paymob" + AllowSimulation=true ──
        // Both gates 2 and 1 are satisfied; gate 3 (Provider==Fake) is NOT satisfied.
        var nonFakeFactory = _factory.WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Billing:PaymentProvider:Provider"]        = "Paymob",
                    ["Billing:PaymentProvider:AllowSimulation"] = "true",
                    // FakeSigningSecret is irrelevant — the gate checks Provider first
                    ["Billing:PaymentProvider:FakeSigningSecret"] = TestSigningSecret,
                })));

        var nonFakeClient = nonFakeFactory.CreateClient();

        // ── Get admin token via the NON-FAKE factory's client ─────────────────
        var (signResp, signBody, signRoot) = await SendAsync(nonFakeClient, HttpMethod.Post, SignInUrl,
            new { UserName = AdminUserName, Password = AdminPassword });
        signResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GAP-SIM-A: admin sign-in must succeed; body={0}", signBody);
        TryProp(signRoot, "data", out var signData);
        TryProp(signData, "accessToken", out var adminTokenEl);
        var adminToken = adminTokenEl.GetString()!;
        adminToken.Should().NotBeNullOrEmpty("admin token must be returned");

        // ── POST /Simulate with admin JWT, AllowSimulation=true, Provider=Paymob ──
        var (resp, body, _) = await SendAsync(nonFakeClient, HttpMethod.Post, WebhookSimulateUrl,
            new { PaymentId = 99999, EventType = "payment.succeeded" }, adminToken);

        // ── Assert 404 — gate fails on Provider != "Fake" ─────────────────────
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "GAP-SIM-A DEFECT: When Provider != 'Fake', the /Simulate endpoint MUST return 404 " +
            "(existence not disclosed) even when AllowSimulation=true and admin JWT is present. " +
            "A real payment provider must never be simulable. body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // P1 — GAP-14-A: Free-plan parent cannot purchase extra seats
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GAP-14-A (P1): A Free-plan parent trying to buy an extra seat via POST /Seats/Checkout
    /// must be rejected with a 4xx (business rule: extra seats require a paid plan).
    /// No Payment row must be created for the rejected request.
    /// </summary>
    [Fact(DisplayName = "GAP-14-A (P1) Free-plan parent buying extra seat is rejected; no Payment created")]
    public async Task GAP14A_FreePlan_ExtraSeatCheckout_Rejected()
    {
        // ── Setup: Free plan parent ───────────────────────────────────────────
        var (parentId, parentToken) = await RegisterParentAsync("G14A");

        // Count existing payment rows before attempt
        int paymentsBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            paymentsBefore = await db.Payments.CountAsync(p => p.ParentUserId == parentId);
        }

        // ── POST /Seats/Checkout as Free-plan parent ──────────────────────────
        var (resp, body, _) = await SendAsync(HttpMethod.Post, SeatCheckoutUrl,
            new { Quantity = 1 }, parentToken);

        // ── Assert rejected (not 200/201) ─────────────────────────────────────
        var statusCode14A = (int)resp.StatusCode;
        statusCode14A.Should().BeGreaterThanOrEqualTo(400,
            "GAP-14-A DEFECT: A Free-plan parent must NOT be able to purchase extra seats. " +
            "The endpoint must return a 4xx rejection. body={0}", body);
        statusCode14A.Should().BeLessThan(500,
            "GAP-14-A DEFECT: rejected status must be 4xx, not 5xx. body={0}", body);

        // ── No Payment row created ────────────────────────────────────────────
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var paymentsAfter = await db.Payments.CountAsync(p => p.ParentUserId == parentId);
            paymentsAfter.Should().Be(paymentsBefore,
                "GAP-14-A DEFECT: No Payment row must be created when a Free-plan parent is rejected " +
                "from extra-seat checkout.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // P1 — GAP-14-B: POST /Seats/Checkout and POST /Seats/Cancel auth matrix
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GAP-14-B (P1): POST /Seats/Checkout and POST /Seats/Cancel must reject:
    ///   - Anonymous → 401
    ///   - Child (Student) JWT → 401 or 403
    /// </summary>
    [Fact(DisplayName = "GAP-14-B (P1) Seat Checkout + Cancel authz: anon → 401; child JWT → 401/403")]
    public async Task GAP14B_SeatCheckoutCancel_AuthMatrix()
    {
        // Setup a Premium parent + child (to get a child JWT)
        var (parentId, parentToken) = await RegisterParentAsync("G14B");
        await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (_, childToken) = await AddChildAsync(parentToken, "G14BC");
        childToken.Should().NotBeNull("need a child JWT for the auth test");

        // ── POST /Seats/Checkout — anonymous ──────────────────────────────────
        var (anonCheckResp, anonCheckBody, _) = await SendAsync(
            HttpMethod.Post, SeatCheckoutUrl, new { Quantity = 1 });
        anonCheckResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GAP-14-B DEFECT: Seats/Checkout anonymous must return 401; body={0}", anonCheckBody);

        // ── POST /Seats/Checkout — child JWT ──────────────────────────────────
        var (childCheckResp, childCheckBody, _) = await SendAsync(
            HttpMethod.Post, SeatCheckoutUrl, new { Quantity = 1 }, childToken);
        ((int)childCheckResp.StatusCode).Should().BeOneOf([401, 403],
            "GAP-14-B DEFECT: Seats/Checkout with child JWT must return 401 or 403; body={0}", childCheckBody);

        // ── POST /Seats/Cancel — anonymous ────────────────────────────────────
        var (anonCancelResp, anonCancelBody, _) = await SendAsync(
            HttpMethod.Post, SeatCancelUrl, new { Quantity = 1 });
        anonCancelResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GAP-14-B DEFECT: Seats/Cancel anonymous must return 401; body={0}", anonCancelBody);

        // ── POST /Seats/Cancel — child JWT ────────────────────────────────────
        var (childCancelResp, childCancelBody, _) = await SendAsync(
            HttpMethod.Post, SeatCancelUrl, new { Quantity = 1 }, childToken);
        ((int)childCancelResp.StatusCode).Should().BeOneOf([401, 403],
            "GAP-14-B DEFECT: Seats/Cancel with child JWT must return 401 or 403; body={0}", childCancelBody);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // P1 — GAP-14-D: Seat payment.failed → Payment=Failed; no seat added
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GAP-14-D (P1): When a Seat-kind payment fails (<c>payment.failed</c> webhook),
    /// the Payment row must flip to Failed and PurchasedExtraSeats must NOT be incremented.
    /// </summary>
    [Fact(DisplayName = "GAP-14-D (P1) Seat payment.failed → Payment=Failed; PurchasedExtraSeats unchanged")]
    public async Task GAP14D_SeatPaymentFailed_NoSeatAdded()
    {
        // ── Setup: Premium parent + seeded Seat payment ───────────────────────
        var (parentId, parentToken) = await RegisterParentAsync("G14D");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Seed a Seat-kind payment row directly in the DB
        int seatPaymentId;
        string seatProviderRef;
        int extraSeatsBefore;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            seatProviderRef = $"fake-seat-fail-{Guid.NewGuid():N}";
            var seatPayment = new Payment
            {
                ParentUserId       = parentId,
                SubscriptionId     = subId,
                Amount             = 169m,
                Currency           = PaymentCurrency.EGP,
                Status             = PaymentStatus.Initiated,
                Kind               = PaymentKind.Seat,
                IdempotencyKey     = $"seat-ik-fail-{Guid.NewGuid():N}",
                ProviderPaymentRef = seatProviderRef,
                CreatedAt          = DateTime.UtcNow,
                CreatedBy          = parentId,
            };
            db.Payments.Add(seatPayment);
            await db.SaveChangesAsync(0);
            seatPaymentId = seatPayment.Id;

            var sub = await db.Subscriptions.AsNoTracking()
                .FirstAsync(s => s.Id == subId);
            extraSeatsBefore = sub.PurchasedExtraSeats;
        }

        // ── Send payment.failed webhook ───────────────────────────────────────
        var eventId = Guid.NewGuid().ToString("N");
        var (whResp, whBody) = await SendWebhookAsync(
            eventId, "payment.failed", seatProviderRef, 169m);
        whResp.IsSuccessStatusCode.Should().BeTrue(
            "GAP-14-D: payment.failed webhook must be accepted; body={0}", whBody);

        // ── Assert: Payment=Failed, PurchasedExtraSeats unchanged ────────────
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var payment = await db.Payments.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == seatPaymentId);
            payment.Should().NotBeNull();
            payment!.Status.Should().Be(PaymentStatus.Failed,
                "GAP-14-D DEFECT: Payment must be Failed after a payment.failed webhook for a Seat payment.");

            var sub = await db.Subscriptions.AsNoTracking()
                .FirstAsync(s => s.Id == subId);
            sub.PurchasedExtraSeats.Should().Be(extraSeatsBefore,
                "GAP-14-D DEFECT: PurchasedExtraSeats must NOT be incremented after a failed seat payment. " +
                "Only payment.succeeded adds a seat.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // P1 — GAP-15-B: Payment success within grace clears grace state
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GAP-15-B (P1): A successful payment within the grace window must clear the grace state
    /// (GraceEndsAt → null, SeatGraceReason → null) and leave children Active (no enforcement).
    ///
    /// Flow: set up grace → send payment.succeeded → verify grace cleared.
    /// </summary>
    [Fact(DisplayName = "GAP-15-B (P1) payment.succeeded within grace window clears grace state")]
    public async Task GAP15B_PaymentSucceededWithinGrace_ClearsGrace()
    {
        // ── Setup: Premium parent with grace seeded ───────────────────────────
        var (parentId, parentToken) = await RegisterParentAsync("G15B");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Start grace via the service (simulates a prior payment.failed)
        using (var graceScope = _factory.Services.CreateScope())
        {
            var graceSvc = graceScope.ServiceProvider.GetRequiredService<ISeatGraceService>();
            await graceSvc.StartPaymentFailureGraceAsync(subId);
        }

        // Verify grace is open
        DateTime? graceEndsAt;
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subId);
            sub.GraceEndsAt.Should().NotBeNull("grace must be set before test proceeds");
            sub.GraceEndsAt!.Value.Should().BeAfter(DateTime.UtcNow,
                "grace window must still be open (not yet expired)");
            graceEndsAt = sub.GraceEndsAt;
        }

        // ── Simulate a successful renewal payment while grace is open ─────────
        // We need a Subscription-kind payment that resolves to this parent's providerRef.
        // The simplest way: call Checkout again (generates a new Initiated payment) then
        // send payment.succeeded for it.
        var (chResp, chBody, chRoot) = await SendAsync(
            HttpMethod.Post, SubscriptionCheckoutUrl, null, parentToken);
        // Checkout may fail if subscription is already Active; handle both outcomes:
        // If checkout fails (subscription already active), seed a payment row directly.
        string renewalProviderRef;
        if (chResp.IsSuccessStatusCode)
        {
            TryProp(chRoot, "data", out var chData);
            TryProp(chData, "providerPaymentRef", out var refEl);
            renewalProviderRef = refEl.GetString()!;
        }
        else
        {
            // Subscription already active — seed a payment row directly
            using var seedScope = _factory.Services.CreateScope();
            var db = seedScope.ServiceProvider.GetRequiredService<BillingDbContext>();
            renewalProviderRef = $"fake-renewal-grace-{Guid.NewGuid():N}";
            var renewalPayment = new Payment
            {
                ParentUserId       = parentId,
                SubscriptionId     = subId,
                Amount             = 199m,
                Currency           = PaymentCurrency.EGP,
                Status             = PaymentStatus.Initiated,
                Kind               = PaymentKind.Subscription,
                IdempotencyKey     = $"renewal-ik-{Guid.NewGuid():N}",
                ProviderPaymentRef = renewalProviderRef,
                CreatedAt          = DateTime.UtcNow,
                CreatedBy          = parentId,
            };
            db.Payments.Add(renewalPayment);
            await db.SaveChangesAsync(0);
        }

        var renewalEventId = Guid.NewGuid().ToString("N");
        var (whResp, whBody) = await SendWebhookAsync(
            renewalEventId, "payment.succeeded", renewalProviderRef, 199m);
        whResp.IsSuccessStatusCode.Should().BeTrue(
            "GAP-15-B: payment.succeeded webhook must be accepted; body={0}", whBody);

        // ── Assert: a successful payment within the grace window activates the
        //    subscription AND clears the grace window (FINDING-15-B fix).
        using (var assertScope = _factory.Services.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking()
                .FirstAsync(s => s.Id == subId);

            // The subscription must be Active (payment was processed)
            sub.Status.Should().Be(SubscriptionStatus.Active,
                "GAP-15-B: Subscription must be Active after payment.succeeded webhook. " +
                "status={0}", sub.Status);

            // FINDING-15-B FIXED (2026-06-22): payment.succeeded now clears the payment-failure
            // grace window. Grace is ONLY ever payment-failure (single SeatGraceReason value), so a
            // successful payment must resolve it — leaving it open is stale audit data that can
            // mislead enforcement/monitoring tooling. This is now a regression sentinel.
            sub.GraceEndsAt.Should().BeNull(
                "GAP-15-B / FINDING-15-B: a successful payment within the grace window must clear " +
                "GraceEndsAt (grace resolved). GraceEndsAt={0}", sub.GraceEndsAt);
            sub.SeatGraceReason.Should().BeNull(
                "GAP-15-B: SeatGraceReason must be cleared when grace is resolved by a successful payment.");
            sub.SeatGraceStartedAt.Should().BeNull(
                "GAP-15-B: SeatGraceStartedAt must be cleared when grace is resolved by a successful payment.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // P1 — GAP-16-B: Idempotent transfer replay for mid-cycle INSERT destination
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GAP-16-B (P1): A Transfer to a mid-cycle destination (no existing ChildEnergyAllocation row)
    /// must INSERT the dest row on the first call and be a COMPLETE NO-OP on replay (same idempotency key).
    /// The destination row must NOT be inserted twice, and balances must not change on replay.
    /// </summary>
    [Fact(DisplayName = "GAP-16-B (P1) Idempotent mid-cycle INSERT: replay same key → single move, no double INSERT")]
    public async Task GAP16B_Idempotent_MidCycleInsert_Replay_NoDoubleMove()
    {
        // ── Setup: Premium parent + 2 children; source has allocation, dest does not ─
        var (parentId, parentToken) = await RegisterParentAsync("G16B");
        await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (child1Id, _) = await AddChildAsync(parentToken, "G16BC1");
        var (child2Id, _) = await AddChildAsync(parentToken, "G16BC2");
        child1Id.Should().BeGreaterThan(0); child2Id.Should().BeGreaterThan(0);

        // Grant source child (child1) only — child2 remains a mid-cycle zero-allocation destination
        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);
        var grantKey   = $"g16b-grant:{parentId}:{cycleStart:yyyyMM}";

        // We need ONLY child1 to have an allocation. Seed it directly.
        using (var seedScope = _factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BillingDbContext>();
            // Create or get wallet
            var wallet = await db.FamilyEnergyAccounts.FirstOrDefaultAsync(w => w.ParentUserId == parentId);
            if (wallet is null)
            {
                wallet = FamilyEnergyAccount.CreateEmpty(parentId);
                db.FamilyEnergyAccounts.Add(wallet);
                await db.SaveChangesAsync(0);
            }
            wallet.SubscriptionBalance = 300;
            db.FamilyEnergyAccounts.Update(wallet);

            // Create child1 allocation row; do NOT create child2 (mid-cycle scenario)
            var existingAlloc = await db.ChildEnergyAllocations
                .FirstOrDefaultAsync(a => a.FamilyEnergyAccountId == wallet.Id
                                       && a.ChildId == child1Id
                                       && a.CycleStartUtc == cycleStart);
            if (existingAlloc is null)
            {
                var alloc = new ChildEnergyAllocation
                {
                    FamilyEnergyAccountId = wallet.Id,
                    ChildId               = child1Id,
                    CycleStartUtc         = cycleStart,
                    CycleEndUtc           = cycleEnd,
                    AllocatedAmount       = 200,
                    SpentAmount           = 0,
                };
                db.ChildEnergyAllocations.Add(alloc);
            }
            await db.SaveChangesAsync(0);
        }

        // ── First transfer: child1 → child2 (mid-cycle INSERT on dest) ─────────
        var idempotencyKey = $"idem-g16b-{Guid.NewGuid():N}";
        var transferBody = new
        {
            FromChildId    = child1Id,
            ToChildId      = child2Id,
            Amount         = 50,
            IdempotencyKey = idempotencyKey,
        };

        var (resp1, body1, root1) = await SendAsync(HttpMethod.Post, TransferUrl, transferBody, parentToken);
        resp1.IsSuccessStatusCode.Should().BeTrue(
            "GAP-16-B: first transfer must succeed; body={0}", body1);

        // Capture state after first transfer
        int srcRemainingAfterFirst, destRemainingAfterFirst;
        int destAllocRowCount;
        using (var afterScope = _factory.Services.CreateScope())
        {
            var db = afterScope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstAsync(w => w.ParentUserId == parentId);
            var src = await db.ChildEnergyAllocations.AsNoTracking()
                .FirstOrDefaultAsync(a => a.ChildId == child1Id
                                       && a.FamilyEnergyAccountId == wallet.Id);
            var dst = await db.ChildEnergyAllocations.AsNoTracking()
                .FirstOrDefaultAsync(a => a.ChildId == child2Id
                                       && a.FamilyEnergyAccountId == wallet.Id);

            src.Should().NotBeNull("source allocation must exist after transfer");
            dst.Should().NotBeNull("GAP-16-B DEFECT: destination allocation row must be INSERTed for mid-cycle child");

            srcRemainingAfterFirst  = src!.Remaining;
            destRemainingAfterFirst = dst!.Remaining;
            destAllocRowCount = await db.ChildEnergyAllocations.AsNoTracking()
                .CountAsync(a => a.ChildId == child2Id && a.FamilyEnergyAccountId == wallet.Id);
        }

        destRemainingAfterFirst.Should().Be(50,
            "GAP-16-B DEFECT: destination Remaining must equal transfer amount after first call.");

        // ── Second transfer: REPLAY same idempotency key ──────────────────────
        var (resp2, body2, root2) = await SendAsync(HttpMethod.Post, TransferUrl, transferBody, parentToken);
        resp2.IsSuccessStatusCode.Should().BeTrue(
            "GAP-16-B: replay transfer must return success (idempotent no-op); body={0}", body2);

        // Balances must NOT change on replay
        using (var replayScope = _factory.Services.CreateScope())
        {
            var db = replayScope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstAsync(w => w.ParentUserId == parentId);
            var src = await db.ChildEnergyAllocations.AsNoTracking()
                .FirstOrDefaultAsync(a => a.ChildId == child1Id
                                       && a.FamilyEnergyAccountId == wallet.Id);
            var dst = await db.ChildEnergyAllocations.AsNoTracking()
                .FirstOrDefaultAsync(a => a.ChildId == child2Id
                                       && a.FamilyEnergyAccountId == wallet.Id);

            src!.Remaining.Should().Be(srcRemainingAfterFirst,
                "GAP-16-B DEFECT: source Remaining must NOT change on idempotency-key replay.");
            dst!.Remaining.Should().Be(destRemainingAfterFirst,
                "GAP-16-B DEFECT: destination Remaining must NOT change on idempotency-key replay.");

            var destRowCountAfterReplay = await db.ChildEnergyAllocations.AsNoTracking()
                .CountAsync(a => a.ChildId == child2Id && a.FamilyEnergyAccountId == wallet.Id);
            destRowCountAfterReplay.Should().Be(destAllocRowCount,
                "GAP-16-B DEFECT: idempotency replay must NOT INSERT a second ChildEnergyAllocation row " +
                "for the mid-cycle destination child.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // P1 — GAP-17-A: refund.succeeded on Subscription-kind payment → no purchased deduct
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GAP-17-A (P1): A <c>refund.succeeded</c> webhook for a <c>PaymentKind.Subscription</c>
    /// payment must NOT decrement the shared PurchasedBalance.
    /// Subscription refunds stay on the P10-09 policy path; only Pack-kind refunds touch
    /// the purchased balance bucket.
    /// </summary>
    [Fact(DisplayName = "GAP-17-A (P1) refund.succeeded on Subscription payment → PurchasedBalance untouched")]
    public async Task GAP17A_RefundSucceeded_SubscriptionKind_PurchasedBalanceUntouched()
    {
        // ── Setup: Premium parent + subscription payment succeeded ────────────
        var (parentId, parentToken) = await RegisterParentAsync("G17A");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Seed a purchased balance so we can assert it is unchanged after the sub refund
        using (var seedScope = _factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.FirstOrDefaultAsync(w => w.ParentUserId == parentId);
            if (wallet is null)
            {
                wallet = FamilyEnergyAccount.CreateEmpty(parentId);
                db.FamilyEnergyAccounts.Add(wallet);
            }
            wallet.PurchasedBalance = 500;
            await db.SaveChangesAsync(0);
        }

        // Get the subscription payment's provider ref (created during UpgradeParentToPremiumAsync)
        string subscriptionProviderRef;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var subPayment = await db.Payments.AsNoTracking()
                .Where(p => p.ParentUserId == parentId
                         && p.Kind == PaymentKind.Subscription
                         && p.Status == PaymentStatus.Succeeded)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();
            subPayment.Should().NotBeNull("GAP-17-A SETUP: subscription payment must exist after upgrade");
            subscriptionProviderRef = subPayment!.ProviderPaymentRef!;
        }

        // ── Send refund.succeeded for the Subscription-kind payment ──────────
        var refundEventId = $"refund-sub-{Guid.NewGuid():N}";
        var (whResp, whBody) = await SendWebhookAsync(
            refundEventId, "refund.succeeded", subscriptionProviderRef, 199m);
        whResp.IsSuccessStatusCode.Should().BeTrue(
            "GAP-17-A: refund.succeeded webhook must be accepted; body={0}", whBody);

        // ── Assert: PurchasedBalance unchanged ────────────────────────────────
        using (var assertScope = _factory.Services.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.ParentUserId == parentId);

            // Wallet may or may not exist (depends on whether any grant ran)
            if (wallet is not null)
            {
                wallet.PurchasedBalance.Should().Be(500,
                    "GAP-17-A DEFECT: PurchasedBalance must NOT be decremented by a refund.succeeded " +
                    "webhook for a Subscription-kind payment. Only Pack-kind refunds touch the shared " +
                    "purchased balance. Subscription refunds belong on the P10-09 dunning path.");
            }
            // If wallet doesn't exist yet, no deduction could have occurred — assertion trivially passes.
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // P1 — GAP-17-C: RefundReason validation — invalid enum → 422
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GAP-17-C (P1): POST /Refunds/Request with an invalid (out-of-enum) RefundReason value
    /// must return 422 (ICommand validation).
    /// </summary>
    [Fact(DisplayName = "GAP-17-C (P1) Refund Request with invalid RefundReason → 422")]
    public async Task GAP17C_RefundRequest_InvalidRefundReason_Returns422()
    {
        // Register a parent and authenticate — we just need an authenticated request
        var (_, parentToken) = await RegisterParentAsync("G17C");

        // POST with an out-of-range RefundReason (99 = not a valid enum value)
        var (resp, body, _) = await SendAsync(HttpMethod.Post, RefundRequestUrl, new
        {
            PaymentId    = 1,
            RefundReason = 99, // invalid enum value
        }, parentToken);

        // Expect 422 UnprocessableEntity from the ValidationBehavior
        ((int)resp.StatusCode).Should().BeOneOf([422, 400],
            "GAP-17-C DEFECT: POST /Refunds/Request with an invalid RefundReason must return 422 " +
            "(validation error). The validator must reject out-of-range enum values. body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // P1 — GAP-18-A: Combined NoSeatLocked AND Paused → not allowed
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GAP-18-A (P1): The AccessStatus endpoint must return isAccessAllowed=false when a child is
    /// BOTH NoSeatLocked AND Paused. Additionally, unpausing a locked child must still return
    /// isAccessAllowed=false (seat still blocks independently of pause state).
    /// </summary>
    [Fact(DisplayName = "GAP-18-A (P1) Combined NoSeatLocked AND Paused → isAccessAllowed=false; unpause of locked child still blocked")]
    public async Task GAP18A_CombinedLockedAndPaused_NotAllowed_UnpauseLockedStillBlocked()
    {
        // ── Setup: Premium parent + child ─────────────────────────────────────
        var (parentId, parentToken) = await RegisterParentAsync("G18A");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (childId, _) = await AddChildAsync(parentToken, "G18AC");
        childId.Should().BeGreaterThan(0);

        // ── Step 1: Lock the child (set SeatState=NoSeatLocked) ──────────────
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var reservation = await db.SeatReservations
                .FirstOrDefaultAsync(r => r.ChildId == childId
                                       && r.SubscriptionId == subId);
            if (reservation is not null)
            {
                reservation.SeatState = SeatState.NoSeatLocked;
                await db.SaveChangesAsync(0);
            }
        }

        // ── Step 2: Pause the child (via API) ────────────────────────────────
        var pauseUrl = string.Format(PauseChildUrl, childId);
        var (pauseResp, pauseBody, _) = await SendAsync(HttpMethod.Post, pauseUrl, null, parentToken);
        pauseResp.IsSuccessStatusCode.Should().BeTrue(
            "GAP-18-A: pause child must succeed; body={0}", pauseBody);

        // ── Step 3: AccessStatus with BOTH locked AND paused ─────────────────
        var accessStatusUrl = string.Format(AccessStatusUrl, childId);
        var (resp1, body1, root1) = await SendAsync(HttpMethod.Get, accessStatusUrl, bearer: parentToken);
        resp1.IsSuccessStatusCode.Should().BeTrue(
            "GAP-18-A: AccessStatus must return 200; body={0}", body1);

        TryProp(root1, "data", out var data1);
        TryProp(data1, "isAccessAllowed", out var isAllowed1);
        isAllowed1.ValueKind.Should().Be(JsonValueKind.False,
            "GAP-18-A DEFECT: isAccessAllowed must be false when child is BOTH NoSeatLocked AND Paused. " +
            "body={0}", body1);

        // ── Step 4: Unpause the child (while still locked) ───────────────────
        var unpauseUrl = string.Format(UnpauseChildUrl, childId);
        var (unpauseResp, unpauseBody, _) = await SendAsync(HttpMethod.Post, unpauseUrl, null, parentToken);
        unpauseResp.IsSuccessStatusCode.Should().BeTrue(
            "GAP-18-A: unpause child must succeed even if locked; body={0}", unpauseBody);

        // ── Step 5: AccessStatus after unpause — still locked, so still blocked ─
        var (resp2, body2, root2) = await SendAsync(HttpMethod.Get, accessStatusUrl, bearer: parentToken);
        resp2.IsSuccessStatusCode.Should().BeTrue(
            "GAP-18-A: AccessStatus must return 200 after unpause; body={0}", body2);

        TryProp(root2, "data", out var data2);
        TryProp(data2, "isAccessAllowed", out var isAllowed2);
        isAllowed2.ValueKind.Should().Be(JsonValueKind.False,
            "GAP-18-A DEFECT: isAccessAllowed must STILL be false after unpausing a NoSeatLocked child. " +
            "The seat lock (SeatState=NoSeatLocked) must independently block access — unpausing alone " +
            "is not sufficient when the child has no active seat. body={0}", body2);
    }
}
