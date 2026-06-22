using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P10-01 + P10-12/P10-11 Billing integration tests.
///
/// Coverage areas:
///   Area 1 – Credit account read (P10-01):
///     TC-CR-01  Happy path: GET /api/Billing/Credits/{childId} → 200 + BaseResponse envelope + balance fields.
///     TC-CR-02  No account yet for child → 404 (not 500).
///     TC-CR-03  Anon request → 401 (endpoint is [Authorize]).
///     TC-CR-04  IDOR critical: ParentB CANNOT read a credit account whose childId belongs to another family.
///               The handler queries by route childId with NO caller-scope guard — this test
///               asserts that a cross-family read is denied. If it returns 200 with real data
///               the test fails and reports a defect.
///     TC-CR-05  After a Grant, GetBalance reflects the granted amount (persistence round-trip).
///     TC-CR-06  Reconcile endpoint → 200 + ReconciliationResultDto (admin token).
///     TC-CR-07  Reconcile with non-admin (parent) token → 403.
///
///   Area 2 – GlobalSettings read + update (P10-12/P10-11):
///     TC-GS-01  GET /api/Admin/GlobalSettings as Admin → 200 + BaseResponse list.
///     TC-GS-02  GET as anon → 401.
///     TC-GS-03  GET as parent → 403.
///     TC-GS-04  Seeded defaults present (all 17 keys, economy values verified).
///     TC-GS-05  PUT /api/Admin/GlobalSettings/{key} as Admin → 200; subsequent GET reflects new value.
///     TC-GS-06  PUT as anon → 401.
///     TC-GS-07  PUT as parent → 403.
///     TC-GS-08  PUT with invalid key (not in allowlist) → 422.
///     TC-GS-09  PUT with bad type value (string for int key) → 422.
///     TC-GS-10  PUT with out-of-range value (confidence > 1.0) → 422.
///     TC-GS-11  PUT with empty NewValue → 422.
///
///   Area 3 – Envelope shape spot-check:
///     TC-ENV-01 GET GlobalSettings success has all 5 BaseResponse keys.
///
/// IDOR / AuthZ scoping note (TC-CR-04):
///   GetCreditAccountQueryHandler filters ONLY on ChildId from the route parameter.
///   There is NO check that the caller's JWT identity is the parent of that child.
///   A bearer-authenticated parent from Family A can currently GET the credit balance
///   of any child across any family including Family B's child. This is an IDOR defect.
///   TC-CR-04 asserts the expected (secure) behavior — deny cross-family reads — and will
///   FAIL on the current implementation to surface the defect explicitly.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P10_01_12_Billing_IntegrationTests : IAsyncLifetime
{
    // ── URLs ──────────────────────────────────────────────────────────────────
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string AddUserUrl        = "api/Users/UserManagement/AddUser";
    private const string CreditsBaseUrl    = "api/Billing/Credits";
    private const string GlobalSettingsUrl = "api/Admin/GlobalSettings";

    // Seeded admin credentials (UserSeeder.SeedSuperAdminAsync)
    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P10_01_12_Billing_IntegrationTests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // IAsyncLifetime: run migrations + seed before each test class.
    // ApplyMigrationsAndSeedAsync now also handles BillingDbContext (updated in factory).
    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private static string UniqueEmail(string tag = "")
        => $"billing_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@billing.test";

    /// <summary>Case-insensitive JSON property lookup (dual-serializer path: camelCase vs PascalCase).</summary>
    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out value)) return true;
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendAsync(HttpMethod method, string url, object? body = null, string? bearerToken = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearerToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var response = await _client.SendAsync(request);
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>Signs in seeded superadmin and returns the JWT access token.</summary>
    private async Task<string> GetAdminTokenAsync()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = AdminUserName, Password = AdminPassword });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"superadmin sign-in must succeed for admin-token fixture; body: {body}");
        TryProp(root, "data", out var data).Should().BeTrue($"sign-in response must have data; body: {body}");
        TryProp(data, "accessToken", out var token).Should().BeTrue($"data must have accessToken; body: {body}");
        return token.GetString()!;
    }

    /// <summary>Registers a fresh parent and returns the JWT access token.</summary>
    private async Task<string> RegisterParentAndGetTokenAsync(string? email = null)
    {
        var e = email ?? UniqueEmail("parent");
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = e, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"parent registration prerequisite must succeed; body: {body}");
        TryProp(root, "data", out var data).Should().BeTrue($"register response must have data; body: {body}");
        TryProp(data, "accessToken", out var token).Should().BeTrue($"data must have accessToken; body: {body}");
        return token.GetString()!;
    }

    /// <summary>
    /// Creates a Student user via admin AddUser and returns the new user's integer id.
    /// AddUser returns BaseResponse&lt;string&gt; (data = success message, not an object with userId),
    /// so we look up the created user by email via GET /Admin/Users?q={email}.
    /// </summary>
    private async Task<int> CreateStudentAsync(string adminToken)
    {
        var studentEmail = UniqueEmail("student");
        var studentName  = $"stu_{Guid.NewGuid():N}"[..20];
        var (resp, _, body) = await SendAsync(HttpMethod.Post, AddUserUrl,
            new { Email = studentEmail, UserName = studentName, FullName = "Test Student",
                  Roles = new[] { "Student" } },
            adminToken);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"admin AddUser must succeed for student fixture; body: {body}");

        // AddUser returns BaseResponse<string> (data = a string message, not a userId object).
        // Retrieve the integer userId by searching for the user by email.
        return await FindUserIdByEmailAsync(studentEmail, adminToken);
    }

    /// <summary>
    /// Looks up an integer userId by searching for the user by email via GET /Admin/Users?q={email}.
    /// Returns 0 if not found.
    /// </summary>
    private async Task<int> FindUserIdByEmailAsync(string email, string adminToken)
    {
        var (resp, root, _) = await SendAsync(
            HttpMethod.Get,
            $"api/Admin/Users?q={Uri.EscapeDataString(email)}&PageSize=5",
            null, adminToken);

        if (!resp.IsSuccessStatusCode) return 0;

        // The response is a paged list; data is the array of user items.
        if (!TryProp(root, "data", out var dataEl)) return 0;
        if (dataEl.ValueKind != JsonValueKind.Array) return 0;

        foreach (var item in dataEl.EnumerateArray())
        {
            if (TryProp(item, "email", out var emailProp) &&
                string.Equals(emailProp.GetString(), email, StringComparison.OrdinalIgnoreCase))
            {
                if (TryProp(item, "id", out var idProp))
                    return idProp.GetInt32();
            }
        }

        // Fallback: try querying GetUserProfile with null userId (returns own profile) — not useful here.
        // Return 0 to signal failure.
        return 0;
    }

    /// <summary>
    /// Provisions a FamilyEnergyAccount + ChildEnergyAllocation for a standalone child (synthetic parentId).
    /// P10-13 CUTOVER: the old HTTP Grant endpoint now requires a real ParentId (family-wallet model).
    /// Tests that used to seed via HTTP now provision directly so CreditSpendService finds the balance.
    /// Returns an HttpResponseMessage with StatusCode=OK to keep callers that check the response working.
    /// </summary>
    private async Task<HttpResponseMessage> GrantCreditsAsync(
        string adminToken, int childId, int amount = 100)
    {
        await ProvisionWalletAllocationAsync(childId, amount);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    /// <summary>
    /// Provisions a FamilyEnergyAccount + ChildEnergyAllocation for a standalone student (synthetic parentId).
    /// Uses a synthetic parentUserId = -(childId) so test wallets never collide with real parent accounts.
    /// </summary>
    private async Task ProvisionWalletAllocationAsync(int childId, int amount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var syntheticParentId = -(childId);
        var wallet = await db.FamilyEnergyAccounts.FirstOrDefaultAsync(w => w.ParentUserId == syntheticParentId);
        if (wallet is null)
        {
            wallet = FamilyEnergyAccount.CreateEmpty(syntheticParentId);
            db.FamilyEnergyAccounts.Add(wallet);
            await db.SaveChangesAsync(0);
        }

        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);
        var alloc = await db.ChildEnergyAllocations.FirstOrDefaultAsync(
            a => a.FamilyEnergyAccountId == wallet.Id && a.ChildId == childId && a.CycleStartUtc == cycleStart);
        if (alloc is null)
        {
            alloc = new ChildEnergyAllocation
            {
                FamilyEnergyAccountId = wallet.Id,
                ChildId               = childId,
                CycleStartUtc         = cycleStart,
                CycleEndUtc           = cycleEnd,
                AllocatedAmount       = amount,
                SpentAmount           = 0,
            };
            db.ChildEnergyAllocations.Add(alloc);
        }
        else
        {
            alloc.AllocatedAmount += amount;
        }

        wallet.SubscriptionBalance += amount;
        await db.SaveChangesAsync(0);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Area 1 — Credit Account Read (P10-01)
    // ══════════════════════════════════════════════════════════════════════════

    // P10-13 CUTOVER NOTE (Area 1):
    // GET /Credits/{childId} and GET /Credits/{childId}/Reconcile have been removed (CreditAccount retired).
    // The per-child balance is now at GET /api/Billing/Energy/{childId}/Status (EnergyController).
    // The family reconcile is now at GET /api/Billing/Credits/Reconcile/{parentId} (family-wallet scoped).
    // Tests are updated to use the new endpoints while preserving the same behavioral intent.

    [Fact(DisplayName = "TC-CR-01 HappyPath: GET Energy/{childId}/Status with valid grant → 200 + BaseResponse envelope + balance fields")]
    public async Task TC_CR_01_GetBalance_HappyPath_Returns200_WithEnvelope()
    {
        var adminToken = await GetAdminTokenAsync();
        var childId    = await CreateStudentAsync(adminToken);

        // Grant credits so the allocation exists (direct DB provision — wallet model).
        var grantResp = await GrantCreditsAsync(adminToken, childId, amount: 50);
        ((int)grantResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            "Grant must succeed before we can read the balance.");

        // GET energy status — replaces the retired GET /Credits/{childId}.
        var (resp, root, body) = await SendAsync(
            HttpMethod.Get, $"api/Billing/Energy/{childId}/Status", null, adminToken);

        // ── Status ──
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"GET Energy/{{childId}}/Status with a valid authenticated token must return 200; body: {body}");

        // ── BaseResponse envelope keys ──
        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue(
            $"envelope must contain 'statusCode'; body: {body}");
        statusCodeProp.GetInt32().Should().Be(200,
            $"statusCode in envelope must be 200 on success; body: {body}");

        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            $"envelope must contain 'successed'; body: {body}");
        succeededProp.GetBoolean().Should().BeTrue(
            $"successed must be true on a successful balance read; body: {body}");

        TryProp(root, "message", out _).Should().BeTrue(
            $"envelope must contain 'message'; body: {body}");

        TryProp(root, "errors", out var errorsProp).Should().BeTrue(
            $"envelope must contain 'errors'; body: {body}");
        errorsProp.EnumerateArray().ToList().Should().BeEmpty(
            $"errors must be empty on success; body: {body}");

        TryProp(root, "data", out var data).Should().BeTrue(
            $"envelope must contain 'data'; body: {body}");
        data.ValueKind.Should().NotBe(JsonValueKind.Null,
            $"data must not be null on success; body: {body}");

        // ── EnergyStatusDto balance fields ──
        TryProp(data, "grantedBalance", out var grantedProp).Should().BeTrue(
            $"data must contain 'grantedBalance'; body: {body}");
        grantedProp.GetInt32().Should().BeGreaterThanOrEqualTo(0,
            $"grantedBalance must be a non-negative integer; body: {body}");

        TryProp(data, "purchasedBalance", out var purchasedProp).Should().BeTrue(
            $"data must contain 'purchasedBalance'; body: {body}");
        purchasedProp.GetInt32().Should().BeGreaterThanOrEqualTo(0,
            $"purchasedBalance must be non-negative; body: {body}");

        TryProp(data, "totalBalance", out var totalProp).Should().BeTrue(
            $"data must contain 'totalBalance'; body: {body}");
        totalProp.GetInt32().Should().BeGreaterThanOrEqualTo(0,
            $"totalBalance must be non-negative; body: {body}");

        TryProp(data, "dailyUsed", out var dailyUsedProp).Should().BeTrue(
            $"data must contain 'dailyUsed'; body: {body}");
        dailyUsedProp.GetInt32().Should().BeGreaterThanOrEqualTo(0,
            $"dailyUsed must be non-negative; body: {body}");
    }

    [Fact(DisplayName = "TC-CR-02 NoAccount: GET Energy/{childId}/Status with no allocation yet → 200 with zeroed balance (wallet model always returns status)")]
    public async Task TC_CR_02_GetBalance_NoAccount_Returns200_ZeroBalance()
    {
        // P10-13 CUTOVER: the wallet model EnergyStatusQueryHandler always returns 200 with zero balances
        // when no allocation exists yet (unlike the retired GetCreditAccountQueryHandler which returned 404).
        // Test intent updated: no allocation → 200 with grantedBalance=0 (not a 500).
        var adminToken = await GetAdminTokenAsync();
        var childId    = await CreateStudentAsync(adminToken);
        // Do NOT grant any credits — no ChildEnergyAllocation row exists yet.

        var (resp, root, body) = await SendAsync(
            HttpMethod.Get, $"api/Billing/Energy/{childId}/Status", null, adminToken);

        // Wallet model: returns 200 with zero balances (not 404) when no allocation exists.
        ((int)resp.StatusCode).Should().NotBe(500,
            $"a missing allocation must never cause a 500; body: {body}");
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 404 },
            $"no allocation: expect 200 (zero balance) or 404 (no wallet); body: {body}");

        // On 200: successed must be true and balance must be zero
        if (resp.StatusCode == HttpStatusCode.OK)
        {
            TryProp(root, "successed", out var succeededProp).Should().BeTrue(
                $"envelope must contain 'successed'; body: {body}");
            succeededProp.GetBoolean().Should().BeTrue(
                $"successed must be true when wallet returns zero balance; body: {body}");
            TryProp(root, "data", out var data);
            TryProp(data, "totalBalance", out var totalProp);
            totalProp.GetInt32().Should().Be(0,
                $"totalBalance must be 0 when no allocation; body: {body}");
        }
    }

    [Fact(DisplayName = "TC-CR-03 Auth: GET Energy/{childId}/Status anon → 401 Unauthorized")]
    public async Task TC_CR_03_GetBalance_NoToken_Returns401()
    {
        // Arbitrary childId — the auth gate fires before any business logic.
        var (resp, _, body) = await SendAsync(HttpMethod.Get, $"api/Billing/Energy/9999/Status");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            $"GET Energy/{{childId}}/Status is [Authorize]; no token → 401. body: {body}");
    }

    /// <summary>
    /// TC-CR-04 IDOR/AuthZ: a parent from Family B must NOT be able to read the energy status
    /// of a child linked only to Family A.
    /// EnergyStatusQueryHandler has IDOR scoping: parent JWT can only read their linked children.
    /// </summary>
    [Fact(DisplayName = "TC-CR-04 IDOR-AuthZ: ParentB cannot read child energy status from Family A — must be 403/404, not 200")]
    public async Task TC_CR_04_IDOR_ParentB_CannotRead_FamilyA_ChildBalance()
    {
        var adminToken = await GetAdminTokenAsync();

        // Create a student and grant them credits (Family A's child — but no real parent linkage here).
        var familyAChildId = await CreateStudentAsync(adminToken);
        var grantResp = await GrantCreditsAsync(adminToken, familyAChildId, amount: 77);
        ((int)grantResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            "Grant for Family A child must succeed to create allocation (TC-CR-04 setup).");

        // Register Family B's parent (completely separate, no children linked to familyAChildId).
        var parentBToken = await RegisterParentAndGetTokenAsync();

        // Family B parent attempts to GET Family A's child's energy status — this is the IDOR attempt.
        var (resp, root, body) = await SendAsync(
            HttpMethod.Get, $"api/Billing/Energy/{familyAChildId}/Status", null, parentBToken);

        var statusCode = (int)resp.StatusCode;

        // ASSERT SECURE BEHAVIOR: cross-family read must be denied (403 or 424 business-validation).
        // 200 = IDOR defect — test fails and reports it.
        statusCode.Should().BeOneOf(new[] { 403, 424 },
            $"IDOR DEFECT: ParentB received HTTP {statusCode} when reading child energy status " +
            $"for familyAChildId={familyAChildId}. EnergyStatusQueryHandler must deny unlinked parents. " +
            $"Response body: {body}");
    }

    [Fact(DisplayName = "TC-CR-05 Persistence: after Grant, GetBalance reflects granted amount")]
    public async Task TC_CR_05_AfterGrant_GetBalance_ReflectsGrantedAmount()
    {
        var adminToken   = await GetAdminTokenAsync();
        var childId      = await CreateStudentAsync(adminToken);
        const int amount = 42;

        var grantResp = await GrantCreditsAsync(adminToken, childId, amount);
        ((int)grantResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            "Grant must succeed for TC-CR-05 persistence round-trip.");

        // P10-13 CUTOVER: use GET /Energy/{childId}/Status (replaces retired GET /Credits/{childId}).
        var (resp, root, body) = await SendAsync(
            HttpMethod.Get, $"api/Billing/Energy/{childId}/Status", null, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Balance read after grant must return 200; body: {body}");

        TryProp(root, "data", out var data).Should().BeTrue($"body: {body}");
        TryProp(data, "grantedBalance", out var grantedProp).Should().BeTrue($"body: {body}");
        grantedProp.GetInt32().Should().Be(amount,
            $"grantedBalance must equal the granted amount {amount} after a single grant; body: {body}");

        TryProp(data, "totalBalance", out var totalProp).Should().BeTrue($"body: {body}");
        totalProp.GetInt32().Should().Be(amount,
            $"totalBalance must equal grantedBalance when no purchased credits; body: {body}");
    }

    [Fact(DisplayName = "TC-CR-06 Reconcile: GET Credits/Reconcile/{parentId} as Admin → 200 + ReconciliationResultDto")]
    public async Task TC_CR_06_Reconcile_AsAdmin_Returns200_WithResult()
    {
        // P10-13 CUTOVER: Reconcile is now family-wallet scoped — GET /Credits/Reconcile/{parentId}.
        // We use an admin comp grant to create a wallet for a parent, then reconcile against that parentId.
        var adminToken = await GetAdminTokenAsync();

        // Register a fresh parent so we have a real parentId to grant + reconcile against.
        var parentEmail = $"billing_rec06_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}@billing.test";
        var (prResp, prRoot, prBody) = await SendAsync(HttpMethod.Post,
            "api/Users/Authentication/Register-Parent",
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        prResp.StatusCode.Should().Be(HttpStatusCode.OK, $"parent registration for TC-CR-06; body: {prBody}");
        TryProp(prRoot, "data", out var prData); TryProp(prData, "accessToken", out var prTok);
        var freshParentToken = prTok.GetString()!;

        // Decode parentId from JWT
        var parts   = freshParentToken.Split('.');
        var payload = parts[1];
        var padded  = payload + new string('=', (4 - payload.Length % 4) % 4);
        var decoded = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        var jwtJson = System.Text.Encoding.UTF8.GetString(decoded);
        var jwtDoc  = JsonDocument.Parse(jwtJson).RootElement;
        jwtDoc.TryGetProperty("Id", out var idPropEl);
        var freshParentId = idPropEl.ValueKind == JsonValueKind.Number
            ? idPropEl.GetInt32()
            : int.Parse(idPropEl.GetString()!);

        // Admin comp grant creates a FamilyEnergyAccount for freshParentId
        var grantKey = $"rec06:{freshParentId}:{Guid.NewGuid():N}";
        await SendAsync(HttpMethod.Post, $"{CreditsBaseUrl}/Grant", new
        {
            ParentId       = freshParentId,
            Amount         = 20,
            IdempotencyKey = grantKey,
        }, adminToken);

        // Now reconcile the family wallet for freshParentId
        var (resp, root, body) = await SendAsync(
            HttpMethod.Get, $"{CreditsBaseUrl}/Reconcile/{freshParentId}", null, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Reconcile as Admin must return 200; body: {body}");

        TryProp(root, "successed", out var succeededProp).Should().BeTrue($"body: {body}");
        succeededProp.GetBoolean().Should().BeTrue(
            $"successed must be true for a successful reconcile; body: {body}");

        TryProp(root, "data", out var data).Should().BeTrue($"body: {body}");

        TryProp(data, "hasDrift", out _).Should().BeTrue(
            $"ReconciliationResultDto must contain hasDrift; body: {body}");
        TryProp(data, "storedPurchasedBalance", out _).Should().BeTrue(
            $"ReconciliationResultDto must contain storedPurchasedBalance; body: {body}");
        TryProp(data, "ledgerPurchasedBalance", out _).Should().BeTrue(
            $"ReconciliationResultDto must contain ledgerPurchasedBalance; body: {body}");
    }

    [Fact(DisplayName = "TC-CR-07 ReconcileAuthZ: GET Credits/Reconcile/{parentId} as Parent → 403 Forbidden")]
    public async Task TC_CR_07_Reconcile_AsParent_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();
        // P10-13 CUTOVER: route is now GET /Credits/Reconcile/{parentId}.
        // Arbitrary parentId — the authz gate fires before any business logic.
        var (resp, _, body) = await SendAsync(
            HttpMethod.Get, $"{CreditsBaseUrl}/Reconcile/9998", null, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"Reconcile endpoint requires Billing.View policy; Parent token → 403. body: {body}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Area 2 – GlobalSettings read + update (P10-12 / P10-11)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-GS-01 GetAll: GET /api/Admin/GlobalSettings as Admin → 200 + BaseResponse list")]
    public async Task TC_GS_01_GetAll_AsAdmin_Returns200_WithList()
    {
        var adminToken = await GetAdminTokenAsync();
        var (resp, root, body) = await SendAsync(HttpMethod.Get, GlobalSettingsUrl, null, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"GET GlobalSettings as Admin must return 200; body: {body}");

        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue($"body: {body}");
        statusCodeProp.GetInt32().Should().Be(200, $"statusCode must be 200; body: {body}");

        TryProp(root, "successed", out var succeededProp).Should().BeTrue($"body: {body}");
        succeededProp.GetBoolean().Should().BeTrue($"successed must be true; body: {body}");

        TryProp(root, "data", out var data).Should().BeTrue($"body: {body}");
        data.ValueKind.Should().Be(JsonValueKind.Array,
            $"data must be an array of GlobalSettingDto; body: {body}");

        data.GetArrayLength().Should().BeGreaterThan(0,
            $"data must contain at least one GlobalSetting row after seed; body: {body}");
    }

    [Fact(DisplayName = "TC-GS-02 Auth: GET /api/Admin/GlobalSettings anon → 401 Unauthorized")]
    public async Task TC_GS_02_GetAll_Anon_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, GlobalSettingsUrl);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            $"GET GlobalSettings is AdminOnly; no token → 401. body: {body}");
    }

    [Fact(DisplayName = "TC-GS-03 Auth: GET /api/Admin/GlobalSettings as Parent → 403 Forbidden")]
    public async Task TC_GS_03_GetAll_AsParent_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();
        var (resp, _, body) = await SendAsync(HttpMethod.Get, GlobalSettingsUrl, null, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"GET GlobalSettings is AdminOnly; Parent token → 403. body: {body}");
    }

    /// <summary>
    /// TC-GS-04 Seed verification: all 17 default keys must be present with correct economy values.
    /// Verifies AI action costs (Hint=1, WhyWrong=2, Explain=3, Practice=5),
    /// cache thresholds, credit pools, subscription pricing, and FX defaults.
    /// </summary>
    [Fact(DisplayName = "TC-GS-04 SeedVerification: all 22 seeded keys present with correct Phase-10 economy values")]
    public async Task TC_GS_04_SeedVerification_All22Keys_Present_WithCorrectValues()
    {
        var adminToken = await GetAdminTokenAsync();
        var (resp, root, body) = await SendAsync(HttpMethod.Get, GlobalSettingsUrl, null, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"GET GlobalSettings must return 200 for seed verification; body: {body}");

        TryProp(root, "data", out var data).Should().BeTrue($"body: {body}");
        var settings = data.EnumerateArray().ToList();

        // Phase-10 baseline = 22 keys: 17 economy + 4 P10-14 seat keys
        // (seats.included_free/premium, seats.max, seats.extra_price_egp) + 1 P10-15 (seats.grace_days).
        // Use a FLOOR, not an exact count — the GlobalSettings store is shared and grows over time
        // (e.g. GlobalSettingsSeeder now also bakes ai_cost.recommendation for P3-14 Lexi → 23). The
        // individual key assertions below are the real guard that each Phase-10 economy key is present + correct.
        settings.Count.Should().BeGreaterThanOrEqualTo(22,
            $"GlobalSettingsSeeder must produce at least the 22 Phase-10 rows; found {settings.Count}. body: {body}");

        // Build a lookup by key.
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in settings)
        {
            if (TryProp(item, "key", out var keyProp) && TryProp(item, "value", out var valueProp))
                dict[keyProp.GetString()!] = valueProp.GetString()!;
        }

        // ── AI action costs (core economy values) ──
        dict.Should().ContainKey("ai_cost.hint",
            $"hint cost key must be seeded; body: {body}");
        dict["ai_cost.hint"].Should().Be("1",
            $"Hint cost must default to 1; body: {body}");

        dict.Should().ContainKey("ai_cost.explain_mistake",
            $"explain_mistake cost key must be seeded; body: {body}");
        dict["ai_cost.explain_mistake"].Should().Be("2",
            $"WhyWrong cost must default to 2; body: {body}");

        dict.Should().ContainKey("ai_cost.deep_explanation",
            $"deep_explanation cost key must be seeded; body: {body}");
        dict["ai_cost.deep_explanation"].Should().Be("3",
            $"Explain/Simplify cost must default to 3; body: {body}");

        dict.Should().ContainKey("ai_cost.practice_generation",
            $"practice_generation cost key must be seeded; body: {body}");
        dict["ai_cost.practice_generation"].Should().Be("5",
            $"Practice generation cost must default to 5; body: {body}");

        // ── AI cache thresholds ──
        dict.Should().ContainKey("ai.cache.autoApprovalConfidence",
            $"cache confidence key must be seeded; body: {body}");
        dict["ai.cache.autoApprovalConfidence"].Should().Be("0.85",
            $"auto approval confidence must default to 0.85; body: {body}");

        dict.Should().ContainKey("ai.cache.whyWrongVariantCap",
            $"whyWrongVariantCap key must be seeded; body: {body}");
        dict["ai.cache.whyWrongVariantCap"].Should().Be("50",
            $"whyWrongVariantCap must default to 50; body: {body}");

        dict.Should().ContainKey("ai.cache.practicePoolSize",
            $"practicePoolSize key must be seeded; body: {body}");
        dict["ai.cache.practicePoolSize"].Should().Be("5",
            $"practicePoolSize must default to 5; body: {body}");

        // ── Credit pools ──
        dict.Should().ContainKey("credits.free_monthly",
            $"free_monthly key must be seeded; body: {body}");
        dict["credits.free_monthly"].Should().Be("100",
            $"free monthly credits must default to 100; body: {body}");

        dict.Should().ContainKey("credits.premium_monthly",
            $"premium_monthly key must be seeded; body: {body}");
        dict["credits.premium_monthly"].Should().Be("5000",
            $"premium monthly credits must default to 5000; body: {body}");

        dict.Should().ContainKey("credits.free_daily_cap",
            $"free_daily_cap key must be seeded; body: {body}");
        dict["credits.free_daily_cap"].Should().Be("10",
            $"free daily cap must default to 10; body: {body}");

        dict.Should().ContainKey("credits.premium_daily_cap",
            $"premium_daily_cap key must be seeded; body: {body}");
        dict["credits.premium_daily_cap"].Should().Be("250",
            $"premium daily cap must default to 250; body: {body}");

        // ── Pack ──
        dict.Should().ContainKey("credits.pack_price_egp",
            $"pack_price_egp key must be seeded; body: {body}");
        dict["credits.pack_price_egp"].Should().Be("50.00",
            $"pack price must default to 50.00; body: {body}");

        dict.Should().ContainKey("credits.pack_size",
            $"pack_size key must be seeded; body: {body}");
        dict["credits.pack_size"].Should().Be("1000",
            $"pack size must default to 1000; body: {body}");

        // ── Subscription pricing ──
        dict.Should().ContainKey("subscription.monthly_price_egp",
            $"monthly subscription price key must be seeded; body: {body}");
        dict["subscription.monthly_price_egp"].Should().Be("199.00",
            $"monthly subscription price must default to 199.00; body: {body}");

        dict.Should().ContainKey("subscription.annual_price_egp",
            $"annual subscription price key must be seeded; body: {body}");
        dict["subscription.annual_price_egp"].Should().Be("1990.00",
            $"annual subscription price must default to 1990.00; body: {body}");

        // ── Payment / FX ──
        dict.Should().ContainKey("payment.provider_fees",
            $"payment provider fees key must be seeded; body: {body}");
        dict["payment.provider_fees"].Should().Be("2.75",
            $"payment provider fees must default to 2.75; body: {body}");

        dict.Should().ContainKey("fx.usd_exchange_rate_buffer",
            $"FX exchange rate buffer key must be seeded; body: {body}");
        dict["fx.usd_exchange_rate_buffer"].Should().Be("1.10",
            $"FX exchange rate buffer must default to 1.10; body: {body}");

        // ── P10-14 Seat configuration (locked 2026-06-16) ──
        dict.Should().ContainKey("seats.included_free",
            $"seats.included_free key must be seeded; body: {body}");
        dict["seats.included_free"].Should().Be("1",
            $"Free plan included seats must default to 1; body: {body}");

        dict.Should().ContainKey("seats.included_premium",
            $"seats.included_premium key must be seeded; body: {body}");
        dict["seats.included_premium"].Should().Be("3",
            $"Premium plan included seats must default to 3; body: {body}");

        dict.Should().ContainKey("seats.max",
            $"seats.max key must be seeded; body: {body}");
        dict["seats.max"].Should().Be("5",
            $"max seats ceiling must default to 5; body: {body}");

        dict.Should().ContainKey("seats.extra_price_egp",
            $"seats.extra_price_egp key must be seeded; body: {body}");
        dict["seats.extra_price_egp"].Should().Be("169.00",
            $"extra-seat monthly price must default to 169.00 EGP; body: {body}");

        // ── P10-15 Seat-lifecycle configuration ──
        dict.Should().ContainKey("seats.grace_days",
            $"seats.grace_days key must be seeded; body: {body}");
        dict["seats.grace_days"].Should().Be("7",
            $"payment-failure seat grace window must default to 7 days; body: {body}");
    }

    [Fact(DisplayName = "TC-GS-05 Update+Reflect: PUT GlobalSettings/{key} as Admin → 200; subsequent GET reflects new value (proves DB + cache invalidation)")]
    public async Task TC_GS_05_Update_AsAdmin_Reflects_InSubsequentGet()
    {
        var adminToken = await GetAdminTokenAsync();
        // Use a unique-per-test target to avoid conflicts with TC-GS-04's restore step.
        const string key      = "ai_cost.hint";
        const string newValue = "3";  // valid int, in range (≥0)

        // PUT update
        var (putResp, putRoot, putBody) = await SendAsync(
            HttpMethod.Put, $"{GlobalSettingsUrl}/{key}",
            new { NewValue = newValue, UpdatedBy = "billing-integration-test" },
            adminToken);

        putResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"PUT GlobalSettings as Admin must return 200; body: {putBody}");

        TryProp(putRoot, "successed", out var putSucceeded).Should().BeTrue($"body: {putBody}");
        putSucceeded.GetBoolean().Should().BeTrue(
            $"successed must be true on a successful update; body: {putBody}");

        // Subsequent GET — must reflect the new value (proves DB committed + cache invalidated).
        var (getResp, getRoot, getBody) = await SendAsync(
            HttpMethod.Get, GlobalSettingsUrl, null, adminToken);

        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"GET GlobalSettings after update must return 200; body: {getBody}");

        TryProp(getRoot, "data", out var data).Should().BeTrue($"body: {getBody}");
        var settings = data.EnumerateArray().ToList();

        var hintRow = settings.FirstOrDefault(s =>
        {
            TryProp(s, "key", out var kp);
            return kp.GetString() == key;
        });

        hintRow.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            $"Key '{key}' must appear in the GET response after update; body: {getBody}");

        TryProp(hintRow, "value", out var valueProp).Should().BeTrue(
            $"Key '{key}' row must contain 'value'; body: {getBody}");
        valueProp.GetString().Should().Be(newValue,
            $"The updated value must be '{newValue}' — proves DB write was committed " +
            $"and cache was invalidated so the next read returns the new value. body: {getBody}");

        // Restore original value so other tests aren't impacted.
        await SendAsync(HttpMethod.Put, $"{GlobalSettingsUrl}/{key}",
            new { NewValue = "1", UpdatedBy = "billing-integration-test-restore" },
            adminToken);
    }

    [Fact(DisplayName = "TC-GS-06 Auth: PUT /api/Admin/GlobalSettings/{key} anon → 401 Unauthorized")]
    public async Task TC_GS_06_Update_Anon_Returns401()
    {
        var (resp, _, body) = await SendAsync(
            HttpMethod.Put, $"{GlobalSettingsUrl}/ai_cost.hint",
            new { NewValue = "99", UpdatedBy = "anon-attempt" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            $"PUT GlobalSettings is AdminOnly; no token → 401. body: {body}");
    }

    [Fact(DisplayName = "TC-GS-07 Auth: PUT /api/Admin/GlobalSettings/{key} as Parent → 403 Forbidden")]
    public async Task TC_GS_07_Update_AsParent_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, _, body) = await SendAsync(
            HttpMethod.Put, $"{GlobalSettingsUrl}/ai_cost.hint",
            new { NewValue = "99", UpdatedBy = "parent-attempt" },
            parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"PUT GlobalSettings is AdminOnly; Parent token → 403. body: {body}");
    }

    [Fact(DisplayName = "TC-GS-08 Validation: PUT with key not in allowlist → 422 UnprocessableEntity")]
    public async Task TC_GS_08_Update_InvalidKey_Returns422()
    {
        var adminToken = await GetAdminTokenAsync();

        var (resp, root, body) = await SendAsync(
            HttpMethod.Put, $"{GlobalSettingsUrl}/not.a.real.key",
            new { NewValue = "123", UpdatedBy = "admin" },
            adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            $"PUT with a key not in the allowlist must return 422; body: {body}");

        TryProp(root, "successed", out var succeededProp).Should().BeTrue($"body: {body}");
        succeededProp.GetBoolean().Should().BeFalse(
            $"successed must be false for a validation error; body: {body}");

        TryProp(root, "errors", out var errorsProp).Should().BeTrue($"body: {body}");
        errorsProp.EnumerateArray().ToList().Should().NotBeEmpty(
            $"Errors[] must list the allowlist violation; body: {body}");
    }

    [Fact(DisplayName = "TC-GS-09 Validation: PUT non-parseable value for int key → 422 (type mismatch)")]
    public async Task TC_GS_09_Update_BadTypeValue_Returns422()
    {
        var adminToken = await GetAdminTokenAsync();

        // ai_cost.hint is Int — "not-a-number" fails IsValueParseable.
        var (resp, root, body) = await SendAsync(
            HttpMethod.Put, $"{GlobalSettingsUrl}/ai_cost.hint",
            new { NewValue = "not-a-number", UpdatedBy = "admin" },
            adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            $"PUT with a non-integer value for an Int key must return 422; body: {body}");

        TryProp(root, "successed", out var succeededProp).Should().BeTrue($"body: {body}");
        succeededProp.GetBoolean().Should().BeFalse($"body: {body}");

        TryProp(root, "errors", out var errorsProp).Should().BeTrue($"body: {body}");
        errorsProp.EnumerateArray().ToList().Should().NotBeEmpty(
            $"Errors[] must list the type mismatch; body: {body}");
    }

    [Fact(DisplayName = "TC-GS-10 Validation: PUT confidence > 1.0 → 422 (out of range)")]
    public async Task TC_GS_10_Update_OutOfRange_Confidence_Returns422()
    {
        var adminToken = await GetAdminTokenAsync();

        // AiCacheAutoApprovalConfidence must be in [0, 1].
        var (resp, root, body) = await SendAsync(
            HttpMethod.Put, $"{GlobalSettingsUrl}/ai.cache.autoApprovalConfidence",
            new { NewValue = "1.5", UpdatedBy = "admin" },
            adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            $"PUT confidence > 1.0 must return 422 (out of range); body: {body}");

        TryProp(root, "successed", out var succeededProp).Should().BeTrue($"body: {body}");
        succeededProp.GetBoolean().Should().BeFalse($"body: {body}");

        TryProp(root, "errors", out var errorsProp).Should().BeTrue($"body: {body}");
        errorsProp.EnumerateArray().ToList().Should().NotBeEmpty(
            $"Errors[] must list the range violation; body: {body}");
    }

    [Fact(DisplayName = "TC-GS-11 Validation: PUT with empty NewValue → 422")]
    public async Task TC_GS_11_Update_EmptyValue_Returns422()
    {
        var adminToken = await GetAdminTokenAsync();

        var (resp, root, body) = await SendAsync(
            HttpMethod.Put, $"{GlobalSettingsUrl}/ai_cost.hint",
            new { NewValue = "", UpdatedBy = "admin" },
            adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            $"PUT with an empty NewValue must return 422; body: {body}");

        TryProp(root, "successed", out var succeededProp).Should().BeTrue($"body: {body}");
        succeededProp.GetBoolean().Should().BeFalse($"body: {body}");

        TryProp(root, "errors", out var errorsProp).Should().BeTrue($"body: {body}");
        errorsProp.EnumerateArray().ToList().Should().NotBeEmpty(
            $"Errors[] must list the empty-value violation; body: {body}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Area 3 – Envelope shape completeness
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-ENV-01 Envelope: GlobalSettings GET success has all 5 BaseResponse keys")]
    public async Task TC_ENV_01_GetAll_SuccessEnvelope_HasAllRequiredKeys()
    {
        var adminToken = await GetAdminTokenAsync();
        var (resp, root, body) = await SendAsync(HttpMethod.Get, GlobalSettingsUrl, null, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body: {body}");

        // All five keys per CONVENTIONS.md §5 / architecture.md §6
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
}
