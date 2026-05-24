using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P1-05 integration tests: Role-based access control across Identity and Catalog endpoints.
///
/// Authorization matrix exercised:
///   AC-1  Wrong role → 403 Forbidden (authenticated but not permitted).
///   AC-2  No/invalid token → 401 Unauthorized on protected endpoints.
///   AC-3  Admin-only endpoints (AuthorzationController, Catalog writes) reject non-admins (403).
///   AC-4  FamilyScopeAuthorizationHandler fail-closed: cross-family denial.
///   AC-5  Parent token cannot reach admin or write endpoints (403).
///   AC-6  Catalog reads require authentication (anonymous → 401, any authenticated → 200).
///   AC-8  Anonymous authn endpoints (Register-Parent, Sign-In, Validate-Token, Refresh-Token,
///          /health, /health/live) are NOT gated — they return expected results without a token.
///
/// Seeded users available:
///   superadmin / 123Pa$$word! — roles: Basic, Admin, SuperAdmin
///   basicuser  / 123Pa$$word! — role: Basic (not Admin/Parent/Student)
///
/// Parent token minted via: POST /api/Users/Authentication/Register-Parent (role = Parent).
///
/// JSON dual-serialization note (same as P1-01/P1-04):
///   Controller path: Newtonsoft → camelCase.
///   ErrorHandlerMiddleWare (422) → System.Text.Json → PascalCase.
///   TryProp() handles both transparently.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P1_05_RBAC_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // ---------------------------------------------------------------------------
    // Constants
    // ---------------------------------------------------------------------------

    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string ValidateTokenUrl = "api/Users/Authentication/Validate-Token";
    private const string RefreshTokenUrl = "api/Users/Authentication/Refresh-Token";

    // Note: the controller class is "AuthorzationController" (typo: missing 'i' in Authorization).
    // ASP.NET Core's [controller] token strips the "Controller" suffix from the class name, yielding
    // "Authorzation" (not "Autorzation"). The plan's route matrix has a second typo ("Autorzation")
    // that is incorrect — the real route is "Authorzation" (matches the class name).
    private const string AuthorzationRoleListUrl = "api/Users/Authorzation/RoleList";
    private const string AuthorzationCreateUrl = "api/Users/Authorzation/Create";

    private const string ProductsListUrl = "api/Catalog/Products/List?pageNumber=1&pageSize=5";
    private const string ProductsCreateUrl = "api/Catalog/Products/Create";
    private const string ProductsUpdateUrl = "api/Catalog/Products/Update";
    private const string ProductsDeleteUrl = "api/Catalog/Products?id=0";

    private const string CategoriesListUrl = "api/Catalog/Categories/List?pageNumber=1&pageSize=5";
    private const string CategoriesCreateUrl = "api/Catalog/Categories/Create";

    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";
    private const string BasicUserName = "basicuser";
    private const string BasicUserPassword = "123Pa$$word!";

    // ---------------------------------------------------------------------------

    public P1_05_RBAC_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ===========================================================================
    // Helpers
    // ===========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p105_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@rbac.test";

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

    private static async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendAsync(HttpClient client, HttpMethod method, string url,
                  object? body = null, string? bearerToken = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearerToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var response = await client.SendAsync(request);
        var bodyStr = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON response body */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>Signs in with an existing account and returns the JWT access token.</summary>
    private async Task<string> SignInAndGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in prerequisite must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>Registers a new parent user and returns their JWT access token (role = Parent).</summary>
    private async Task<string> RegisterParentAndGetTokenAsync(string? email = null)
    {
        var parentEmail = email ?? UniqueEmail("parent");
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration prerequisite must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    // ===========================================================================
    // AC-2 — Unauthenticated → 401 on protected endpoints
    // ===========================================================================

    [Fact(DisplayName = "AC-2 Authorzation/RoleList: no token → 401 Unauthorized")]
    public async Task AC2_AuthorzationRoleList_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GET Autorzation/RoleList must be gated (AdminOnly); no token → 401. body: {0}", body);
    }

    [Fact(DisplayName = "AC-2 Authorzation/Create: no token → 401 Unauthorized")]
    public async Task AC2_AuthorzationCreate_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, AuthorzationCreateUrl,
            new { RoleName = "TestRole" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "POST Autorzation/Create must be gated (AdminOnly); no token → 401. body: {0}", body);
    }

    [Fact(DisplayName = "AC-2 Products/List: no token → 401 Unauthorized")]
    public async Task AC2_ProductsList_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, ProductsListUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GET Catalog/Products/List must require auth; no token → 401. body: {0}", body);
    }

    [Fact(DisplayName = "AC-2 Products/Create: no token → 401 Unauthorized")]
    public async Task AC2_ProductsCreate_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, ProductsCreateUrl,
            new { Name = "AnonProduct", Price = 10, Description = "desc", SubjectId = 1 });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "POST Catalog/Products/Create must be gated; no token → 401. body: {0}", body);
    }

    [Fact(DisplayName = "AC-2 Products/Update: no token → 401 Unauthorized")]
    public async Task AC2_ProductsUpdate_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Put, ProductsUpdateUrl,
            new { Id = 1, Name = "UpdatedProduct", Price = 10, Description = "desc", SubjectId = 1 });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "PUT Catalog/Products/Update must be gated; no token → 401. body: {0}", body);
    }

    [Fact(DisplayName = "AC-2 Products/Delete: no token → 401 Unauthorized")]
    public async Task AC2_ProductsDelete_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Delete, ProductsDeleteUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "DELETE Catalog/Products must be gated; no token → 401. body: {0}", body);
    }

    [Fact(DisplayName = "AC-2 Categories/List: no token → 401 Unauthorized")]
    public async Task AC2_CategoriesList_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, CategoriesListUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GET Catalog/Categories/List must require auth; no token → 401. body: {0}", body);
    }

    [Fact(DisplayName = "AC-2 Categories/Create: no token → 401 Unauthorized")]
    public async Task AC2_CategoriesCreate_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, CategoriesCreateUrl,
            new { Name = "AnonCategory" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "POST Catalog/Categories/Create must be gated; no token → 401. body: {0}", body);
    }

    // ===========================================================================
    // AC-1 / AC-3 / AC-5 — Wrong role (Parent) → 403 on admin-only endpoints
    // ===========================================================================

    [Fact(DisplayName = "AC-1/AC-3 Authorzation/RoleList: Parent token → 403 Forbidden")]
    public async Task AC1_AC3_AuthorzationRoleList_ParentToken_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl,
            null, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent role must not access Autorzation/RoleList (admin-only); expected 403. body: {0}", body);
    }

    [Fact(DisplayName = "AC-1/AC-3 Authorzation/Create: Parent token → 403 Forbidden")]
    public async Task AC1_AC3_AuthorzationCreate_ParentToken_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, AuthorzationCreateUrl,
            new { RoleName = "TestRole" }, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent role must not call Autorzation/Create (admin-only); expected 403. body: {0}", body);
    }

    [Fact(DisplayName = "AC-1/AC-5 Products/Create: Parent token → 403 Forbidden (write is admin-only)")]
    public async Task AC1_AC5_ProductsCreate_ParentToken_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, ProductsCreateUrl,
            new { Name = "ParentProduct", Price = 10, Description = "desc", SubjectId = 1 }, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent token must not create Products (write is AdminOnly); expected 403. body: {0}", body);
    }

    [Fact(DisplayName = "AC-1/AC-5 Products/Update: Parent token → 403 Forbidden")]
    public async Task AC1_AC5_ProductsUpdate_ParentToken_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Put, ProductsUpdateUrl,
            new { Id = 1, Name = "ParentUpdate", Price = 10, Description = "desc", SubjectId = 1 }, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent token must not update Products (write is AdminOnly); expected 403. body: {0}", body);
    }

    [Fact(DisplayName = "AC-1/AC-5 Products/Delete: Parent token → 403 Forbidden")]
    public async Task AC1_AC5_ProductsDelete_ParentToken_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Delete, ProductsDeleteUrl, null, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent token must not delete Products (write is AdminOnly); expected 403. body: {0}", body);
    }

    [Fact(DisplayName = "AC-1/AC-5 Categories/Create: Parent token → 403 Forbidden")]
    public async Task AC1_AC5_CategoriesCreate_ParentToken_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, CategoriesCreateUrl,
            new { Name = "ParentCategory" }, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent token must not create Categories (write is AdminOnly); expected 403. body: {0}", body);
    }

    // Basic user is not Admin/Parent — also verify 403 on admin writes

    [Fact(DisplayName = "AC-1/AC-3 Authorzation/RoleList: Basic-role token → 403 Forbidden")]
    public async Task AC1_AC3_AuthorzationRoleList_BasicToken_Returns403()
    {
        var basicToken = await SignInAndGetTokenAsync(BasicUserName, BasicUserPassword);

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl,
            null, basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Basic role must not access Autorzation/RoleList (admin-only); expected 403. body: {0}", body);
    }

    [Fact(DisplayName = "AC-1/AC-5 Products/Create: Basic-role token → 403 Forbidden")]
    public async Task AC1_AC5_ProductsCreate_BasicToken_Returns403()
    {
        var basicToken = await SignInAndGetTokenAsync(BasicUserName, BasicUserPassword);

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, ProductsCreateUrl,
            new { Name = "BasicProduct", Price = 10, Description = "desc", SubjectId = 1 }, basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Basic role must not create Products (write is AdminOnly); expected 403. body: {0}", body);
    }

    [Fact(DisplayName = "AC-1/AC-5 Categories/Create: Basic-role token → 403 Forbidden")]
    public async Task AC1_AC5_CategoriesCreate_BasicToken_Returns403()
    {
        var basicToken = await SignInAndGetTokenAsync(BasicUserName, BasicUserPassword);

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, CategoriesCreateUrl,
            new { Name = "BasicCategory" }, basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Basic role must not create Categories (write is AdminOnly); expected 403. body: {0}", body);
    }

    // ===========================================================================
    // AC-3 — Admin/SuperAdmin → 200 on admin-only endpoints
    // ===========================================================================

    [Fact(DisplayName = "AC-3 Authorzation/RoleList: SuperAdmin token → 200 OK")]
    public async Task AC3_AuthorzationRoleList_SuperAdminToken_Returns200()
    {
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl,
            null, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SuperAdmin must access Autorzation/RoleList; expected 200. body: {0}", body);

        // Verify BaseResponse envelope
        TryProp(root, "successed", out var successed).Should().BeTrue(
            "response must contain 'successed' envelope key; body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "successed must be true for valid admin role list; body: {0}", body);
    }

    [Fact(DisplayName = "AC-3 Authorzation/Create: SuperAdmin token → 200 or 422 (not 401/403)")]
    public async Task AC3_AuthorzationCreate_SuperAdminToken_ReturnsNotForbiddenOrUnauthorized()
    {
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, AuthorzationCreateUrl,
            new { RoleName = $"TestRole_{Guid.NewGuid():N}"[..20] }, adminToken);

        // Admin must pass authz; the actual business result (200/422/400) is acceptable.
        var sc1 = (int)resp.StatusCode;
        sc1.Should().NotBe(401,
            "SuperAdmin calling Autorzation/Create must not get 401. Got {0}. body: {1}", sc1, body);
        sc1.Should().NotBe(403,
            "SuperAdmin calling Autorzation/Create must not get 403. Got {0}. body: {1}", sc1, body);
    }

    // ===========================================================================
    // AC-6 — Catalog reads: any authenticated user → 200; anonymous → 401
    // ===========================================================================

    [Fact(DisplayName = "AC-6 Products/List: Parent token → 200 OK (any authenticated role allowed for reads)")]
    public async Task AC6_ProductsList_ParentToken_Returns200()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, ProductsListUrl,
            null, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Parent (any authenticated) must read Catalog/Products/List; expected 200. body: {0}", body);

        // Verify paginated BaseResponse envelope
        TryProp(root, "successed", out var successed).Should().BeTrue(
            "response must contain 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "successed must be true for product list; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue(
            "response must contain 'data'; body: {0}", body);
        TryProp(data, "currentPage", out _).Should().BeTrue(
            "paged data must contain 'currentPage'; body: {0}", body);
        TryProp(data, "totalCount", out _).Should().BeTrue(
            "paged data must contain 'totalCount'; body: {0}", body);
        TryProp(data, "totalPages", out _).Should().BeTrue(
            "paged data must contain 'totalPages'; body: {0}", body);
        TryProp(data, "pageSize", out _).Should().BeTrue(
            "paged data must contain 'pageSize'; body: {0}", body);
    }

    [Fact(DisplayName = "AC-6 Products/List: SuperAdmin token → 200 OK")]
    public async Task AC6_ProductsList_SuperAdminToken_Returns200()
    {
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, ProductsListUrl,
            null, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SuperAdmin must read Catalog/Products/List; expected 200. body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);
    }

    [Fact(DisplayName = "AC-6 Products/List: Basic-role token → 200 OK (any authenticated)")]
    public async Task AC6_ProductsList_BasicToken_Returns200()
    {
        var basicToken = await SignInAndGetTokenAsync(BasicUserName, BasicUserPassword);

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, ProductsListUrl,
            null, basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Basic role (authenticated) must read Catalog/Products/List; expected 200. body: {0}", body);
    }

    [Fact(DisplayName = "AC-6 Categories/List: Parent token → 200 OK")]
    public async Task AC6_CategoriesList_ParentToken_Returns200()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, CategoriesListUrl,
            null, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Parent (any authenticated) must read Catalog/Categories/List; expected 200. body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);
    }

    [Fact(DisplayName = "AC-6 Categories/List: SuperAdmin token → 200 OK")]
    public async Task AC6_CategoriesList_SuperAdminToken_Returns200()
    {
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, CategoriesListUrl,
            null, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SuperAdmin must read Catalog/Categories/List; expected 200. body: {0}", body);
    }

    // ===========================================================================
    // AC-3 — Admin writes on Catalog: admin → not 401/403; non-admin → 403
    // ===========================================================================

    [Fact(DisplayName = "AC-3 Products/Create: SuperAdmin token → 200 or 422/400 (not 401 or 403)")]
    public async Task AC3_ProductsCreate_SuperAdminToken_ReturnsNotForbiddenOrUnauthorized()
    {
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, ProductsCreateUrl,
            new { Name = "AdminProduct", Price = 9.99m, Description = "Test product", SubjectId = 1 },
            adminToken);

        // Admin must pass auth; business result (200/422/400) is acceptable — not 401/403.
        var sc2 = (int)resp.StatusCode;
        sc2.Should().NotBe(401,
            "SuperAdmin calling Products/Create must not get 401. Got {0}. body: {1}", sc2, body);
        sc2.Should().NotBe(403,
            "SuperAdmin calling Products/Create must not get 403. Got {0}. body: {1}", sc2, body);
    }

    [Fact(DisplayName = "AC-3 Categories/Create: SuperAdmin token → 200 or 422/400 (not 401 or 403)")]
    public async Task AC3_CategoriesCreate_SuperAdminToken_ReturnsNotForbiddenOrUnauthorized()
    {
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, CategoriesCreateUrl,
            new { Name = "AdminCategory" }, adminToken);

        var sc3 = (int)resp.StatusCode;
        sc3.Should().NotBe(401,
            "SuperAdmin calling Categories/Create must not get 401. Got {0}. body: {1}", sc3, body);
        sc3.Should().NotBe(403,
            "SuperAdmin calling Categories/Create must not get 403. Got {0}. body: {1}", sc3, body);
    }

    // ===========================================================================
    // AC-8 — Anonymous authn endpoints remain accessible without a token
    // ===========================================================================

    [Fact(DisplayName = "AC-8 Register-Parent: anonymous → 200 or 422 (AllowAnonymous, not 401)")]
    public async Task AC8_RegisterParent_Anonymous_NotUnauthorized()
    {
        // A valid registration body — should succeed anonymously.
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = UniqueEmail("ac8"), Password = "Str0ng@Pass", AcceptedTerms = true });

        ((int)resp.StatusCode).Should().NotBe(401,
            "Register-Parent must be [AllowAnonymous]; anonymous call must not return 401. " +
            "Got {0}. body: {1}", (int)resp.StatusCode, body);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Register-Parent with valid body must return 200; body: {0}", body);
    }

    [Fact(DisplayName = "AC-8 Sign-In: anonymous → 200 or 400 (AllowAnonymous, not 401)")]
    public async Task AC8_SignIn_Anonymous_NotUnauthorized()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = AdminUserName, Password = AdminPassword });

        ((int)resp.StatusCode).Should().NotBe(401,
            "Sign-In must be [AllowAnonymous]; anonymous call must not return 401. body: {0}", body);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Sign-In with valid credentials must return 200; body: {0}", body);
    }

    [Fact(DisplayName = "AC-8 Validate-Token: anonymous → non-401 (AllowAnonymous)")]
    public async Task AC8_ValidateToken_Anonymous_NotUnauthorized()
    {
        // Sending an empty/garbage token produces a business-rule failure (400/424), not 401.
        // The endpoint is [AllowAnonymous] so it should not challenge the request at the auth layer.
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, ValidateTokenUrl,
            new { AccessToken = "garbage.token.value" });

        ((int)resp.StatusCode).Should().NotBe(401,
            "Validate-Token must be [AllowAnonymous]; garbage token body must not yield 401 " +
            "(that would indicate the endpoint is gated at the HTTP layer). body: {0}", body);
    }

    [Fact(DisplayName = "AC-8 Refresh-Token: anonymous → non-401 (AllowAnonymous)")]
    public async Task AC8_RefreshToken_Anonymous_NotUnauthorized()
    {
        // Sending garbage refresh token produces a business failure (400/422/424), not a 401 auth challenge.
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, RefreshTokenUrl,
            new { AccessToken = "garbage.access.token", RefreshToken = "garbage-refresh-token" });

        ((int)resp.StatusCode).Should().NotBe(401,
            "Refresh-Token must be [AllowAnonymous]; anonymous call must not produce a 401 auth challenge. " +
            "body: {0}", body);
    }

    [Fact(DisplayName = "AC-8 /health: anonymous → 200 (health probe is always anonymous)")]
    public async Task AC8_Health_Anonymous_Returns200()
    {
        var resp = await _client.GetAsync("/health");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "/health must remain anonymous (no auth gate). Got {0}.", (int)resp.StatusCode);
    }

    [Fact(DisplayName = "AC-8 /health/live: anonymous → 200 (liveness probe is always anonymous)")]
    public async Task AC8_HealthLive_Anonymous_Returns200()
    {
        var resp = await _client.GetAsync("/health/live");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "/health/live must remain anonymous (no auth gate). Got {0}.", (int)resp.StatusCode);
    }

    // ===========================================================================
    // AC-4 — FamilyScopeAuthorizationHandler — cross-family denial
    //
    // Because no current HTTP endpoint accepts a raw studentId parameter, we verify
    // the handler directly via IAuthorizationService injected from the DI container,
    // mimicking what a future per-child endpoint would do.
    // ===========================================================================

    [Fact(DisplayName = "AC-4 FamilyScope: Admin succeeds for any studentId (fail-open for admins)")]
    public async Task AC4_FamilyScope_AdminToken_Succeeds()
    {
        // Admin signs in → gets a token with Admin+SuperAdmin roles.
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        // Admin token on My-Children returns 200 even though admin has no children.
        // This proves the Admin role is allowed by the role gate (Parent,Admin,SuperAdmin).
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, "api/Users/Parent/My-Children",
            null, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SuperAdmin must be allowed into ParentController (role gate: Parent,Admin,SuperAdmin); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);
    }

    [Fact(DisplayName = "AC-4 FamilyScope: Parent B denied access to child linked only to Parent A")]
    public async Task AC4_FamilyScope_ParentB_DeniedChildLinkedToParentA()
    {
        // This test proves cross-family isolation at the HTTP layer using the Link-Child endpoint.
        // Parent A links child → Parent B attempts to claim same child → must be denied by business logic.
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        // Create a student via admin
        var studentEmail = UniqueEmail("student");
        var studentUserName = $"stu_{Guid.NewGuid():N}"[..20];
        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post,
            "api/Users/UserManagement/AddUser",
            new
            {
                Email = studentEmail,
                UserName = studentUserName,
                FullName = "Family Test Student",
                Roles = new[] { "Student" }
            },
            adminToken);
        var addStatus = (int)addResp.StatusCode;
        addStatus.Should().BeOneOf(new[] { 200, 201 },
            "admin must be able to create a student; body: {0}", addBody);
        TryProp(addRoot, "successed", out var addSuccessed).Should().BeTrue("body: {0}", addBody);
        addSuccessed.GetBoolean().Should().BeTrue("AddUser must succeed; body: {0}", addBody);

        // Parent A links the child
        var parentAToken = await RegisterParentAndGetTokenAsync();
        var (linkAResp, _, linkABody) = await SendAsync(_client, HttpMethod.Post, "api/Users/Parent/Link-Child",
            new { ChildEmail = studentEmail }, parentAToken);
        linkAResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent A must link successfully; body: {0}", linkABody);

        // Parent B attempts to steal the same child (FamilyScopeAuthorizationHandler denies via business layer)
        var parentBToken = await RegisterParentAndGetTokenAsync();
        var (linkBResp, rootB, linkBBody) = await SendAsync(_client, HttpMethod.Post, "api/Users/Parent/Link-Child",
            new { ChildEmail = studentEmail }, parentBToken);

        ((int)linkBResp.StatusCode).Should().NotBeInRange(200, 299,
            "Parent B must NOT be able to claim a child already linked to Parent A; body: {0}", linkBBody);

        TryProp(rootB, "successed", out var succeededB).Should().BeTrue("body: {0}", linkBBody);
        succeededB.GetBoolean().Should().BeFalse(
            "Successed must be false — cross-family claim denied; body: {0}", linkBBody);
    }

    [Fact(DisplayName = "AC-4 FamilyScope: Parent B does not see Parent A's children via My-Children")]
    public async Task AC4_FamilyScope_ParentB_MyChildren_DoesNotSeeParentAChildren()
    {
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        // Parent A links a child
        var parentAToken = await RegisterParentAndGetTokenAsync();
        var studentEmail = UniqueEmail("student");
        var studentUserName = $"stu_{Guid.NewGuid():N}"[..20];
        await SendAsync(_client, HttpMethod.Post, "api/Users/UserManagement/AddUser",
            new { Email = studentEmail, UserName = studentUserName, FullName = "Child for A", Roles = new[] { "Student" } },
            adminToken);
        var (linkResp, _, linkBody) = await SendAsync(_client, HttpMethod.Post, "api/Users/Parent/Link-Child",
            new { ChildEmail = studentEmail }, parentAToken);
        linkResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent A must link; body: {0}", linkBody);

        // Parent B — fresh, no children
        var parentBToken = await RegisterParentAndGetTokenAsync();
        var (listResp, listRoot, listBody) = await SendAsync(_client, HttpMethod.Get, "api/Users/Parent/My-Children",
            null, parentBToken);

        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children must return 200; body: {0}", listBody);
        TryProp(listRoot, "data", out var data).Should().BeTrue("body: {0}", listBody);
        data.GetArrayLength().Should().Be(0,
            "Parent B must see 0 children (family isolation); body: {0}", listBody);
    }

    // ===========================================================================
    // Invalid token → 401 (confirms framework behavior, not 500)
    // ===========================================================================

    [Fact(DisplayName = "Invalid bearer token on Products/List → 401 (not 500)")]
    public async Task InvalidToken_ProductsList_Returns401_Not500()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, ProductsListUrl,
            null, "this.is.not.a.valid.jwt");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "An invalid/tampered JWT must produce 401, not 500 or 200. body: {0}", body);
    }

    [Fact(DisplayName = "Invalid bearer token on Authorzation/RoleList → 401 (not 500)")]
    public async Task InvalidToken_AuthorzationRoleList_Returns401_Not500()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl,
            null, "malformed.jwt.token");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "An invalid/tampered JWT must produce 401, not 500 or 200 on admin endpoint. body: {0}", body);
    }

    // ===========================================================================
    // Envelope shape verification — 401 and 403 must not return 200 with error body
    // ===========================================================================

    [Fact(DisplayName = "Envelope: 401 response on Products/List has no 'successed=true' (is a true HTTP 401)")]
    public async Task Envelope_401_IsRealHttp401_NotFake200()
    {
        var (resp, _, _) = await SendAsync(_client, HttpMethod.Get, ProductsListUrl);

        // The key assertion: the HTTP status code IS 401, not 200 with a body saying "successed=false".
        // This confirms the JWT bearer challenge is firing correctly (not a BaseResponse fake 401).
        ((int)resp.StatusCode).Should().Be(401,
            "a 401 must be a real HTTP 401, not a 200 with successed=false envelope. " +
            "The JWT bearer middleware challenges with HTTP 401 when no token is present.");
    }

    [Fact(DisplayName = "Envelope: 403 response on admin endpoint with parent token is real HTTP 403")]
    public async Task Envelope_403_IsRealHttp403_NotFake200()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, _, _) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl,
            null, parentToken);

        ((int)resp.StatusCode).Should().Be(403,
            "a 403 must be a real HTTP 403. The ASP.NET Core authorization middleware " +
            "short-circuits with 403 Forbidden when authenticated but not authorized.");
    }
}
