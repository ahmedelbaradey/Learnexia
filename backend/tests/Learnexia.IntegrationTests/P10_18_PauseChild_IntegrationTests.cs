// ReSharper disable InconsistentNaming
// P10-18 Pause / Unpause Child Access — Integration Tests
//
// Tests in this file exercise the running backend API (HTTP + service layer) for all
// P10-18 acceptance criteria using WebApplicationFactory<Program> + the shared
// Testcontainers Postgres container (LearnexiaWebAppFactory / [Collection("IntegrationTests")]).
//
// Coverage map:
//   PAUSE-01   Pause happy path: POST Pause → 200; GET AccessStatus shows ParentPauseState=Paused,
//              IsAccessAllowed=false; SeatLedgerEntry{ParentPause} written; seat/energy unchanged.
//   PAUSE-02   Unpause happy path: after pause, POST Unpause → 200; AccessStatus ParentPauseState=Active,
//              IsAccessAllowed=true (seat is Active); SeatLedgerEntry{ParentUnpause} written.
//   PAUSE-03   Idempotency — re-pausing a paused child → 200 no-op; no duplicate ledger row.
//              Re-unpausing an active child → 200 no-op; no duplicate ledger row.
//   PAUSE-04   Family-scope IDOR: Parent A pausing/unpausing/reading Parent B's child → 403;
//              no state change on B's child.
//   PAUSE-05   AuthZ: anonymous → 401; child (Student) JWT → 403 on all 3 routes.
//   PAUSE-06   Paused child denied AI (pre-LLM gate): pause child, call POST /api/AiTutor/Hint
//              → SSE error frame with code=ChildAccessPausedByParent, no energy charged.
//              After Unpause, same call is NOT blocked by the pause (may fail for other reasons).
//   PAUSE-07   AccessStatus DTO shape: returns {childId, seatState, parentPauseState, isAccessAllowed}
//              correctly for active / paused / seat-locked children.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Providers;
using Learnexia.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P10-18 Pause/Unpause Child Access integration tests.
///
/// Uses the shared <see cref="LearnexiaWebAppFactory"/> / [Collection("IntegrationTests")] so that
/// the Testcontainers Postgres container is shared across all P10 test classes within the same run.
/// <c>ApplyMigrationsAndSeedAsync</c> is idempotent — safe to call in <see cref="InitializeAsync"/>
/// for every test class.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P10_18_PauseChild_IntegrationTests : IAsyncLifetime
{
    // ── URL constants ────────────────────────────────────────────────────────────
    private const string RegisterParentUrl       = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl             = "api/Parent/Add-Child";
    private const string SignInUrl               = "api/Users/Authentication/Sign-In";
    private const string SubscriptionUpgradeUrl  = "api/Billing/Subscription/Upgrade";
    private const string SubscriptionCheckoutUrl = "api/Billing/Subscription/Checkout";
    private const string WebhookProviderUrl      = "api/Billing/Webhooks/Provider";

    // P10-18 routes (from ChildAccessController)
    // Route template: api/Billing/Children/{childId:int}/Pause|Unpause|AccessStatus
    private const string PauseUrlTemplate        = "api/Billing/Children/{0}/Pause";
    private const string UnpauseUrlTemplate      = "api/Billing/Children/{0}/Unpause";
    private const string AccessStatusUrlTemplate = "api/Billing/Children/{0}/AccessStatus";

    // AI tutor endpoint (SSE) — used for case PAUSE-06
    private const string AiHintUrl = "api/AiTutor/Hint";

    private const string ValidChildPassword = "Child@Pass1";

    // Same signing secret wired in LearnexiaWebAppFactory
    private const string TestSigningSecret = P10_W3_SubscriptionPayment_IntegrationTests.TestSigningSecret;

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P10_18_PauseChild_IntegrationTests(LearnexiaWebAppFactory factory)
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
        => $"p18_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@pause.test";

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
        try { root = JsonDocument.Parse(bodyStr).RootElement; }
        catch { /* non-JSON (e.g. SSE stream) */ }
        return (resp, bodyStr, root);
    }

    /// <summary>Registers a parent and returns their JWT + parsed user id.</summary>
    private async Task<(int ParentId, string ParentToken)> RegisterParentAsync(string tag = "")
    {
        var email = UniqueEmail($"par_{tag}");
        var (resp, body, root) = await SendAsync(HttpMethod.Post, RegisterParentUrl, new
        {
            Email         = email,
            Password      = "Str0ng@Pass!",
            AcceptedTerms = true,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration; body={0}", body);
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        var token = tok.GetString()!;
        return (SeatTestSupport.DecodeUserId(token), token);
    }

    /// <summary>
    /// Upgrades a parent to Premium via Upgrade → Checkout → webhook.
    /// Returns the subscriptionId after activation.
    /// </summary>
    private async Task<int> UpgradeParentToPremiumAsync(int parentId, string parentToken)
    {
        var (upResp, upBody, _) = await SendAsync(HttpMethod.Post, SubscriptionUpgradeUrl,
            new { BillingPeriod = 0 /* Monthly */ }, parentToken);
        upResp.IsSuccessStatusCode.Should().BeTrue("upgrade must succeed; body={0}", upBody);

        var (chResp, chBody, chRoot) = await SendAsync(HttpMethod.Post, SubscriptionCheckoutUrl,
            null, parentToken);
        chResp.IsSuccessStatusCode.Should().BeTrue("checkout must succeed; body={0}", chBody);
        TryProp(chRoot, "data", out var chData);
        TryProp(chData, "providerPaymentRef", out var provRefEl);
        var providerRef = provRefEl.GetString()!;

        var eventId = Guid.NewGuid().ToString("N");
        var (rawBody, sig) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, "payment.succeeded", providerRef, 199m, TestSigningSecret);

        var whReq = new HttpRequestMessage(HttpMethod.Post, WebhookProviderUrl)
        {
            Content = new ByteArrayContent(rawBody),
        };
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

    /// <summary>
    /// Adds a child to the parent and signs in as the child.
    /// Returns (childId, childToken) on success; asserts success inline.
    /// </summary>
    private async Task<(int ChildId, string ChildToken)> AddChildAndSignInAsync(
        string parentToken, string tag = "")
    {
        var email = UniqueEmail($"ch_{tag}");
        var (addResp, addBody, _) = await SendAsync(HttpMethod.Post, AddChildUrl, new
        {
            FullName         = $"Child {tag}",
            Email            = email,
            Password         = ValidChildPassword,
            Grade            = 4,
            Language         = "ar",
            Country          = "EG",
            LearningLanguage = "ar",
        }, parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf([200, 201],
            "add-child must succeed; body={0}", addBody);

        var (signResp, signBody, signRoot) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = ValidChildPassword });
        signResp.StatusCode.Should().Be(HttpStatusCode.OK, "child sign-in; body={0}", signBody);
        TryProp(signRoot, "data", out var sd);
        TryProp(sd, "accessToken", out var t);
        var childToken = t.GetString()!;
        return (SeatTestSupport.DecodeUserId(childToken), childToken);
    }

    /// <summary>Builds the Pause URL for a child id.</summary>
    private static string PauseUrl(int childId)   => string.Format(PauseUrlTemplate, childId);
    /// <summary>Builds the Unpause URL for a child id.</summary>
    private static string UnpauseUrl(int childId) => string.Format(UnpauseUrlTemplate, childId);
    /// <summary>Builds the AccessStatus URL for a child id.</summary>
    private static string AccessStatusUrl(int childId) => string.Format(AccessStatusUrlTemplate, childId);

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 1 — PAUSE HAPPY PATH (PAUSE-01)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// PAUSE-01: Parent pauses their own active-seat child.
    ///
    /// Asserts:
    ///  - POST Pause → 200 + successed=true.
    ///  - GET AccessStatus → ParentPauseState=Paused, IsAccessAllowed=false.
    ///  - SeatLedgerEntry{ParentPause} row exists in DB.
    ///  - SeatReservation.SeatState is unchanged (still Active).
    ///  - No energy balance rows were written (CreditTransaction count unchanged).
    /// </summary>
    [Fact(DisplayName = "PAUSE-01 Pause happy path: 200; AccessStatus=Paused; ledger written; seat/energy unchanged")]
    public async Task PAUSE_01_Pause_HappyPath()
    {
        var (parentId, parentToken) = await RegisterParentAsync("P01");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "P01C");

        // Baseline: CreditTransaction count before pause
        int creditTxsBefore;
        {
            using var s = _factory.Services.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<BillingDbContext>();
            creditTxsBefore = await db.CreditTransactions.CountAsync();
        }

        // ── POST Pause ────────────────────────────────────────────────────────
        var (pauseResp, pauseBody, pauseRoot) = await SendAsync(
            HttpMethod.Post, PauseUrl(childId), bearer: parentToken);

        pauseResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PAUSE-01 DEFECT: POST Pause must return 200; body={0}", pauseBody);

        TryProp(pauseRoot, "successed", out var succEl).Should().BeTrue(
            "PAUSE-01 DEFECT: response must have 'successed'; body={0}", pauseBody);
        succEl.GetBoolean().Should().BeTrue(
            "PAUSE-01 DEFECT: successed must be true on pause; body={0}", pauseBody);

        // ── GET AccessStatus ──────────────────────────────────────────────────
        var (statusResp, statusBody, statusRoot) = await SendAsync(
            HttpMethod.Get, AccessStatusUrl(childId), bearer: parentToken);

        statusResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PAUSE-01 DEFECT: GET AccessStatus must return 200 after pause; body={0}", statusBody);

        TryProp(statusRoot, "data", out var statusData).Should().BeTrue(
            "PAUSE-01 DEFECT: response must have 'data'; body={0}", statusBody);

        TryProp(statusData, "parentPauseState", out var pauseStateEl).Should().BeTrue(
            "PAUSE-01 DEFECT: data must have 'parentPauseState'; body={0}", statusBody);
        // ParentPauseState enum: Active=0, Paused=1 — API may return either int or string
        var pauseStateRaw = pauseStateEl.ValueKind == JsonValueKind.Number
            ? pauseStateEl.GetInt32().ToString()
            : pauseStateEl.GetString() ?? "";
        (pauseStateRaw == "1" || pauseStateRaw.Equals("Paused", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue(
                "PAUSE-01 DEFECT: parentPauseState must be Paused(1) after pause; actual={0}; body={1}",
                pauseStateRaw, statusBody);

        TryProp(statusData, "isAccessAllowed", out var accessEl).Should().BeTrue(
            "PAUSE-01 DEFECT: data must have 'isAccessAllowed'; body={0}", statusBody);
        accessEl.GetBoolean().Should().BeFalse(
            "PAUSE-01 DEFECT: isAccessAllowed must be false when paused; body={0}", statusBody);

        // ── DB: SeatLedgerEntry{ParentPause} written ──────────────────────────
        using var verifyScope = _factory.Services.CreateScope();
        var dbV = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var pauseEntry = await dbV.SeatLedgerEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SubscriptionId == subId
                                   && e.ChildId       == childId
                                   && e.EventType     == SeatLedgerEventType.ParentPause);
        pauseEntry.Should().NotBeNull(
            "PAUSE-01 DEFECT: SeatLedgerEntry{{ParentPause}} must be written after pause; " +
            "childId={0}, subId={1}", childId, subId);

        // ── DB: SeatReservation.SeatState unchanged (still Active) ───────────
        var res = await dbV.SeatReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SubscriptionId == subId && r.ChildId == childId);
        res.Should().NotBeNull("seat reservation must exist for child after add-child");
        res!.SeatState.Should().Be(SeatState.Active,
            "PAUSE-01 DEFECT: pause must NOT change SeatState; SeatState must remain Active; actual={0}",
            res.SeatState);

        // ── DB: ParentPauseState written on the reservation row ───────────────
        res.ParentPauseState.Should().Be(ParentPauseState.Paused,
            "PAUSE-01 DEFECT: SeatReservation.ParentPauseState must be Paused after pause; actual={0}",
            res.ParentPauseState);

        // ── DB: No CreditTransaction rows added (no energy side-effect) ───────
        var creditTxsAfter = await dbV.CreditTransactions.CountAsync();
        creditTxsAfter.Should().Be(creditTxsBefore,
            "PAUSE-01 DEFECT: pause must NOT write any CreditTransaction (no energy side-effect); " +
            "before={0}, after={1}", creditTxsBefore, creditTxsAfter);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 2 — UNPAUSE HAPPY PATH (PAUSE-02)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// PAUSE-02: After pausing a child, the parent unpauses.
    ///
    /// Asserts:
    ///  - POST Unpause → 200 + successed=true.
    ///  - GET AccessStatus → ParentPauseState=Active, IsAccessAllowed=true (seat is Active).
    ///  - SeatLedgerEntry{ParentUnpause} row exists in DB.
    ///  - SeatReservation.SeatState unchanged (still Active).
    ///  - No energy balance rows were written (CreditTransaction count unchanged).
    /// </summary>
    [Fact(DisplayName = "PAUSE-02 Unpause happy path: 200; AccessStatus=Active; ledger written; seat/energy unchanged")]
    public async Task PAUSE_02_Unpause_HappyPath()
    {
        var (parentId, parentToken) = await RegisterParentAsync("P02");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "P02C");

        // Pause first
        var (pauseResp, pauseBody, _) = await SendAsync(
            HttpMethod.Post, PauseUrl(childId), bearer: parentToken);
        pauseResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: Pause must succeed; body={0}", pauseBody);

        // Baseline: CreditTransaction count before unpause
        int creditTxsBefore;
        {
            using var s = _factory.Services.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<BillingDbContext>();
            creditTxsBefore = await db.CreditTransactions.CountAsync();
        }

        // ── POST Unpause ──────────────────────────────────────────────────────
        var (unpauseResp, unpauseBody, unpauseRoot) = await SendAsync(
            HttpMethod.Post, UnpauseUrl(childId), bearer: parentToken);

        unpauseResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PAUSE-02 DEFECT: POST Unpause must return 200; body={0}", unpauseBody);

        TryProp(unpauseRoot, "successed", out var succEl).Should().BeTrue(
            "PAUSE-02 DEFECT: response must have 'successed'; body={0}", unpauseBody);
        succEl.GetBoolean().Should().BeTrue(
            "PAUSE-02 DEFECT: successed must be true on unpause; body={0}", unpauseBody);

        // ── GET AccessStatus ──────────────────────────────────────────────────
        var (statusResp, statusBody, statusRoot) = await SendAsync(
            HttpMethod.Get, AccessStatusUrl(childId), bearer: parentToken);

        statusResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PAUSE-02 DEFECT: GET AccessStatus must return 200 after unpause; body={0}", statusBody);

        TryProp(statusRoot, "data", out var statusData).Should().BeTrue("body={0}", statusBody);

        TryProp(statusData, "parentPauseState", out var pauseStateEl).Should().BeTrue(
            "PAUSE-02 DEFECT: data must have 'parentPauseState'; body={0}", statusBody);
        var pauseStateRaw = pauseStateEl.ValueKind == JsonValueKind.Number
            ? pauseStateEl.GetInt32().ToString()
            : pauseStateEl.GetString() ?? "";
        (pauseStateRaw == "0" || pauseStateRaw.Equals("Active", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue(
                "PAUSE-02 DEFECT: parentPauseState must be Active(0) after unpause; actual={0}; body={1}",
                pauseStateRaw, statusBody);

        TryProp(statusData, "isAccessAllowed", out var accessEl).Should().BeTrue(
            "PAUSE-02 DEFECT: data must have 'isAccessAllowed'; body={0}", statusBody);
        accessEl.GetBoolean().Should().BeTrue(
            "PAUSE-02 DEFECT: isAccessAllowed must be true after unpause (seat is still Active); body={0}",
            statusBody);

        // ── DB: SeatLedgerEntry{ParentUnpause} written ─────────────────────
        using var verifyScope = _factory.Services.CreateScope();
        var dbV = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var unpauseEntry = await dbV.SeatLedgerEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SubscriptionId == subId
                                   && e.ChildId       == childId
                                   && e.EventType     == SeatLedgerEventType.ParentUnpause);
        unpauseEntry.Should().NotBeNull(
            "PAUSE-02 DEFECT: SeatLedgerEntry{{ParentUnpause}} must be written after unpause; " +
            "childId={0}, subId={1}", childId, subId);

        // ── DB: SeatReservation.SeatState unchanged ──────────────────────────
        var res = await dbV.SeatReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SubscriptionId == subId && r.ChildId == childId);
        res.Should().NotBeNull("seat reservation must exist for child");
        res!.SeatState.Should().Be(SeatState.Active,
            "PAUSE-02 DEFECT: unpause must NOT change SeatState; SeatState must remain Active; actual={0}",
            res.SeatState);

        // ── DB: ParentPauseState restored to Active ───────────────────────────
        res.ParentPauseState.Should().Be(ParentPauseState.Active,
            "PAUSE-02 DEFECT: SeatReservation.ParentPauseState must be Active after unpause; actual={0}",
            res.ParentPauseState);

        // ── DB: No CreditTransaction rows added ───────────────────────────────
        var creditTxsAfter = await dbV.CreditTransactions.CountAsync();
        creditTxsAfter.Should().Be(creditTxsBefore,
            "PAUSE-02 DEFECT: unpause must NOT write any CreditTransaction (no energy side-effect); " +
            "before={0}, after={1}", creditTxsBefore, creditTxsAfter);
    }

    /// <summary>
    /// PAUSE-08: a legitimate SAME-UTC-DAY toggle sequence (pause → unpause → re-pause) must all
    /// succeed — the re-pause must NOT 500. (Regression: the ledger IdempotencyKey was day-scoped,
    /// so the 2nd pause collided on the unique SeatLedgerEntries index → 500. Fixed to a
    /// per-occurrence key.) Asserts all three calls return 200, the final state is Paused, and TWO
    /// distinct ParentPause ledger rows exist.
    /// </summary>
    [Fact(DisplayName = "PAUSE-08 Same-day pause→unpause→re-pause: all 200, no 500, distinct ledger rows")]
    public async Task PAUSE_08_SameDay_Pause_Unpause_Repause_NoCollision()
    {
        var (parentId, parentToken) = await RegisterParentAsync("P08");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "P08C");

        var (p1, p1Body, _) = await SendAsync(HttpMethod.Post, PauseUrl(childId), bearer: parentToken);
        p1.StatusCode.Should().Be(HttpStatusCode.OK, "first Pause must succeed; body={0}", p1Body);

        var (u1, u1Body, _) = await SendAsync(HttpMethod.Post, UnpauseUrl(childId), bearer: parentToken);
        u1.StatusCode.Should().Be(HttpStatusCode.OK, "Unpause must succeed; body={0}", u1Body);

        var (p2, p2Body, _) = await SendAsync(HttpMethod.Post, PauseUrl(childId), bearer: parentToken);
        p2.StatusCode.Should().Be(HttpStatusCode.OK,
            "PAUSE-08 DEFECT: same-day RE-PAUSE must succeed (not 500 from a day-scoped ledger key collision); body={0}", p2Body);

        // Final state = Paused
        var (st, stBody, stRoot) = await SendAsync(HttpMethod.Get, AccessStatusUrl(childId), bearer: parentToken);
        st.StatusCode.Should().Be(HttpStatusCode.OK, "AccessStatus must return 200; body={0}", stBody);
        TryProp(stRoot, "data", out var stData).Should().BeTrue("body={0}", stBody);
        TryProp(stData, "isAccessAllowed", out var allowedEl).Should().BeTrue("body={0}", stBody);
        allowedEl.GetBoolean().Should().BeFalse("after re-pause the child must be access-blocked; body={0}", stBody);

        // Two DISTINCT ParentPause ledger rows (per-occurrence key, not collapsed to one day key)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var pauseRows = await db.SeatLedgerEntries.AsNoTracking()
            .Where(e => e.SubscriptionId == subId && e.ChildId == childId
                     && e.EventType == SeatLedgerEventType.ParentPause)
            .CountAsync();
        pauseRows.Should().Be(2,
            "PAUSE-08 DEFECT: two real pause transitions must yield two distinct ParentPause ledger rows; actual={0}", pauseRows);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 3 — IDEMPOTENCY (PAUSE-03)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// PAUSE-03a: Re-pausing an already-paused child is a no-op.
    ///
    /// Asserts:
    ///  - Second POST Pause → 200 + successed=true (no-op, no error).
    ///  - NO duplicate SeatLedgerEntry{ParentPause} row (count unchanged from first pause).
    /// </summary>
    [Fact(DisplayName = "PAUSE-03a Idempotency — re-pausing a paused child → 200 no-op; no duplicate ledger")]
    public async Task PAUSE_03a_RePause_Idempotent_NoDuplicateLedger()
    {
        var (parentId, parentToken) = await RegisterParentAsync("P03A");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "P03AC");

        // First pause
        var (p1Resp, p1Body, _) = await SendAsync(
            HttpMethod.Post, PauseUrl(childId), bearer: parentToken);
        p1Resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: first pause must succeed; body={0}", p1Body);

        // Count ParentPause ledger entries after first pause
        int ledgerCountAfterFirst;
        {
            using var s = _factory.Services.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<BillingDbContext>();
            ledgerCountAfterFirst = await db.SeatLedgerEntries.AsNoTracking()
                .CountAsync(e => e.SubscriptionId == subId
                              && e.ChildId       == childId
                              && e.EventType     == SeatLedgerEventType.ParentPause);
        }
        ledgerCountAfterFirst.Should().Be(1,
            "prerequisite: exactly 1 ParentPause ledger entry must exist after first pause");

        // Second pause (re-pause of already-paused child — idempotent no-op)
        var (p2Resp, p2Body, p2Root) = await SendAsync(
            HttpMethod.Post, PauseUrl(childId), bearer: parentToken);
        p2Resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PAUSE-03a DEFECT: re-pausing an already-paused child must return 200 (no-op); body={0}", p2Body);

        TryProp(p2Root, "successed", out var succEl);
        succEl.GetBoolean().Should().BeTrue(
            "PAUSE-03a DEFECT: successed must be true for idempotent re-pause; body={0}", p2Body);

        // Ledger count must NOT have grown
        int ledgerCountAfterSecond;
        {
            using var s = _factory.Services.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<BillingDbContext>();
            ledgerCountAfterSecond = await db.SeatLedgerEntries.AsNoTracking()
                .CountAsync(e => e.SubscriptionId == subId
                              && e.ChildId       == childId
                              && e.EventType     == SeatLedgerEventType.ParentPause);
        }
        ledgerCountAfterSecond.Should().Be(ledgerCountAfterFirst,
            "PAUSE-03a DEFECT: re-pausing must NOT write a duplicate SeatLedgerEntry{{ParentPause}}; " +
            "after first={0}, after second={1}", ledgerCountAfterFirst, ledgerCountAfterSecond);
    }

    /// <summary>
    /// PAUSE-03b: Re-unpausing an already-active (not paused) child is a no-op.
    ///
    /// Asserts:
    ///  - POST Unpause on a child that is NOT paused → 200 + successed=true (no-op, no error).
    ///  - NO SeatLedgerEntry{ParentUnpause} row written (count stays at 0).
    /// </summary>
    [Fact(DisplayName = "PAUSE-03b Idempotency — re-unpausing an active child → 200 no-op; no ledger written")]
    public async Task PAUSE_03b_ReUnpause_Idempotent_NoLedger()
    {
        var (parentId, parentToken) = await RegisterParentAsync("P03B");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "P03BC");

        // Child is Active (never paused). Verify baseline.
        {
            using var s = _factory.Services.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<BillingDbContext>();
            var res = await db.SeatReservations.AsNoTracking()
                .FirstOrDefaultAsync(r => r.SubscriptionId == subId && r.ChildId == childId);
            res.Should().NotBeNull("seat reservation must exist");
            res!.ParentPauseState.Should().Be(ParentPauseState.Active,
                "prerequisite: child must be Active (not paused) before re-unpause test");
        }

        // Unpause a child that is NOT paused (idempotent no-op)
        var (unpResp, unpBody, unpRoot) = await SendAsync(
            HttpMethod.Post, UnpauseUrl(childId), bearer: parentToken);

        unpResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PAUSE-03b DEFECT: unpausing an already-active child must return 200 (no-op); body={0}", unpBody);

        TryProp(unpRoot, "successed", out var succEl);
        succEl.GetBoolean().Should().BeTrue(
            "PAUSE-03b DEFECT: successed must be true for idempotent unpause; body={0}", unpBody);

        // No ParentUnpause ledger entry must have been written
        {
            using var s = _factory.Services.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<BillingDbContext>();
            var count = await db.SeatLedgerEntries.AsNoTracking()
                .CountAsync(e => e.SubscriptionId == subId
                              && e.ChildId       == childId
                              && e.EventType     == SeatLedgerEventType.ParentUnpause);
            count.Should().Be(0,
                "PAUSE-03b DEFECT: unpausing an already-active child must NOT write a SeatLedgerEntry{{ParentUnpause}}; " +
                "count={0}", count);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 4 — FAMILY-SCOPE IDOR (PAUSE-04)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// PAUSE-04: Parent A cannot pause, unpause, or read Parent B's child.
    ///
    /// Asserts:
    ///  - POST Pause by parent A on parent B's child → 403.
    ///  - POST Unpause by parent A on parent B's child → 403.
    ///  - GET AccessStatus by parent A on parent B's child → 403.
    ///  - Parent B's child ParentPauseState is unchanged (still Active) after all IDOR attempts.
    /// </summary>
    [Fact(DisplayName = "PAUSE-04 IDOR: Parent A cannot pause/unpause/read Parent B's child → 403; no mutation")]
    public async Task PAUSE_04_IDOR_CrossFamily_Rejected_NoMutation()
    {
        // Parent A — Premium, one child
        var (parentAId, parentAToken) = await RegisterParentAsync("P04A");
        var subAId = await UpgradeParentToPremiumAsync(parentAId, parentAToken);
        var (childBId_for_B, _) = await AddChildAndSignInAsync(parentAToken, "P04ACA"); // Parent A's own child — not used in IDOR

        // Parent B — Premium, one child
        var (parentBId, parentBToken) = await RegisterParentAsync("P04B");
        var subBId = await UpgradeParentToPremiumAsync(parentBId, parentBToken);
        var (childBId, _) = await AddChildAndSignInAsync(parentBToken, "P04BC");  // Parent B's child

        // ── Parent A tries to Pause Parent B's child ──────────────────────────
        var (pauseResp, pauseBody, _) = await SendAsync(
            HttpMethod.Post, PauseUrl(childBId), bearer: parentAToken);
        pauseResp.IsSuccessStatusCode.Should().BeFalse(
            "PAUSE-04 DEFECT: Parent A must NOT be able to pause Parent B's child; " +
            "actual status={0}; body={1}", (int)pauseResp.StatusCode, pauseBody);
        ((int)pauseResp.StatusCode).Should().BeOneOf([403, 404],
            "PAUSE-04 DEFECT: IDOR rejection must be 403 (or 404); actual={0}; body={1}",
            (int)pauseResp.StatusCode, pauseBody);

        // ── Parent A tries to Unpause Parent B's child ────────────────────────
        var (unpauseResp, unpauseBody, _) = await SendAsync(
            HttpMethod.Post, UnpauseUrl(childBId), bearer: parentAToken);
        unpauseResp.IsSuccessStatusCode.Should().BeFalse(
            "PAUSE-04 DEFECT: Parent A must NOT be able to unpause Parent B's child; " +
            "actual status={0}; body={1}", (int)unpauseResp.StatusCode, unpauseBody);
        ((int)unpauseResp.StatusCode).Should().BeOneOf([403, 404],
            "PAUSE-04 DEFECT: IDOR unpause rejection must be 403 (or 404); actual={0}; body={1}",
            (int)unpauseResp.StatusCode, unpauseBody);

        // ── Parent A tries to GET AccessStatus for Parent B's child ───────────
        var (statusResp, statusBody, _) = await SendAsync(
            HttpMethod.Get, AccessStatusUrl(childBId), bearer: parentAToken);
        statusResp.IsSuccessStatusCode.Should().BeFalse(
            "PAUSE-04 DEFECT: Parent A must NOT be able to read Parent B's child access status; " +
            "actual status={0}; body={1}", (int)statusResp.StatusCode, statusBody);
        ((int)statusResp.StatusCode).Should().BeOneOf([403, 404],
            "PAUSE-04 DEFECT: IDOR AccessStatus rejection must be 403 (or 404); actual={0}; body={1}",
            (int)statusResp.StatusCode, statusBody);

        // ── Verify Parent B's child state is UNCHANGED ────────────────────────
        using var verifyScope = _factory.Services.CreateScope();
        var dbV = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var resB = await dbV.SeatReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SubscriptionId == subBId && r.ChildId == childBId);
        resB.Should().NotBeNull("Parent B's child seat reservation must still exist after IDOR attempts");
        resB!.ParentPauseState.Should().Be(ParentPauseState.Active,
            "PAUSE-04 DEFECT: Parent A's IDOR attempts must NOT change Parent B's child ParentPauseState; " +
            "actual={0}", resB.ParentPauseState);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 5 — AUTHORIZATION (PAUSE-05)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// PAUSE-05: All three endpoints require authentication with a Parent role.
    ///
    /// Asserts:
    ///  - Anonymous → 401 on all three routes.
    ///  - Child (Student role) JWT → 401 or 403 on all three routes (not 200).
    /// </summary>
    [Fact(DisplayName = "PAUSE-05 AuthZ: anonymous → 401; child (Student) JWT → 401/403 on all 3 routes")]
    public async Task PAUSE_05_AuthZ_AnonymousAndChildRejected()
    {
        // Use a real child id (any positive int would trigger auth before the handler fires,
        // but we use a real setup so DB-level routing works for all test environments)
        var (parentId, parentToken) = await RegisterParentAsync("P05");
        await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (childId, childToken) = await AddChildAndSignInAsync(parentToken, "P05C");

        // ── Anonymous → 401 ───────────────────────────────────────────────────
        var (anonPauseResp, anonPauseBody, _) = await SendAsync(
            HttpMethod.Post, PauseUrl(childId));
        anonPauseResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "PAUSE-05 DEFECT: anonymous POST Pause must return 401; body={0}", anonPauseBody);

        var (anonUnpauseResp, anonUnpauseBody, _) = await SendAsync(
            HttpMethod.Post, UnpauseUrl(childId));
        anonUnpauseResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "PAUSE-05 DEFECT: anonymous POST Unpause must return 401; body={0}", anonUnpauseBody);

        var (anonStatusResp, anonStatusBody, _) = await SendAsync(
            HttpMethod.Get, AccessStatusUrl(childId));
        anonStatusResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "PAUSE-05 DEFECT: anonymous GET AccessStatus must return 401; body={0}", anonStatusBody);

        // ── Child (Student) JWT → 401 or 403 ─────────────────────────────────
        childToken.Should().NotBeNullOrEmpty("child must have a valid token for authz test");

        var (childPauseResp, childPauseBody, _) = await SendAsync(
            HttpMethod.Post, PauseUrl(childId), bearer: childToken);
        ((int)childPauseResp.StatusCode).Should().BeOneOf([401, 403],
            "PAUSE-05 DEFECT: child JWT (role=Student) must be denied POST Pause; " +
            "actual={0}; body={1}", (int)childPauseResp.StatusCode, childPauseBody);

        var (childUnpauseResp, childUnpauseBody, _) = await SendAsync(
            HttpMethod.Post, UnpauseUrl(childId), bearer: childToken);
        ((int)childUnpauseResp.StatusCode).Should().BeOneOf([401, 403],
            "PAUSE-05 DEFECT: child JWT (role=Student) must be denied POST Unpause; " +
            "actual={0}; body={1}", (int)childUnpauseResp.StatusCode, childUnpauseBody);

        var (childStatusResp, childStatusBody, _) = await SendAsync(
            HttpMethod.Get, AccessStatusUrl(childId), bearer: childToken);
        ((int)childStatusResp.StatusCode).Should().BeOneOf([401, 403],
            "PAUSE-05 DEFECT: child JWT (role=Student) must be denied GET AccessStatus; " +
            "actual={0}; body={1}", (int)childStatusResp.StatusCode, childStatusBody);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 6 — PAUSED CHILD DENIED AI — PRE-LLM GATE (PAUSE-06)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// PAUSE-06: A paused child's AI request is denied at the pre-LLM gate.
    ///
    /// This test works WITHOUT LLM keys because the pause-gate fires BEFORE the safety layer
    /// or the AI gateway is invoked. The SSE response carries an error frame with code
    /// matching "ChildAccessPausedByParent".
    ///
    /// Asserts:
    ///  - After pause, child calls POST /api/AiTutor/Hint.
    ///  - Response body contains an SSE "event: error" frame.
    ///  - The error code in the data field contains "ChildAccessPausedByParent" (case-insensitive).
    ///  - No CreditTransaction rows are written (no energy charged).
    ///  - After Unpause, the same call no longer returns the pause-blocked error
    ///    (it may fail for other reasons such as missing LLM keys — that is expected and acceptable).
    /// </summary>
    [Fact(DisplayName = "PAUSE-06 Paused child denied AI (pre-LLM gate): error=ChildAccessPausedByParent; no energy charged")]
    public async Task PAUSE_06_PausedChild_DeniedAI_PreLLMGate_NoEnergyCharged()
    {
        var (parentId, parentToken) = await RegisterParentAsync("P06");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (childId, childToken) = await AddChildAndSignInAsync(parentToken, "P06C");

        // Baseline: CreditTransaction count before the AI call
        int creditTxsBefore;
        {
            using var s = _factory.Services.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<BillingDbContext>();
            creditTxsBefore = await db.CreditTransactions.CountAsync();
        }

        // Pause the child
        var (pauseResp, pauseBody, _) = await SendAsync(
            HttpMethod.Post, PauseUrl(childId), bearer: parentToken);
        pauseResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: pause must succeed; body={0}", pauseBody);

        // Child calls AI Hint endpoint (minimal body; QuestionId=1 is enough — gate fires before DB lookup)
        // The SSE controller does not return a 4xx — it returns 200 with SSE frames.
        // We send Accept: text/event-stream to get the full stream.
        // HelperIntent.Hint = 2 (must pass FluentValidation before the pause gate fires)
        var aiReq = new HttpRequestMessage(HttpMethod.Post, AiHintUrl);
        aiReq.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                QuestionId = 1,
                AttemptId  = 1,
                Intent     = 2, // HelperIntent.Hint = 2
                HintLevel  = (int?)null,
                WrongAnswer = (string?)null,
            }),
            Encoding.UTF8, "application/json");
        aiReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", childToken);
        aiReq.Headers.TryAddWithoutValidation("Accept", "text/event-stream");

        var aiResp = await _client.SendAsync(aiReq, HttpCompletionOption.ResponseContentRead);
        var aiBody = await aiResp.Content.ReadAsStringAsync();

        // SSE controllers return HTTP 200 even on error frames — the error is in the stream body.
        // We do NOT assert status code is 200 here since a pre-authentication 401 is also valid
        // in certain middleware configurations — but the child IS authenticated (Student role), so:
        ((int)aiResp.StatusCode).Should().NotBe(401,
            "PAUSE-06 DEFECT: a Student-authenticated child must not get 401 from the AI endpoint; " +
            "verify the child's JWT is valid; body={0}", aiBody);

        // The body must contain an "event: error" SSE frame with the pause code.
        aiBody.Should().Contain("event: error",
            "PAUSE-06 DEFECT: paused child AI response must contain an SSE error frame; body={0}", aiBody);
        aiBody.ToLower().Should().Contain("childaccesspausedbyparent",
            "PAUSE-06 DEFECT: error frame must contain the ChildAccessPausedByParent code; body={0}", aiBody);

        // No energy charged: CreditTransaction count must NOT have grown
        int creditTxsAfterPausedCall;
        {
            using var s = _factory.Services.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<BillingDbContext>();
            creditTxsAfterPausedCall = await db.CreditTransactions.CountAsync();
        }
        creditTxsAfterPausedCall.Should().Be(creditTxsBefore,
            "PAUSE-06 DEFECT: paused-child AI denial must NOT write any CreditTransaction (no energy charged); " +
            "before={0}, after={1}", creditTxsBefore, creditTxsAfterPausedCall);

        // ── Unpause and verify the pause error is gone ────────────────────────
        var (unpauseResp, unpauseBody, _) = await SendAsync(
            HttpMethod.Post, UnpauseUrl(childId), bearer: parentToken);
        unpauseResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: unpause must succeed; body={0}", unpauseBody);

        // After unpause, the same child AI call must NOT be blocked by the pause gate.
        // It may still fail (e.g. missing LLM keys, InsufficientEnergy, etc.) — that is expected.
        // We only assert the response does NOT contain the pause-specific error code.
        var aiReq2 = new HttpRequestMessage(HttpMethod.Post, AiHintUrl);
        aiReq2.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                QuestionId = 1,
                AttemptId  = 1,
                Intent     = 2, // HelperIntent.Hint = 2
                HintLevel  = (int?)null,
                WrongAnswer = (string?)null,
            }),
            Encoding.UTF8, "application/json");
        aiReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", childToken);
        aiReq2.Headers.TryAddWithoutValidation("Accept", "text/event-stream");

        var aiResp2 = await _client.SendAsync(aiReq2, HttpCompletionOption.ResponseContentRead);
        var aiBody2 = await aiResp2.Content.ReadAsStringAsync();

        // After unpause the response must NOT contain the pause-specific code.
        // (It may contain a different error code from other gates — that is acceptable.)
        aiBody2.ToLower().Should().NotContain("childaccesspausedbyparent",
            "PAUSE-06 DEFECT: after unpause, the AI response must NOT be blocked by the pause gate; " +
            "body={0}", aiBody2);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 7 — ACCESS STATUS DTO SHAPE (PAUSE-07)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// PAUSE-07a: GET AccessStatus for a child with Active seat, Active pause state.
    ///
    /// Asserts:
    ///  - Response has {childId, seatState, parentPauseState, isAccessAllowed} shape.
    ///  - seatState=Active (or 0), parentPauseState=Active (or 0), isAccessAllowed=true.
    /// </summary>
    [Fact(DisplayName = "PAUSE-07a AccessStatus DTO: active seat + not paused → isAccessAllowed=true")]
    public async Task PAUSE_07a_AccessStatus_ActiveSeat_NotPaused_IsAllowed()
    {
        var (parentId, parentToken) = await RegisterParentAsync("P07A");
        await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "P07AC");

        var (resp, body, root) = await SendAsync(
            HttpMethod.Get, AccessStatusUrl(childId), bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PAUSE-07a DEFECT: GET AccessStatus must return 200; body={0}", body);

        // Envelope shape
        root.TryGetProperty("statusCode", out _).Should().BeTrue(
            "PAUSE-07a DEFECT: envelope must have 'statusCode'; body={0}", body);
        TryProp(root, "successed", out var succEl).Should().BeTrue(
            "PAUSE-07a DEFECT: envelope must have 'successed'; body={0}", body);
        succEl.GetBoolean().Should().BeTrue("body={0}", body);
        TryProp(root, "data", out var data).Should().BeTrue(
            "PAUSE-07a DEFECT: envelope must have 'data'; body={0}", body);

        // Data fields
        TryProp(data, "childId", out var cidEl).Should().BeTrue(
            "PAUSE-07a DEFECT: data must have 'childId'; body={0}", body);
        cidEl.GetInt32().Should().Be(childId,
            "PAUSE-07a DEFECT: data.childId must match the queried child; body={0}", body);

        TryProp(data, "seatState", out var seatEl).Should().BeTrue(
            "PAUSE-07a DEFECT: data must have 'seatState'; body={0}", body);
        var seatRaw = seatEl.ValueKind == JsonValueKind.Number
            ? seatEl.GetInt32().ToString()
            : seatEl.GetString() ?? "";
        (seatRaw == "0" || seatRaw.Equals("Active", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue(
                "PAUSE-07a DEFECT: seatState must be Active(0) for a child with an active seat; actual={0}; body={1}",
                seatRaw, body);

        TryProp(data, "parentPauseState", out var pauseEl).Should().BeTrue(
            "PAUSE-07a DEFECT: data must have 'parentPauseState'; body={0}", body);
        var pauseRaw = pauseEl.ValueKind == JsonValueKind.Number
            ? pauseEl.GetInt32().ToString()
            : pauseEl.GetString() ?? "";
        (pauseRaw == "0" || pauseRaw.Equals("Active", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue(
                "PAUSE-07a DEFECT: parentPauseState must be Active(0) for a never-paused child; actual={0}; body={1}",
                pauseRaw, body);

        TryProp(data, "isAccessAllowed", out var accessEl).Should().BeTrue(
            "PAUSE-07a DEFECT: data must have 'isAccessAllowed'; body={0}", body);
        accessEl.GetBoolean().Should().BeTrue(
            "PAUSE-07a DEFECT: isAccessAllowed must be true (active seat, not paused); body={0}", body);
    }

    /// <summary>
    /// PAUSE-07b: GET AccessStatus for a paused child (active seat + parent paused).
    ///
    /// Asserts:
    ///  - After pause: seatState=Active, parentPauseState=Paused, isAccessAllowed=false.
    /// </summary>
    [Fact(DisplayName = "PAUSE-07b AccessStatus DTO: active seat + parent paused → isAccessAllowed=false")]
    public async Task PAUSE_07b_AccessStatus_ActiveSeat_Paused_NotAllowed()
    {
        var (parentId, parentToken) = await RegisterParentAsync("P07B");
        await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "P07BC");

        // Pause the child
        await SendAsync(HttpMethod.Post, PauseUrl(childId), bearer: parentToken);

        var (resp, body, root) = await SendAsync(
            HttpMethod.Get, AccessStatusUrl(childId), bearer: parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body={0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body={0}", body);

        TryProp(data, "seatState", out var seatEl).Should().BeTrue("body={0}", body);
        var seatRaw = seatEl.ValueKind == JsonValueKind.Number
            ? seatEl.GetInt32().ToString()
            : seatEl.GetString() ?? "";
        (seatRaw == "0" || seatRaw.Equals("Active", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue(
                "PAUSE-07b DEFECT: seatState must remain Active after parent pause; actual={0}; body={1}",
                seatRaw, body);

        TryProp(data, "parentPauseState", out var pauseEl).Should().BeTrue("body={0}", body);
        var pauseRaw = pauseEl.ValueKind == JsonValueKind.Number
            ? pauseEl.GetInt32().ToString()
            : pauseEl.GetString() ?? "";
        (pauseRaw == "1" || pauseRaw.Equals("Paused", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue(
                "PAUSE-07b DEFECT: parentPauseState must be Paused(1) after parent pause; actual={0}; body={1}",
                pauseRaw, body);

        TryProp(data, "isAccessAllowed", out var accessEl).Should().BeTrue("body={0}", body);
        accessEl.GetBoolean().Should().BeFalse(
            "PAUSE-07b DEFECT: isAccessAllowed must be false (paused child); body={0}", body);
    }

    /// <summary>
    /// PAUSE-07c: GET AccessStatus for a NoSeatLocked child (seat locked, not paused).
    ///
    /// Asserts:
    ///  - seatState=NoSeatLocked, parentPauseState=Active (default), isAccessAllowed=false.
    ///  - Verifies that a seat-locked child who is NOT parent-paused still has isAccessAllowed=false
    ///    (both states independently block access).
    /// </summary>
    [Fact(DisplayName = "PAUSE-07c AccessStatus DTO: NoSeatLocked seat + not paused → isAccessAllowed=false")]
    public async Task PAUSE_07c_AccessStatus_NoSeatLocked_NotPaused_NotAllowed()
    {
        var (parentId, parentToken) = await RegisterParentAsync("P07C");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "P07CC");

        // Manually lock the child's seat in DB (simulate NoSeatLocked billing state)
        {
            using var s = _factory.Services.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<BillingDbContext>();
            var res = await db.SeatReservations.FirstOrDefaultAsync(
                r => r.SubscriptionId == subId && r.ChildId == childId);
            if (res is not null)
            {
                res.SeatState = SeatState.NoSeatLocked;
                await db.SaveChangesAsync(0);
            }
        }

        var (resp, body, root) = await SendAsync(
            HttpMethod.Get, AccessStatusUrl(childId), bearer: parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PAUSE-07c DEFECT: GET AccessStatus must return 200 even for a locked child; body={0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body={0}", body);

        TryProp(data, "seatState", out var seatEl).Should().BeTrue(
            "PAUSE-07c DEFECT: data must have 'seatState'; body={0}", body);
        var seatRaw = seatEl.ValueKind == JsonValueKind.Number
            ? seatEl.GetInt32().ToString()
            : seatEl.GetString() ?? "";
        (seatRaw == "1" || seatRaw.Equals("NoSeatLocked", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue(
                "PAUSE-07c DEFECT: seatState must be NoSeatLocked(1) after DB lock; actual={0}; body={1}",
                seatRaw, body);

        TryProp(data, "parentPauseState", out var pauseEl).Should().BeTrue(
            "PAUSE-07c DEFECT: data must have 'parentPauseState'; body={0}", body);
        var pauseRaw = pauseEl.ValueKind == JsonValueKind.Number
            ? pauseEl.GetInt32().ToString()
            : pauseEl.GetString() ?? "";
        (pauseRaw == "0" || pauseRaw.Equals("Active", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue(
                "PAUSE-07c DEFECT: parentPauseState must be Active(0) (child was never paused); actual={0}; body={1}",
                pauseRaw, body);

        TryProp(data, "isAccessAllowed", out var accessEl).Should().BeTrue(
            "PAUSE-07c DEFECT: data must have 'isAccessAllowed'; body={0}", body);
        accessEl.GetBoolean().Should().BeFalse(
            "PAUSE-07c DEFECT: isAccessAllowed must be false (NoSeatLocked — either state alone blocks access); body={0}",
            body);
    }
}
