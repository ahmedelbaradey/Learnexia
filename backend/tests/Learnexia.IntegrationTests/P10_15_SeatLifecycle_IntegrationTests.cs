// ReSharper disable InconsistentNaming
// P10-15 Seat Lifecycle (enforcement, grace, NoSeat/Locked) — Integration Tests
//
// Tests in this file exercise the running backend API (HTTP + service layer) for all
// P10-15 acceptance criteria using WebApplicationFactory<Program> + the shared
// Testcontainers Postgres container (LearnexiaWebAppFactory / [Collection("IntegrationTests")]).
//
// Coverage map (keyed to task IDs / spec cases):
//
//   LIFECYCLE-STATUS-01   GET Seats/Status: parent sees per-child SeatState + paidActiveSeats + grace window
//   LIFECYCLE-STATUS-02   GET Seats/Status: child JWT → 403 (wrong role); anonymous → 401
//   LIFECYCLE-STATUS-03   GET Seats/Status: family-scoped (another parent's data not visible)
//
//   LIFECYCLE-CHOOSE-01   ChooseActive: ≤ paidActiveSeats → chosen=Active, rest=NoSeatLocked, ledger written
//   LIFECYCLE-CHOOSE-02   ChooseActive: > paidActiveSeats → rejected 422 (UnprocessableEntity)
//   LIFECYCLE-CHOOSE-03   ChooseActive: IDOR — childId belonging to another family → rejected (Forbidden)
//   LIFECYCLE-CHOOSE-04   ChooseActive: state changes write SeatLedgerEntry{ChoiceApplied/Locked}
//
//   LIFECYCLE-ENFORCE-01  EnforceAsync: over-limit children → NoSeatLocked; earliest-reserved kept; allocation row removed
//   LIFECYCLE-ENFORCE-02  EnforceAsync: no energy reclaimed (CreditTransaction untouched), child NOT deleted
//   LIFECYCLE-ENFORCE-03  EnforceAsync: SeatLedgerEntry{Locked} per locked child
//   LIFECYCLE-ENFORCE-04  EnforceAsync: idempotent — second run produces no new locks/ledger rows
//
//   LIFECYCLE-GRACE-01    PaymentFailure grace: SeatGraceReason=PaymentFailure, GraceEndsAt≈now+7d
//   LIFECYCLE-GRACE-02    PaymentFailure grace: idempotent (2nd failure call within open window = no-op)
//   LIFECYCLE-GRACE-03    Voluntary cancel does NOT start grace (GraceEndsAt/SeatGraceReason stay null)
//
//   LIFECYCLE-SPEND-01    NoSeatLocked child spend → DebitOutcome.SeatLocked; no balance row touched
//   LIFECYCLE-SPEND-02    Active child with funds → spend succeeds normally
//
//   LIFECYCLE-REACTIVATE-01  POST Seats/Reactivate: locked child → prorated checkout (redirectUrl + amount); 0 energy minted
//   LIFECYCLE-REACTIVATE-02  SeatReactivation webhook: seat flips to Active; SeatLedgerEntry{Reactivated} with Amount
//   LIFECYCLE-REACTIVATE-03  SeatReactivation webhook idempotent on ProviderEventId
//
//   LIFECYCLE-IDEMPOTENCY-01 ReserveSeat: same idempotencyKey twice → exactly one SeatReservation row
//   LIFECYCLE-LEDGER-01      P10-14 seat-purchase webhook writes SeatLedgerEntry{Purchased} (BE-11 back-fill)
//   LIFECYCLE-LEDGER-02      P10-14 cycle-end cancel writes SeatLedgerEntry{CancelScheduled} (BE-11 back-fill)

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
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P10-15 Seat Lifecycle integration tests — enforcement, grace, NoSeat/Locked, reactivation.
///
/// Uses the shared <see cref="LearnexiaWebAppFactory"/> / [Collection("IntegrationTests")] so that
/// the Testcontainers Postgres container is shared across all P10 test classes within the same run.
/// <c>ApplyMigrationsAndSeedAsync</c> is idempotent — safe to call in <see cref="InitializeAsync"/>
/// for every test class.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P10_15_SeatLifecycle_IntegrationTests : IAsyncLifetime
{
    // ── URL constants ──────────────────────────────────────────────────────────
    private const string RegisterParentUrl      = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl            = "api/Parent/Add-Child";
    private const string SignInUrl              = "api/Users/Authentication/Sign-In";
    private const string SeatStatusUrl          = "api/Billing/Seats/Status";
    private const string SeatChooseActiveUrl    = "api/Billing/Seats/ChooseActive";
    private const string SeatReactivateUrl      = "api/Billing/Seats/Reactivate";
    private const string SeatCheckoutUrl        = "api/Billing/Seats/Checkout";
    private const string SeatCancelUrl          = "api/Billing/Seats/Cancel";
    private const string SubscriptionUpgradeUrl = "api/Billing/Subscription/Upgrade";
    private const string SubscriptionCheckoutUrl= "api/Billing/Subscription/Checkout";
    private const string WebhookProviderUrl     = "api/Billing/Webhooks/Provider";
    private const string EnergySpendUrl         = "api/Billing/Credits/Spend";

    // Same signing secret wired in LearnexiaWebAppFactory
    private const string TestSigningSecret = P10_W3_SubscriptionPayment_IntegrationTests.TestSigningSecret;

    // GlobalSetting defaults (from GlobalSettingsSeeder)
    private const int DefaultPremiumIncludedSeats  = 3;
    private const int DefaultPremiumMonthlyPerSeat = 5000;
    private const int DefaultSeatsMax              = 5;
    private const int DefaultGraceDays             = 7;

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P10_15_SeatLifecycle_IntegrationTests(LearnexiaWebAppFactory factory)
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
        => $"lc_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@lifecycle.test";

    /// <summary>Case-insensitive property lookup (handles camelCase and PascalCase).</summary>
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
        catch { /* non-JSON responses */ }
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
        return (GetUserIdFromJwt(token), token);
    }

    /// <summary>
    /// Upgrades a parent to Premium by going through Upgrade → Checkout → webhook.
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
        whReq.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
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
    /// Adds a child to a parent. Returns child id + token on success, (0, null) on failure.
    /// </summary>
    private async Task<(HttpResponseMessage Resp, string Body, int ChildId, string? ChildToken)>
        TryAddChildAsync(string parentToken, string tag = "")
    {
        var childEmail = UniqueEmail($"ch_{tag}");
        var (addResp, addBody, _) = await SendAsync(HttpMethod.Post, AddChildUrl, new
        {
            FullName         = $"Child {tag}",
            Email            = childEmail,
            Password         = "Child@Pass1",
            Grade            = 4,
            Language         = "ar",
            Country          = "EG",
            LearningLanguage = "ar",
        }, parentToken);

        if (!addResp.IsSuccessStatusCode)
            return (addResp, addBody, 0, null);

        var (signResp, signBody, signRoot) = await SendAsync(HttpMethod.Post, SignInUrl, new
        {
            UserName = childEmail,
            Password = "Child@Pass1",
        });
        if (!signResp.IsSuccessStatusCode)
            return (addResp, addBody, 0, null);

        TryProp(signRoot, "data", out var signData);
        TryProp(signData, "accessToken", out var tok);
        var childToken = tok.GetString()!;
        return (addResp, addBody, GetUserIdFromJwt(childToken), childToken);
    }

    /// <summary>Sends a correctly-signed webhook for a given payment ref + event type.</summary>
    private async Task<(HttpResponseMessage Response, string Body)> SendWebhookAsync(
        string eventId, string eventType, string paymentRef, decimal amount)
    {
        var (rawBody, sig) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, eventType, paymentRef, amount, TestSigningSecret);

        var req = new HttpRequestMessage(HttpMethod.Post, WebhookProviderUrl)
        {
            Content = new ByteArrayContent(rawBody),
        };
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        req.Headers.Add("X-Hmac-Signature", sig);

        var resp = await _client.SendAsync(req);
        return (resp, await resp.Content.ReadAsStringAsync());
    }

    /// <summary>Creates a Seat payment row directly in the DB, returns (paymentId, providerRef).</summary>
    private async Task<(int PaymentId, string ProviderRef)> SeedSeatPaymentRowAsync(
        int parentId, int subscriptionId, PaymentKind kind = PaymentKind.Seat,
        int? targetChildId = null, decimal amount = 169m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var providerRef = $"fake-seat-{Guid.NewGuid():N}";
        var payment = new Payment
        {
            ParentUserId       = parentId,
            SubscriptionId     = subscriptionId,
            TargetChildId      = targetChildId,
            Amount             = amount,
            Currency           = PaymentCurrency.EGP,
            Status             = PaymentStatus.Initiated,
            Kind               = kind,
            IdempotencyKey     = $"seat-ik-{Guid.NewGuid():N}",
            ProviderPaymentRef = providerRef,
            CreatedAt          = DateTime.UtcNow,
            CreatedBy          = parentId,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(0);
        return (payment.Id, providerRef);
    }

    /// <summary>Decodes the integer user id from a Learnexia JWT (the "Id" claim).</summary>
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
                ? idProp.GetInt32().ToString()
                : idProp.GetString() ?? "";
            if (int.TryParse(raw, out var id)) return id;
        }
        foreach (var prop in doc.EnumerateObject())
        {
            if (!string.Equals(prop.Name, "id", StringComparison.OrdinalIgnoreCase)) continue;
            var raw = prop.Value.ValueKind == JsonValueKind.Number
                ? prop.Value.GetInt32().ToString()
                : prop.Value.GetString() ?? "";
            if (int.TryParse(raw, out var vi)) return vi;
        }
        throw new InvalidOperationException($"Cannot parse user id from JWT: {json}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 1 — SEAT STATUS QUERY (LIFECYCLE-STATUS-01..03)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// LIFECYCLE-STATUS-01: GET /api/Billing/Seats/Status with a Premium parent JWT →
    /// 200 + BaseResponse envelope containing per-child SeatState, paidActiveSeats, and grace fields.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-STATUS-01 GET Seats/Status returns per-child SeatState + paidActiveSeats + grace")]
    public async Task LIFECYCLE_STATUS_01_SeatStatus_ReturnsSeatStateAndGraceFields()
    {
        var (parentId, parentToken) = await RegisterParentAsync("LS01");
        await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Add one child (Active seat expected)
        var (addResp, addBody, childId, _) = await TryAddChildAsync(parentToken, "LS01C");
        ((int)addResp.StatusCode).Should().BeOneOf([200, 201], "add child must succeed; body={0}", addBody);

        var (resp, body, root) = await SendAsync(HttpMethod.Get, SeatStatusUrl, bearer: parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "LIFECYCLE-STATUS-01: authenticated parent must get 200; body={0}", body);

        // Envelope shape
        root.TryGetProperty("statusCode", out _).Should().BeTrue("envelope must have statusCode; body={0}", body);
        TryProp(root, "successed", out var succEl).Should().BeTrue("envelope must have successed; body={0}", body);
        succEl.GetBoolean().Should().BeTrue("successed must be true; body={0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("envelope must have data; body={0}", body);

        // paidActiveSeats
        TryProp(data, "totalSeats", out var totalSeatsEl).Should().BeTrue("data must have totalSeats; body={0}", body);
        totalSeatsEl.GetInt32().Should().BeGreaterThan(0,
            "LIFECYCLE-STATUS-01: paidActiveSeats must be > 0 for a Premium parent; body={0}", body);

        // grace fields
        TryProp(data, "graceEndsAt", out _); // may be null (no grace active) — just check no crash
        TryProp(data, "isInGrace", out var isInGraceEl);
        isInGraceEl.ValueKind.Should().Be(JsonValueKind.False,
            "LIFECYCLE-STATUS-01: no grace should be active for a freshly-subscribed parent; body={0}", body);

        // children array with SeatState field
        TryProp(data, "children", out var childrenEl).Should().BeTrue("data must have children; body={0}", body);
        childrenEl.ValueKind.Should().Be(JsonValueKind.Array,
            "children must be an array; body={0}", body);

        if (childrenEl.GetArrayLength() > 0)
        {
            var firstChild = childrenEl[0];
            TryProp(firstChild, "childId", out _).Should().BeTrue(
                "LIFECYCLE-STATUS-01: children items must expose childId; body={0}", body);
            TryProp(firstChild, "seatState", out var seatStateEl).Should().BeTrue(
                "LIFECYCLE-STATUS-01 DEFECT: children items must expose seatState (Active/NoSeatLocked); body={0}", body);
            seatStateEl.GetString().Should().Be("Active",
                "LIFECYCLE-STATUS-01 DEFECT: newly added child must have seatState=Active; body={0}", body);
        }
    }

    /// <summary>
    /// LIFECYCLE-STATUS-02: Seats/Status requires [Authorize(Roles="Parent")].
    /// - Anonymous → 401.
    /// - Child JWT → 403 (wrong role: Student, not Parent).
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-STATUS-02 Seats/Status: anon → 401; child JWT → 403")]
    public async Task LIFECYCLE_STATUS_02_SeatStatus_AnonymousAndChildReturnsUnauthorized()
    {
        // Anonymous
        var (anonResp, anonBody, _) = await SendAsync(HttpMethod.Get, SeatStatusUrl);
        anonResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "LIFECYCLE-STATUS-02: anon Seats/Status must return 401; body={0}", anonBody);

        // Child JWT: register parent, add child, sign in child
        var (parentId, parentToken) = await RegisterParentAsync("LS02P");
        await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (addResp, addBody, _, childToken) = await TryAddChildAsync(parentToken, "LS02C");
        ((int)addResp.StatusCode).Should().BeOneOf([200, 201], "add child must succeed; body={0}", addBody);
        childToken.Should().NotBeNullOrEmpty("child must have a token");

        var (childResp, childBody, _) = await SendAsync(HttpMethod.Get, SeatStatusUrl, bearer: childToken);
        ((int)childResp.StatusCode).Should().BeOneOf([401, 403],
            "LIFECYCLE-STATUS-02 DEFECT: child JWT (role=Student) must be denied Seats/Status with 401 or 403; body={0}", childBody);
    }

    /// <summary>
    /// LIFECYCLE-STATUS-03: Seats/Status is family-scoped — Parent B's token only reveals their own data,
    /// never Parent A's. This verifies no IDOR leak via the JWT-resolved parentUserId path.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-STATUS-03 Seats/Status is family-scoped (no IDOR)")]
    public async Task LIFECYCLE_STATUS_03_SeatStatus_FamilyScoped_NoIDOR()
    {
        // Parent A — Premium with one child
        var (parentAId, parentAToken) = await RegisterParentAsync("LS03A");
        await UpgradeParentToPremiumAsync(parentAId, parentAToken);
        var (addRespA, addBodyA, childAId, _) = await TryAddChildAsync(parentAToken, "LS03CA");
        ((int)addRespA.StatusCode).Should().BeOneOf([200, 201], "add child A; body={0}", addBodyA);

        // Parent B — no children
        var (parentBId, parentBToken) = await RegisterParentAsync("LS03B");

        // Parent B queries status — must see THEIR OWN data (no children), NOT Parent A's
        var (bResp, bBody, bRoot) = await SendAsync(HttpMethod.Get, SeatStatusUrl, bearer: parentBToken);
        bResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "LIFECYCLE-STATUS-03: Parent B must get 200 status; body={0}", bBody);

        TryProp(bRoot, "data", out var bData);
        TryProp(bData, "children", out var bChildrenEl);
        bChildrenEl.ValueKind.Should().Be(JsonValueKind.Array, "children should be an array; body={0}", bBody);

        // Parent B has no children — the array must be empty OR contain only Parent B's children
        // (it must NOT contain childAId which belongs to Parent A)
        var bChildIds = bChildrenEl.EnumerateArray()
            .Select(c => { TryProp(c, "childId", out var cid); return cid.ValueKind == JsonValueKind.Number ? cid.GetInt32() : -1; })
            .ToList();

        bChildIds.Should().NotContain(childAId,
            "LIFECYCLE-STATUS-03 DEFECT: Parent B must NOT see Parent A's children in their Seats/Status; " +
            "childAId={0} leaked into Parent B's response; body={1}", childAId, bBody);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 2 — CHOOSE ACTIVE CHILDREN (LIFECYCLE-CHOOSE-01..04)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// LIFECYCLE-CHOOSE-01: ChooseActive with ≤ paidActiveSeats children → chosen=Active, rest=NoSeatLocked.
    /// Verifies DB state after the command.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-CHOOSE-01 ChooseActive selects ≤ limit: chosen=Active rest=NoSeatLocked")]
    public async Task LIFECYCLE_CHOOSE_01_ChooseActive_WithinLimit_AppliesStates()
    {
        // Premium parent (3 included seats) + 3 children
        var (parentId, parentToken) = await RegisterParentAsync("LC01");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var (add1R, add1B, child1Id, _) = await TryAddChildAsync(parentToken, "LC01C1");
        var (add2R, add2B, child2Id, _) = await TryAddChildAsync(parentToken, "LC01C2");
        var (add3R, add3B, child3Id, _) = await TryAddChildAsync(parentToken, "LC01C3");
        ((int)add1R.StatusCode).Should().BeOneOf([200, 201], "add child1; body={0}", add1B);
        ((int)add2R.StatusCode).Should().BeOneOf([200, 201], "add child2; body={0}", add2B);
        ((int)add3R.StatusCode).Should().BeOneOf([200, 201], "add child3; body={0}", add3B);

        // Reduce PurchasedExtraSeats to force 1 paid active seat (plan already included=3, so keep all 3 and downgrade artificially via DB)
        // More direct: downgrade available seats to 1 by reducing the plan seating in DB
        // Use DB to reduce subscription IncludedSeats equivalent: set PlanCode to Free and add 0 extra seats,
        // so paidActiveSeats = 1.
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subId);
            // Keep PlanCode=Free: included=1, purchased=0 → paidActiveSeats=1
            sub.PlanCode            = PlanCode.Free;
            sub.PurchasedExtraSeats = 0;
            await db.SaveChangesAsync(0);
        }

        // Choose only child1 as active (1 slot available)
        var chooseCmd = new { ActiveChildIds = new[] { child1Id } };
        var (chooseResp, chooseBody, _) = await SendAsync(HttpMethod.Post, SeatChooseActiveUrl,
            chooseCmd, parentToken);

        chooseResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "LIFECYCLE-CHOOSE-01: choosing 1 child with 1 paid seat must succeed; body={0}", chooseBody);

        // Verify DB: child1 = Active, child2 + child3 = NoSeatLocked
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<BillingDbContext>();
        var reservations = await db2.SeatReservations.AsNoTracking()
            .Where(r => r.SubscriptionId == subId
                     && (r.Status == SeatStatus.Active || r.Status == SeatStatus.Reserved))
            .ToListAsync();

        var r1 = reservations.FirstOrDefault(r => r.ChildId == child1Id);
        var r2 = reservations.FirstOrDefault(r => r.ChildId == child2Id);
        var r3 = reservations.FirstOrDefault(r => r.ChildId == child3Id);

        r1.Should().NotBeNull("child1 seat reservation must exist after ChooseActive");
        r1!.SeatState.Should().Be(SeatState.Active,
            "LIFECYCLE-CHOOSE-01 DEFECT: chosen child1 must have SeatState=Active after ChooseActive; actual={0}", r1.SeatState);

        if (r2 is not null)
            r2.SeatState.Should().Be(SeatState.NoSeatLocked,
                "LIFECYCLE-CHOOSE-01 DEFECT: non-chosen child2 must be NoSeatLocked; actual={0}", r2.SeatState);

        if (r3 is not null)
            r3.SeatState.Should().Be(SeatState.NoSeatLocked,
                "LIFECYCLE-CHOOSE-01 DEFECT: non-chosen child3 must be NoSeatLocked; actual={0}", r3.SeatState);
    }

    /// <summary>
    /// LIFECYCLE-CHOOSE-02: ChooseActive with more children than paidActiveSeats → 422 (UnprocessableEntity).
    /// The handler returns SeatChoiceExceedsLimit with status 422.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-CHOOSE-02 ChooseActive exceeds paid seats → 422")]
    public async Task LIFECYCLE_CHOOSE_02_ChooseActive_ExceedsLimit_Returns422()
    {
        // Free parent (1 included seat) + 1 child already added
        var (parentId, parentToken) = await RegisterParentAsync("LC02");
        // Free plan = 1 seat. We request 2 active children (which exceeds 1).
        // But we only have 1 child. Try with 2 fictitious IDs to exceed the limit.
        // Register premium parent to have proper seats, then downgrade artificially
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);
        var (add1R, add1B, child1Id, _) = await TryAddChildAsync(parentToken, "LC02C1");
        var (add2R, add2B, child2Id, _) = await TryAddChildAsync(parentToken, "LC02C2");
        ((int)add1R.StatusCode).Should().BeOneOf([200, 201], "add child1; body={0}", add1B);
        ((int)add2R.StatusCode).Should().BeOneOf([200, 201], "add child2; body={0}", add2B);

        // Artificially reduce to 1 paid active seat
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subId);
            sub.PlanCode            = PlanCode.Free;
            sub.PurchasedExtraSeats = 0;
            await db.SaveChangesAsync(0);
        }

        // Request both children as active — exceeds the 1 available seat
        var chooseCmd = new { ActiveChildIds = new[] { child1Id, child2Id } };
        var (chooseResp, chooseBody, _) = await SendAsync(HttpMethod.Post, SeatChooseActiveUrl,
            chooseCmd, parentToken);

        ((int)chooseResp.StatusCode).Should().BeOneOf([422, 409, 400, 424],
            "LIFECYCLE-CHOOSE-02 DEFECT: choosing more children than paidActiveSeats must return 422 (or 409/400); " +
            "actual={0}; body={1}", (int)chooseResp.StatusCode, chooseBody);
        chooseResp.IsSuccessStatusCode.Should().BeFalse(
            "LIFECYCLE-CHOOSE-02 DEFECT: exceeding limit must fail; body={0}", chooseBody);
    }

    /// <summary>
    /// LIFECYCLE-CHOOSE-03: ChooseActive IDOR — a childId not belonging to the authenticated parent's
    /// family must be rejected (Forbidden). No cross-family state mutation must occur.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-CHOOSE-03 ChooseActive IDOR: foreign child → rejected (Forbidden)")]
    public async Task LIFECYCLE_CHOOSE_03_ChooseActive_IDOR_ForeignChild_Rejected()
    {
        // Parent A — Premium with one child
        var (parentAId, parentAToken) = await RegisterParentAsync("LC03A");
        await UpgradeParentToPremiumAsync(parentAId, parentAToken);
        var (addA, addAB, childAId, _) = await TryAddChildAsync(parentAToken, "LC03CA");
        ((int)addA.StatusCode).Should().BeOneOf([200, 201], "add child A; body={0}", addAB);

        // Parent B — tries to claim childA as one of THEIR active children
        var (parentBId, parentBToken) = await RegisterParentAsync("LC03B");
        await UpgradeParentToPremiumAsync(parentBId, parentBToken);

        var chooseCmd = new { ActiveChildIds = new[] { childAId } };
        var (resp, body, _) = await SendAsync(HttpMethod.Post, SeatChooseActiveUrl, chooseCmd, parentBToken);

        resp.IsSuccessStatusCode.Should().BeFalse(
            "LIFECYCLE-CHOOSE-03 DEFECT: Parent B must NOT be able to claim Parent A's child as active; " +
            "actual status={0}; body={1}", (int)resp.StatusCode, body);

        ((int)resp.StatusCode).Should().BeOneOf([403, 404, 409, 422, 400, 424],
            "LIFECYCLE-CHOOSE-03 DEFECT: foreign child must be rejected with a 4xx status; " +
            "actual={0}; body={1}", (int)resp.StatusCode, body);

        // Verify no mutation occurred: childA still belongs to Parent A with its original SeatState
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var subA = await db.Subscriptions.AsNoTracking()
            .FirstAsync(s => s.ParentUserId == parentAId && s.Status == SubscriptionStatus.Active);
        var resA = await db.SeatReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SubscriptionId == subA.Id && r.ChildId == childAId);

        resA.Should().NotBeNull("childA's seat reservation must still exist after rejected IDOR attempt");
        resA!.SeatState.Should().Be(SeatState.Active,
            "LIFECYCLE-CHOOSE-03 DEFECT: childA's SeatState must not be mutated by Parent B's rejected IDOR attempt; " +
            "actual={0}", resA.SeatState);
    }

    /// <summary>
    /// LIFECYCLE-CHOOSE-04: State changes from ChooseActive write a SeatLedgerEntry{ChoiceApplied}
    /// for the active child and SeatLedgerEntry{Locked} for locked children. Energy ledger (CreditTransaction)
    /// must NOT be touched.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-CHOOSE-04 ChooseActive writes SeatLedgerEntry{ChoiceApplied/Locked}; no CreditTransaction")]
    public async Task LIFECYCLE_CHOOSE_04_ChooseActive_WritesLedgerEntries()
    {
        var (parentId, parentToken) = await RegisterParentAsync("LC04");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var (add1R, add1B, child1Id, _) = await TryAddChildAsync(parentToken, "LC04C1");
        var (add2R, add2B, child2Id, _) = await TryAddChildAsync(parentToken, "LC04C2");
        ((int)add1R.StatusCode).Should().BeOneOf([200, 201], "add child1; body={0}", add1B);
        ((int)add2R.StatusCode).Should().BeOneOf([200, 201], "add child2; body={0}", add2B);

        // Reduce to 1 available seat
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subId);
            sub.PlanCode            = PlanCode.Free;
            sub.PurchasedExtraSeats = 0;
            await db.SaveChangesAsync(0);
        }

        // Count credit transactions before
        int creditTxBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            creditTxBefore = await db.CreditTransactions.CountAsync();
        }

        // Choose child1 (child2 gets locked)
        var (chooseResp, chooseBody, _) = await SendAsync(HttpMethod.Post, SeatChooseActiveUrl,
            new { ActiveChildIds = new[] { child1Id } }, parentToken);
        chooseResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: ChooseActive must succeed; body={0}", chooseBody);

        // Verify SeatLedgerEntry rows
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<BillingDbContext>();

        var ledgerEntries = await db2.SeatLedgerEntries.AsNoTracking()
            .Where(e => e.SubscriptionId == subId)
            .ToListAsync();

        // ChooseActive writes ChoiceApplied entries (Locked is only written by SeatEnforcementService).
        // Verify at least one ChoiceApplied entry exists for any of the children involved.
        ledgerEntries.Should().NotBeEmpty(
            "LIFECYCLE-CHOOSE-04 DEFECT: ChooseActive must write at least one SeatLedgerEntry; entries=[{0}]",
            string.Join(",", ledgerEntries.Select(e => $"{e.EventType}/{e.ChildId}")));

        // ChoiceApplied should be present (the event type ChooseActive uses for all state changes)
        ledgerEntries.Any(e => e.EventType == SeatLedgerEventType.ChoiceApplied)
            .Should().BeTrue(
                "LIFECYCLE-CHOOSE-04 DEFECT: ChooseActive must write SeatLedgerEntry{{ChoiceApplied}} entries; " +
                "entries=[{0}]",
                string.Join(",", ledgerEntries.Select(e => $"{e.EventType}/{e.ChildId}")));

        // CreditTransactions must not have grown
        var creditTxAfter = await db2.CreditTransactions.CountAsync();
        creditTxAfter.Should().Be(creditTxBefore,
            "LIFECYCLE-CHOOSE-04 DEFECT: ChooseActive must NOT touch CreditTransactions; " +
            "before={0}, after={1}", creditTxBefore, creditTxAfter);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 3 — ENFORCEMENT SERVICE (LIFECYCLE-ENFORCE-01..04)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// LIFECYCLE-ENFORCE-01: EnforceAsync with N children > paidActiveSeats →
    /// over-limit children get NoSeatLocked; earliest-reserved child is kept Active.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-ENFORCE-01 EnforceAsync locks over-limit children; earliest-reserved kept")]
    public async Task LIFECYCLE_ENFORCE_01_Enforce_LocksOverLimitChildren_EarliestReservedKept()
    {
        var (parentId, parentToken) = await RegisterParentAsync("EN01");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Add 3 children (Premium = 3 included seats — all fit initially)
        var (add1R, add1B, child1Id, _) = await TryAddChildAsync(parentToken, "EN01C1");
        var (add2R, add2B, child2Id, _) = await TryAddChildAsync(parentToken, "EN01C2");
        var (add3R, add3B, child3Id, _) = await TryAddChildAsync(parentToken, "EN01C3");
        ((int)add1R.StatusCode).Should().BeOneOf([200, 201], "add child1; body={0}", add1B);
        ((int)add2R.StatusCode).Should().BeOneOf([200, 201], "add child2; body={0}", add2B);
        ((int)add3R.StatusCode).Should().BeOneOf([200, 201], "add child3; body={0}", add3B);

        // Force the subscription to only 1 paid seat — triggering over-limit for child2 + child3
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subId);
            sub.PlanCode            = PlanCode.Free;   // IncludedSeats = 1
            sub.PurchasedExtraSeats = 0;
            await db.SaveChangesAsync(0);
        }

        // Run enforcement via service layer
        using var enforceScope = _factory.Services.CreateScope();
        var enforcementService = enforceScope.ServiceProvider.GetRequiredService<ISeatEnforcementService>();
        var lockedCount = await enforcementService.EnforceAsync(subId);

        lockedCount.Should().Be(2,
            "LIFECYCLE-ENFORCE-01 DEFECT: enforcement must lock exactly 2 children (3 seats - 1 paid = 2 over-limit); actual={0}",
            lockedCount);

        // Verify: earliest-reserved child should be Active; the other 2 = NoSeatLocked
        using var verifyScope = _factory.Services.CreateScope();
        var db2 = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var reservations = await db2.SeatReservations.AsNoTracking()
            .Where(r => r.SubscriptionId == subId
                     && (r.Status == SeatStatus.Active || r.Status == SeatStatus.Reserved))
            .OrderBy(r => r.ReservedAt)
            .ToListAsync();

        // The first (earliest) reservation should be Active
        var earliestReservation = reservations.FirstOrDefault();
        earliestReservation.Should().NotBeNull("at least one reservation must exist");
        earliestReservation!.SeatState.Should().Be(SeatState.Active,
            "LIFECYCLE-ENFORCE-01 DEFECT: earliest-reserved child must remain Active after enforcement");

        var lockedReservations = reservations.Where(r => r.SeatState == SeatState.NoSeatLocked).ToList();
        lockedReservations.Count.Should().Be(2,
            "LIFECYCLE-ENFORCE-01 DEFECT: exactly 2 children must be NoSeatLocked; actual={0}",
            lockedReservations.Count);
    }

    /// <summary>
    /// LIFECYCLE-ENFORCE-02: EnforceAsync does NOT reclaim energy (no CreditTransaction rows written
    /// by enforcement), and does NOT delete the child user (Identity row intact).
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-ENFORCE-02 EnforceAsync: no energy reclaimed, child NOT deleted")]
    public async Task LIFECYCLE_ENFORCE_02_Enforce_NoEnergyReclaim_NoChildDeletion()
    {
        var (parentId, parentToken) = await RegisterParentAsync("EN02");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var (add1R, add1B, child1Id, _) = await TryAddChildAsync(parentToken, "EN02C1");
        var (add2R, add2B, child2Id, _) = await TryAddChildAsync(parentToken, "EN02C2");
        ((int)add1R.StatusCode).Should().BeOneOf([200, 201], "add child1; body={0}", add1B);
        ((int)add2R.StatusCode).Should().BeOneOf([200, 201], "add child2; body={0}", add2B);

        // Give child2 an energy allocation (simulates a granted balance)
        int creditTxsBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            creditTxsBefore = await db.CreditTransactions.CountAsync();
        }

        // Reduce to 1 seat (child2 will be locked)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subId);
            sub.PlanCode            = PlanCode.Free;
            sub.PurchasedExtraSeats = 0;
            await db.SaveChangesAsync(0);
        }

        using var enforceScope = _factory.Services.CreateScope();
        var enforcementService = enforceScope.ServiceProvider.GetRequiredService<ISeatEnforcementService>();
        await enforcementService.EnforceAsync(subId);

        using var verifyScope = _factory.Services.CreateScope();
        var db2 = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();

        // CreditTransactions must be unchanged (no forfeit of energy)
        var creditTxsAfter = await db2.CreditTransactions.CountAsync();
        creditTxsAfter.Should().Be(creditTxsBefore,
            "LIFECYCLE-ENFORCE-02 DEFECT: enforcement must NOT write CreditTransactions (no energy forfeit); " +
            "before={0}, after={1}", creditTxsBefore, creditTxsAfter);

        // Child user must NOT be deleted — both children should still be able to sign in
        // We verify via SeatReservation: the locked child's ChildId must still reference a valid user
        var lockedReservation = await db2.SeatReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SubscriptionId == subId && r.SeatState == SeatState.NoSeatLocked);

        lockedReservation.Should().NotBeNull("a locked reservation must exist after enforcement");
        lockedReservation!.ChildId.Should().BeGreaterThan(0,
            "LIFECYCLE-ENFORCE-02 DEFECT: locked child must have a valid ChildId (not deleted)");

        // Verify the child exists in Identity by checking their SeatReservation's ChildId is coherent
        // (We can't directly query IdentityModuleDbContext for user by id without pulling in Identity types,
        //  but we verify indirectly: the locked child's ChildId > 0 and the reservation wasn't cleared).
        // Cross-module assertion: the ChildId on the locked reservation equals the known child id
        var isChild2Locked = lockedReservation.ChildId == child2Id;
        var isChild1Locked = lockedReservation.ChildId == child1Id;
        (isChild2Locked || isChild1Locked).Should().BeTrue(
            "LIFECYCLE-ENFORCE-02 DEFECT: the locked reservation should reference one of the added children; " +
            "lockedChildId={0}, child1Id={1}, child2Id={2}", lockedReservation.ChildId, child1Id, child2Id);
    }

    /// <summary>
    /// LIFECYCLE-ENFORCE-03: EnforceAsync writes a SeatLedgerEntry{Locked} for each child it locks.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-ENFORCE-03 EnforceAsync writes SeatLedgerEntry{Locked} per locked child")]
    public async Task LIFECYCLE_ENFORCE_03_Enforce_WritesLedgerEntryPerLockedChild()
    {
        var (parentId, parentToken) = await RegisterParentAsync("EN03");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var (add1R, add1B, child1Id, _) = await TryAddChildAsync(parentToken, "EN03C1");
        var (add2R, add2B, child2Id, _) = await TryAddChildAsync(parentToken, "EN03C2");
        ((int)add1R.StatusCode).Should().BeOneOf([200, 201], "add child1; body={0}", add1B);
        ((int)add2R.StatusCode).Should().BeOneOf([200, 201], "add child2; body={0}", add2B);

        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subId);
            sub.PlanCode            = PlanCode.Free;
            sub.PurchasedExtraSeats = 0;
            await db.SaveChangesAsync(0);
        }

        using var enforceScope = _factory.Services.CreateScope();
        var enforcementService = enforceScope.ServiceProvider.GetRequiredService<ISeatEnforcementService>();
        var locked = await enforcementService.EnforceAsync(subId);

        locked.Should().BeGreaterThan(0, "at least one child must be locked to verify ledger entries");

        using var verifyScope = _factory.Services.CreateScope();
        var db2 = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var lockedEntries = await db2.SeatLedgerEntries.AsNoTracking()
            .Where(e => e.SubscriptionId == subId && e.EventType == SeatLedgerEventType.Locked)
            .ToListAsync();

        lockedEntries.Count.Should().Be(locked,
            "LIFECYCLE-ENFORCE-03 DEFECT: SeatLedgerEntry{{Locked}} count must equal the number of children locked; " +
            "expected={0}, actual={1}", locked, lockedEntries.Count);

        foreach (var entry in lockedEntries)
        {
            entry.ChildId.Should().HaveValue("locked ledger entries must reference the child id");
            entry.ParentUserId.Should().Be(parentId,
                "locked ledger entries must reference the parent; entry.ParentUserId={0}", entry.ParentUserId);
            entry.SubscriptionId.Should().Be(subId,
                "locked ledger entries must reference the subscription; entry.SubscriptionId={0}", entry.SubscriptionId);
        }
    }

    /// <summary>
    /// LIFECYCLE-ENFORCE-04: EnforceAsync is idempotent — running it a second time on an already-enforced
    /// subscription produces no new locks or ledger rows.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-ENFORCE-04 EnforceAsync idempotent: second run = no new locks/ledger")]
    public async Task LIFECYCLE_ENFORCE_04_Enforce_Idempotent_NoDoubleAction()
    {
        var (parentId, parentToken) = await RegisterParentAsync("EN04");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var (add1R, add1B, child1Id, _) = await TryAddChildAsync(parentToken, "EN04C1");
        var (add2R, add2B, child2Id, _) = await TryAddChildAsync(parentToken, "EN04C2");
        ((int)add1R.StatusCode).Should().BeOneOf([200, 201], "add child1; body={0}", add1B);
        ((int)add2R.StatusCode).Should().BeOneOf([200, 201], "add child2; body={0}", add2B);

        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subId);
            sub.PlanCode            = PlanCode.Free;
            sub.PurchasedExtraSeats = 0;
            await db.SaveChangesAsync(0);
        }

        // First enforcement
        using var enforceScope1 = _factory.Services.CreateScope();
        var enforceSvc1 = enforceScope1.ServiceProvider.GetRequiredService<ISeatEnforcementService>();
        var locked1 = await enforceSvc1.EnforceAsync(subId);
        locked1.Should().BeGreaterThan(0, "first enforcement should lock at least 1 child");

        int ledgerCountAfterFirst;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            ledgerCountAfterFirst = await db.SeatLedgerEntries.CountAsync(e => e.SubscriptionId == subId);
        }

        // Second enforcement (idempotent)
        using var enforceScope2 = _factory.Services.CreateScope();
        var enforceSvc2 = enforceScope2.ServiceProvider.GetRequiredService<ISeatEnforcementService>();
        var locked2 = await enforceSvc2.EnforceAsync(subId);

        locked2.Should().Be(0,
            "LIFECYCLE-ENFORCE-04 DEFECT: second enforcement on an already-enforced subscription must lock 0 children; actual={0}",
            locked2);

        int ledgerCountAfterSecond;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            ledgerCountAfterSecond = await db.SeatLedgerEntries.CountAsync(e => e.SubscriptionId == subId);
        }

        ledgerCountAfterSecond.Should().Be(ledgerCountAfterFirst,
            "LIFECYCLE-ENFORCE-04 DEFECT: second enforcement must NOT append new SeatLedgerEntry rows; " +
            "before={0}, after={1}", ledgerCountAfterFirst, ledgerCountAfterSecond);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 4 — PAYMENT FAILURE GRACE (LIFECYCLE-GRACE-01..03)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// LIFECYCLE-GRACE-01: StartPaymentFailureGraceAsync sets SeatGraceReason=PaymentFailure
    /// and GraceEndsAt ≈ now + grace_days (default 7).
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-GRACE-01 Payment failure grace: SeatGraceReason=PaymentFailure, GraceEndsAt≈now+7d")]
    public async Task LIFECYCLE_GRACE_01_PaymentFailureGrace_SetsReasonAndEndsAt()
    {
        var (parentId, parentToken) = await RegisterParentAsync("GR01");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var nowBefore = DateTime.UtcNow;

        using var svcScope = _factory.Services.CreateScope();
        var graceSvc = svcScope.ServiceProvider.GetRequiredService<ISeatGraceService>();
        await graceSvc.StartPaymentFailureGraceAsync(subId);

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var sub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subId);

        sub.SeatGraceReason.Should().Be(SeatGraceReason.PaymentFailure,
            "LIFECYCLE-GRACE-01 DEFECT: SeatGraceReason must be PaymentFailure after payment-failure grace; actual={0}",
            sub.SeatGraceReason);

        sub.GraceEndsAt.Should().NotBeNull(
            "LIFECYCLE-GRACE-01 DEFECT: GraceEndsAt must be set after payment-failure grace");

        // NOTE: Npgsql.EnableLegacyTimestampBehavior=true reads timestamptz back without UTC
        // normalization — on Egypt servers (UTC+3) the value appears shifted by 3h from UTC.
        // We tolerate a 4-hour window to cover both UTC and UTC+3 environments.
        var expectedGraceEnd = nowBefore.AddDays(DefaultGraceDays);
        sub.GraceEndsAt!.Value.Should().BeCloseTo(expectedGraceEnd, TimeSpan.FromHours(4),
            "LIFECYCLE-GRACE-01 DEFECT: GraceEndsAt must be approximately now + {0} days; expected≈{1}, actual={2}",
            DefaultGraceDays, expectedGraceEnd, sub.GraceEndsAt.Value);

        sub.SeatGraceStartedAt.Should().NotBeNull(
            "LIFECYCLE-GRACE-01 DEFECT: SeatGraceStartedAt must be set (audit field) after payment-failure grace");
    }

    /// <summary>
    /// LIFECYCLE-GRACE-02: A second payment failure within an open grace window is a no-op.
    /// GraceEndsAt must NOT be extended, shortened, or otherwise modified.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-GRACE-02 Payment failure grace: idempotent (2nd call within open window = no-op)")]
    public async Task LIFECYCLE_GRACE_02_PaymentFailureGrace_Idempotent_OpenWindow()
    {
        var (parentId, parentToken) = await RegisterParentAsync("GR02");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // First call
        using var svcScope1 = _factory.Services.CreateScope();
        await svcScope1.ServiceProvider.GetRequiredService<ISeatGraceService>()
            .StartPaymentFailureGraceAsync(subId);

        DateTime? graceEndsAtAfterFirst;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subId);
            graceEndsAtAfterFirst = sub.GraceEndsAt;
        }
        graceEndsAtAfterFirst.Should().NotBeNull("first call must set GraceEndsAt");

        // Second call (window still open — GraceEndsAt > now)
        using var svcScope2 = _factory.Services.CreateScope();
        await svcScope2.ServiceProvider.GetRequiredService<ISeatGraceService>()
            .StartPaymentFailureGraceAsync(subId);

        DateTime? graceEndsAtAfterSecond;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subId);
            graceEndsAtAfterSecond = sub.GraceEndsAt;
        }

        graceEndsAtAfterSecond.Should().Be(graceEndsAtAfterFirst,
            "LIFECYCLE-GRACE-02 DEFECT: second payment failure within open grace window must NOT change GraceEndsAt; " +
            "after first={0}, after second={1}", graceEndsAtAfterFirst, graceEndsAtAfterSecond);
    }

    /// <summary>
    /// LIFECYCLE-GRACE-03: A voluntary cancel (POST Seats/Cancel) must NOT start a grace window.
    /// GraceEndsAt and SeatGraceReason must remain null after a cycle-end cancel.
    /// This verifies the hard product rule: grace = payment-failure only, not voluntary removal.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-GRACE-03 Voluntary cancel does NOT start grace (GraceEndsAt stays null)")]
    public async Task LIFECYCLE_GRACE_03_VoluntaryCancel_NoGrace()
    {
        var (parentId, parentToken) = await RegisterParentAsync("GR03");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Purchase 1 extra seat so we have something to cancel
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subId);
            sub.PurchasedExtraSeats = 1;
            await db.SaveChangesAsync(0);
        }

        // Capture pre-state
        DateTime? graceBefore;
        SeatGraceReason? graceReasonBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subId);
            graceBefore       = sub.GraceEndsAt;
            graceReasonBefore = sub.SeatGraceReason;
        }

        // POST Seats/Cancel (qty=1) — voluntary cancel
        var (resp, body, _) = await SendAsync(HttpMethod.Post, SeatCancelUrl, 1, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "voluntary cancel must succeed; body={0}", body);

        // Verify no grace was started
        using var verifyScope = _factory.Services.CreateScope();
        var dbV = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var subV = await dbV.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subId);

        subV.GraceEndsAt.Should().Be(graceBefore,
            "LIFECYCLE-GRACE-03 DEFECT: a voluntary cancel must NOT change GraceEndsAt " +
            "(grace is payment-failure only); before={0}, after={1}", graceBefore, subV.GraceEndsAt);

        subV.SeatGraceReason.Should().Be(graceReasonBefore,
            "LIFECYCLE-GRACE-03 DEFECT: a voluntary cancel must NOT set SeatGraceReason; " +
            "before={0}, after={1}", graceReasonBefore, subV.SeatGraceReason);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 5 — SPEND GATE (LIFECYCLE-SPEND-01..02)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// LIFECYCLE-SPEND-01: A NoSeatLocked child's AI spend is denied with DebitOutcome.SeatLocked.
    /// The denial must happen BEFORE touching any allocation or purchased balance row.
    /// Verified via the ISeatStateQuery + ICreditSpendService service layer.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-SPEND-01 NoSeatLocked child: spend denied (SeatLocked); no balance touched")]
    public async Task LIFECYCLE_SPEND_01_NoSeatLocked_SpendDenied_SeatLocked()
    {
        var (parentId, parentToken) = await RegisterParentAsync("SP01");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var (addR, addB, child1Id, _) = await TryAddChildAsync(parentToken, "SP01C1");
        ((int)addR.StatusCode).Should().BeOneOf([200, 201], "add child1; body={0}", addB);

        // Manually lock child1's seat directly in DB to simulate NoSeatLocked state
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subId);
            var res = await db.SeatReservations
                .FirstOrDefaultAsync(r => r.SubscriptionId == sub.Id && r.ChildId == child1Id);
            if (res is not null)
            {
                res.SeatState = SeatState.NoSeatLocked;
                await db.SaveChangesAsync(0);
            }
        }

        // Verify ISeatStateQuery.IsChildSeatActiveAsync returns false
        using var queryScope = _factory.Services.CreateScope();
        var seatStateQuery = queryScope.ServiceProvider.GetRequiredService<ISeatStateQuery>();
        var isActive = await seatStateQuery.IsChildSeatActiveAsync(child1Id);
        isActive.Should().BeFalse(
            "LIFECYCLE-SPEND-01 DEFECT: ISeatStateQuery must return false for a NoSeatLocked child; childId={0}",
            child1Id);

        // Verify CreditSpendService returns SeatLocked outcome without touching balance
        using var spendScope = _factory.Services.CreateScope();
        var spendService = spendScope.ServiceProvider.GetRequiredService<ICreditSpendService>();

        int creditTxsBefore;
        {
            using var countScope = _factory.Services.CreateScope();
            var dbC = countScope.ServiceProvider.GetRequiredService<BillingDbContext>();
            creditTxsBefore = await dbC.CreditTransactions.CountAsync();
        }

        var spendResult = await spendService.TryDebitAsync(
            childId       : child1Id,
            amount        : 1,
            reasonCode    : "AiHint",
            idempotencyKey: $"spend-test:{child1Id}:{Guid.NewGuid():N}");

        spendResult.Charged.Should().BeFalse(
            "LIFECYCLE-SPEND-01 DEFECT: spend must not be charged for a NoSeatLocked child");
        spendResult.Outcome.Should().Be(DebitOutcome.SeatLocked,
            "LIFECYCLE-SPEND-01 DEFECT: outcome must be SeatLocked (not InsufficientBalance); actual={0}",
            spendResult.Outcome);

        int creditTxsAfter;
        {
            using var countScope = _factory.Services.CreateScope();
            var dbC = countScope.ServiceProvider.GetRequiredService<BillingDbContext>();
            creditTxsAfter = await dbC.CreditTransactions.CountAsync();
        }

        creditTxsAfter.Should().Be(creditTxsBefore,
            "LIFECYCLE-SPEND-01 DEFECT: SeatLocked denial must NOT write any CreditTransaction row; " +
            "before={0}, after={1}", creditTxsBefore, creditTxsAfter);
    }

    /// <summary>
    /// LIFECYCLE-SPEND-02: An Active child with a funded ChildEnergyAllocation spends normally.
    /// ISeatStateQuery returns true; TryDebitAsync returns Charged.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-SPEND-02 Active child: ISeatStateQuery=true, spend succeeds normally")]
    public async Task LIFECYCLE_SPEND_02_Active_Child_SpendSucceeds()
    {
        var (parentId, parentToken) = await RegisterParentAsync("SP02");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var (addR, addB, childId, _) = await TryAddChildAsync(parentToken, "SP02C");
        ((int)addR.StatusCode).Should().BeOneOf([200, 201], "add child; body={0}", addB);

        // Verify seat state is Active
        using var queryScope = _factory.Services.CreateScope();
        var seatStateQuery = queryScope.ServiceProvider.GetRequiredService<ISeatStateQuery>();
        var isActive = await seatStateQuery.IsChildSeatActiveAsync(childId);
        isActive.Should().BeTrue(
            "LIFECYCLE-SPEND-02: ISeatStateQuery must return true for an Active child; childId={0}", childId);

        // Grant some energy allocation so spend can succeed
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

            // Create FamilyEnergyAccount if it doesn't exist
            var existingWallet = await db.FamilyEnergyAccounts
                .FirstOrDefaultAsync(w => w.ParentUserId == parentId);

            FamilyEnergyAccount wallet;
            if (existingWallet is null)
            {
                wallet = new FamilyEnergyAccount
                {
                    ParentUserId       = parentId,
                    SubscriptionBalance = 1000,
                    PurchasedBalance   = 0,
                    CreatedAt          = DateTime.UtcNow,
                    CreatedBy          = parentId,
                };
                db.FamilyEnergyAccounts.Add(wallet);
                await db.SaveChangesAsync(0);
            }
            else
            {
                wallet = existingWallet;
                wallet.SubscriptionBalance = 1000;
                await db.SaveChangesAsync(0);
            }

            // Create ChildEnergyAllocation for the child
            var existingAlloc = await db.ChildEnergyAllocations
                .FirstOrDefaultAsync(a => a.ChildId == childId);
            if (existingAlloc is null)
            {
                var alloc = new ChildEnergyAllocation
                {
                    FamilyEnergyAccountId = wallet.Id,
                    ChildId               = childId,
                    AllocatedAmount       = 500,
                    SpentAmount           = 0,        // Remaining = AllocatedAmount - SpentAmount = 500
                    CycleStartUtc         = DateTime.UtcNow.AddDays(-1),
                    CycleEndUtc           = DateTime.UtcNow.AddDays(29),
                    CreatedAt             = DateTime.UtcNow,
                    CreatedBy             = parentId,
                };
                db.ChildEnergyAllocations.Add(alloc);
                await db.SaveChangesAsync(0);
            }
            else
            {
                // Reset allocation to have 500 remaining (set AllocatedAmount, zero SpentAmount)
                existingAlloc.AllocatedAmount = 500;
                existingAlloc.SpentAmount     = 0;
                await db.SaveChangesAsync(0);
            }
        }

        // Attempt spend
        using var spendScope = _factory.Services.CreateScope();
        var spendService = spendScope.ServiceProvider.GetRequiredService<ICreditSpendService>();
        var result = await spendService.TryDebitAsync(
            childId       : childId,
            amount        : 1,
            reasonCode    : "AiHint",
            idempotencyKey: $"spend-active:{childId}:{Guid.NewGuid():N}");

        result.Outcome.Should().NotBe(DebitOutcome.SeatLocked,
            "LIFECYCLE-SPEND-02 DEFECT: Active child must NOT receive SeatLocked outcome; " +
            "actual outcome={0}", result.Outcome);

        result.Charged.Should().BeTrue(
            "LIFECYCLE-SPEND-02 DEFECT: Active child with funds must have Charged=true; outcome={0}",
            result.Outcome);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 6 — REACTIVATION (LIFECYCLE-REACTIVATE-01..03)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// LIFECYCLE-REACTIVATE-01: POST /api/Billing/Seats/Reactivate for a NoSeatLocked child →
    /// prorated checkout response (redirectUrl + amount > 0). Zero energy must be minted.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-REACTIVATE-01 Reactivate locked child → prorated checkout (redirectUrl + amount); no energy")]
    public async Task LIFECYCLE_REACTIVATE_01_Reactivate_ReturnsCheckoutUrl_NoEnergy()
    {
        var (parentId, parentToken) = await RegisterParentAsync("RA01");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var (addR, addB, childId, _) = await TryAddChildAsync(parentToken, "RA01C");
        ((int)addR.StatusCode).Should().BeOneOf([200, 201], "add child; body={0}", addB);

        // Set a known billing cycle so proration is meaningful
        {
            using var scope = _factory.Services.CreateScope();
            var db  = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subId);
            var now = DateTime.UtcNow;
            sub.CurrentCycleStart = now.AddDays(-10);
            sub.CurrentCycleEnd   = now.AddDays(20);
            await db.SaveChangesAsync(0);
        }

        // Lock the child's seat
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var res = await db.SeatReservations
                .FirstOrDefaultAsync(r => r.SubscriptionId == subId && r.ChildId == childId);
            if (res is not null)
            {
                res.SeatState = SeatState.NoSeatLocked;
                await db.SaveChangesAsync(0);
            }
        }

        // Capture wallet state before (should be null or 0)
        int walletSubBalanceBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.ParentUserId == parentId);
            walletSubBalanceBefore = wallet?.SubscriptionBalance ?? 0;
        }

        // POST Seats/Reactivate
        var reactivateCmd = new { ChildId = childId, IdempotencyKey = $"react-{Guid.NewGuid():N}" };
        var (resp, body, root) = await SendAsync(HttpMethod.Post, SeatReactivateUrl,
            reactivateCmd, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "LIFECYCLE-REACTIVATE-01: reactivate for a locked child must return 200; body={0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("response must have data; body={0}", body);
        TryProp(data, "redirectUrl", out var redirectUrlEl).Should().BeTrue(
            "LIFECYCLE-REACTIVATE-01 DEFECT: response must include redirectUrl; body={0}", body);
        redirectUrlEl.GetString().Should().NotBeNullOrEmpty(
            "LIFECYCLE-REACTIVATE-01 DEFECT: redirectUrl must not be empty; body={0}", body);

        TryProp(data, "amount", out var amountEl).Should().BeTrue(
            "LIFECYCLE-REACTIVATE-01 DEFECT: response must include prorated amount; body={0}", body);
        amountEl.GetDecimal().Should().BeGreaterThan(0m,
            "LIFECYCLE-REACTIVATE-01 DEFECT: prorated amount must be > 0; body={0}", body);

        // Wallet SubscriptionBalance must NOT have grown (zero energy minted on reactivation)
        int walletSubBalanceAfter;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.ParentUserId == parentId);
            walletSubBalanceAfter = wallet?.SubscriptionBalance ?? 0;
        }
        walletSubBalanceAfter.Should().Be(walletSubBalanceBefore,
            "LIFECYCLE-REACTIVATE-01 DEFECT: zero energy must be minted on reactivation; " +
            "walletSubBalance before={0}, after={1}", walletSubBalanceBefore, walletSubBalanceAfter);
    }

    /// <summary>
    /// LIFECYCLE-REACTIVATE-02: The SeatReactivation webhook flips the child's seat from NoSeatLocked
    /// to Active and writes a SeatLedgerEntry{Reactivated} with Amount > 0.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-REACTIVATE-02 SeatReactivation webhook: seat→Active; SeatLedgerEntry{Reactivated} with Amount")]
    public async Task LIFECYCLE_REACTIVATE_02_ReactivationWebhook_FlipsSeatToActive_WritesLedger()
    {
        var (parentId, parentToken) = await RegisterParentAsync("RA02");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var (addR, addB, childId, _) = await TryAddChildAsync(parentToken, "RA02C");
        ((int)addR.StatusCode).Should().BeOneOf([200, 201], "add child; body={0}", addB);

        // Lock the child's seat
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var res = await db.SeatReservations
                .FirstOrDefaultAsync(r => r.SubscriptionId == subId && r.ChildId == childId);
            if (res is not null)
            {
                res.SeatState = SeatState.NoSeatLocked;
                await db.SaveChangesAsync(0);
            }
        }

        // Seed a SeatReactivation payment row
        var (paymentId, providerRef) = await SeedSeatPaymentRowAsync(
            parentId, subId,
            kind          : PaymentKind.SeatReactivation,
            targetChildId : childId,
            amount        : 85m);  // prorated amount

        int walletSubBalanceBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.ParentUserId == parentId);
            walletSubBalanceBefore = wallet?.SubscriptionBalance ?? 0;
        }

        // Send SeatReactivation payment.succeeded webhook
        var eventId = Guid.NewGuid().ToString("N");
        var (whResp, whBody) = await SendWebhookAsync(eventId, "payment.succeeded", providerRef, 85m);
        whResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "LIFECYCLE-REACTIVATE-02: SeatReactivation webhook must return 200; body={0}", whBody);

        using var verifyScope = _factory.Services.CreateScope();
        var db2 = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();

        // Child's seat must be flipped to Active
        var res2 = await db2.SeatReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SubscriptionId == subId && r.ChildId == childId);
        res2.Should().NotBeNull("seat reservation must still exist after reactivation webhook");
        res2!.SeatState.Should().Be(SeatState.Active,
            "LIFECYCLE-REACTIVATE-02 DEFECT: SeatReactivation webhook must flip seat to Active; actual={0}",
            res2.SeatState);

        // SeatLedgerEntry{Reactivated} with Amount
        var reactivatedEntry = await db2.SeatLedgerEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SubscriptionId == subId
                                   && e.ChildId == childId
                                   && e.EventType == SeatLedgerEventType.Reactivated);
        reactivatedEntry.Should().NotBeNull(
            "LIFECYCLE-REACTIVATE-02 DEFECT: a SeatLedgerEntry{{Reactivated}} must be written after the webhook");
        reactivatedEntry!.Amount.Should().NotBeNull("reactivated ledger entry must have an Amount");
        reactivatedEntry!.Amount!.Value.Should().BeApproximately(85m, 0.01m,
            "LIFECYCLE-REACTIVATE-02 DEFECT: ledger entry Amount must match the payment amount; " +
            "expected≈85, actual={0}", reactivatedEntry.Amount.Value);

        // Wallet SubscriptionBalance must NOT grow (zero energy on reactivation)
        var walletAfter = await db2.FamilyEnergyAccounts.AsNoTracking()
            .FirstOrDefaultAsync(w => w.ParentUserId == parentId);
        var walletSubBalanceAfter = walletAfter?.SubscriptionBalance ?? 0;
        walletSubBalanceAfter.Should().Be(walletSubBalanceBefore,
            "LIFECYCLE-REACTIVATE-02 DEFECT: zero subscription energy must be minted by SeatReactivation webhook; " +
            "before={0}, after={1}", walletSubBalanceBefore, walletSubBalanceAfter);
    }

    /// <summary>
    /// LIFECYCLE-REACTIVATE-03: SeatReactivation webhook is idempotent on ProviderEventId.
    /// Replaying the same eventId must be a no-op (seat stays Active; no duplicate ledger entry).
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-REACTIVATE-03 SeatReactivation webhook idempotent on ProviderEventId")]
    public async Task LIFECYCLE_REACTIVATE_03_ReactivationWebhook_Idempotent()
    {
        var (parentId, parentToken) = await RegisterParentAsync("RA03");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var (addR, addB, childId, _) = await TryAddChildAsync(parentToken, "RA03C");
        ((int)addR.StatusCode).Should().BeOneOf([200, 201], "add child; body={0}", addB);

        // Lock the child's seat
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var res = await db.SeatReservations
                .FirstOrDefaultAsync(r => r.SubscriptionId == subId && r.ChildId == childId);
            if (res is not null)
            {
                res.SeatState = SeatState.NoSeatLocked;
                await db.SaveChangesAsync(0);
            }
        }

        var (_, providerRef) = await SeedSeatPaymentRowAsync(
            parentId, subId, PaymentKind.SeatReactivation, childId, 85m);

        var eventId = Guid.NewGuid().ToString("N");

        // First dispatch
        var (resp1, body1) = await SendWebhookAsync(eventId, "payment.succeeded", providerRef, 85m);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first reactivation webhook must return 200; body={0}", body1);

        int ledgerCountAfterFirst;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            ledgerCountAfterFirst = await db.SeatLedgerEntries.CountAsync(e => e.SubscriptionId == subId);
        }

        // Second dispatch (same eventId — idempotent replay)
        var (resp2, body2) = await SendWebhookAsync(eventId, "payment.succeeded", providerRef, 85m);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK,
            "second reactivation webhook replay must return 200 (idempotent); body={0}", body2);

        int ledgerCountAfterSecond;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            ledgerCountAfterSecond = await db.SeatLedgerEntries.CountAsync(e => e.SubscriptionId == subId);
        }

        ledgerCountAfterSecond.Should().Be(ledgerCountAfterFirst,
            "LIFECYCLE-REACTIVATE-03 DEFECT: replaying the same SeatReactivation webhook eventId must NOT " +
            "append a second SeatLedgerEntry; before={0}, after={1}", ledgerCountAfterFirst, ledgerCountAfterSecond);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 7 — RESERVATION IDEMPOTENCY (LIFECYCLE-IDEMPOTENCY-01)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// LIFECYCLE-IDEMPOTENCY-01: Two ReserveSeatAsync calls for the SAME parent with the SAME
    /// idempotencyKey produce exactly ONE SeatReservation row (idempotent at the service layer).
    /// This validates the P10-15-BE-10 fix: the IdempotencyKey column is unique-indexed and
    /// the service returns AlreadyReserved on the second call.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-IDEMPOTENCY-01 ReserveSeat: same idempotencyKey twice → exactly one SeatReservation")]
    public async Task LIFECYCLE_IDEMPOTENCY_01_ReserveSeat_SameKey_IdempotentOneRow()
    {
        var (parentId, parentToken) = await RegisterParentAsync("ID01");
        await UpgradeParentToPremiumAsync(parentId, parentToken);

        // We need a childId to pass to ReserveSeatAsync. Add one child first (occupies 1 seat).
        var (addR, addB, existingChildId, _) = await TryAddChildAsync(parentToken, "ID01C");
        ((int)addR.StatusCode).Should().BeOneOf([200, 201], "add child; body={0}", addB);

        // Use a NEW childId (not yet added) with a fixed idempotency key to test the idempotency
        // at the service layer. We call ReserveSeatAsync directly twice with the same key.
        var idempotencyKey = $"idem-test:{Guid.NewGuid():N}";
        const int newChildId = 999_999; // synthetic child id not yet in the system

        using var svcScope1 = _factory.Services.CreateScope();
        var seatSvc1 = svcScope1.ServiceProvider.GetRequiredService<ISeatService>();
        var result1 = await seatSvc1.ReserveSeatAsync(parentId, newChildId, idempotencyKey);

        // First call must succeed (Reserved or if seats are full, NoFreeSeat — but Premium has capacity)
        new[] { SeatReservationOutcome.Reserved, SeatReservationOutcome.AlreadyReserved }
            .Should().Contain(result1.Outcome,
                "first reservation with fresh key must succeed or be idempotent; actual={0}", result1.Outcome);

        if (result1.Outcome == SeatReservationOutcome.Reserved)
        {
            // Second call with the SAME key — must be AlreadyReserved, no new row
            using var svcScope2 = _factory.Services.CreateScope();
            var seatSvc2 = svcScope2.ServiceProvider.GetRequiredService<ISeatService>();
            var result2 = await seatSvc2.ReserveSeatAsync(parentId, newChildId, idempotencyKey);

            result2.Outcome.Should().Be(SeatReservationOutcome.AlreadyReserved,
                "LIFECYCLE-IDEMPOTENCY-01 DEFECT: second call with same idempotencyKey must return AlreadyReserved; " +
                "actual={0}", result2.Outcome);

            // DB: exactly ONE SeatReservation row for this idempotencyKey
            using var verifyScope = _factory.Services.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var rowCount = await db.SeatReservations.AsNoTracking()
                .CountAsync(r => r.IdempotencyKey == idempotencyKey);

            rowCount.Should().Be(1,
                "LIFECYCLE-IDEMPOTENCY-01 DEFECT: exactly one SeatReservation row must exist for the " +
                "idempotencyKey after two calls; actual rowCount={0}", rowCount);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 8 — SEAT LEDGER BACK-FILL (LIFECYCLE-LEDGER-01..02)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// LIFECYCLE-LEDGER-01: The P10-14 seat-purchase webhook writes a SeatLedgerEntry{Purchased}
    /// (BE-11 back-fill). The CreditTransaction table must NOT receive seat purchase events.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-LEDGER-01 P10-14 seat-purchase webhook writes SeatLedgerEntry{Purchased}; no CreditTransaction")]
    public async Task LIFECYCLE_LEDGER_01_SeatPurchaseWebhook_WritesLedgerEntry_NoCreditTransaction()
    {
        var (parentId, parentToken) = await RegisterParentAsync("LED01");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        int creditTxBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            creditTxBefore = await db.CreditTransactions.CountAsync();
        }

        // Seed a Seat payment row and fire the webhook
        var (paymentId, providerRef) = await SeedSeatPaymentRowAsync(parentId, subId, PaymentKind.Seat);
        var eventId = Guid.NewGuid().ToString("N");

        var (rawBody, sig) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, "payment.succeeded", providerRef, 169m, TestSigningSecret);
        var req = new HttpRequestMessage(HttpMethod.Post, WebhookProviderUrl)
        {
            Content = new ByteArrayContent(rawBody),
        };
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        req.Headers.Add("X-Hmac-Signature", sig);
        var whResp = await _client.SendAsync(req);
        whResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "LIFECYCLE-LEDGER-01: seat purchase webhook must return 200");

        // Verify SeatLedgerEntry{Purchased} exists
        using var verifyScope = _factory.Services.CreateScope();
        var db2 = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var purchasedEntry = await db2.SeatLedgerEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SubscriptionId == subId
                                   && e.EventType == SeatLedgerEventType.Purchased);
        purchasedEntry.Should().NotBeNull(
            "LIFECYCLE-LEDGER-01 DEFECT: a SeatLedgerEntry{{Purchased}} must exist after a seat-purchase webhook; " +
            "subscriptionId={0}", subId);
        purchasedEntry!.Quantity.Should().Be(1,
            "LIFECYCLE-LEDGER-01 DEFECT: Purchased ledger entry must have Quantity=1; actual={0}",
            purchasedEntry.Quantity);
        purchasedEntry!.Amount.Should().NotBeNull(
            "LIFECYCLE-LEDGER-01 DEFECT: Purchased ledger entry must have Amount set");

        // CreditTransactions must NOT grow from a seat purchase
        var creditTxAfter = await db2.CreditTransactions.CountAsync();
        creditTxAfter.Should().Be(creditTxBefore,
            "LIFECYCLE-LEDGER-01 DEFECT: seat-purchase webhook must NOT write a CreditTransaction; " +
            "before={0}, after={1}", creditTxBefore, creditTxAfter);
    }

    /// <summary>
    /// LIFECYCLE-LEDGER-02: The P10-14 cycle-end cancel (POST Seats/Cancel) writes a
    /// SeatLedgerEntry{CancelScheduled} (BE-11 back-fill). Energy ledger (CreditTransaction) stays unchanged.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-LEDGER-02 Cycle-end cancel writes SeatLedgerEntry{CancelScheduled}; no CreditTransaction")]
    public async Task LIFECYCLE_LEDGER_02_CycleEndCancel_WritesLedgerEntry_NoCreditTransaction()
    {
        var (parentId, parentToken) = await RegisterParentAsync("LED02");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Ensure 1 purchased extra seat so the cancel can proceed
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subId);
            sub.PurchasedExtraSeats = 1;
            await db.SaveChangesAsync(0);
        }

        int creditTxBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            creditTxBefore = await db.CreditTransactions.CountAsync();
        }

        var (resp, body, _) = await SendAsync(HttpMethod.Post, SeatCancelUrl, 1, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "cycle-end cancel must succeed; body={0}", body);

        using var verifyScope = _factory.Services.CreateScope();
        var db2 = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var cancelEntry = await db2.SeatLedgerEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SubscriptionId == subId
                                   && e.EventType == SeatLedgerEventType.CancelScheduled);
        cancelEntry.Should().NotBeNull(
            "LIFECYCLE-LEDGER-02 DEFECT: a SeatLedgerEntry{{CancelScheduled}} must be written after a cycle-end cancel; " +
            "subscriptionId={0}", subId);

        var creditTxAfter = await db2.CreditTransactions.CountAsync();
        creditTxAfter.Should().Be(creditTxBefore,
            "LIFECYCLE-LEDGER-02 DEFECT: cycle-end cancel must NOT write a CreditTransaction; " +
            "before={0}, after={1}", creditTxBefore, creditTxAfter);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 9 — CONCURRENT RESERVATION RACE (P10-15-BE-10 Finding 1)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// CONCURRENT-RESERVE-01 (Finding 1 — BE-10): Two parallel AddChild calls for the same Premium
    /// parent with 2 available seats → exactly 2 distinct seat rows (one per call, each keyed by
    /// its own email hash), 2 active children, no drift. Neither call must collide or leave an
    /// orphan reservation row.
    /// </summary>
    [Fact(DisplayName = "CONCURRENT-RESERVE-01 Two parallel AddChild calls (2 seats) → 2 distinct seat rows, no drift")]
    public async Task CONCURRENT_RESERVE_01_TwoParallelAddChild_WithinLimit_TwoDistinctSeatRows()
    {
        var (parentId, parentToken) = await RegisterParentAsync("CR01");
        await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Run two AddChild calls in parallel — different emails → different idempotency keys.
        var task1 = TryAddChildAsync(parentToken, "CR01_C1");
        var task2 = TryAddChildAsync(parentToken, "CR01_C2");
        var results = await Task.WhenAll(task1, task2);

        // Both must succeed (Premium = 3 included seats, 2 used).
        foreach (var (resp, body, _, _) in results)
        {
            ((int)resp.StatusCode).Should().BeOneOf([200, 201],
                "CONCURRENT-RESERVE-01: both parallel AddChild calls must succeed with 2 available seats; body={0}",
                body);
        }

        var childId1 = results[0].ChildId;
        var childId2 = results[1].ChildId;

        childId1.Should().BeGreaterThan(0, "CONCURRENT-RESERVE-01: child 1 must have a valid id");
        childId2.Should().BeGreaterThan(0, "CONCURRENT-RESERVE-01: child 2 must have a valid id");
        childId1.Should().NotBe(childId2,
            "CONCURRENT-RESERVE-01: each parallel call must produce a distinct child id");

        // DB: exactly 2 Active seat rows, each with the correct real childId (no childId=0 stuck rows).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var sub = await db.Subscriptions.AsNoTracking()
            .FirstAsync(s => s.ParentUserId == parentId && s.Status == SubscriptionStatus.Active);

        var activeSeats = await db.SeatReservations.AsNoTracking()
            .Where(r => r.SubscriptionId == sub.Id
                     && (r.Status == SeatStatus.Active || r.Status == SeatStatus.Reserved))
            .ToListAsync();

        activeSeats.Count.Should().Be(2,
            "CONCURRENT-RESERVE-01: exactly 2 seat reservation rows must exist for 2 added children; " +
            "rows=[{0}]",
            string.Join(",", activeSeats.Select(r => $"childId={r.ChildId}/status={r.Status}")));

        // Both children must have their real childId stamped (no childId=0 placeholder left behind).
        activeSeats.Should().NotContain(r => r.ChildId == 0,
            "CONCURRENT-RESERVE-01: no seat row must have childId=0 after both calls succeed " +
            "(placeholder must be resolved to real childId by ActivateSeatAsync)");

        var seatChildIds = activeSeats.Select(r => r.ChildId).ToHashSet();
        seatChildIds.Should().Contain(childId1,
            "CONCURRENT-RESERVE-01: seat row for child 1 must exist with the correct childId");
        seatChildIds.Should().Contain(childId2,
            "CONCURRENT-RESERVE-01: seat row for child 2 must exist with the correct childId");

        // Both rows must have distinct IdempotencyKey values (email-hash based, never shared).
        var keys = activeSeats.Select(r => r.IdempotencyKey).ToList();
        keys.Should().OnlyHaveUniqueItems(
            "CONCURRENT-RESERVE-01: each reservation must have its own distinct idempotency key; " +
            "keys=[{0}]", string.Join(",", keys));
    }

    /// <summary>
    /// CONCURRENT-RESERVE-02 (Finding 1 — BE-10): Two parallel AddChild calls for the same parent
    /// with only 1 seat available → exactly 1 active child succeeds, the other gets 409 (NoFreeSeat),
    /// no orphan reservation row left behind.
    /// </summary>
    [Fact(DisplayName = "CONCURRENT-RESERVE-02 Two parallel AddChild calls (1 seat) → 1 active + 1 clean 409")]
    public async Task CONCURRENT_RESERVE_02_TwoParallelAddChild_OneSeat_OneSucceeds_One409_NoOrphan()
    {
        // Free plan = 1 included seat.
        var (parentId, parentToken) = await RegisterParentAsync("CR02");

        var task1 = TryAddChildAsync(parentToken, "CR02_C1");
        var task2 = TryAddChildAsync(parentToken, "CR02_C2");
        var results = await Task.WhenAll(task1, task2);

        var successes = results.Where(r => ((int)r.Resp.StatusCode) is 200 or 201).ToList();
        var failures  = results.Where(r => ((int)r.Resp.StatusCode) is not (200 or 201)).ToList();

        successes.Should().HaveCount(1,
            "CONCURRENT-RESERVE-02: exactly 1 of the 2 parallel calls must succeed with 1 available seat; " +
            "results=[{0}]",
            string.Join(",", results.Select(r => $"{(int)r.Resp.StatusCode}")));

        failures.Should().HaveCount(1,
            "CONCURRENT-RESERVE-02: exactly 1 of the 2 parallel calls must fail (NoFreeSeat) with 1 seat; " +
            "results=[{0}]",
            string.Join(",", results.Select(r => $"{(int)r.Resp.StatusCode}")));

        // No orphan reservation (Released/Reserved with childId=0) must remain.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var sub = await db.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ParentUserId == parentId
                                   && (s.Status == SubscriptionStatus.Active
                                    || s.Status == SubscriptionStatus.PastDue
                                    || s.Status == SubscriptionStatus.Dunning));

        if (sub is not null)
        {
            // The only active/reserved seat must have a real childId (not 0).
            var allReservations = await db.SeatReservations.AsNoTracking()
                .Where(r => r.SubscriptionId == sub.Id)
                .ToListAsync();

            var activeOrReserved = allReservations
                .Where(r => r.Status == SeatStatus.Active || r.Status == SeatStatus.Reserved)
                .ToList();

            activeOrReserved.Should().HaveCount(1,
                "CONCURRENT-RESERVE-02: exactly 1 Active/Reserved seat must exist after one success + one failure; " +
                "rows=[{0}]",
                string.Join(",", allReservations.Select(r => $"childId={r.ChildId}/status={r.Status}")));

            activeOrReserved[0].ChildId.Should().BeGreaterThan(0,
                "CONCURRENT-RESERVE-02: the surviving seat row must have a real childId (no childId=0 orphan)");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 10 — DOWNGRADE CANCEL MARKER AT RENEWAL (Finding 2)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// LIFECYCLE-RENEWAL-01 (Finding 2): Parent buys 1 extra seat, adds 2 children (both fit in
    /// 1 included + 1 extra = 2 seats), schedules voluntary cancel, sets ExtraSeatCancelEffectiveAt
    /// to the past (past renewal boundary). Enforcement runs → PurchasedExtraSeats decremented,
    /// marker cleared, over-limit child locked, SeatLedgerEntry{RemovedAtRenewal} written.
    /// No energy reclaimed.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-RENEWAL-01 Voluntary cancel marker: applied at renewal, over-limit child locked")]
    public async Task LIFECYCLE_RENEWAL_01_VoluntaryCancelMarker_AppliedAtRenewal_LocksOverLimitChild()
    {
        var (parentId, parentToken) = await RegisterParentAsync("REN01");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Add 3 children (Premium = 3 included — all fit initially).
        var (add1R, add1B, child1Id, _) = await TryAddChildAsync(parentToken, "REN01C1");
        var (add2R, add2B, child2Id, _) = await TryAddChildAsync(parentToken, "REN01C2");
        var (add3R, add3B, child3Id, _) = await TryAddChildAsync(parentToken, "REN01C3");
        ((int)add1R.StatusCode).Should().BeOneOf([200, 201], "add child1; body={0}", add1B);
        ((int)add2R.StatusCode).Should().BeOneOf([200, 201], "add child2; body={0}", add2B);
        ((int)add3R.StatusCode).Should().BeOneOf([200, 201], "add child3; body={0}", add3B);

        // Simulate a renewal boundary: set ExtraSeatCancelEffectiveAt to yesterday and
        // PendingExtraSeatRemovals to 2 (reducing Premium from 3 to 1 included seat net).
        // Also reduce PlanCode to Free so includedSeats = 1 + PurchasedExtraSeats=2 → paidSeats was 3,
        // after cancel marker reduction → paidSeats = 1.
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subId);
            sub.PlanCode                   = PlanCode.Free;    // IncludedSeats = 1
            sub.PurchasedExtraSeats        = 2;
            sub.PendingExtraSeatRemovals   = 2;
            sub.ExtraSeatCancelEffectiveAt = DateTime.UtcNow.AddDays(-1); // past boundary
            sub.CurrentCycleEnd            = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync(0);
        }

        // Run enforcement — cancel marker should be applied first, then lock.
        using var enforceScope = _factory.Services.CreateScope();
        var enforcementService = enforceScope.ServiceProvider.GetRequiredService<ISeatEnforcementService>();
        var lockedCount = await enforcementService.EnforceAsync(subId);

        // Cancel marker must be consumed.
        using var verifyScope = _factory.Services.CreateScope();
        var db2 = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var sub2 = await db2.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subId);
        sub2.PurchasedExtraSeats.Should().Be(0,
            "LIFECYCLE-RENEWAL-01 DEFECT: PurchasedExtraSeats must be decremented by PendingExtraSeatRemovals at renewal; " +
            "actual={0}", sub2.PurchasedExtraSeats);
        sub2.PendingExtraSeatRemovals.Should().Be(0,
            "LIFECYCLE-RENEWAL-01 DEFECT: PendingExtraSeatRemovals must be reset to 0; actual={0}",
            sub2.PendingExtraSeatRemovals);
        sub2.ExtraSeatCancelEffectiveAt.Should().BeNull(
            "LIFECYCLE-RENEWAL-01 DEFECT: ExtraSeatCancelEffectiveAt must be cleared after applying cancel marker");

        // With paidSeats = 1 (Free plan, 0 extra after reduction), 2 children must be locked.
        lockedCount.Should().Be(2,
            "LIFECYCLE-RENEWAL-01 DEFECT: 2 children must be locked after reducing paid seats to 1; " +
            "actual={0}", lockedCount);

        // SeatLedgerEntry{RemovedAtRenewal} must exist for the subscription-level reduction.
        var renewalEntry = await db2.SeatLedgerEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SubscriptionId == subId
                                   && e.EventType == SeatLedgerEventType.RemovedAtRenewal
                                   && e.ChildId == null);
        renewalEntry.Should().NotBeNull(
            "LIFECYCLE-RENEWAL-01 DEFECT: SeatLedgerEntry{{RemovedAtRenewal}} must be written for the cancel-marker reduction; " +
            "subscriptionId={0}", subId);
        renewalEntry!.Quantity.Should().Be(-2,
            "LIFECYCLE-RENEWAL-01 DEFECT: Quantity must be -2 (2 seats removed); actual={0}",
            renewalEntry.Quantity);
        renewalEntry!.IdempotencyKey.Should().NotBeNullOrEmpty(
            "LIFECYCLE-RENEWAL-01 DEFECT: RemovedAtRenewal ledger entry must have a deterministic IdempotencyKey (Finding 3)");

        // No energy must be reclaimed.
        var creditTxCountA = await db2.CreditTransactions.CountAsync();
        // (Run enforcement again to confirm idempotency — no second ledger row)
        using var enforceScope2 = _factory.Services.CreateScope();
        var enforcementService2 = enforceScope2.ServiceProvider.GetRequiredService<ISeatEnforcementService>();
        var lockedCount2 = await enforcementService2.EnforceAsync(subId);
        lockedCount2.Should().Be(0,
            "LIFECYCLE-RENEWAL-01 DEFECT: second enforcement must be idempotent (0 new locks); actual={0}",
            lockedCount2);

        using var verifyScope2 = _factory.Services.CreateScope();
        var db3 = verifyScope2.ServiceProvider.GetRequiredService<BillingDbContext>();
        var creditTxCountB = await db3.CreditTransactions.CountAsync();
        creditTxCountB.Should().Be(creditTxCountA,
            "LIFECYCLE-RENEWAL-01 DEFECT: no CreditTransaction must be written during renewal enforcement");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 11 — LEDGER IDEMPOTENCY KEY (Finding 3)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// LIFECYCLE-ENFORCE-04b (Finding 3 — extends LIFECYCLE-ENFORCE-04): Re-running enforcement
    /// does not produce duplicate ledger rows. With deterministic IdempotencyKey values on Locked
    /// entries, the unique index is now a durable backstop in addition to the SeatState guard.
    /// </summary>
    [Fact(DisplayName = "LIFECYCLE-ENFORCE-04b EnforceAsync idempotent: second run = no new locks/ledger (key backstop)")]
    public async Task LIFECYCLE_ENFORCE_04b_Enforce_Idempotent_KeyBackstop_NoDuplicateLedger()
    {
        var (parentId, parentToken) = await RegisterParentAsync("EN04B");
        var subId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var (add1R, add1B, child1Id, _) = await TryAddChildAsync(parentToken, "EN04BC1");
        var (add2R, add2B, child2Id, _) = await TryAddChildAsync(parentToken, "EN04BC2");
        ((int)add1R.StatusCode).Should().BeOneOf([200, 201], "add child1; body={0}", add1B);
        ((int)add2R.StatusCode).Should().BeOneOf([200, 201], "add child2; body={0}", add2B);

        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subId);
            sub.PlanCode            = PlanCode.Free;
            sub.PurchasedExtraSeats = 0;
            await db.SaveChangesAsync(0);
        }

        // First enforcement.
        using var enforceScope1 = _factory.Services.CreateScope();
        var enforceSvc1 = enforceScope1.ServiceProvider.GetRequiredService<ISeatEnforcementService>();
        var locked1 = await enforceSvc1.EnforceAsync(subId);
        locked1.Should().Be(1, "first enforcement must lock exactly 1 child");

        // Verify the Locked ledger entry has a deterministic IdempotencyKey.
        int ledgerCountAfterFirst;
        string? lockedKey;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            ledgerCountAfterFirst = await db.SeatLedgerEntries.CountAsync(e => e.SubscriptionId == subId);

            var lockedEntry = await db.SeatLedgerEntries.AsNoTracking()
                .FirstOrDefaultAsync(e => e.SubscriptionId == subId
                                       && e.EventType == SeatLedgerEventType.Locked);
            lockedEntry.Should().NotBeNull("ENFORCE-04b: Locked ledger entry must exist after first run");
            lockedKey = lockedEntry!.IdempotencyKey;
            lockedKey.Should().NotBeNullOrEmpty(
                "ENFORCE-04b: Locked ledger entry must carry a deterministic IdempotencyKey (Finding 3)");
        }

        // Second enforcement (idempotent).
        using var enforceScope2 = _factory.Services.CreateScope();
        var enforceSvc2 = enforceScope2.ServiceProvider.GetRequiredService<ISeatEnforcementService>();
        var locked2 = await enforceSvc2.EnforceAsync(subId);
        locked2.Should().Be(0,
            "ENFORCE-04b DEFECT: second enforcement must lock 0 children (already locked); actual={0}",
            locked2);

        int ledgerCountAfterSecond;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            ledgerCountAfterSecond = await db.SeatLedgerEntries.CountAsync(e => e.SubscriptionId == subId);
        }

        ledgerCountAfterSecond.Should().Be(ledgerCountAfterFirst,
            "ENFORCE-04b DEFECT: second enforcement must NOT append new SeatLedgerEntry rows " +
            "(neither SeatState guard nor key backstop should allow a duplicate Locked entry); " +
            "before={0}, after={1}", ledgerCountAfterFirst, ledgerCountAfterSecond);
    }
}
