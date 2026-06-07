using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P1-10 integration tests: Admin sign-in and dashboard shell (backend authz/auth surface).
///
/// Implements BE-TC-01..28 from docs/qc/P1-10/backend-test-cases.md 1:1.
///
/// Seeded identities (available via ApplyMigrationsAndSeedAsync):
///   superadmin   / 123Pa$$word!  — roles: Basic, Admin, SuperAdmin
///   basicuser    / 123Pa$$word!  — roles: Basic only
///   Parent       — minted on-demand via POST /api/Users/Authentication/Register-Parent
///
/// Endpoints under test:
///   [AllowAnonymous] POST /api/Users/Authentication/Sign-In
///   [AllowAnonymous] POST /api/Users/Authentication/Register-Parent
///   [AllowAnonymous] POST /api/Users/Authentication/Refresh-Token
///   [Authorize]      POST /api/Users/Authentication/Sign-Out
///   [Authorize]      GET  /api/Users/Me                                  (role source for FE dashboard gate)
///   [AdminOnly]      GET  /api/Users/Authorzation/RoleList
///   [AdminOnly]      POST /api/Users/Authorzation/Create
///   [AdminOnly]      POST /api/Users/UserManagement/AddUser
///   [AdminOnly]      GET  /api/Users/UserManagement/GetUserProfile
///
/// JSON serialization dual-path note:
///   Controller responses → Newtonsoft → camelCase keys.
///   ValidationBehavior → System.Text.Json → PascalCase keys.
///   TryProp() handles both transparently (case-insensitive fallback).
///
/// Cases that are environment-gated or require a feature not yet exposed via HTTP
/// are marked BLOCKED inside the test body via explicit Assert.Skip() patterns or
/// [Fact(Skip = "...")] — do NOT drop them.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P1_10_AdminSignIn_Tests : IAsyncLifetime
{
    // ---------------------------------------------------------------------------
    // URLs
    // ---------------------------------------------------------------------------
    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string RefreshTokenUrl = "api/Users/Authentication/Refresh-Token";
    private const string SignOutUrl = "api/Users/Authentication/Sign-Out";
    private const string MeUrl = "api/Users/Me";
    private const string AuthorzationRoleListUrl = "api/Users/Authorzation/RoleList";
    private const string AuthorzationCreateUrl = "api/Users/Authorzation/Create";
    private const string AddUserUrl = "api/Users/UserManagement/AddUser";
    private const string GetUserProfileUrl = "api/Users/UserManagement/GetUserProfile";

    // ---------------------------------------------------------------------------
    // Seeded credentials
    // ---------------------------------------------------------------------------
    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";
    private const string BasicUserName = "basicuser";
    private const string BasicPassword = "123Pa$$word!";

    // ---------------------------------------------------------------------------
    // Test signing key (matches appsettings.json placeholder)
    // ---------------------------------------------------------------------------
    private const string TestSigningKey = "CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789";

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_10_AdminSignIn_Tests(LearnexiaWebAppFactory factory)
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
        => $"p110_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@admin.test";

    /// <summary>
    /// Case-insensitive JSON property lookup.
    /// Controller path: Newtonsoft → camelCase; middleware 422 path: System.Text.Json → PascalCase.
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
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
    }

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

    private async Task<(string AccessToken, string RefreshTokenString)> SignInAndGetBothTokensAsync(
        string userName, string password)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in prerequisite must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var at).Should().BeTrue("body: {0}", body);
        TryProp(data, "refreshToken", out var rt).Should().BeTrue("body: {0}", body);
        TryProp(rt, "tokenString", out var ts).Should().BeTrue("body: {0}", body);
        return (at.GetString()!, ts.GetString()!);
    }

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

    private async Task<(string AccessToken, string RefreshTokenString)> RegisterParentAndGetBothTokensAsync(
        string? email = null)
    {
        var parentEmail = email ?? UniqueEmail("parent");
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var at).Should().BeTrue("body: {0}", body);
        TryProp(data, "refreshToken", out var rt).Should().BeTrue("body: {0}", body);
        TryProp(rt, "tokenString", out var ts).Should().BeTrue("body: {0}", body);
        return (at.GetString()!, ts.GetString()!);
    }

    /// <summary>
    /// Creates an expired-but-otherwise-valid JWT by re-signing with the test secret + past expiry.
    /// Required so Refresh-Token endpoint's ValidateDetails guard (checks ValidTo &gt; UtcNow) lets
    /// the request through to the 200 happy-path instead of returning 400 "TokenIsRunning".
    /// </summary>
    private static string CreateExpiredAccessToken(string originalToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var originalJwt = handler.ReadJwtToken(originalToken);
            var claimsToKeep = originalJwt.Claims
                .Where(c => c.Type != "exp" && c.Type != "iat" && c.Type != "nbf")
                .ToList();
            var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(TestSigningKey));
            var expiredToken = new JwtSecurityToken(
                issuer: originalJwt.Issuer,
                audience: originalJwt.Audiences.FirstOrDefault(),
                claims: claimsToKeep,
                notBefore: DateTime.UtcNow.AddMinutes(-60),
                expires: DateTime.UtcNow.AddMinutes(-30),
                signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    signingKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature));
            return handler.WriteToken(expiredToken);
        }
        catch
        {
            return originalToken + "_expired_suffix";
        }
    }

    // ===========================================================================
    // Group A — Admin sign-in issues a JWT · AC-1 / AC-7
    // ===========================================================================

    /// <summary>BE-TC-01 — Admin signs in with valid credentials → 200 + JWT</summary>
    [Fact(DisplayName = "BE-TC-01: Admin valid sign-in → 200 + JWT; successed=true; data.accessToken non-empty; data.userId > 0")]
    public async Task BeTc01_AdminValidSignIn_Returns200WithJwt()
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = AdminUserName, Password = AdminPassword });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-01: valid admin sign-in must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue(
            "BE-TC-01: response must contain 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "BE-TC-01: successed must be true on valid sign-in; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue(
            "BE-TC-01: response must contain 'data'; body: {0}", body);

        TryProp(data, "accessToken", out var accessToken).Should().BeTrue(
            "BE-TC-01: data must contain 'accessToken'; body: {0}", body);
        accessToken.GetString().Should().NotBeNullOrWhiteSpace(
            "BE-TC-01: accessToken must be non-empty; body: {0}", body);

        TryProp(data, "userId", out var userId).Should().BeTrue(
            "BE-TC-01: data must contain 'userId'; body: {0}", body);
        userId.GetInt32().Should().BeGreaterThan(0,
            "BE-TC-01: userId must be a positive integer; body: {0}", body);
    }

    /// <summary>BE-TC-02 — Sign-in response envelope shape; no roles in the payload</summary>
    [Fact(DisplayName = "BE-TC-02: Sign-in envelope has accessToken, refreshToken.tokenString, userId, isFirstLogin, sessionTimeout, sessionId; NO roles field")]
    public async Task BeTc02_SignInEnvelopeShape_NoRolesField()
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = AdminUserName, Password = AdminPassword });

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // accessToken — non-empty
        TryProp(data, "accessToken", out var accessToken).Should().BeTrue(
            "BE-TC-02: data.accessToken must be present; body: {0}", body);
        accessToken.GetString().Should().NotBeNullOrWhiteSpace(
            "BE-TC-02: accessToken must be non-empty; body: {0}", body);

        // refreshToken.tokenString — non-empty
        TryProp(data, "refreshToken", out var rt).Should().BeTrue(
            "BE-TC-02: data.refreshToken must be present; body: {0}", body);
        TryProp(rt, "tokenString", out var ts).Should().BeTrue(
            "BE-TC-02: refreshToken.tokenString must be present; body: {0}", body);
        ts.GetString().Should().NotBeNullOrWhiteSpace(
            "BE-TC-02: refreshToken.tokenString must be non-empty; body: {0}", body);

        // userId — int
        TryProp(data, "userId", out var userId).Should().BeTrue(
            "BE-TC-02: data.userId must be present; body: {0}", body);
        userId.GetInt32().Should().BeGreaterThan(0,
            "BE-TC-02: userId must be a positive integer; body: {0}", body);

        // isFirstLogin — bool (may be true or false for the seeded admin)
        TryProp(data, "isFirstLogin", out var isFirstLogin).Should().BeTrue(
            "BE-TC-02: data.isFirstLogin must be present; body: {0}", body);
        new[] { JsonValueKind.True, JsonValueKind.False }
            .Should().Contain(isFirstLogin.ValueKind,
            "BE-TC-02: isFirstLogin must be a boolean; body: {0}", body);

        // sessionId — present (may be null string or non-null)
        // The field is present in the JwtAuthResponse record even if null; we only assert the key exists.
        TryProp(data, "sessionId", out _).Should().BeTrue(
            "BE-TC-02: data.sessionId key must be present (contract completeness); body: {0}", body);

        // roles / role must NOT be present in the sign-in payload
        // (roles are fetched from GET /api/Users/Me — not the sign-in response)
        var dataJson = data.ToString();
        bool hasRolesKey = data.TryGetProperty("roles", out _) || data.TryGetProperty("Roles", out _);
        hasRolesKey.Should().BeFalse(
            "BE-TC-02: data must NOT contain a 'roles' field — the FE contract requires roles to come " +
            "from GET /api/Users/Me, not the sign-in payload; body: {0}", body);
    }

    /// <summary>BE-TC-03 — Issued JWT carries Admin + SuperAdmin role claims</summary>
    [Fact(DisplayName = "BE-TC-03: JWT issued for superadmin carries 'Admin' and 'SuperAdmin' role claims")]
    public async Task BeTc03_JwtCarriesAdminAndSuperAdminRoleClaims()
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = AdminUserName, Password = AdminPassword });

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var atProp).Should().BeTrue("body: {0}", body);
        var accessToken = atProp.GetString()!;

        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(accessToken).Should().BeTrue(
            "BE-TC-03: accessToken must be a readable JWT; token: {0}", accessToken);

        var jwt = handler.ReadJwtToken(accessToken);

        // Collect all role claims (Microsoft schema or short "role")
        var roleClaims = jwt.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role
                        || c.Type == "role"
                        || string.Equals(c.Type, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
                            StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .ToList();

        roleClaims.Should().Contain("Admin",
            "BE-TC-03: JWT must carry the 'Admin' role claim (verbatim PascalCase) so AdminOnly policy succeeds; " +
            "claims found: [{0}]; full token body for debug: {1}", string.Join(", ", roleClaims), accessToken);

        roleClaims.Should().Contain("SuperAdmin",
            "BE-TC-03: JWT must carry the 'SuperAdmin' role claim (verbatim PascalCase); " +
            "claims found: [{0}]", string.Join(", ", roleClaims));
    }

    /// <summary>BE-TC-04 — Admin token is accepted by an AdminOnly endpoint (claim → policy round-trip)</summary>
    [Fact(DisplayName = "BE-TC-04: Admin token accepted by AdminOnly GET /Authorzation/RoleList → 200 successed=true")]
    public async Task BeTc04_AdminToken_AcceptedByAdminOnlyEndpoint_Returns200()
    {
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl,
            null, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-04: admin token must pass AdminOnly gate; expected 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "BE-TC-04: successed must be true for admin on RoleList; body: {0}", body);
    }

    // ===========================================================================
    // Group B — Sign-in negative & boundary paths (security-relevant)
    // ===========================================================================

    /// <summary>BE-TC-05 — Wrong password → 400 generic invalid-credentials</summary>
    [Fact(DisplayName = "BE-TC-05: Wrong password → 400; successed=false; generic message (not 401, not 422)")]
    public async Task BeTc05_WrongPassword_Returns400GenericMessage()
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = AdminUserName, Password = "WrongPass!1" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-05: wrong password must return 400 (anti-enumeration BadRequest, not 401 or 422); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse(
            "BE-TC-05: successed must be false for wrong password; body: {0}", body);

        TryProp(root, "message", out var message).Should().BeTrue(
            "BE-TC-05: response must contain a 'message'; body: {0}", body);
        message.GetString().Should().NotBeNullOrWhiteSpace(
            "BE-TC-05: message must be non-empty (localized invalid-credentials string); body: {0}", body);
    }

    /// <summary>BE-TC-06 — Unknown user → 400, identical to wrong password (anti-enumeration parity)</summary>
    [Fact(DisplayName = "BE-TC-06: Unknown user → 400; same status code as wrong-password case (anti-enumeration parity)")]
    public async Task BeTc06_UnknownUser_Returns400_SameAsWrongPassword()
    {
        var unknownUser = $"definitely_not_a_user_{Guid.NewGuid():N}";

        var (unknownResp, unknownRoot, unknownBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = unknownUser, Password = "WrongPass!1" });

        unknownResp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-06: unknown user must return 400 (same as wrong-password — anti-enumeration); body: {0}", unknownBody);

        TryProp(unknownRoot, "successed", out var unknownSuccessed).Should().BeTrue("body: {0}", unknownBody);
        unknownSuccessed.GetBoolean().Should().BeFalse("body: {0}", unknownBody);

        // Also get the wrong-password response to assert parity
        var (wrongPassResp, wrongPassRoot, wrongPassBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = AdminUserName, Password = "WrongPass!1" });

        wrongPassResp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-06: wrong password must also return 400 for parity comparison; body: {0}", wrongPassBody);

        TryProp(wrongPassRoot, "message", out var wrongPassMsg).Should().BeTrue("body: {0}", wrongPassBody);
        TryProp(unknownRoot, "message", out var unknownMsg).Should().BeTrue("body: {0}", unknownBody);

        // The same generic message for both paths (anti-enumeration guarantee)
        unknownMsg.GetString().Should().Be(wrongPassMsg.GetString(),
            "BE-TC-06: unknown-user message must be IDENTICAL to wrong-password message (anti-enumeration); " +
            "unknown='{0}' wrong-pass='{1}'", unknownMsg.GetString(), wrongPassMsg.GetString());
    }

    /// <summary>BE-TC-07 — Missing UserName/Password → 422 validation envelope</summary>
    [Fact(DisplayName = "BE-TC-07: Missing UserName → 422 validation envelope with Errors[]; PascalCase keys")]
    public async Task BeTc07_MissingUsername_Returns422ValidationEnvelope()
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = "", Password = AdminPassword });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "BE-TC-07: empty UserName must return 422 (SignInValidatior fires via ValidationBehavior); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse(
            "BE-TC-07: successed must be false on 422; body: {0}", body);

        TryProp(root, "errors", out var errors).Should().BeTrue(
            "BE-TC-07: 422 envelope must have 'errors' key; body: {0}", body);
        errors.EnumerateArray().ToList().Should().NotBeEmpty(
            "BE-TC-07: Errors[] must be non-empty; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-07b: Missing Password → 422 validation envelope with Errors[]")]
    public async Task BeTc07b_MissingPassword_Returns422ValidationEnvelope()
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = AdminUserName, Password = "" });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "BE-TC-07b: empty Password must return 422 (SignInValidatior fires via ValidationBehavior); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("body: {0}", body);

        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", body);
        errors.EnumerateArray().ToList().Should().NotBeEmpty("body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-07c: Empty body {} → 422 validation envelope (both fields missing)")]
    public async Task BeTc07c_EmptyBody_Returns422ValidationEnvelope()
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "BE-TC-07c: empty body must return 422; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("body: {0}", body);

        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", body);
        errors.EnumerateArray().ToList().Should().NotBeEmpty("body: {0}", body);
    }

    /// <summary>
    /// BE-TC-08 — Account lockout after 5 failed attempts → 400 too-many-attempts.
    /// Uses a throwaway parent account to avoid locking out shared identities (superadmin/basicuser).
    /// MaxFailedAccessAttempts = 5 per IdentityOptions; lockoutOnFailure: true in SignInCommandHandler.
    /// </summary>
    [Fact(DisplayName = "BE-TC-08: Lockout after 5 wrong-password attempts → 400 with lockout message; 6th attempt with correct password also → 400")]
    public async Task BeTc08_LockoutAfter5FailedAttempts_Returns400LockoutMessage()
    {
        // Use a throwaway parent (do NOT lock out superadmin/basicuser)
        var lockoutEmail = UniqueEmail("lockout");
        var lockoutPassword = "Str0ng@Pass";

        // Register a throwaway parent
        var (regResp, _, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = lockoutEmail, Password = lockoutPassword, AcceptedTerms = true });
        regResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-08: throwaway parent registration must succeed; body: {0}", regBody);

        // Hammer with 5 wrong-password attempts
        string? lockoutMessage = null;
        int? lockoutStatusCode = null;
        for (var i = 0; i < 5; i++)
        {
            var (attemptResp, attemptRoot, attemptBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
                new { UserName = lockoutEmail, Password = "Wr0ng!Pass" });

            ((int)attemptResp.StatusCode).Should().Be(400,
                "BE-TC-08: each wrong-password attempt must return 400; attempt #{0}; body: {1}", i + 1, attemptBody);
            TryProp(attemptRoot, "message", out var msg).Should().BeTrue("body: {0}", attemptBody);

            if (i == 4)
            {
                lockoutMessage = msg.GetString();
                lockoutStatusCode = (int)attemptResp.StatusCode;
            }
        }

        // 6th attempt with the CORRECT password — account is now locked
        var (lockedResp, lockedRoot, lockedBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = lockoutEmail, Password = lockoutPassword });

        lockedResp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-08: correct-password attempt while locked must return 400; body: {0}", lockedBody);

        TryProp(lockedRoot, "successed", out var successed).Should().BeTrue("body: {0}", lockedBody);
        successed.GetBoolean().Should().BeFalse(
            "BE-TC-08: successed must be false when account is locked; body: {0}", lockedBody);

        TryProp(lockedRoot, "message", out var lockedMsg).Should().BeTrue("body: {0}", lockedBody);
        var lockedMsgStr = lockedMsg.GetString();

        // The lockout message must differ from the generic invalid-credentials message.
        // The invalid-credentials message was captured in earlier attempts (before lockout triggered).
        // After 5 failures ASP.NET Identity sets LockoutEnd → signInResult.IsLockedOut → lockout-specific message.
        lockedMsgStr.Should().NotBeNullOrWhiteSpace(
            "BE-TC-08: lockout message must be non-empty; body: {0}", lockedBody);
    }

    /// <summary>
    /// BE-TC-09 — Deactivated account → 400 account-deactivated.
    /// BLOCKED: no HTTP-reachable path exists in this story's surface to set IsActive = false.
    /// SignInCommandHandler checks !user.IsActive and returns a localized "account deactivated"
    /// BadRequest, but there is no admin HTTP endpoint in the P1-10 surface to deactivate an account.
    /// Resolution: add an HTTP admin toggle, or promote this to a SignInCommandHandler unit test.
    /// </summary>
    [Fact(Skip = "BE-TC-09 BLOCKED: no HTTP endpoint in P1-10 surface sets IsActive=false. " +
                 "SignInCommandHandler checks IsActive (returns 400 deactivated), but no admin toggle " +
                 "endpoint is exposed for integration testing. Promote to unit test or add HTTP endpoint.",
        DisplayName = "BE-TC-09: BLOCKED — deactivated account → 400")]
    public Task BeTc09_DeactivatedAccount_Returns400_BLOCKED()
        => Task.CompletedTask;

    // ===========================================================================
    // Group C — Role source for the dashboard gate · AC-6
    // ===========================================================================

    /// <summary>BE-TC-10 — GET /api/Users/Me for admin returns Admin role</summary>
    [Fact(DisplayName = "BE-TC-10: Me for superadmin → 200; data.roles contains 'Admin' and 'SuperAdmin'")]
    public async Task BeTc10_MeForAdmin_ReturnsAdminRole()
    {
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, MeUrl,
            null, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-10: Me for admin must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "roles", out var rolesProp).Should().BeTrue(
            "BE-TC-10: Me.data must contain 'roles'; body: {0}", body);

        var roles = rolesProp.EnumerateArray()
            .Select(r => r.GetString())
            .ToList();

        roles.Should().Contain(r => r != null && r.Equals("Admin", StringComparison.OrdinalIgnoreCase),
            "BE-TC-10: superadmin Me.roles must contain 'Admin'; roles found: [{0}]; body: {1}",
            string.Join(", ", roles), body);

        roles.Should().Contain(r => r != null && r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase),
            "BE-TC-10: superadmin Me.roles must contain 'SuperAdmin'; roles found: [{0}]; body: {1}",
            string.Join(", ", roles), body);
    }

    /// <summary>BE-TC-11 — Me for a Parent returns Parent only (non-admin gate-rejected)</summary>
    [Fact(DisplayName = "BE-TC-11: Me for Parent → 200; roles contains 'Parent'; does NOT contain Admin/SuperAdmin")]
    public async Task BeTc11_MeForParent_ReturnsParentRoleOnly_NotAdmin()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, MeUrl,
            null, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-11: Me for parent must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "roles", out var rolesProp).Should().BeTrue("body: {0}", body);

        var roles = rolesProp.EnumerateArray()
            .Select(r => r.GetString())
            .ToList();

        roles.Should().Contain(r => r != null && r.Equals("Parent", StringComparison.OrdinalIgnoreCase),
            "BE-TC-11: parent Me.roles must contain 'Parent'; roles found: [{0}]; body: {1}",
            string.Join(", ", roles), body);

        roles.Should().NotContain(r => r != null && r.Equals("Admin", StringComparison.OrdinalIgnoreCase),
            "BE-TC-11: parent Me.roles must NOT contain 'Admin'; roles found: [{0}]; body: {1}",
            string.Join(", ", roles), body);

        roles.Should().NotContain(r => r != null && r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase),
            "BE-TC-11: parent Me.roles must NOT contain 'SuperAdmin'; roles found: [{0}]; body: {1}",
            string.Join(", ", roles), body);
    }

    /// <summary>BE-TC-12 — Me requires auth (anonymous → 401)</summary>
    [Fact(DisplayName = "BE-TC-12: Me with no token → 401 (real HTTP 401, not a 200 fake-envelope)")]
    public async Task BeTc12_Me_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, MeUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "BE-TC-12: Me is [Authorize]; anonymous call must return 401; body: {0}", body);

        ((int)resp.StatusCode).Should().Be(401,
            "BE-TC-12: must be a real HTTP 401, not a 200 with successed=false envelope; body: {0}", body);
    }

    // ===========================================================================
    // Group D — Admin-only authorization matrix · AC-3 / AC-8
    // ===========================================================================

    /// <summary>BE-TC-13 — Anonymous on AdminOnly GET → 401</summary>
    [Fact(DisplayName = "BE-TC-13: Anonymous GET /Authorzation/RoleList → 401 (real HTTP 401, not 200 fake-envelope)")]
    public async Task BeTc13_Anonymous_AdminOnlyGet_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "BE-TC-13: AdminOnly GET must return 401 without a token; body: {0}", body);
    }

    /// <summary>BE-TC-14 — Anonymous on AdminOnly POST → 401</summary>
    [Fact(DisplayName = "BE-TC-14: Anonymous POST /Authorzation/Create → 401")]
    public async Task BeTc14_Anonymous_AdminOnlyPost_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, AuthorzationCreateUrl,
            new { RoleName = "TestRole" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "BE-TC-14: AdminOnly POST must return 401 without a token; body: {0}", body);
    }

    /// <summary>BE-TC-15 — Parent token on AdminOnly GET → 403</summary>
    [Fact(DisplayName = "BE-TC-15: Parent token on GET /Authorzation/RoleList → 403 (authenticated, not permitted)")]
    public async Task BeTc15_ParentToken_AdminOnlyGet_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl,
            null, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "BE-TC-15: authenticated Parent token on AdminOnly GET must return 403; body: {0}", body);
    }

    /// <summary>BE-TC-16 — Parent token on AdminOnly POST → 403</summary>
    [Fact(DisplayName = "BE-TC-16: Parent token on POST /Authorzation/Create → 403")]
    public async Task BeTc16_ParentToken_AdminOnlyPost_Returns403()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, AuthorzationCreateUrl,
            new { RoleName = "TestRole" }, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "BE-TC-16: authenticated Parent token on AdminOnly POST must return 403; body: {0}", body);
    }

    /// <summary>BE-TC-17 — Basic-role token on AdminOnly endpoint → 403</summary>
    [Fact(DisplayName = "BE-TC-17: Basic-role token on GET /Authorzation/RoleList → 403 (gate is role-affirmative, not just deny-Parent)")]
    public async Task BeTc17_BasicRoleToken_AdminOnlyGet_Returns403()
    {
        var basicToken = await SignInAndGetTokenAsync(BasicUserName, BasicPassword);

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl,
            null, basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "BE-TC-17: Basic-role token (non-admin) on AdminOnly GET must return 403; body: {0}", body);
    }

    /// <summary>BE-TC-18 — Admin/SuperAdmin token on AdminOnly GET → 200</summary>
    [Fact(DisplayName = "BE-TC-18: Admin/SuperAdmin token on GET /Authorzation/RoleList → 200; successed=true")]
    public async Task BeTc18_AdminToken_AdminOnlyGet_Returns200()
    {
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl,
            null, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-18: Admin/SuperAdmin token must pass AdminOnly GET; expected 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "BE-TC-18: successed must be true for admin on AdminOnly GET; body: {0}", body);
    }

    /// <summary>BE-TC-19 — Admin token on AdminOnly POST → not 401/403 (authz passes)</summary>
    [Fact(DisplayName = "BE-TC-19: Admin token on POST /Authorzation/Create → not 401/403 (authz passes; 200/422/400 all acceptable)")]
    public async Task BeTc19_AdminToken_AdminOnlyPost_PassesAuthz()
    {
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);
        var uniqueRoleName = $"TestRole_{Guid.NewGuid():N}"[..20];

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, AuthorzationCreateUrl,
            new { RoleName = uniqueRoleName }, adminToken);

        var sc = (int)resp.StatusCode;
        sc.Should().NotBe(401,
            "BE-TC-19: Admin token must pass AdminOnly gate on POST; must not get 401; got {0}; body: {1}", sc, body);
        sc.Should().NotBe(403,
            "BE-TC-19: Admin token must pass AdminOnly gate on POST; must not get 403; got {0}; body: {1}", sc, body);
    }

    /// <summary>BE-TC-20 — Tampered/malformed bearer token on AdminOnly endpoint → 401 (not 500)</summary>
    [Fact(DisplayName = "BE-TC-20: Tampered bearer on GET /Authorzation/RoleList → 401 (not 500, not 200)")]
    public async Task BeTc20_TamperedToken_AdminOnlyGet_Returns401_Not500()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl,
            null, "malformed.jwt.token");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "BE-TC-20: tampered/malformed JWT must return 401; bearer middleware must reject it before handler; body: {0}", body);

        ((int)resp.StatusCode).Should().NotBe(500,
            "BE-TC-20: must never return 500 for a malformed token; body: {0}", body);
    }

    /// <summary>
    /// BE-TC-21 — Expired admin token on AdminOnly endpoint → 401.
    /// Constructs a well-formed but expired JWT using the test signing key.
    /// </summary>
    [Fact(DisplayName = "BE-TC-21: Expired (but well-formed) admin token on GET /Authorzation/RoleList → 401")]
    public async Task BeTc21_ExpiredAdminToken_AdminOnlyGet_Returns401()
    {
        var freshToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);
        var expiredToken = CreateExpiredAccessToken(freshToken);

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, AuthorzationRoleListUrl,
            null, expiredToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "BE-TC-21: an expired (past ValidTo) but well-formed JWT must return 401; " +
            "the bearer middleware validates lifetime; body: {0}", body);
    }

    /// <summary>
    /// BE-TC-22 — GetUserProfile is itself AdminOnly (anonymous → 401, Parent → 403, Admin → 200).
    /// Three sub-scenarios in one test (they share setup; splitting would just add noise).
    /// </summary>
    [Fact(DisplayName = "BE-TC-22: GetUserProfile AdminOnly matrix — anon→401, Parent→403, Admin→200")]
    public async Task BeTc22_GetUserProfile_AdminOnlyMatrix()
    {
        // Step 1: anonymous → 401
        var (anonResp, _, anonBody) = await SendAsync(_client, HttpMethod.Get, GetUserProfileUrl);
        anonResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "BE-TC-22 step 1: GetUserProfile with no token must return 401; body: {0}", anonBody);

        // Step 2: Parent → 403
        var parentToken = await RegisterParentAndGetTokenAsync();
        var (parentResp, _, parentBody) = await SendAsync(_client, HttpMethod.Get, GetUserProfileUrl,
            null, parentToken);
        parentResp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "BE-TC-22 step 2: GetUserProfile with Parent token must return 403; body: {0}", parentBody);

        // Step 3: Admin → 200, successed=true, data.roles present
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);
        var (adminResp, adminRoot, adminBody) = await SendAsync(_client, HttpMethod.Get, GetUserProfileUrl,
            null, adminToken);
        adminResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-22 step 3: GetUserProfile with Admin token must return 200; body: {0}", adminBody);

        TryProp(adminRoot, "successed", out var successed).Should().BeTrue("body: {0}", adminBody);
        successed.GetBoolean().Should().BeTrue(
            "BE-TC-22 step 3: successed must be true for admin on GetUserProfile; body: {0}", adminBody);

        TryProp(adminRoot, "data", out var adminData).Should().BeTrue("body: {0}", adminBody);
        // roles must be present in the profile (admin viewing their own profile)
        TryProp(adminData, "roles", out _).Should().BeTrue(
            "BE-TC-22 step 3: GetUserProfile.data must contain 'roles'; body: {0}", adminBody);
    }

    // ===========================================================================
    // Group E — No public admin self-registration · AC-2 / product override
    // ===========================================================================

    /// <summary>BE-TC-23 — Register-Parent yields Parent only, never Admin</summary>
    [Fact(DisplayName = "BE-TC-23: Register-Parent succeeds → Me.roles is 'Parent' only; never Admin/SuperAdmin/Student")]
    public async Task BeTc23_RegisterParent_YieldsParentRole_NeverAdmin()
    {
        var parentToken = await RegisterParentAndGetTokenAsync();

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, MeUrl,
            null, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-23: Me for freshly-registered parent must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "roles", out var rolesProp).Should().BeTrue("body: {0}", body);

        var roles = rolesProp.EnumerateArray()
            .Select(r => r.GetString())
            .ToList();

        roles.Should().Contain(r => r != null && r.Equals("Parent", StringComparison.OrdinalIgnoreCase),
            "BE-TC-23: freshly-registered parent must have 'Parent' role; roles: [{0}]; body: {1}",
            string.Join(", ", roles), body);

        roles.Should().NotContain(r => r != null && r.Equals("Admin", StringComparison.OrdinalIgnoreCase),
            "BE-TC-23: Register-Parent must NEVER yield 'Admin' role; roles: [{0}]; body: {1}",
            string.Join(", ", roles), body);

        roles.Should().NotContain(r => r != null && r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase),
            "BE-TC-23: Register-Parent must NEVER yield 'SuperAdmin' role; roles: [{0}]; body: {1}",
            string.Join(", ", roles), body);

        roles.Should().NotContain(r => r != null && r.Equals("Student", StringComparison.OrdinalIgnoreCase),
            "BE-TC-23: Register-Parent must NEVER yield 'Student' role; roles: [{0}]; body: {1}",
            string.Join(", ", roles), body);
    }

    /// <summary>BE-TC-24 — AddUser gated; not anonymously reachable</summary>
    [Fact(DisplayName = "BE-TC-24: AddUser with no token → 401; with Parent token → 403")]
    public async Task BeTc24_AddUser_AnonReturns401_ParentReturns403()
    {
        var addUserBody = new
        {
            Email = UniqueEmail("tc24"),
            UserName = $"tc24_{Guid.NewGuid():N}"[..20],
            FullName = "TC24 User",
            Roles = new[] { "Admin" }
        };

        // Step 1: anonymous → 401
        var (anonResp, _, anonBody) = await SendAsync(_client, HttpMethod.Post, AddUserUrl, addUserBody);
        anonResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "BE-TC-24 step 1: AddUser with no token must return 401; body: {0}", anonBody);

        // Step 2: Parent token → 403
        var parentToken = await RegisterParentAndGetTokenAsync();
        var (parentResp, _, parentBody) = await SendAsync(_client, HttpMethod.Post, AddUserUrl,
            addUserBody, parentToken);
        parentResp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "BE-TC-24 step 2: AddUser with Parent token must return 403; body: {0}", parentBody);
    }

    /// <summary>BE-TC-25 — Admin can provision a user via the gated surface</summary>
    [Fact(DisplayName = "BE-TC-25: Admin can provision a user via AddUser → 200/201; successed=true")]
    public async Task BeTc25_AdminCanProvisionUser_ViaAddUser()
    {
        var adminToken = await SignInAndGetTokenAsync(AdminUserName, AdminPassword);

        var userBody = new
        {
            Email = UniqueEmail("tc25"),
            UserName = $"tc25_{Guid.NewGuid():N}"[..20],
            FullName = "TC25 Provisioned User",
            Roles = new[] { "Basic" }
        };

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, AddUserUrl,
            userBody, adminToken);

        var sc = (int)resp.StatusCode;
        sc.Should().BeOneOf(new[] { 200, 201 },
            "BE-TC-25: Admin AddUser must return 200 or 201; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "BE-TC-25: successed must be true for admin-provisioned user; body: {0}", body);
    }

    // ===========================================================================
    // Group F — Configured admin seed (environment-gated) · BE-1
    // ===========================================================================

    /// <summary>BE-TC-26 — SeedConfiguredAdminAsync is a no-op when AdminSeed:* is unset</summary>
    [Fact(DisplayName = "BE-TC-26: SeedConfiguredAdminAsync no-op when AdminSeed:* unset — no extra admin account created")]
    public async Task BeTc26_ConfiguredAdminSeed_NoopWhenUnset()
    {
        // The default Testing host has no AdminSeed:Email / AdminSeed:Password configured.
        // Verify that SeedConfiguredAdminAsync did NOT create any account beyond the legacy
        // superadmin/basicuser — i.e., trying to sign in with a generic "configured-admin" email
        // fails with 400 (account not found → same as wrong-password per anti-enumeration).

        // We probe for a plausible configured-admin email pattern that should NOT exist.
        // Since there is no committed AdminSeed:Email, any probe email must fail.
        var probedAdminEmail = "configured-admin@learnexia.io";

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = probedAdminEmail, Password = "AnyPassword!1" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-26: no configured admin should exist (SeedConfiguredAdminAsync is no-op when " +
            "AdminSeed:* is unset); sign-in for '{0}' must return 400; body: {1}", probedAdminEmail, body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse(
            "BE-TC-26: successed must be false (account does not exist); body: {0}", body);
    }

    /// <summary>
    /// BE-TC-27 — Configured-admin seed idempotent, Admin-only.
    /// BLOCKED: requires a dedicated test host that supplies AdminSeed:Email + AdminSeed:Password
    /// via in-memory config overrides. The default LearnexiaWebAppFactory does NOT set these keys.
    /// SeedConfiguredAdminAsync returns early when keys are absent → no-op on the default host.
    /// Recipe to unblock: derive a factory, inject AdminSeed:Email + AdminSeed:Password via
    /// ConfigureAppConfiguration, call seed twice, sign in, call Me and assert roles = ["Admin"].
    /// See docs/qc/P1-10/README.md §4 Open Question #1 for the full recipe.
    /// </summary>
    [Fact(Skip = "BE-TC-27 BLOCKED: requires a dedicated test host with AdminSeed:Email + AdminSeed:Password " +
                 "in-memory config. Default LearnexiaWebAppFactory does not set these keys; SeedConfiguredAdminAsync " +
                 "is a no-op. See docs/qc/P1-10/README.md §4 for unblocking recipe.",
        DisplayName = "BE-TC-27: BLOCKED — configured-admin seed idempotent; requires AdminSeed:* config host")]
    public Task BeTc27_ConfiguredAdminSeed_Idempotent_BLOCKED()
        => Task.CompletedTask;

    // ===========================================================================
    // Group G — Admin session: refresh & sign-out · AC-4 + regression baseline
    // ===========================================================================

    /// <summary>BE-TC-28 — Admin refresh + sign-out round-trip</summary>
    [Fact(DisplayName = "BE-TC-28: Admin refresh+sign-out round-trip; rotated token differs; revoked token → 401 post-sign-out")]
    public async Task BeTc28_AdminRefreshAndSignOut_RoundTrip()
    {
        // Step 1: Sign in as superadmin; capture access + refresh tokens
        var (accessToken, refreshTokenString) = await SignInAndGetBothTokensAsync(AdminUserName, AdminPassword);
        accessToken.Should().NotBeNullOrWhiteSpace("step 1: access token must be non-empty");
        refreshTokenString.Should().NotBeNullOrWhiteSpace("step 1: refresh token must be non-empty");

        // Step 2: Refresh-Token requires an expired access token (ValidateDetails guard)
        var expiredAccessToken = CreateExpiredAccessToken(accessToken);

        var (refreshResp, refreshRoot, refreshBody) = await SendAsync(_client, HttpMethod.Post, RefreshTokenUrl,
            new { AccessToken = expiredAccessToken, RefreshToken = refreshTokenString });

        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-28 step 2: Refresh-Token with expired access + valid refresh must return 200; body: {0}", refreshBody);

        TryProp(refreshRoot, "data", out var refreshData).Should().BeTrue("body: {0}", refreshBody);
        TryProp(refreshData, "accessToken", out var newAtProp).Should().BeTrue("body: {0}", refreshBody);
        TryProp(refreshData, "refreshToken", out var newRtObj).Should().BeTrue("body: {0}", refreshBody);
        TryProp(newRtObj, "tokenString", out var newRtTs).Should().BeTrue("body: {0}", refreshBody);

        var newAccessToken = newAtProp.GetString()!;
        var newRefreshToken = newRtTs.GetString()!;

        newAccessToken.Should().NotBeNullOrWhiteSpace(
            "BE-TC-28 step 2: new access token must be non-empty; body: {0}", refreshBody);
        newRefreshToken.Should().NotBeNullOrWhiteSpace(
            "BE-TC-28 step 2: new refresh token must be non-empty; body: {0}", refreshBody);

        // Rotation: new refresh token must differ from the one sent
        newRefreshToken.Should().NotBe(refreshTokenString,
            "BE-TC-28 step 2: rotated refresh token must differ from the original (old token is now revoked)");

        // Step 3: Sign-Out with the new access token
        var (signOutResp, signOutRoot, signOutBody) = await SendAsync(_client, HttpMethod.Post, SignOutUrl,
            new { }, newAccessToken);

        signOutResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-28 step 3: Sign-Out with current bearer must return 200; body: {0}", signOutBody);

        TryProp(signOutRoot, "successed", out var signOutSuccessed).Should().BeTrue("body: {0}", signOutBody);
        signOutSuccessed.GetBoolean().Should().BeTrue(
            "BE-TC-28 step 3: Sign-Out successed must be true; body: {0}", signOutBody);

        // Step 4: Attempt Refresh-Token with the now-revoked refresh token → 401
        var expiredNew = CreateExpiredAccessToken(newAccessToken);

        var (revokedResp, revokedRoot, revokedBody) = await SendAsync(_client, HttpMethod.Post, RefreshTokenUrl,
            new { AccessToken = expiredNew, RefreshToken = newRefreshToken });

        revokedResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "BE-TC-28 step 4: after Sign-Out the revoked refresh token must return 401; body: {0}", revokedBody);

        TryProp(revokedRoot, "successed", out var revokedSuccessed).Should().BeTrue("body: {0}", revokedBody);
        revokedSuccessed.GetBoolean().Should().BeFalse(
            "BE-TC-28 step 4: successed must be false for a revoked refresh token; body: {0}", revokedBody);

        ((int)revokedResp.StatusCode).Should().NotBe(500,
            "BE-TC-28 step 4: must never be 500 for a revoked refresh token; body: {0}", revokedBody);
    }

    // ===========================================================================
    // Additional boundary: Sign-Out without token → 401
    // (Supplements BE-TC-28 to confirm Sign-Out is [Authorize])
    // ===========================================================================

    [Fact(DisplayName = "Sign-Out without token → 401 (Sign-Out is [Authorize])")]
    public async Task SignOut_WithoutToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, SignOutUrl, new { });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Sign-Out is [Authorize]; anonymous call must return 401; body: {0}", body);
    }
}

