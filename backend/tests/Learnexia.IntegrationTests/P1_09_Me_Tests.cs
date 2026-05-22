using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P1-09 integration tests: GET /api/Users/Me
///
/// Endpoint: [Authorize] GET /api/Users/Me
/// Contract:  BaseResponse&lt;MeResponse&gt; — HTTP 200 on success, HTTP 401 without a valid JWT.
/// MeResponse shape: { id, roles[], fullName, preferredLanguage, isFirstLogin, hasChildren }
///
/// Design notes:
///   - No id parameter exists on this route: the handler resolves the caller from the JWT
///     (ICurrentUserService.UserId). This means there is no IDOR surface to test — only the
///     "caller-scoped" guarantee that Me returns data for the token-holder, not any other user.
///   - The caller's identity is self-asserted by the JWT claims; the test confirms the returned
///     id/roles match what was obtained at registration time.
///   - Sensitive fields (PasswordHash, SecurityStamp, ConcurrencyStamp, tokens, etc.) must not
///     appear anywhere in the JSON body.
///   - HasChildren transitions from false (fresh parent) to true after a successful Add-Child.
///
/// Acceptance criteria covered:
///   AC-Auth    Anonymous → 401; no body leak on 401
///   AC-Shape   200 envelope has all BaseResponse keys: statusCode, successed, message, data, errors
///   AC-Shape   data has all MeResponse keys: id, roles, fullName, preferredLanguage, isFirstLogin, hasChildren
///   AC-Scoped  Caller A's Me returns A's id/roles, not any other user's
///   AC-NoIDOR  Route has no id query-param — there is no surface to request another user's profile
///   AC-Role-P  Freshly-registered parent → roles includes "Parent"
///   AC-Role-A  Seeded superadmin → roles includes "Admin" and/or "SuperAdmin"
///   AC-First   Freshly-registered parent → isFirstLogin=true (RegistrationIsCompleted=false)
///   AC-Kids-0  Freshly-registered parent → hasChildren=false
///   AC-Kids-1  Parent who added a child via Add-Child → hasChildren=true
///   AC-Safe    Password hash and other sensitive internal fields are NOT in the response body
///   AC-NoQuery Route does NOT accept ?userId=... (extra query params are silently ignored but
///              the endpoint never reads them; confirmed by sending a crafted ?userId query)
/// </summary>
[Collection("IntegrationTests")]
public sealed class P1_09_Me_Tests : IAsyncLifetime
{
    // ---------------------------------------------------------------------------
    // URLs
    // ---------------------------------------------------------------------------
    private const string MeUrl = "api/Users/Me";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl = "api/Users/Parent/Add-Child";

    // Seeded accounts
    private const string SuperAdminUserName = "superadmin";
    private const string SuperAdminPassword = "123Pa$$word!";

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_09_Me_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static string UniqueEmail(string tag = "")
        => $"p109_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@me.test";

    /// <summary>
    /// Case-insensitive property lookup: the project has two JSON serialisation paths —
    /// controller responses (Newtonsoft, camelCase) and the 422/middleware path (System.Text.Json,
    /// PascalCase). This helper handles both transparently.
    /// </summary>
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
        SendAsync(HttpClient client, HttpMethod method, string url, object? body = null, string? bearerToken = null)
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
            catch { /* non-JSON body; leave root default */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>
    /// Registers a new parent and returns (accessToken, userId).
    /// userId is parsed from the data.userId field of the registration response.
    /// </summary>
    private async Task<(string Token, int UserId)> RegisterParentAsync(
        string email, string password = "Str0ng@Pass", string? fullName = null)
    {
        var payload = fullName is not null
            ? (object)new { Email = email, Password = password, FullName = fullName }
            : new { Email = email, Password = password };

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl, payload);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", body);
        TryProp(data, "userId", out var userIdProp).Should().BeTrue("body: {0}", body);
        return (tokenProp.GetString()!, userIdProp.GetInt32());
    }

