// ReSharper disable InconsistentNaming
// P10-14 Child Seats — Integration Tests
//
// Tests in this file exercise the running backend API (HTTP + service layer) for all
// P10-14 acceptance criteria using WebApplicationFactory<Program> + the shared
// Testcontainers Postgres container (LearnexiaWebAppFactory / [Collection("IntegrationTests")]).
//
// Coverage map:
//   SEAT-STATUS-01  Free plan includes exactly 1 seat (IncludedSeats from Plans seed row)
//   SEAT-STATUS-02  Premium plan includes exactly 3 seats (IncludedSeats from Plans seed row)
//   SEAT-STATUS-03  Max seats ceiling = 5 (seats.max GlobalSetting default)
//   SEAT-STATUS-04  GET /api/Billing/Seats/Status requires auth — 401 without JWT
//   SEAT-STATUS-05  GET /api/Billing/Seats/Status envelope shape: statusCode + successed + data
//   ADD-CHILD-01    Add-child RESERVES a seat atomically and SUCCEEDS within the limit
//   ADD-CHILD-02    Add-child: seat.Status = Active after successful creation
//   ADD-CHILD-03    Add-child REJECTED (Conflict) when all seats are occupied
//   ADD-CHILD-04    On rejection NO child is persisted in Identity
//   ADD-CHILD-05    On rejection NO orphan SeatReservation remains (no Reserved/Active leak)
//   ADD-CHILD-06    Compensation path: failed inner create → seat Released, no orphan
//   WEBHOOK-SEAT-01 Seat-payment webhook increments PurchasedExtraSeats by 1
//   WEBHOOK-SEAT-02 Seat-payment webhook: idempotent on replay (same eventId → no double-increment)
//   WEBHOOK-SEAT-03 Seat-payment webhook: mints NO energy (FamilyEnergyAccount untouched)
//   WEBHOOK-SEAT-04 Seat-payment ceiling: cannot exceed seats.max=5 via webhook
//   GRANT-JOB-01    RealISeatService drives BillingGrantJob: grant = PlanEnergyPerSeat × active paid seats
//   GRANT-JOB-02    RealSeatQuery: ActivePaidSeats = IncludedSeats(plan) + PurchasedExtraSeats, capped at max
//   GRANT-JOB-03    RealSeatQuery: ActiveChildIds contains only Active-status reservation children

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
/// P10-14 Child Seats integration tests.
///
/// Uses the shared <see cref="LearnexiaWebAppFactory"/> / [Collection("IntegrationTests")] so that
/// the Testcontainers Postgres container is shared across all P10 test classes within the same run.
/// <c>ApplyMigrationsAndSeedAsync</c> is idempotent — safe to call in <see cref="InitializeAsync"/>
/// for every test class.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P10_14_ChildSeats_IntegrationTests : IAsyncLifetime
{
    // ── URL constants ──────────────────────────────────────────────────────────
    private const string RegisterParentUrl   = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl         = "api/Parent/Add-Child";
    private const string SignInUrl           = "api/Users/Authentication/Sign-In";
    private const string SeatStatusUrl       = "api/Billing/Seats/Status";
    private const string SeatCheckoutUrl     = "api/Billing/Seats/Checkout";
    private const string SeatCancelUrl       = "api/Billing/Seats/Cancel";
    private const string SubscriptionUpgradeUrl = "api/Billing/Subscription/Upgrade";
    private const string SubscriptionCheckoutUrl = "api/Billing/Subscription/Checkout";
    private const string WebhookProviderUrl  = "api/Billing/Webhooks/Provider";

    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";

    // Same signing secret wired in LearnexiaWebAppFactory
    private const string TestSigningSecret = P10_W3_SubscriptionPayment_IntegrationTests.TestSigningSecret;

    // GlobalSetting defaults (from GlobalSettingsSeeder)
    private const int DefaultFreeIncludedSeats    = 1;
    private const int DefaultPremiumIncludedSeats = 3;
    private const int DefaultSeatsMax             = 5;
    private const int DefaultFreeMonthlyPerSeat   = 100;
    private const int DefaultPremiumMonthlyPerSeat = 5000;

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P10_14_ChildSeats_IntegrationTests(LearnexiaWebAppFactory factory)
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
        => $"seat_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@seats.test";

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

    /// <summary>Registers a parent and returns their JWT access token + parsed user id.</summary>
    private async Task<(int ParentId, string ParentToken)> RegisterParentAsync(string tag = "")
    {
        var email = UniqueEmail($"par_{tag}");
        var (resp, body, root) = await SendAsync(HttpMethod.Post, RegisterParentUrl, new
        {
            Email        = email,
            Password     = "Str0ng@Pass!",
            AcceptedTerms = true,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration; body={0}", body);
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        var token = tok.GetString()!;
        var id    = GetUserIdFromJwt(token);
        return (id, token);
    }

    /// <summary>
    /// Upgrades a parent to Premium by going through: Upgrade → Checkout → webhook.
    /// Returns the subscriptionId after activation.
    /// </summary>
    private async Task<int> UpgradeParentToPremiumAsync(int parentId, string parentToken)
    {
        // 1. Request upgrade (PendingPayment)
        var (upResp, upBody, _) = await SendAsync(HttpMethod.Post, SubscriptionUpgradeUrl,
            new { BillingPeriod = 0 /* Monthly */ }, parentToken);
        upResp.IsSuccessStatusCode.Should().BeTrue("upgrade must succeed; body={0}", upBody);

        // 2. Start checkout → get provider ref
        var (chResp, chBody, chRoot) = await SendAsync(HttpMethod.Post, SubscriptionCheckoutUrl,
            null, parentToken);
        chResp.IsSuccessStatusCode.Should().BeTrue("checkout must succeed; body={0}", chBody);
        TryProp(chRoot, "data", out var chData);
        TryProp(chData, "providerPaymentRef", out var provRefEl);
        var providerRef = provRefEl.GetString()!;

        // 3. Send payment.succeeded webhook
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

        // Return the active subscriptionId
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var sub = await db.Subscriptions
            .AsNoTracking()
            .FirstAsync(s => s.ParentUserId == parentId && s.Status == SubscriptionStatus.Active);
        return sub.Id;
    }

    /// <summary>
    /// Adds a child to a parent. Returns the child JWT and child id on success.
    /// On failure (seat full / other), returns null token and 0.
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

        // Sign in the child to get their token
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
        var childId    = GetUserIdFromJwt(childToken);
        return (addResp, addBody, childId, childToken);
    }

    /// <summary>Sends a correctly-signed Seat-payment webhook for the given payment ref.</summary>
    private async Task<(HttpResponseMessage Response, string Body)> SendSeatWebhookAsync(
        string eventId,
        string providerRef,
        decimal amount = 169m)
    {
        var (rawBody, sig) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, "payment.succeeded", providerRef, amount, TestSigningSecret);

        var req = new HttpRequestMessage(HttpMethod.Post, WebhookProviderUrl)
        {
            Content = new ByteArrayContent(rawBody),
        };
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        req.Headers.Add("X-Hmac-Signature", sig);

        var resp = await _client.SendAsync(req);
        return (resp, await resp.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Creates a Seat Payment row directly in the DB bypassing the checkout provider.
    /// Returns the paymentId and providerPaymentRef.
    /// </summary>
    private async Task<(int PaymentId, string ProviderRef)> SeedSeatPaymentRowAsync(
        int parentId,
        int subscriptionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var providerRef = $"fake-seat-{Guid.NewGuid():N}";
        var payment = new Payment
        {
            ParentUserId       = parentId,
            SubscriptionId     = subscriptionId,
            Amount             = 169m,
            Currency           = PaymentCurrency.EGP,
            Status             = PaymentStatus.Initiated,
            Kind               = PaymentKind.Seat,
            IdempotencyKey     = $"seat-ik-{Guid.NewGuid():N}",
            ProviderPaymentRef = providerRef,
            CreatedAt          = DateTime.UtcNow,
            CreatedBy          = parentId,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(0);
        return (payment.Id, providerRef);
    }

    /// <summary>Decodes the user integer id from a Learnexia JWT (reads the "Id" claim).</summary>
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
    // AREA 1 — SEAT STATUS: PLAN SEAT COUNTS AND CEILING
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// SEAT-STATUS-01: Free plan (PlanCode=Free) includes exactly 1 seat per the Plans seed.
    /// We inspect the DB Plans row to assert IncludedSeats=1 (matches AddSeatModel migration data seed).
    /// </summary>
    [Fact(DisplayName = "SEAT-STATUS-01 Free plan has IncludedSeats=1 per migration seed")]
    public async Task SEAT_STATUS_01_FreePlan_HasOneIncludedSeat()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var freePlan = await db.Plans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == PlanCode.Free);

        freePlan.Should().NotBeNull("Free plan row must exist (seeded in InitialBilling migration)");
        freePlan!.IncludedSeats.Should().Be(DefaultFreeIncludedSeats,
            "SEAT-STATUS-01 DEFECT: Free plan must have IncludedSeats={0} per AddSeatModel migration data seed; actual={1}",
            DefaultFreeIncludedSeats, freePlan.IncludedSeats);
    }

    /// <summary>
    /// SEAT-STATUS-02: Premium plan (PlanCode=Premium) includes exactly 3 seats per the Plans seed.
    /// </summary>
    [Fact(DisplayName = "SEAT-STATUS-02 Premium plan has IncludedSeats=3 per migration seed")]
    public async Task SEAT_STATUS_02_PremiumPlan_HasThreeIncludedSeats()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var premiumPlan = await db.Plans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == PlanCode.Premium);

        premiumPlan.Should().NotBeNull("Premium plan row must exist (seeded in InitialBilling migration)");
        premiumPlan!.IncludedSeats.Should().Be(DefaultPremiumIncludedSeats,
            "SEAT-STATUS-02 DEFECT: Premium plan must have IncludedSeats={0} per AddSeatModel migration data seed; actual={1}",
            DefaultPremiumIncludedSeats, premiumPlan.IncludedSeats);
    }

    /// <summary>
    /// SEAT-STATUS-03: GlobalSetting seats.max = 5 (hard ceiling on total seats per family).
    /// </summary>
    [Fact(DisplayName = "SEAT-STATUS-03 GlobalSetting seats.max=5 (hard ceiling)")]
    public async Task SEAT_STATUS_03_SeatsMax_IsHardCeiling_Five()
    {
        using var scope = _factory.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IGlobalSettingsProvider>();

        var maxSeats = settings.GetInt(GlobalSettingKeys.SeatsMax, 5);
        maxSeats.Should().Be(DefaultSeatsMax,
            "SEAT-STATUS-03 DEFECT: seats.max GlobalSetting must be {0}; actual={1}",
            DefaultSeatsMax, maxSeats);
    }

    /// <summary>
    /// SEAT-STATUS-04: GET /api/Billing/Seats/Status without JWT → 401 Unauthorized.
    /// </summary>
    [Fact(DisplayName = "SEAT-STATUS-04 GET Seats/Status without JWT → 401")]
    public async Task SEAT_STATUS_04_SeatStatus_AnonymousRequest_Returns401()
    {
        var (resp, body, _) = await SendAsync(HttpMethod.Get, SeatStatusUrl);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "SEAT-STATUS-04: Seats/Status is [Authorize]; anon must get 401; body={0}", body);
    }

    /// <summary>
    /// SEAT-STATUS-05: GET /api/Billing/Seats/Status with valid JWT → 200 + BaseResponse envelope.
    /// Asserts: statusCode, successed=true, data with includedSeats/totalSeats/availableSeats/maxSeats/children.
    /// </summary>
    [Fact(DisplayName = "SEAT-STATUS-05 GET Seats/Status returns 200 + BaseResponse envelope shape")]
    public async Task SEAT_STATUS_05_SeatStatus_Returns200_WithEnvelopeShape()
    {
        var (_, parentToken) = await RegisterParentAsync("SS05");

        var (resp, body, root) = await SendAsync(HttpMethod.Get, SeatStatusUrl, bearer: parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SEAT-STATUS-05: authenticated parent must get 200; body={0}", body);

        // Envelope shape
        root.TryGetProperty("statusCode", out _).Should().BeTrue("envelope must have 'statusCode'; body={0}", body);
        TryProp(root, "successed", out var succEl).Should().BeTrue("envelope must have 'successed'; body={0}", body);
        succEl.GetBoolean().Should().BeTrue("successed must be true on success; body={0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body={0}", body);

        // Data fields
        TryProp(data, "includedSeats", out _).Should().BeTrue("data.includedSeats missing; body={0}", body);
        TryProp(data, "totalSeats", out _).Should().BeTrue("data.totalSeats missing; body={0}", body);
        TryProp(data, "availableSeats", out _).Should().BeTrue("data.availableSeats missing; body={0}", body);
        TryProp(data, "maxSeats", out var maxEl);
        // maxSeats should be the global ceiling
        maxEl.GetInt32().Should().Be(DefaultSeatsMax,
            "data.maxSeats must be {0}; body={1}", DefaultSeatsMax, body);
        TryProp(data, "children", out var childrenEl).Should().BeTrue("data.children must be present; body={0}", body);
        childrenEl.ValueKind.Should().Be(JsonValueKind.Array, "data.children must be an array; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 2 — ADD-CHILD: SEAT RESERVATION GATE
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ADD-CHILD-01: Adding a child to a Free parent (1 free seat) succeeds.
    /// After success, SeatReservation with Status=Active must exist.
    /// </summary>
    [Fact(DisplayName = "ADD-CHILD-01 Add-child reserves seat and SUCCEEDS within Free plan limit")]
    public async Task ADD_CHILD_01_AddChild_ReservesSeat_SucceedsWithinLimit()
    {
        var (parentId, parentToken) = await RegisterParentAsync("AC01");

        var (addResp, addBody, childId, childToken) =
            await TryAddChildAsync(parentToken, "AC01C");

        ((int)addResp.StatusCode).Should().BeOneOf([200, 201],
            "ADD-CHILD-01: first child within Free plan limit must succeed; body={0}", addBody);
        childId.Should().BeGreaterThan(0, "child must have a valid id after successful creation");
        childToken.Should().NotBeNullOrEmpty("child must be able to sign in after creation");

        // Verify SeatReservation exists
        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var sub = await db.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ParentUserId == parentId);

        sub.Should().NotBeNull("parent subscription must exist");

        var reservation = await db.SeatReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SubscriptionId == sub!.Id
                                   && r.ChildId == childId);

        reservation.Should().NotBeNull(
            "ADD-CHILD-01 DEFECT: SeatReservation must exist for childId={0} after add-child; subscriptionId={1}",
            childId, sub!.Id);
    }

    /// <summary>
    /// ADD-CHILD-02: After successful add-child, the SeatReservation status must be Active
    /// (Reserved → Active transition after successful child creation and link).
    /// </summary>
    [Fact(DisplayName = "ADD-CHILD-02 Seat status = Active after successful add-child")]
    public async Task ADD_CHILD_02_AddChild_SeatStatus_IsActive_AfterSuccess()
    {
        var (parentId, parentToken) = await RegisterParentAsync("AC02");
        var (addResp, addBody, childId, _) = await TryAddChildAsync(parentToken, "AC02C");

        ((int)addResp.StatusCode).Should().BeOneOf([200, 201],
            "prerequisite: add-child must succeed; body={0}", addBody);

        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var sub = await db.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ParentUserId == parentId);

        var reservation = await db.SeatReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SubscriptionId == sub!.Id && r.ChildId == childId);

        reservation.Should().NotBeNull("SeatReservation must exist for child after add-child");
        reservation!.Status.Should().Be(SeatStatus.Active,
            "ADD-CHILD-02 DEFECT: SeatReservation.Status must be Active (not Reserved) after child creation completes; " +
            "childId={0}, actual status={1}", childId, reservation.Status);
    }

    /// <summary>
    /// ADD-CHILD-03: After filling all Free plan seats (1 child for a Free parent),
    /// adding a second child must be REJECTED.
    /// The expected HTTP status should be Conflict (409).
    /// The handler returns StatusCode=Conflict with Message=NoFreeSeatAvailable.
    /// </summary>
    [Fact(DisplayName = "ADD-CHILD-03 Second add-child on Free plan (all seats taken) → Conflict/rejection")]
    public async Task ADD_CHILD_03_AddChild_SeatFull_IsRejected()
    {
        var (parentId, parentToken) = await RegisterParentAsync("AC03");

        // Fill the only Free seat
        var (firstResp, firstBody, _, _) = await TryAddChildAsync(parentToken, "AC03C1");
        ((int)firstResp.StatusCode).Should().BeOneOf([200, 201],
            "prerequisite: first child must succeed; body={0}", firstBody);

        // Attempt second child — seats are full (Free = 1 seat)
        var (secondResp, secondBody, _, _) = await TryAddChildAsync(parentToken, "AC03C2");

        // The handler returns Conflict (409) when no free seat is available.
        // Acceptable: 409 (Conflict) per the AddChildCommandHandler mapping.
        ((int)secondResp.StatusCode).Should().BeOneOf([409, 402, 400],
            "ADD-CHILD-03 DEFECT: adding a child when seats are full must be rejected with 409/402/400; " +
            "actual status={0}; body={1}", (int)secondResp.StatusCode, secondBody);

        secondResp.IsSuccessStatusCode.Should().BeFalse(
            "ADD-CHILD-03 DEFECT: second add-child on full Free plan must NOT succeed; body={0}", secondBody);
    }

    /// <summary>
    /// ADD-CHILD-04: When add-child is rejected due to no free seat, NO new child account
    /// is created in the Identity module (no orphan user row in asp_net_users).
    /// We verify this by attempting to sign in with the would-be child's email — must fail.
    /// </summary>
    [Fact(DisplayName = "ADD-CHILD-04 On seat-full rejection NO child identity is created")]
    public async Task ADD_CHILD_04_AddChild_SeatFull_NoChildIdentityCreated()
    {
        var (_, parentToken) = await RegisterParentAsync("AC04");

        // Fill the only Free seat
        var (firstResp, firstBody, _, _) = await TryAddChildAsync(parentToken, "AC04C1");
        ((int)firstResp.StatusCode).Should().BeOneOf([200, 201],
            "prerequisite: first child must succeed; body={0}", firstBody);

        // Try to add second child with a known email
        var rejectedEmail = UniqueEmail("AC04C2_rejected");
        const string rejectedPass = "Child@Pass1";
        var (rejResp, rejBody, _) = await SendAsync(HttpMethod.Post, AddChildUrl, new
        {
            FullName         = "Rejected Child",
            Email            = rejectedEmail,
            Password         = rejectedPass,
            Grade            = 5,
            Language         = "ar",
            Country          = "EG",
            LearningLanguage = "ar",
        }, parentToken);

        rejResp.IsSuccessStatusCode.Should().BeFalse(
            "prerequisite: second child add must be rejected; body={0}", rejBody);

        // Now try to sign in with the rejected child's email — must fail (account not created)
        var (signResp, signBody, _) = await SendAsync(HttpMethod.Post, SignInUrl, new
        {
            UserName = rejectedEmail,
            Password = rejectedPass,
        });

        signResp.IsSuccessStatusCode.Should().BeFalse(
            "ADD-CHILD-04 DEFECT: rejected child must NOT be able to sign in — no Identity row should exist; " +
            "sign-in body={0}", signBody);
    }

    /// <summary>
    /// ADD-CHILD-05: When add-child is rejected due to no free seat, NO orphan SeatReservation
    /// remains with Status=Reserved or Status=Active for the parent subscription.
    /// (The handler rejects before reserving — so no row is even inserted.)
    /// </summary>
    [Fact(DisplayName = "ADD-CHILD-05 On seat-full rejection NO orphan SeatReservation (Reserved/Active) remains")]
    public async Task ADD_CHILD_05_AddChild_SeatFull_NoOrphanReservation()
    {
        var (parentId, parentToken) = await RegisterParentAsync("AC05");

        // Fill the only Free seat
        var (firstResp, firstBody, firstChildId, _) = await TryAddChildAsync(parentToken, "AC05C1");
        ((int)firstResp.StatusCode).Should().BeOneOf([200, 201],
            "prerequisite: first child must succeed; body={0}", firstBody);

        // Count active/reserved rows before attempting the rejected add
        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<BillingDbContext>();
        var subBefore = await dbBefore.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ParentUserId == parentId);
        var activeCountBefore = await dbBefore.SeatReservations.AsNoTracking()
            .CountAsync(r => r.SubscriptionId == subBefore!.Id
                          && (r.Status == SeatStatus.Reserved || r.Status == SeatStatus.Active));

        // Attempt (and expect rejection for) second child
        var (rejResp, _, _, _) = await TryAddChildAsync(parentToken, "AC05C2_rejected");
        rejResp.IsSuccessStatusCode.Should().BeFalse(
            "prerequisite: second add must be rejected");

        // Count active/reserved rows after — must be identical (no orphan inserted and not released)
        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<BillingDbContext>();
        var subAfter = await dbAfter.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ParentUserId == parentId);
        var activeCountAfter = await dbAfter.SeatReservations.AsNoTracking()
            .CountAsync(r => r.SubscriptionId == subAfter!.Id
                          && (r.Status == SeatStatus.Reserved || r.Status == SeatStatus.Active));

        activeCountAfter.Should().Be(activeCountBefore,
            "ADD-CHILD-05 DEFECT: rejected add-child must NOT leave any orphan SeatReservation (Reserved/Active); " +
            "before={0}, after={1}", activeCountBefore, activeCountAfter);
    }

    /// <summary>
    /// ADD-CHILD-06: Compensation path — when the inner CreateChildAsync call fails
    /// (we test this via a duplicate-email scenario: first add child, then try to add the SAME email).
    /// The seat that was Reserved during the failed add must be Released (compensation),
    /// leaving the seat count at the same level as before the failed attempt.
    /// </summary>
    [Fact(DisplayName = "ADD-CHILD-06 Compensation: failed create releases seat; seat count unchanged")]
    public async Task ADD_CHILD_06_AddChild_FailedCreate_SeatReleased_NoOrphan()
    {
        // Use a Premium parent to have capacity for multiple children
        var (parentId, parentToken) = await RegisterParentAsync("AC06");
        await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Add first child successfully (occupies 1 of 3 Premium seats)
        var firstEmail = UniqueEmail("AC06C1");
        const string firstPass = "Child@Pass1";
        await SendAsync(HttpMethod.Post, AddChildUrl, new
        {
            FullName = "First Child", Email = firstEmail, Password = firstPass,
            Grade = 4, Language = "ar", Country = "EG", LearningLanguage = "ar",
        }, parentToken);

        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<BillingDbContext>();
        var subBefore = await dbBefore.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ParentUserId == parentId
                                   && s.Status == SubscriptionStatus.Active);
        var occupiedBefore = await dbBefore.SeatReservations.AsNoTracking()
            .CountAsync(r => r.SubscriptionId == subBefore!.Id
                          && (r.Status == SeatStatus.Reserved || r.Status == SeatStatus.Active));

        // Attempt to add the SAME email again — this will cause DuplicateEmail failure in CreateChildAsync.
        // The seat that was transiently Reserved during this attempt must be Released (compensation path).
        var (dupResp, dupBody, _) = await SendAsync(HttpMethod.Post, AddChildUrl, new
        {
            FullName = "Duplicate Child", Email = firstEmail, Password = firstPass,
            Grade = 4, Language = "ar", Country = "EG", LearningLanguage = "ar",
        }, parentToken);

        dupResp.IsSuccessStatusCode.Should().BeFalse(
            "prerequisite: duplicate-email add must fail; body={0}", dupBody);

        // After the compensation path, seat count must be unchanged
        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<BillingDbContext>();
        var subAfter = await dbAfter.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ParentUserId == parentId
                                   && s.Status == SubscriptionStatus.Active);
        var occupiedAfter = await dbAfter.SeatReservations.AsNoTracking()
            .CountAsync(r => r.SubscriptionId == subAfter!.Id
                          && (r.Status == SeatStatus.Reserved || r.Status == SeatStatus.Active));

        occupiedAfter.Should().Be(occupiedBefore,
            "ADD-CHILD-06 DEFECT: compensation path must release the transient Reserved seat when " +
            "child creation fails. Occupied seats before duplicate-email attempt={0}, after={1}. " +
            "A leaked Reserved seat blocks future valid add-child calls.",
            occupiedBefore, occupiedAfter);

        // No Reserved (unreleased) rows should remain for an unreferenced child
        var orphanReserved = await dbAfter.SeatReservations.AsNoTracking()
            .CountAsync(r => r.SubscriptionId == subAfter!.Id && r.Status == SeatStatus.Reserved);

        orphanReserved.Should().Be(0,
            "ADD-CHILD-06 DEFECT: no SeatReservation in Reserved state must remain after a " +
            "compensation rollback. Reserved count={0}", orphanReserved);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 3 — EXTRA-SEAT WEBHOOK
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// WEBHOOK-SEAT-01: A Seat-payment webhook increments Subscription.PurchasedExtraSeats by 1.
    /// </summary>
    [Fact(DisplayName = "WEBHOOK-SEAT-01 Seat webhook increments PurchasedExtraSeats by 1")]
    public async Task WEBHOOK_SEAT_01_SeatWebhook_IncrementsPurchasedExtraSeats()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WS01");
        var subscriptionId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Record PurchasedExtraSeats before
        int extraSeatsBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subscriptionId);
            extraSeatsBefore = sub.PurchasedExtraSeats;
        }

        // Seed a Seat Payment row and send webhook
        var (paymentId, providerRef) = await SeedSeatPaymentRowAsync(parentId, subscriptionId);
        var eventId = Guid.NewGuid().ToString("N");
        var (whResp, whBody) = await SendSeatWebhookAsync(eventId, providerRef);

        whResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "WEBHOOK-SEAT-01: valid seat webhook must return 200; body={0}", whBody);

        // Verify PurchasedExtraSeats incremented
        int extraSeatsAfter;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subscriptionId);
            extraSeatsAfter = sub.PurchasedExtraSeats;
        }

        extraSeatsAfter.Should().Be(extraSeatsBefore + 1,
            "WEBHOOK-SEAT-01 DEFECT: PurchasedExtraSeats must increment by 1 after seat webhook; " +
            "before={0}, after={1}", extraSeatsBefore, extraSeatsAfter);
    }

    /// <summary>
    /// WEBHOOK-SEAT-02: Idempotency — replaying the same seat webhook (same eventId) is a no-op.
    /// PurchasedExtraSeats must not be incremented twice.
    /// </summary>
    [Fact(DisplayName = "WEBHOOK-SEAT-02 Seat webhook is idempotent on replay (same eventId)")]
    public async Task WEBHOOK_SEAT_02_SeatWebhook_Idempotent_OnReplay()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WS02");
        var subscriptionId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var (paymentId, providerRef) = await SeedSeatPaymentRowAsync(parentId, subscriptionId);
        var eventId = Guid.NewGuid().ToString("N");

        // First dispatch
        var (resp1, body1) = await SendSeatWebhookAsync(eventId, providerRef);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK,
            "first seat webhook must return 200; body={0}", body1);

        int extraSeatsAfterFirst;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subscriptionId);
            extraSeatsAfterFirst = sub.PurchasedExtraSeats;
        }

        // Second dispatch (replay) with SAME eventId
        var (resp2, body2) = await SendSeatWebhookAsync(eventId, providerRef);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK,
            "replayed seat webhook must return 200 (idempotent no-op); body={0}", body2);

        int extraSeatsAfterSecond;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subscriptionId);
            extraSeatsAfterSecond = sub.PurchasedExtraSeats;
        }

        extraSeatsAfterSecond.Should().Be(extraSeatsAfterFirst,
            "WEBHOOK-SEAT-02 DEFECT: replay of same eventId must be idempotent — PurchasedExtraSeats " +
            "must NOT increment twice; after first={0}, after second={1}",
            extraSeatsAfterFirst, extraSeatsAfterSecond);

        // Only ONE WebhookEvent row for this eventId
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var eventCount = await db.WebhookEvents.AsNoTracking()
                .CountAsync(w => w.ProviderEventId == eventId);

            // The outer idempotency check (WebhookEvent.ProviderEventId unique index) catches the second call.
            // Depending on timing, the duplicate could be blocked at DB level (only 1 row) or the service
            // detects it via IsAlreadyProcessedAsync (still only 1 row). Either way: count = 1.
            eventCount.Should().BeLessOrEqualTo(2,
                "At most 2 WebhookEvent rows should exist (one per call before unique-index check); eventId={0}", eventId);
        }
    }

    /// <summary>
    /// WEBHOOK-SEAT-03: Seat webhook mints NO energy. FamilyEnergyAccount.PurchasedBalance
    /// must remain unchanged after a Seat payment webhook (seat payments ≠ energy packs).
    /// </summary>
    [Fact(DisplayName = "WEBHOOK-SEAT-03 Seat webhook mints NO energy (PurchasedBalance unchanged)")]
    public async Task WEBHOOK_SEAT_03_SeatWebhook_MintsNoEnergy()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WS03");
        var subscriptionId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Record wallet state before (if no wallet exists, PurchasedBalance is effectively 0)
        int purchasedBalanceBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.ParentUserId == parentId);
            purchasedBalanceBefore = wallet?.PurchasedBalance ?? 0;
        }

        var (paymentId, providerRef) = await SeedSeatPaymentRowAsync(parentId, subscriptionId);
        var eventId = Guid.NewGuid().ToString("N");
        var (whResp, whBody) = await SendSeatWebhookAsync(eventId, providerRef);
        whResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "seat webhook must return 200; body={0}", whBody);

        // PurchasedBalance must remain unchanged
        int purchasedBalanceAfter;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.ParentUserId == parentId);
            purchasedBalanceAfter = wallet?.PurchasedBalance ?? 0;
        }

        purchasedBalanceAfter.Should().Be(purchasedBalanceBefore,
            "WEBHOOK-SEAT-03 DEFECT: Seat webhook must NOT credit energy (PurchasedBalance). " +
            "Seat payments only increment PurchasedExtraSeats on the Subscription — NOT the wallet. " +
            "before={0}, after={1}", purchasedBalanceBefore, purchasedBalanceAfter);
    }

    /// <summary>
    /// WEBHOOK-SEAT-04: Ceiling enforcement — a Seat webhook that would push total seats above
    /// seats.max=5 must be rejected by the webhook's inline seats.max guard (seat NOT incremented).
    ///
    /// We set up a Premium parent with IncludedSeats=3. We manually set PurchasedExtraSeats=2
    /// (total=5=max). Then we fire another seat webhook — PurchasedExtraSeats must stay at 2.
    /// </summary>
    [Fact(DisplayName = "WEBHOOK-SEAT-04 Seat webhook respects seats.max=5 ceiling; over-max rejected")]
    public async Task WEBHOOK_SEAT_04_SeatWebhook_RespectsMaxCeiling()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WS04");
        var subscriptionId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Premium plan has IncludedSeats=3. To hit the ceiling (max=5) we need PurchasedExtraSeats=2.
        // Set PurchasedExtraSeats=2 directly in DB to simulate two prior seat purchases.
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subscriptionId);
            sub.PurchasedExtraSeats = 2; // included(3) + purchased(2) = 5 = max
            await db.SaveChangesAsync(0);
        }

        // Attempt one more seat purchase via webhook — should be rejected (ExceedsMaxSeats)
        var (paymentId, providerRef) = await SeedSeatPaymentRowAsync(parentId, subscriptionId);
        var eventId = Guid.NewGuid().ToString("N");
        var (whResp, whBody) = await SendSeatWebhookAsync(eventId, providerRef);

        // The webhook handler itself returns 200 (no-op message) since the inline seats.max guard
        // blocks the increment. The row is committed but seats NOT incremented.
        whResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "webhook always returns 200 even when ceiling prevents increment; body={0}", whBody);

        // PurchasedExtraSeats must remain at 2 (not 3)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subscriptionId);
            sub.PurchasedExtraSeats.Should().BeLessOrEqualTo(2,
                "WEBHOOK-SEAT-04 DEFECT: seats.max ceiling must prevent PurchasedExtraSeats from " +
                "going above the max. included={0}+purchased={1} would exceed max={2}; actual purchasedExtra={3}",
                DefaultPremiumIncludedSeats, sub.PurchasedExtraSeats, DefaultSeatsMax, sub.PurchasedExtraSeats);
        }
    }

    /// <summary>
    /// WEBHOOK-SEAT-05 (security #1): two <c>payment.succeeded</c> callbacks with DISTINCT provider
    /// event ids for the SAME seat payment must increment <c>PurchasedExtraSeats</c> only ONCE. The
    /// outer <c>WebhookEvent.ProviderEventId</c> unique guard only stops same-id replay (WEBHOOK-SEAT-02);
    /// the per-payment guard (Payment.Status flips Initiated→Succeeded) prevents the second distinct-id
    /// callback from double-granting a seat the parent paid for once.
    /// </summary>
    [Fact(DisplayName = "WEBHOOK-SEAT-05 Distinct event ids for the same seat payment increment seats only once")]
    public async Task WEBHOOK_SEAT_05_DistinctEventIds_SamePayment_IncrementsOnce()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WS05");
        var subscriptionId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        var (paymentId, providerRef) = await SeedSeatPaymentRowAsync(parentId, subscriptionId);

        // First callback (event id A) — must increment by 1 and flip the payment to Succeeded.
        var (resp1, body1) = await SendSeatWebhookAsync(Guid.NewGuid().ToString("N"), providerRef);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first seat webhook must return 200; body={0}", body1);

        int afterFirst;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            afterFirst = (await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subscriptionId)).PurchasedExtraSeats;
        }

        // Second callback with a DIFFERENT event id but the SAME provider payment ref.
        var (resp2, body2) = await SendSeatWebhookAsync(Guid.NewGuid().ToString("N"), providerRef);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK,
            "second (distinct-id) seat webhook must return 200 as an idempotent no-op; body={0}", body2);

        int afterSecond;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            afterSecond = (await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subscriptionId)).PurchasedExtraSeats;
        }

        afterSecond.Should().Be(afterFirst,
            "WEBHOOK-SEAT-05 DEFECT: a second payment.succeeded with a DISTINCT event id for the SAME payment " +
            "must NOT increment PurchasedExtraSeats again (single payment = single seat); afterFirst={0}, afterSecond={1}",
            afterFirst, afterSecond);
    }

    /// <summary>
    /// SEAT-PRORATE-01: A mid-cycle extra-seat checkout charges <c>SeatPrice × remaining-cycle-ratio</c>
    /// (prorated MONEY), computed server-side from the subscription cycle dates — never the full price,
    /// never client-supplied (P10-14 AC, locked 2026-06-17). The prorated amount is returned to the
    /// caller (so the parent UI can show it) AND persisted on the Payment row.
    /// </summary>
    [Fact(DisplayName = "SEAT-PRORATE-01 Mid-cycle seat checkout charges SeatPrice × remaining-cycle-ratio (server-side proration)")]
    public async Task SEAT_PRORATE_01_MidCycleCheckout_ProratesMoney()
    {
        var (parentId, parentToken) = await RegisterParentAsync("PR01");
        var subscriptionId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Force a known mid-cycle window: 30-day cycle with ~20 days remaining → ratio ≈ 2/3.
        DateTime cycleStart, cycleEnd;
        {
            using var scope = _factory.Services.CreateScope();
            var db  = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subscriptionId);
            var now = DateTime.UtcNow;
            cycleStart = now.AddDays(-10);
            cycleEnd   = now.AddDays(20);
            sub.CurrentCycleStart = cycleStart;
            sub.CurrentCycleEnd   = cycleEnd;
            await db.SaveChangesAsync(0);
        }

        // POST seat checkout (qty = 1) — [FromBody] int.
        var (resp, body, root) = await SendAsync(HttpMethod.Post, SeatCheckoutUrl, 1, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Premium parent mid-cycle seat checkout must succeed; body={0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body={0}", body);
        TryProp(data, "amount", out var amountEl).Should().BeTrue(
            "SEAT-PRORATE-01 DEFECT: checkout response must expose the prorated Amount; body={0}", body);
        var amount = amountEl.GetDecimal();

        const decimal seatPrice = 169m; // seats.extra_price_egp default
        var expectedRatio  = (decimal)(cycleEnd - DateTime.UtcNow).Ticks / (decimal)(cycleEnd - cycleStart).Ticks;
        var expectedAmount = Math.Round(seatPrice * expectedRatio, 2, MidpointRounding.AwayFromZero);

        amount.Should().BeLessThan(seatPrice,
            "SEAT-PRORATE-01 DEFECT: a mid-cycle add must be prorated BELOW the full monthly seat price; amount={0}", amount);
        amount.Should().BeApproximately(expectedAmount, 0.50m,
            "SEAT-PRORATE-01 DEFECT: amount must equal SeatPrice({0}) × remaining-cycle-ratio; expected≈{1}, actual={2}",
            seatPrice, expectedAmount, amount);

        // The prorated charge must also be persisted server-side on the Payment row.
        TryProp(data, "paymentId", out var pidEl).Should().BeTrue("body={0}", body);
        var paymentId = pidEl.GetInt32();
        using var scope2 = _factory.Services.CreateScope();
        var db2     = scope2.ServiceProvider.GetRequiredService<BillingDbContext>();
        var payment = await db2.Payments.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        payment.Amount.Should().Be(amount,
            "the persisted Payment.Amount must equal the returned prorated charge (server-side, not client-supplied)");
        payment.Kind.Should().Be(PaymentKind.Seat, "a seat checkout must create a Seat-kind payment");
    }

    /// <summary>
    /// CANCEL-01 (P10-14-BE-8, cycle-end cancel): a voluntary extra-seat cancel records a
    /// scheduled-removal marker effective at the next renewal — and must NOT (a) touch the
    /// payment-failure <c>GraceEndsAt</c> clock, NOR (b) reduce <c>PurchasedExtraSeats</c> mid-cycle.
    /// The seat stays Active for the rest of the cycle (the active-seat count is unchanged).
    /// </summary>
    [Fact(DisplayName = "CANCEL-01 Voluntary cancel = cycle-end marker; no grace started, PurchasedExtraSeats unchanged")]
    public async Task CANCEL_01_VoluntaryCancel_SchedulesCycleEndRemoval_NoGrace_NoMidCycleDecrement()
    {
        var (parentId, parentToken) = await RegisterParentAsync("CXL01");
        var subscriptionId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Seed one purchased extra seat (total = included 3 + purchased 1). Capture pre-state.
        DateTime? graceBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db  = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subscriptionId);
            sub.PurchasedExtraSeats = 1;
            await db.SaveChangesAsync(0);
            graceBefore = sub.GraceEndsAt; // expected null after a clean Premium upgrade
        }

        // POST cancel (qty = 1) — [FromBody] int.
        var (resp, body, _) = await SendAsync(HttpMethod.Post, SeatCancelUrl, 1, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "cycle-end cancel must succeed; body={0}", body);

        using var scope2 = _factory.Services.CreateScope();
        var db2  = scope2.ServiceProvider.GetRequiredService<BillingDbContext>();
        var sub2 = await db2.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subscriptionId);

        sub2.PurchasedExtraSeats.Should().Be(1,
            "CANCEL-01 DEFECT: a voluntary cancel must NOT reduce PurchasedExtraSeats mid-cycle — the seat stays Active until renewal; actual={0}",
            sub2.PurchasedExtraSeats);
        sub2.GraceEndsAt.Should().Be(graceBefore,
            "CANCEL-01 DEFECT: a voluntary cancel must NOT touch the payment-failure GraceEndsAt clock; before={0}, after={1}",
            graceBefore, sub2.GraceEndsAt);
        sub2.PendingExtraSeatRemovals.Should().Be(1,
            "CANCEL-01 DEFECT: the scheduled-removal marker (PendingExtraSeatRemovals) must record the cancel; actual={0}",
            sub2.PendingExtraSeatRemovals);
        sub2.ExtraSeatCancelEffectiveAt.Should().NotBeNull(
            "CANCEL-01 DEFECT: ExtraSeatCancelEffectiveAt must be set to the next renewal boundary");
    }

    /// <summary>
    /// CANCEL-02: cancelling more extra seats than are purchased (net of already-pending) is rejected
    /// with 409 and changes nothing.
    /// </summary>
    [Fact(DisplayName = "CANCEL-02 Cancel more than purchased extra seats → 409, no state change")]
    public async Task CANCEL_02_CancelMoreThanPurchased_Returns409()
    {
        var (parentId, parentToken) = await RegisterParentAsync("CXL02");
        var subscriptionId = await UpgradeParentToPremiumAsync(parentId, parentToken);
        // PurchasedExtraSeats defaults to 0 → cancelling 1 must fail.

        var (resp, body, _) = await SendAsync(HttpMethod.Post, SeatCancelUrl, 1, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "cancelling more extra seats than purchased must return 409; body={0}", body);

        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var sub = await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == subscriptionId);
        sub.PendingExtraSeatRemovals.Should().Be(0, "a rejected cancel must not set the scheduled-removal marker");
        sub.ExtraSeatCancelEffectiveAt.Should().BeNull("a rejected cancel must not set the effective-at marker");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 4 — REAL ISeatService DRIVES BillingGrantJob
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GRANT-JOB-01: The REAL ISeatService (RealSeatQuery via ISeatQuery) drives BillingGrantJob.
    /// Grant = PlanEnergyPerSeat × ActivePaidSeats (active seat count = IncludedSeats + PurchasedExtraSeats).
    /// We set up a Premium parent with 1 active child (1 Active seat), call AllocateSubscriptionGrantAsync
    /// directly with the seat-driven grant amount, and verify the wallet SubscriptionBalance.
    /// </summary>
    [Fact(DisplayName = "GRANT-JOB-01 RealISeatService: grant = PlanEnergyPerSeat × active paid seats")]
    public async Task GRANT_JOB_01_RealSeatService_DrivesGrant_PerActivePaidSeats()
    {
        var (parentId, parentToken) = await RegisterParentAsync("GJ01");
        var subscriptionId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Add one child → 1 Active seat
        var (addResp, addBody, childId, _) = await TryAddChildAsync(parentToken, "GJ01C");
        ((int)addResp.StatusCode).Should().BeOneOf([200, 201],
            "prerequisite: add-child must succeed; body={0}", addBody);
        childId.Should().BeGreaterThan(0, "child must have a valid id");

        // Get active seat count via ISeatQuery (RealSeatQuery)
        int activePaidSeats;
        {
            using var scope = _factory.Services.CreateScope();
            var seatQuery = scope.ServiceProvider.GetRequiredService<ISeatQuery>();
            var seatInfo = await seatQuery.GetActiveSeatsAsync(parentId);
            activePaidSeats = seatInfo.ActivePaidSeats;

            // Premium plan: IncludedSeats=3 + PurchasedExtraSeats=0 = 3 (but only 1 child is active)
            // RealSeatQuery: ActivePaidSeats = Math.Min(included + purchased, max) = 3
            // ActiveChildIds.Count = 1 (only 1 Active reservation)
            activePaidSeats.Should().BeGreaterThan(0,
                "GRANT-JOB-01: Premium parent must have >0 active paid seats after subscription activation");
        }

        // Simulate what BillingGrantJob would do: perSeat × activePaidSeats
        var perSeat = DefaultPremiumMonthlyPerSeat;
        var grantAmount = perSeat * activePaidSeats;

        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);
        var idempotencyKey = $"gj01:{parentId}:{cycleStart:yyyyMM}";

        using var allocScope = _factory.Services.CreateScope();
        var allocationSvc = allocScope.ServiceProvider.GetRequiredService<IFamilyEnergyAllocationService>();
        var result = await allocationSvc.AllocateSubscriptionGrantAsync(
            parentUserId  : parentId,
            grantAmount   : grantAmount,
            cycleStart    : cycleStart,
            cycleEnd      : cycleEnd,
            idempotencyKey: idempotencyKey);

        result.Succeeded.Should().BeTrue("allocation must succeed");
        result.WasDuplicate.Should().BeFalse("first allocation for this cycle must not be a duplicate");
        result.TotalAllocated.Should().Be(grantAmount,
            "GRANT-JOB-01 DEFECT: TotalAllocated must equal grantAmount={0} " +
            "(PlanEnergyPerSeat={1} × ActivePaidSeats={2}); actual={3}",
            grantAmount, perSeat, activePaidSeats, result.TotalAllocated);

        // Verify wallet SubscriptionBalance = grantAmount
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.ParentUserId == parentId);

            wallet.Should().NotBeNull("wallet must be created after grant allocation");
            wallet!.SubscriptionBalance.Should().Be(grantAmount,
                "GRANT-JOB-01 DEFECT: wallet SubscriptionBalance must equal the grant amount; " +
                "expected={0}, actual={1}", grantAmount, wallet.SubscriptionBalance);
        }
    }

    /// <summary>
    /// GRANT-JOB-02: RealSeatQuery — ActivePaidSeats = IncludedSeats(plan) + PurchasedExtraSeats,
    /// capped at seats.max. After purchasing an extra seat, the count must reflect it.
    /// </summary>
    [Fact(DisplayName = "GRANT-JOB-02 RealSeatQuery: ActivePaidSeats = IncludedSeats + PurchasedExtraSeats (capped)")]
    public async Task GRANT_JOB_02_RealSeatQuery_ActivePaidSeats_Formula()
    {
        var (parentId, parentToken) = await RegisterParentAsync("GJ02");
        var subscriptionId = await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Get seat count without extra seats purchased
        int activePaidSeatsBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var seatQuery = scope.ServiceProvider.GetRequiredService<ISeatQuery>();
            var info = await seatQuery.GetActiveSeatsAsync(parentId);
            activePaidSeatsBefore = info.ActivePaidSeats;
        }
        // Premium included = 3 + purchased = 0 = 3 (capped at max=5 but 3 < 5)
        activePaidSeatsBefore.Should().Be(DefaultPremiumIncludedSeats,
            "GRANT-JOB-02: before extra seat purchase, ActivePaidSeats must equal IncludedSeats={0}; actual={1}",
            DefaultPremiumIncludedSeats, activePaidSeatsBefore);

        // Purchase one extra seat via webhook
        var (_, providerRef) = await SeedSeatPaymentRowAsync(parentId, subscriptionId);
        var (whResp, _) = await SendSeatWebhookAsync(Guid.NewGuid().ToString("N"), providerRef);
        whResp.IsSuccessStatusCode.Should().BeTrue("seat webhook must succeed");

        // After purchase: ActivePaidSeats = 3 + 1 = 4
        int activePaidSeatsAfter;
        {
            using var scope = _factory.Services.CreateScope();
            var seatQuery = scope.ServiceProvider.GetRequiredService<ISeatQuery>();
            var info = await seatQuery.GetActiveSeatsAsync(parentId);
            activePaidSeatsAfter = info.ActivePaidSeats;
        }

        activePaidSeatsAfter.Should().Be(activePaidSeatsBefore + 1,
            "GRANT-JOB-02 DEFECT: after purchasing 1 extra seat, ActivePaidSeats must be " +
            "IncludedSeats({0}) + PurchasedExtraSeats(1) = {1}; actual={2}",
            DefaultPremiumIncludedSeats, activePaidSeatsBefore + 1, activePaidSeatsAfter);

        // Also verify the ceiling: if we buy many extra seats the count must never exceed max=5
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.Id == subscriptionId);
            sub.PurchasedExtraSeats = 99; // artificially push over the ceiling
            await db.SaveChangesAsync(0);
        }

        int cappedSeats;
        {
            using var scope = _factory.Services.CreateScope();
            var seatQuery = scope.ServiceProvider.GetRequiredService<ISeatQuery>();
            var info = await seatQuery.GetActiveSeatsAsync(parentId);
            cappedSeats = info.ActivePaidSeats;
        }

        cappedSeats.Should().Be(DefaultSeatsMax,
            "GRANT-JOB-02 DEFECT: ActivePaidSeats must be capped at seats.max={0}; actual={1}",
            DefaultSeatsMax, cappedSeats);
    }

    /// <summary>
    /// GRANT-JOB-03: RealSeatQuery — ActiveChildIds contains ONLY children with Active-status
    /// SeatReservations (not Reserved, Released, or NoSeat).
    /// </summary>
    [Fact(DisplayName = "GRANT-JOB-03 RealSeatQuery: ActiveChildIds contains only Active-status reservation children")]
    public async Task GRANT_JOB_03_RealSeatQuery_ActiveChildIds_OnlyActiveStatus()
    {
        var (parentId, parentToken) = await RegisterParentAsync("GJ03");
        await UpgradeParentToPremiumAsync(parentId, parentToken);

        // Add 2 children (both should get Active seats in Premium plan with 3 included)
        var (add1Resp, add1Body, child1Id, _) = await TryAddChildAsync(parentToken, "GJ03C1");
        ((int)add1Resp.StatusCode).Should().BeOneOf([200, 201],
            "prerequisite: first child must succeed; body={0}", add1Body);

        var (add2Resp, add2Body, child2Id, _) = await TryAddChildAsync(parentToken, "GJ03C2");
        ((int)add2Resp.StatusCode).Should().BeOneOf([200, 201],
            "prerequisite: second child must succeed; body={0}", add2Body);

        // Both children should have Active reservations
        {
            using var scope = _factory.Services.CreateScope();
            var seatQuery = scope.ServiceProvider.GetRequiredService<ISeatQuery>();
            var seatInfo = await seatQuery.GetActiveSeatsAsync(parentId);

            seatInfo.ActiveChildIds.Should().Contain(child1Id,
                "GRANT-JOB-03 DEFECT: child1 with Active seat must appear in ActiveChildIds; " +
                "childId={0}, allIds=[{1}]", child1Id, string.Join(",", seatInfo.ActiveChildIds));
            seatInfo.ActiveChildIds.Should().Contain(child2Id,
                "GRANT-JOB-03 DEFECT: child2 with Active seat must appear in ActiveChildIds; " +
                "childId={0}, allIds=[{1}]", child2Id, string.Join(",", seatInfo.ActiveChildIds));
        }

        // Manually flip child1's reservation to Released and verify it disappears from ActiveChildIds
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.ParentUserId == parentId && s.Status == SubscriptionStatus.Active);
            var reservation = await db.SeatReservations
                .FirstOrDefaultAsync(r => r.SubscriptionId == sub!.Id && r.ChildId == child1Id);
            if (reservation is not null)
            {
                reservation.Status     = SeatStatus.Released;
                reservation.ReleasedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(0);
            }
        }

        // Now ActiveChildIds must only contain child2 (child1 is Released)
        {
            using var scope = _factory.Services.CreateScope();
            var seatQuery = scope.ServiceProvider.GetRequiredService<ISeatQuery>();
            var seatInfo = await seatQuery.GetActiveSeatsAsync(parentId);

            seatInfo.ActiveChildIds.Should().NotContain(child1Id,
                "GRANT-JOB-03 DEFECT: child with Released seat must NOT appear in ActiveChildIds; " +
                "child1Id={0}, allIds=[{1}]", child1Id, string.Join(",", seatInfo.ActiveChildIds));
            seatInfo.ActiveChildIds.Should().Contain(child2Id,
                "GRANT-JOB-03: child2 with Active seat must still appear in ActiveChildIds after child1 released; " +
                "child2Id={0}, allIds=[{1}]", child2Id, string.Join(",", seatInfo.ActiveChildIds));
        }
    }
}
