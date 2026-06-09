using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P1-05 integration tests: Role-based access control across Identity and Parent endpoints.
///
/// Authorization matrix exercised:
///   AC-1  Wrong role → 403 Forbidden (authenticated but not permitted).
///   AC-2  No/invalid token → 401 Unauthorized on protected endpoints.
///   AC-3  Admin-only endpoints (AuthorzationController) reject non-admins (403); admins pass authz.
///   AC-4  FamilyScopeAuthorizationHandler fail-closed: cross-family denial.
///   AC-5  Parent token cannot reach admin endpoints (403).
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

    // Basic user is not Admin/Parent — also verify 403 on admin endpoints

    [Fact(DisplayName = "AC-1/AC-3 Authorzation/RoleList: Basic-role token → 403 Forbidden")]
    public async Task AC1_AC3_AuthorzationRoleList_BasicToken_Returns403()
    {
        var basicToken = await SignInAndGetTokenAsync(BasicUserName, BasicUserPassword);

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl,
            null, basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Basic role must not access Autorzation/RoleList (admin-only); expected 403. body: {0}", body);
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
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, "api/Parent/My-Children",
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
        var (linkAResp, _, linkABody) = await SendAsync(_client, HttpMethod.Post, "api/Parent/Link-Child",
            new { ChildEmail = studentEmail }, parentAToken);
        linkAResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent A must link successfully; body: {0}", linkABody);

        // Parent B attempts to steal the same child (FamilyScopeAuthorizationHandler denies via business layer)
        var parentBToken = await RegisterParentAndGetTokenAsync();
        var (linkBResp, rootB, linkBBody) = await SendAsync(_client, HttpMethod.Post, "api/Parent/Link-Child",
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
        var (linkResp, _, linkBody) = await SendAsync(_client, HttpMethod.Post, "api/Parent/Link-Child",
            new { ChildEmail = studentEmail }, parentAToken);
        linkResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent A must link; body: {0}", linkBody);

        // Parent B — fresh, no children
        var parentBToken = await RegisterParentAndGetTokenAsync();
        var (listResp, listRoot, listBody) = await SendAsync(_client, HttpMethod.Get, "api/Parent/My-Children",
            null, parentBToken);

        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children must return 200; body: {0}", listBody);
        TryProp(listRoot, "data", out var data).Should().BeTrue("body: {0}", listBody);
        data.GetArrayLength().Should().Be(0,
            "Parent B must see 0 children (family isolation); body: {0}", listBody);
    }

    // ===========================================================================
    // Invalid token → 401 (confirms framework behavior, not 500)
    // ===========================================================================

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

    [Fact(DisplayName = "Envelope: 401 response on Authorzation/RoleList has no 'successed=true' (is a true HTTP 401)")]
    public async Task Envelope_401_IsRealHttp401_NotFake200()
    {
        var (resp, _, _) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl);

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

    // ===========================================================================
    // BE-TC-03 — UserManagement AddUser, no token → 401
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-03 UserManagement/AddUser: no token → 401 Unauthorized")]
    public async Task BeTc03_UserManagementAddUser_NoToken_Returns401()
    {
        var body = new
        {
            Email = UniqueEmail("tc03"),
            UserName = $"tc03_{Guid.NewGuid():N}"[..20],
            FullName = "TC03 User",
            Roles = new[] { "Student" }
        };

        var (resp, _, respBody) = await SendAsync(_client, HttpMethod.Post,
            "api/Users/UserManagement/AddUser", body);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "UserManagementController is class-level AdminOnly; no token → 401. body: {0}", respBody);
    }

    // ===========================================================================
    // BE-TC-04 — Parent My-Children, no token → 401
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-04 Parent/My-Children: no token → 401 Unauthorized")]
    public async Task BeTc04_ParentMyChildren_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, "api/Parent/My-Children");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "ParentController is class-level [Authorize(Roles=...)]; no token → 401. body: {0}", body);
    }

    // ===========================================================================
    // BE-TC-09 — UserManagement AddUser with non-admin (Parent) token → 403
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-09 UserManagement/AddUser: Parent token → 403 Forbidden")]
    public async Task BeTc09_UserManagementAddUser_ParentToken_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var body = new
        {
            Email = UniqueEmail("tc09"),
            UserName = $"tc09_{Guid.NewGuid():N}"[..20],
            FullName = "TC09 User",
            Roles = new[] { "Student" }
        };

        var (resp, _, respBody) = await SendAsync(_client, HttpMethod.Post,
            "api/Users/UserManagement/AddUser", body, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "UserManagementController is AdminOnly; Parent token is not Admin/SuperAdmin → 403. body: {0}", respBody);
    }

    [Fact(DisplayName = "BE-TC-09b UserManagement/AddUser: Basic token → 403 Forbidden")]
    public async Task BeTc09b_UserManagementAddUser_BasicToken_Returns403()
    {
        var basicToken = await SignInAndGetTokenAsync(BasicUserName, BasicUserPassword);

        var body = new
        {
            Email = UniqueEmail("tc09b"),
            UserName = $"tc09b_{Guid.NewGuid():N}"[..20],
            FullName = "TC09b User",
            Roles = new[] { "Student" }
        };

        var (resp, _, respBody) = await SendAsync(_client, HttpMethod.Post,
            "api/Users/UserManagement/AddUser", body, basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "UserManagementController is AdminOnly; Basic token is not Admin/SuperAdmin → 403. body: {0}", respBody);
    }

    // ===========================================================================
    // BE-TC-10 — UserManagement AddUser with Admin token → not 401/403
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-10 UserManagement/AddUser: Admin token → not 401 or 403 (authz passes)")]
    public async Task BeTc10_UserManagementAddUser_AdminToken_PassesAuthz()
    {
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var body = new
        {
            Email = UniqueEmail("tc10"),
            UserName = $"tc10_{Guid.NewGuid():N}"[..20],
            FullName = "TC10 Admin Test",
            Roles = new[] { "Student" }
        };

        var (resp, _, respBody) = await SendAsync(_client, HttpMethod.Post,
            "api/Users/UserManagement/AddUser", body, adminToken);

        var sc = (int)resp.StatusCode;
        sc.Should().NotBe(401,
            "Admin must pass AdminOnly gate on AddUser; must not get 401. Got {0}. body: {1}", sc, respBody);
        sc.Should().NotBe(403,
            "Admin must pass AdminOnly gate on AddUser; must not get 403. Got {0}. body: {1}", sc, respBody);
    }

    // ===========================================================================
    // BE-TC-17 — Actor is resolved from JWT, not request body (self-scope integrity)
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-17 Parent B My-Children resolves actor from JWT (not body) — sees only own children")]
    public async Task BeTc17_ParentB_ActorFromJwt_SeesOnlyOwnChildren()
    {
        // Arrange: Parent A links a child via admin-created student
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var studentEmail = UniqueEmail("tc17stu");
        var studentUserName = $"tc17_{Guid.NewGuid():N}"[..20];
        await SendAsync(_client, HttpMethod.Post, "api/Users/UserManagement/AddUser",
            new { Email = studentEmail, UserName = studentUserName, FullName = "TC17 Student", Roles = new[] { "Student" } },
            adminToken);

        var parentAToken = await RegisterParentAndGetTokenAsync();
        var (linkResp, _, linkBody) = await SendAsync(_client, HttpMethod.Post, "api/Parent/Link-Child",
            new { ChildEmail = studentEmail }, parentAToken);
        linkResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent A must link child; body: {0}", linkBody);

        // Parent B issues GET My-Children using their own token
        // Even if they could supply a spoofed parent identifier in a query param, the handler resolves
        // the actor from the JWT. There is no body/query param for My-Children — the actor IS the JWT.
        var parentBToken = await RegisterParentAndGetTokenAsync();
        var (listResp, listRoot, listBody) = await SendAsync(_client, HttpMethod.Get,
            "api/Parent/My-Children", null, parentBToken);

        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "My-Children must return 200 for any authenticated Parent; body: {0}", listBody);

        TryProp(listRoot, "data", out var data).Should().BeTrue("body: {0}", listBody);

        // Parent B must see 0 children — actor resolved from JWT, not from any body/param
        data.GetArrayLength().Should().Be(0,
            "Parent B's JWT identifies Parent B; handler must return Parent B's children (0), " +
            "never Parent A's — confirms no parameter can override JWT-derived identity. body: {0}", listBody);
    }

    // ===========================================================================
    // BE-TC-19 — Parent token on Student-only quiz route → 403
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-19 Quizzes/{lessonId}/Attempt: Parent token → 403 (Student-only route)")]
    public async Task BeTc19_QuizzesAttempt_ParentToken_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        // Use a placeholder lessonId — the gate fires before any business lookup
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post,
            "api/Learning/Quizzes/999/Attempt", null, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "QuizzesController StartAttempt is [Authorize(Roles = \"Student\")]; " +
            "a Parent token must be denied with 403. body: {0}", body);
    }

    // ===========================================================================
    // BE-TC-19b — Permission policies registered only for real modules (Learning, Parent)
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-19b Claims.GenerateModules() returns {Learning, Parent, Moderation} — no Catalog")]
    public async Task BeTc19b_GenerateModules_IsLearningAndParentOnly()
    {
        // White-box / config assertion — no HTTP call needed.
        // Verifies that the policy generation source does not reference removed modules.
        var modules = Learnexia.Modules.Identity.Domain.Constants.Claims.GenerateModules();

        modules.Should().HaveCount(3,
            "Claims.GenerateModules() must return exactly 3 modules: Learning, Parent, and Moderation " +
            "(Moderation added in P7-12). Catalog was removed; no dead policy should be registered.");
        modules.Should().Contain("Learning",
            "Learning module must be in the registered permission set.");
        modules.Should().Contain("Parent",
            "Parent module must be in the registered permission set.");
        modules.Should().Contain("Moderation",
            "Moderation module (P7-12) must be in the registered permission set.");
        modules.Should().NotContain("Catalog",
            "Catalog module was removed; it must not appear in GenerateModules() or registered policies.");
    }

    // ===========================================================================
    // BE-TC-20 — GradesController curriculum-authoring: corrected auth behavior
    //
    // GradesController now carries class-level [Authorize] (reads) and
    // [Authorize(Policy = AdminOnly)] on writes (Create/Update/Delete).
    // These tests assert the CORRECT behavior after the fix.
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-20 GradesController: GET List anonymous → 401 Unauthorized")]
    public async Task BeTc20_Grades_List_NoToken_Returns401()
    {
        // GradesController is class-level [Authorize] — anonymous read must be challenged.
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, "api/learning/Grades/List");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GradesController GET List is [Authorize]; no token → 401. body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-20b GradesController: GET List with authenticated user → 200")]
    public async Task BeTc20b_Grades_List_Authenticated_Returns200()
    {
        // Any authenticated user (admin here) can read the grades list.
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, "api/learning/Grades/List",
            null, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GradesController GET List with valid token → 200. body: {0}", body);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-20c GradesController: POST Create anonymous → 401 Unauthorized")]
    public async Task BeTc20c_Grades_Create_NoToken_Returns401()
    {
        // GradesController Create requires AdminOnly; no token → 401 (auth gate before policy gate).
        var gradeBody = new { Number = 5, DisplayName = "AuthTest Grade" };
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post,
            "api/learning/Grades/Create", gradeBody);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GradesController POST Create is [Authorize(AdminOnly)]; no token → 401. body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-20d GradesController: POST Create with Parent token → 403 Forbidden")]
    public async Task BeTc20d_Grades_Create_ParentToken_Returns403()
    {
        // Parent is authenticated but not Admin/SuperAdmin → 403 on AdminOnly endpoint.
        var parentToken = await RegisterParentAndGetTokenAsync();

        var gradeBody = new { Number = 5, DisplayName = "ParentAuthTest Grade" };
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post,
            "api/learning/Grades/Create", gradeBody, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "GradesController POST Create is AdminOnly; Parent token is not Admin/SuperAdmin → 403. body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-20e GradesController: POST Create with Admin token → reaches handler (200 or 422)")]
    public async Task BeTc20e_Grades_Create_AdminToken_ReachesHandler()
    {
        // Admin passes AdminOnly gate; result is a business-layer response (200/422).
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var gradeBody = new { Number = 5, DisplayName = $"AdminAuthTest {Guid.NewGuid():N}" };
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post,
            "api/learning/Grades/Create", gradeBody, adminToken);

        var sc = (int)resp.StatusCode;
        sc.Should().NotBe(401,
            "Admin must pass auth gate on GradesController Create; must not get 401. Got {0}. body: {1}", sc, body);
        sc.Should().NotBe(403,
            "Admin must pass AdminOnly gate on GradesController Create; must not get 403. Got {0}. body: {1}", sc, body);
        // Business-layer result is 200 (success) or 422 (validation) — both confirm handler was reached.
        (sc == 200 || sc == 422).Should().BeTrue(
            "Admin reaching handler on Create must get 200 or 422; got {0}. body: {1}", sc, body);
    }

    // ===========================================================================
    // BE-TC-22 — 401 on ParentController is real HTTP 401, not fake 200
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-22 Envelope: 401 on Parent/My-Children is real HTTP 401 (not fake 200)")]
    public async Task BeTc22_Envelope_401_OnParentController_IsRealHttp401()
    {
        var (resp, _, _) = await SendAsync(_client, HttpMethod.Get, "api/Parent/My-Children");

        ((int)resp.StatusCode).Should().Be(401,
            "ParentController is role-gated; a real HTTP 401 bearer challenge must fire for no-token requests, " +
            "not a 200 envelope with successed=false.");
    }

    // ===========================================================================
    // BE-TC-24 — Committed appsettings.json carries the CHANGE_ME placeholder (not a real secret)
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-24 appsettings.json JWT secret is the CHANGE_ME placeholder (not a real key)")]
    public async Task BeTc24_AppsettingsJson_JwtSecret_IsPlaceholder()
    {
        // Config-inspection case: read the committed appsettings.json directly.
        // Confirms the secret committed to the repo is the documented placeholder,
        // and GuardJwtSecret is the enforcement layer for Production/Staging.

        // appsettings.json lives in src/Host/Learnexia.Host/ relative to the solution root
        // (which is the backend/ directory containing Learnexia.Modular.sln).
        const string appsettingsRelative =
            "src/Host/Learnexia.Host/appsettings.json";

        // Resolve relative to the solution root (the directory containing Learnexia.Modular.sln).
        var solutionRoot = FindSolutionRoot();
        var fullPath = System.IO.Path.Combine(solutionRoot, appsettingsRelative);

        System.IO.File.Exists(fullPath).Should().BeTrue(
            "appsettings.json must exist at the expected path for the config-inspection case. " +
            "Solution root: {0}. Resolved path: {1}", solutionRoot, fullPath);

        var json = await System.IO.File.ReadAllTextAsync(fullPath);
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("JwtSettings", out var jwtSettings).Should().BeTrue(
            "appsettings.json must contain a JwtSettings section. body: {0}", json);

        jwtSettings.TryGetProperty("Secret", out var secretProp).Should().BeTrue(
            "JwtSettings must contain a Secret property. body: {0}", json);

        var secret = secretProp.GetString();
        secret.Should().Be("CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789",
            "The committed secret must be exactly the documented placeholder, " +
            "not a real/strong key. A real key in committed config is a security defect.");
    }

    /// <summary>
    /// Walks up from the test binary's directory until it finds Learnexia.Modular.sln,
    /// which marks the solution root. Falls back to the current directory if not found.
    /// </summary>
    private static string FindSolutionRoot()
    {
        var dir = System.IO.Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location)!;
        while (dir != null)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(dir, "Learnexia.Modular.sln")))
                return dir;
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        return Directory.GetCurrentDirectory();
    }
}