    private async Task<string> SignInAndGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetMeAsync(string bearerToken)
        => SendAsync(_client, HttpMethod.Get, MeUrl, null, bearerToken);

    private async Task AddChildAsync(string parentToken, string childEmail)
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName = "Test Child P109",
                Email = childEmail,
                Password = "Child@Pass1",
                Grade = 3,
                Language = "ar",
                Country = "EG"
            },
            parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Add-Child prerequisite must succeed; body: {0}", body);
    }

    // ===========================================================================
    // AC-Auth: anonymous caller → 401
    // ===========================================================================

    [Fact(DisplayName = "AC-Auth: GET /api/Users/Me without token → 401 Unauthorized")]
    public async Task Auth_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, MeUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Me without a bearer token must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-Auth: GET /api/Users/Me with garbage token → 401 Unauthorized")]
    public async Task Auth_InvalidToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, MeUrl,
            null, "this.is.not.a.valid.jwt");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Me with an invalid bearer token must return 401; body: {0}", body);
    }

    // ===========================================================================
    // AC-Shape: 200 envelope shape (all BaseResponse keys present)
    // ===========================================================================

    [Fact(DisplayName = "AC-Shape: authenticated Me → 200 with full BaseResponse envelope")]
    public async Task Shape_Authenticated_Returns200_WithBaseResponseEnvelope()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("shape"));

        var (resp, root, body) = await GetMeAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Me with a valid token must return 200; body: {0}", body);

        // All BaseResponse envelope keys
        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue(
            "envelope must have 'statusCode'; body: {0}", body);
        statusCodeProp.GetInt32().Should().Be(200,
            "statusCode in envelope must be 200; body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "envelope must have 'successed'; body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true for an authenticated user; body: {0}", body);

        TryProp(root, "message", out _).Should().BeTrue(
            "envelope must have 'message'; body: {0}", body);

        TryProp(root, "errors", out _).Should().BeTrue(
            "envelope must have 'errors'; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue(
            "envelope must have 'data'; body: {0}", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Null,
            "data must not be null on success; body: {0}", body);
    }

    [Fact(DisplayName = "AC-Shape: Me data has all MeResponse fields (id, roles, fullName, preferredLanguage, isFirstLogin, hasChildren)")]
    public async Task Shape_Data_HasAllMeResponseFields()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("fields"), fullName: "Fields Test Parent");

        var (resp, root, body) = await GetMeAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // id
        TryProp(data, "id", out var idProp).Should().BeTrue(
            "data must contain 'id'; body: {0}", body);
        idProp.GetInt32().Should().BeGreaterThan(0,
            "id must be a positive integer; body: {0}", body);

        // roles (array)
        TryProp(data, "roles", out var rolesProp).Should().BeTrue(
            "data must contain 'roles'; body: {0}", body);
        rolesProp.ValueKind.Should().Be(JsonValueKind.Array,
            "roles must be a JSON array; body: {0}", body);

        // fullName
        TryProp(data, "fullName", out _).Should().BeTrue(
            "data must contain 'fullName'; body: {0}", body);

        // preferredLanguage
        TryProp(data, "preferredLanguage", out _).Should().BeTrue(
            "data must contain 'preferredLanguage'; body: {0}", body);

        // isFirstLogin
        TryProp(data, "isFirstLogin", out var isFirstLoginProp).Should().BeTrue(
            "data must contain 'isFirstLogin'; body: {0}", body);
        new[] { JsonValueKind.True, JsonValueKind.False }.Should().Contain(isFirstLoginProp.ValueKind,
            "isFirstLogin must be a boolean; body: {0}", body);

        // hasChildren
        TryProp(data, "hasChildren", out var hasChildrenProp).Should().BeTrue(
            "data must contain 'hasChildren'; body: {0}", body);
        new[] { JsonValueKind.True, JsonValueKind.False }.Should().Contain(hasChildrenProp.ValueKind,
            "hasChildren must be a boolean; body: {0}", body);
    }

    // ===========================================================================
    // AC-Scoped: caller-scoped — Me returns the token-holder's own data
    // ===========================================================================

    [Fact(DisplayName = "AC-Scoped: Me returns the authenticated caller's own id (not another user's)")]
    public async Task Scoped_MeReturnsCallerOwnId()
    {
        // Register two distinct parents
        var (tokenA, userIdA) = await RegisterParentAsync(UniqueEmail("userA"));
        var (tokenB, userIdB) = await RegisterParentAsync(UniqueEmail("userB"));

        // Each token should only return its own id
        var (respA, rootA, bodyA) = await GetMeAsync(tokenA);
        respA.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyA);
        TryProp(rootA, "data", out var dataA).Should().BeTrue("body: {0}", bodyA);
        TryProp(dataA, "id", out var idA).Should().BeTrue("body: {0}", bodyA);
        idA.GetInt32().Should().Be(userIdA,
            "Me with token A must return A's id ({0}), not any other user's; body: {1}", userIdA, bodyA);

        var (respB, rootB, bodyB) = await GetMeAsync(tokenB);
        respB.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyB);
        TryProp(rootB, "data", out var dataB).Should().BeTrue("body: {0}", bodyB);
        TryProp(dataB, "id", out var idB).Should().BeTrue("body: {0}", bodyB);
        idB.GetInt32().Should().Be(userIdB,
            "Me with token B must return B's id ({0}), not any other user's; body: {1}", userIdB, bodyB);

        // Sanity: the two ids must be different
        idA.GetInt32().Should().NotBe(idB.GetInt32(),
            "the two distinct parents must have different ids");
    }

    [Fact(DisplayName = "AC-NoIDOR: GET /api/Users/Me?userId=<other-id> is ignored — returns caller's own data")]
    public async Task NoIDOR_QueryParamUserId_IsIgnored_ReturnsCaller()
    {
        // Register two parents; confirm that passing the other parent's id as a query string
        // does NOT make Me return the other user's profile.
        var (tokenA, userIdA) = await RegisterParentAsync(UniqueEmail("idorA"));
        var (_, userIdB) = await RegisterParentAsync(UniqueEmail("idorB"));

        // Token A calls Me with ?userId=<userIdB> — the route has no id parameter;
        // any extra query string must be ignored (not model-bound).
        var urlWithQuery = $"{MeUrl}?userId={userIdB}";
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, urlWithQuery, null, tokenA);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Me with extra query param must still return 200 for the authenticated caller; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "id", out var returnedId).Should().BeTrue("body: {0}", body);

        returnedId.GetInt32().Should().Be(userIdA,
            "Me must return the JWT-holder's id ({0}) even when ?userId={1} is present in the URL; body: {2}",
            userIdA, userIdB, body);
    }

    // ===========================================================================
    // AC-Role-P: freshly-registered parent → roles contains "Parent"
    // ===========================================================================

    [Fact(DisplayName = "AC-Role-P: freshly-registered parent → Me.roles includes 'Parent'")]
    public async Task Role_Parent_IsPresent_ForNewlyRegisteredParent()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("rolep"));

        var (resp, root, body) = await GetMeAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "roles", out var rolesProp).Should().BeTrue("body: {0}", body);

        var roles = rolesProp.EnumerateArray()
            .Select(r => r.GetString())
            .ToList();

        roles.Should().ContainSingle(r => r != null && r.Equals("Parent", StringComparison.OrdinalIgnoreCase),
            "a freshly-registered parent must have exactly the 'Parent' role in Me.roles; roles found: [{0}]; body: {1}",
            string.Join(", ", roles), body);
    }

    // ===========================================================================
    // AC-Role-A: seeded superadmin → roles contains "Admin" and/or "SuperAdmin"
    // ===========================================================================

    [Fact(DisplayName = "AC-Role-A: seeded superadmin → Me.roles includes 'Admin' and 'SuperAdmin'")]
    public async Task Role_SuperAdmin_HasAdminAndSuperAdminRoles()
    {
        var adminToken = await SignInAndGetTokenAsync(SuperAdminUserName, SuperAdminPassword);

        var (resp, root, body) = await GetMeAsync(adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "roles", out var rolesProp).Should().BeTrue("body: {0}", body);

        var roles = rolesProp.EnumerateArray()
            .Select(r => r.GetString())
            .ToList();

        roles.Should().Contain(r => r != null && r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase),
            "seeded superadmin must have 'SuperAdmin' in roles; roles found: [{0}]; body: {1}",
            string.Join(", ", roles), body);

        roles.Should().Contain(r => r != null && r.Equals("Admin", StringComparison.OrdinalIgnoreCase),
            "seeded superadmin must also have 'Admin' in roles; roles found: [{0}]; body: {1}",
            string.Join(", ", roles), body);
    }

    // ===========================================================================
    // AC-First: freshly-registered parent → isFirstLogin = true
    // ===========================================================================

    [Fact(DisplayName = "AC-First: freshly-registered parent → Me.isFirstLogin = true (onboarding not yet complete)")]
    public async Task IsFirstLogin_FreshParent_IsTrue()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("first"));

        var (resp, root, body) = await GetMeAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "isFirstLogin", out var isFirstLogin).Should().BeTrue("body: {0}", body);

        isFirstLogin.GetBoolean().Should().BeTrue(
            "a freshly-registered parent has not completed onboarding yet, so isFirstLogin must be true; body: {0}", body);
    }

    // ===========================================================================
    // AC-Kids-0: freshly-registered parent → hasChildren = false
    // ===========================================================================

    [Fact(DisplayName = "AC-Kids-0: freshly-registered parent with no children → Me.hasChildren = false")]
    public async Task HasChildren_FreshParent_IsFalse()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("nochild"));

        var (resp, root, body) = await GetMeAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "hasChildren", out var hasChildren).Should().BeTrue("body: {0}", body);

        hasChildren.GetBoolean().Should().BeFalse(
            "a freshly-registered parent with no linked children must have hasChildren=false; body: {0}", body);
    }

    // ===========================================================================
    // AC-Kids-1: parent who added a child → hasChildren = true
    // ===========================================================================

    [Fact(DisplayName = "AC-Kids-1: parent who added a child via Add-Child → Me.hasChildren = true")]
    public async Task HasChildren_AfterAddChild_IsTrue()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("withchild"));
        var childEmail = UniqueEmail("child");

        // Add a child (auto-links to parent)
        await AddChildAsync(token, childEmail);

        // Now call Me
        var (resp, root, body) = await GetMeAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "hasChildren", out var hasChildren).Should().BeTrue("body: {0}", body);

        hasChildren.GetBoolean().Should().BeTrue(
            "after adding a child, Me.hasChildren must become true; body: {0}", body);
    }

    // ===========================================================================
    // AC-Safe: sensitive fields absent from response body
    // ===========================================================================

    [Fact(DisplayName = "AC-Safe: Me response body does NOT contain sensitive internal fields")]
    public async Task Safe_SensitiveFields_NotPresent_InResponseBody()
    {
        var password = "Str0ng@Pass";
        var (token, _) = await RegisterParentAsync(UniqueEmail("safe"), password: password);

        var (resp, _, body) = await GetMeAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // Password value itself must never appear
        body.Should().NotContainEquivalentOf(password,
            "the user's plaintext password must never appear in the Me response; body: {0}", body);

        // Well-known sensitive field names must not be present
        body.Should().NotContainEquivalentOf("passwordHash",
            "PasswordHash must not be serialized in the Me response; body: {0}", body);
        body.Should().NotContainEquivalentOf("securityStamp",
            "SecurityStamp must not be in the Me response; body: {0}", body);
        body.Should().NotContainEquivalentOf("concurrencyStamp",
            "ConcurrencyStamp must not be in the Me response; body: {0}", body);
        body.Should().NotContainEquivalentOf("normalizedEmail",
            "NormalizedEmail must not be in the Me response; body: {0}", body);
        body.Should().NotContainEquivalentOf("normalizedUserName",
            "NormalizedUserName must not be in the Me response; body: {0}", body);
        body.Should().NotContainEquivalentOf("lockoutEnd",
            "LockoutEnd must not be in the Me response; body: {0}", body);
    }

    // ===========================================================================
    // AC-Shape: data.roles is an array (even for a user with one role)
    // ===========================================================================

    [Fact(DisplayName = "AC-Shape: Me.data.roles is always a JSON array (not null, not a string)")]
    public async Task Shape_Roles_IsArray()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("rolesarr"));

        var (resp, root, body) = await GetMeAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "roles", out var rolesProp).Should().BeTrue("body: {0}", body);

        rolesProp.ValueKind.Should().Be(JsonValueKind.Array,
            "Me.data.roles must be a JSON array (not null, not a string); body: {0}", body);
        rolesProp.GetArrayLength().Should().BeGreaterThan(0,
            "roles must have at least one entry for any signed-in user; body: {0}", body);
    }

    // ===========================================================================
    // AC-Scoped: Me returns caller's fullName when set at registration
    // ===========================================================================

    [Fact(DisplayName = "AC-Scoped: Me returns the caller's fullName as registered")]
    public async Task Scoped_Me_Returns_CorrectFullName()
    {
        var expectedFullName = "Integration Test Parent";
        var (token, _) = await RegisterParentAsync(UniqueEmail("fname"), fullName: expectedFullName);

        var (resp, root, body) = await GetMeAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "fullName", out var fullNameProp).Should().BeTrue("body: {0}", body);

        fullNameProp.GetString().Should().Be(expectedFullName,
            "Me.data.fullName must match the fullName provided at registration; body: {0}", body);
    }

    // ===========================================================================
    // AC-Scoped: superadmin Me returns admin's own id (not a parent's id)
    // ===========================================================================

    [Fact(DisplayName = "AC-Scoped: superadmin Me returns different id than a parent's Me")]
    public async Task Scoped_SuperAdmin_Me_ReturnsAdminId_NotParentId()
    {
        var (parentToken, parentId) = await RegisterParentAsync(UniqueEmail("adminscope"));
        var adminToken = await SignInAndGetTokenAsync(SuperAdminUserName, SuperAdminPassword);

        var (adminResp, adminRoot, adminBody) = await GetMeAsync(adminToken);
        adminResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", adminBody);
        TryProp(adminRoot, "data", out var adminData).Should().BeTrue("body: {0}", adminBody);
        TryProp(adminData, "id", out var adminId).Should().BeTrue("body: {0}", adminBody);

        adminId.GetInt32().Should().NotBe(parentId,
            "the superadmin's Me must return the admin's id, not the parent's id; body: {0}", adminBody);
    }

    // ===========================================================================
    // AC-Kids-1: hasChildren stays false for a different parent after Add-Child by parent A
    // ===========================================================================

    [Fact(DisplayName = "AC-Kids-0b: parent B's Me.hasChildren is false even after parent A adds a child")]
    public async Task HasChildren_ParentB_StaysFalse_WhenParentAHasChild()
    {
        var (tokenA, _) = await RegisterParentAsync(UniqueEmail("parentA"));
        var (tokenB, _) = await RegisterParentAsync(UniqueEmail("parentB"));

        // Parent A adds a child
        await AddChildAsync(tokenA, UniqueEmail("child"));

        // Parent B's Me must still show hasChildren=false
        var (resp, root, body) = await GetMeAsync(tokenB);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "hasChildren", out var hasChildren).Should().BeTrue("body: {0}", body);

        hasChildren.GetBoolean().Should().BeFalse(
            "parent B's hasChildren must remain false when only parent A has added a child; body: {0}", body);
    }
}
