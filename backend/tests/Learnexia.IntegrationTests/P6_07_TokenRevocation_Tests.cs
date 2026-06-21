using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P6-07 integration tests: Access-token revocation via per-request SessionId validation.
///
/// Story: feat/P6-07-access-token-revocation
///
/// What changed (BE-1..BE-4):
///   - CreateSessionAsync now takes a 3rd param (sessionId) and stores the session keyed by the JWT's
///     "SessionId" claim, fixing the GUID-A/GUID-B mismatch that made all TerminateSessionAsync calls
///     a silent no-op.
///   - OnTokenValidated is now wired in AddJwtBearer: reads "SessionId" claim, resolves scoped
///     ISessionManagementService from RequestServices, does a read-only GetSessionAsync + IsActive check.
///     context.Fail("session_revoked") on absent / terminated / expired session.
///   - Fail-mode: fail-closed in Production/Staging, fail-open-with-warning-log in Development/Testing
///     (same IsProtectedEnvironment helper as GuardJwtSecret). Tests run as "Testing" environment, so
///     store errors allow-through rather than fail-closed.
///   - ResetPasswordCommandHandler now calls TerminateAllUserSessionsAsync (P6-07 BE-3).
///   - SuspendAccountCommandHandler already called TerminateAllUserSessionsAsync (confirmed already correct).
///
/// The test process runs in "Testing" environment (LearnexiaWebAppFactory.UseEnvironment("Testing")),
/// backed by an in-memory IDistributedCache (no Redis needed). All session create/terminate calls are
/// in-process and deterministic.
///
/// Scenarios (1:1 with brief section "api-tester scenarios BE-5"):
///   SC-1  Baseline — sign-in → authorized endpoint → 200 (no false rejection; proves BE-1 fixed the mismatch)
///   SC-2  Revoke on sign-out — sign-in → sign-out → same access token → 401
///   SC-3  Change-password keeps current, kills others — device A + B; from A call ChangePassword;
///         A still 200, B → 401
///   SC-4  Anonymous reset revokes all — sign-in → generate reset token via UserManager → call Reset-Password
///         → original access token → 401
///   SC-5  Admin suspend revokes — admin suspends user → user's outstanding token → 401
///   SC-6  Read-only hot path — multiple consecutive authorized calls all 200 (no false rejection,
///         confirms no per-request write blocks subsequent calls)
///   SC-7  Existing auth suite regression — scoped to just the key sign-in and Me flows; the full
///         suite (P1-02, P1-09, P7-06/07/08) shares the same collection and runs automatically.
///
/// Additional assertion:
///   SC-2b  Sign-out returns 200 with successed=true (envelope contract for the revoking action itself)
///   SC-5b  Reactivation restores access (new sign-in returns 200 after account reactivated)
/// </summary>
[Collection("IntegrationTests")]
public sealed class P6_07_TokenRevocation_Tests : IAsyncLifetime
{
    // ── URL constants ────────────────────────────────────────────────────────────
    private const string RegisterParentUrl      = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl              = "api/Users/Authentication/Sign-In";
    private const string SignOutUrl             = "api/Users/Authentication/Sign-Out";
    private const string ForgotPasswordUrl      = "api/Users/Authentication/Forgot-Password";
    private const string ResetPasswordUrl       = "api/Users/Authentication/Reset-Password";
    private const string MeUrl                  = "api/Users/Me";
    private const string ChangePasswordUrl      = "api/Users/Account/ChangePassword";
    private const string AdminUsersBase         = "api/Admin/Users";

    // ── Seeded credentials ───────────────────────────────────────────────────────
    private const string SuperAdminUserName     = "superadmin";
    private const string SuperAdminPassword     = "123Pa$$word!";

    private const string DefaultPassword        = "Str0ng@Pass";
    private const string NewPassword            = "N3wP@ss99!";
    private const string NewPassword2           = "N3wP@ss88!";

    // JWT signing key from appsettings.json (test default)
    private const string TestSigningKey =
        "CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789";

    // ── Infrastructure ───────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P6_07_TokenRevocation_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync()          => Task.CompletedTask;

    // ============================================================================
    // SC-1: Baseline — sign-in produces a token that works on [Authorize] endpoint
    //       Proves BE-1 keyed the session under the JWT's "SessionId" claim (not a
    //       server-generated GUID-B), so OnTokenValidated finds the session and allows
    //       the request instead of false-rejecting with 401.
    // ============================================================================

    [Fact(DisplayName = "SC-1 Baseline_SignIn_Token_AuthorizesMe: sign-in → GET /Me → 200 (no false rejection; BE-1 session keyed on claim)")]
    public async Task SC1_Baseline_SignIn_Token_AuthorizesMe()
    {
        // Arrange: register a fresh parent so the session is brand-new in this test run.
        var (token, _) = await RegisterParentGetTokenAndIdAsync("sc1");

        // Act: call an [Authorize] endpoint with the token minted at registration.
        var (resp, root, body) = await GetAsync(MeUrl, bearer: token);

        // Assert: must not false-reject — the fix in BE-1 ensures the session is keyed
        // under the JWT's "SessionId" claim, so OnTokenValidated finds it.
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-1: a freshly-issued token must authorize GET /Me without false rejection " +
            "(proves BE-1 fixed the GUID-A/GUID-B mismatch); body: {0}", body);

        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("SC-1: successed must be true; body: {0}", body);

        TryProp(root, "data", out var d).Should().BeTrue("body: {0}", body);
        d.ValueKind.Should().NotBe(JsonValueKind.Null, "SC-1: data must not be null; body: {0}", body);
    }

    [Fact(DisplayName = "SC-1b Baseline_SignIn_TokenCarriesSessionIdClaim: JWT minted at sign-in carries a non-empty 'SessionId' claim (BE-1 prerequisite)")]
    public async Task SC1b_Baseline_SignIn_JwtCarriesSessionIdClaim()
    {
        // Arrange: sign in as superadmin to get a fresh JWT.
        var token = await SignInGetTokenAsync(SuperAdminUserName, SuperAdminPassword);
        token.Should().NotBeNullOrWhiteSpace("SC-1b: sign-in must return a non-empty access token");

        // Act: decode the JWT without validation.
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue("SC-1b: access token must be a readable JWT");
        var jwt = handler.ReadJwtToken(token);

        var sessionIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == "SessionId");

        // Assert: the claim must be present and non-empty (BE-1 mint contract).
        sessionIdClaim.Should().NotBeNull(
            "SC-1b: JWT must carry a 'SessionId' claim — required by OnTokenValidated (BE-1/BE-2)");
        sessionIdClaim!.Value.Should().NotBeNullOrWhiteSpace(
            "SC-1b: 'SessionId' claim must be a non-empty GUID string");
    }

    // ============================================================================
    // SC-2: Revoke on sign-out — the access token must be rejected after Sign-Out
    //       (AC1 from brief)
    // ============================================================================

    [Fact(DisplayName = "SC-2 Revoke_OnSignOut_AccessToken401: sign-in → sign-out → same access token on [Authorize] → 401 (AC1)")]
    public async Task SC2_Revoke_OnSignOut_AccessToken_Returns401()
    {
        // Arrange: register a fresh parent and capture the access token.
        var (token, _) = await RegisterParentGetTokenAndIdAsync("sc2");

        // Verify the token works before sign-out (guard — mirrors SC-1).
        var (preResp, _, preBody) = await GetAsync(MeUrl, bearer: token);
        preResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-2 precondition: token must authorize /Me before sign-out; body: {0}", preBody);

        // Act: sign out using that same token — the handler terminates the session.
        var (signOutResp, signOutRoot, signOutBody) = await PostJsonAsync(
            SignOutUrl, new { }, bearer: token);

        // Assert sign-out itself succeeds (SC-2b envelope check).
        signOutResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-2b: Sign-Out must return 200 OK; body: {0}", signOutBody);
        TryProp(signOutRoot, "successed", out var so).Should().BeTrue("body: {0}", signOutBody);
        so.GetBoolean().Should().BeTrue("SC-2b: Sign-Out successed must be true; body: {0}", signOutBody);

        // Act: reuse the same (still within 30-min JWT lifetime) access token after sign-out.
        var (postResp, _, postBody) = await GetAsync(MeUrl, bearer: token);

        // Assert: OnTokenValidated must reject the token because the session is now terminated.
        postResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "SC-2: after Sign-Out the same access token must be rejected with 401 by OnTokenValidated " +
            "(session is terminated; GetSessionAsync returns null/inactive); body: {0}", postBody);
    }

    // ============================================================================
    // SC-3: Change-password keeps current device, kills other sessions (AC2 from brief)
    // ============================================================================

    [Fact(DisplayName = "SC-3 ChangePassword_KeepsCurrent_KillsOthers: device A changes password → A still 200, device B → 401 (AC2)")]
    public async Task SC3_ChangePassword_KeepsCurrent_KillsOthers()
    {
        // Arrange: register one parent, sign in TWICE to get two distinct tokens/sessions.
        var email = UniqueEmail("sc3");
        await RegisterParentByEmailAsync(email);

        // Device A: first sign-in.
        var tokenA = await SignInGetTokenAsync(email, DefaultPassword);
        tokenA.Should().NotBeNullOrWhiteSpace("SC-3 arrange: device-A sign-in must return a token");

        // Device B: second sign-in for the same user → distinct session.
        var tokenB = await SignInGetTokenAsync(email, DefaultPassword);
        tokenB.Should().NotBeNullOrWhiteSpace("SC-3 arrange: device-B sign-in must return a token");

        // Sanity: both tokens should have distinct SessionId claims.
        var sessionIdA = ExtractClaim(tokenA, "SessionId");
        var sessionIdB = ExtractClaim(tokenB, "SessionId");
        sessionIdA.Should().NotBeNullOrWhiteSpace("device-A token must carry a SessionId claim");
        sessionIdB.Should().NotBeNullOrWhiteSpace("device-B token must carry a SessionId claim");
        sessionIdA.Should().NotBe(sessionIdB,
            "SC-3 sanity: two separate sign-ins must produce distinct SessionId claims");

        // Verify both tokens work before the password change.
        var (preA, _, preBodyA) = await GetAsync(MeUrl, bearer: tokenA);
        preA.StatusCode.Should().Be(HttpStatusCode.OK, "SC-3 precondition A: tokenA must work before ChangePassword; body: {0}", preBodyA);

        var (preB, _, preBodyB) = await GetAsync(MeUrl, bearer: tokenB);
        preB.StatusCode.Should().Be(HttpStatusCode.OK, "SC-3 precondition B: tokenB must work before ChangePassword; body: {0}", preBodyB);

        // Act: device A calls ChangePassword — uses tokenA (the "current" session to preserve).
        var (cpResp, _, cpBody) = await PostJsonAsync(ChangePasswordUrl,
            new { CurrentPassword = DefaultPassword, NewPassword = NewPassword, ConfirmPassword = NewPassword },
            bearer: tokenA);
        cpResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-3: ChangePassword must succeed; body: {0}", cpBody);

        // Assert: device A's token — the "current" session — must still authorize.
        var (postA, _, postBodyA) = await GetAsync(MeUrl, bearer: tokenA);
        postA.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-3: device A's token (the session that changed the password) must STILL be authorized " +
            "after ChangePassword (P2-12 keep-current contract); body: {0}", postBodyA);

        // Assert: device B's token — a different session — must now be rejected (401).
        var (postB, _, postBodyB) = await GetAsync(MeUrl, bearer: tokenB);
        postB.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "SC-3: device B's token (a different session) must be 401 after ChangePassword " +
            "(AC2: all other sessions terminated); body: {0}", postBodyB);
    }

    // ============================================================================
    // SC-4: Anonymous reset revokes all sessions — after Reset-Password the
    //       pre-reset access token must be 401 (AC3 from brief)
    // ============================================================================

    [Fact(DisplayName = "SC-4 AnonymousReset_RevokesAllSessions: sign-in → reset password via real token → original access token → 401 (AC3)")]
    public async Task SC4_AnonymousReset_RevokesAllSessions()
    {
        // Arrange: register a parent, sign in to get an access token.
        var email = UniqueEmail("sc4");
        await RegisterParentByEmailAsync(email);

        var preResetToken = await SignInGetTokenAsync(email, DefaultPassword);
        preResetToken.Should().NotBeNullOrWhiteSpace("SC-4 arrange: sign-in must return a token");

        // Verify the token works before reset.
        var (preResp, _, preBody) = await GetAsync(MeUrl, bearer: preResetToken);
        preResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-4 precondition: token must authorize /Me before reset; body: {0}", preBody);

        // Generate a real Identity reset token in-process via UserManager (same approach as
        // P1_12_BE6_PasswordReset_Tests — no need to go through the email round-trip).
        var resetToken = await GenerateResetTokenViaUserManagerAsync(email);
        resetToken.Should().NotBeNullOrWhiteSpace("SC-4: UserManager.GeneratePasswordResetTokenAsync must return a non-empty token");

        // Act: call Reset-Password with the real reset token.
        var (resetResp, resetRoot, resetBody) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = resetToken, NewPassword = NewPassword2 });
        resetResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-4: Reset-Password must succeed with a valid token and policy-compliant password; body: {0}", resetBody);
        TryProp(resetRoot, "successed", out var rs).Should().BeTrue("body: {0}", resetBody);
        rs.GetBoolean().Should().BeTrue("SC-4: Reset-Password successed must be true; body: {0}", resetBody);

        // Assert: the pre-reset access token must now be rejected (session terminated by BE-3).
        var (postResp, _, postBody) = await GetAsync(MeUrl, bearer: preResetToken);
        postResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "SC-4: after Reset-Password the original access token must be 401 " +
            "(AC3: TerminateAllUserSessionsAsync called; OnTokenValidated finds no active session); body: {0}", postBody);
    }

    // ============================================================================
    // SC-5: Admin suspend revokes the target user's sessions (AC4 from brief)
    // ============================================================================

    [Fact(DisplayName = "SC-5 AdminSuspend_RevokesUserToken: admin suspends user → user's outstanding access token → 401 (AC4)")]
    public async Task SC5_AdminSuspend_RevokesUserToken()
    {
        // Arrange: register a parent and sign in to get an active access token.
        var email = UniqueEmail("sc5");
        var (targetToken, targetUserId) = await RegisterParentGetTokenAndIdAsync("sc5", email: email);
        targetToken.Should().NotBeNullOrWhiteSpace("SC-5 arrange: register must return access token");

        // Verify the target user's token is currently active.
        var (preResp, _, preBody) = await GetAsync(MeUrl, bearer: targetToken);
        preResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-5 precondition: target user's token must work before suspension; body: {0}", preBody);

        // Arrange: sign in as superadmin to get an admin token.
        var adminToken = await SignInGetTokenAsync(SuperAdminUserName, SuperAdminPassword);

        // Act: admin suspends the target user.
        var (suspendResp, suspendRoot, suspendBody) = await PostJsonAsync(
            $"{AdminUsersBase}/{targetUserId}/suspend",
            new { Reason = "P6-07 integration test — session revocation on suspend" },
            bearer: adminToken);

        suspendResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-5: SuspendAccount must succeed (200 OK); body: {0}", suspendBody);
        TryProp(suspendRoot, "successed", out var ss).Should().BeTrue("body: {0}", suspendBody);
        ss.GetBoolean().Should().BeTrue("SC-5: successed must be true on suspend; body: {0}", suspendBody);

        // Assert: the target user's outstanding access token must now return 401.
        var (postResp, _, postBody) = await GetAsync(MeUrl, bearer: targetToken);
        postResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "SC-5: after admin suspension, the target user's access token must be 401 " +
            "(AC4: SuspendAccountCommandHandler calls TerminateAllUserSessionsAsync; " +
            "OnTokenValidated finds no active session); body: {0}", postBody);
    }

    // ============================================================================
    // SC-6: Read-only hot path — multiple consecutive [Authorize] calls all 200.
    //       Proves no per-request mutation breaks subsequent calls, and no false
    //       rejection on repeated use of the same valid token (AC5 from brief).
    // ============================================================================

    [Fact(DisplayName = "SC-6 ReadOnlyHotPath_MultipleCallsAll200: sign-in → N consecutive authorized calls all 200 (no false reject, no per-request write side-effect)")]
    public async Task SC6_ReadOnlyHotPath_MultipleConsecutiveCallsAll200()
    {
        // Arrange
        var (token, _) = await RegisterParentGetTokenAndIdAsync("sc6");
        const int callCount = 5;

        // Act + Assert: each call must individually return 200.
        for (var i = 1; i <= callCount; i++)
        {
            var (resp, root, body) = await GetAsync(MeUrl, bearer: token);
            resp.StatusCode.Should().Be(HttpStatusCode.OK,
                "SC-6: call #{0}/{1} to GET /Me must return 200 (OnTokenValidated must not mutate " +
                "the session and must not false-reject on repeated calls); body: {2}", i, callCount, body);

            TryProp(root, "successed", out var s).Should().BeTrue(
                "SC-6 call #{0}: response must have 'successed' key; body: {1}", i, body);
            s.GetBoolean().Should().BeTrue(
                "SC-6 call #{0}: successed must be true; body: {1}", i, body);
        }
    }

    // ============================================================================
    // SC-7: Regression — core sign-in + Me flows still work after P6-07 changes.
    //       The full existing suite (P1_02, P1_09, P7_06_07_08) shares the
    //       [Collection("IntegrationTests")] and runs automatically; these cases
    //       provide an explicit focused regression check for the auth hot path.
    // ============================================================================

    [Fact(DisplayName = "SC-7a Regression_SuperAdminSignIn_AndMe: seeded superadmin can still sign in and call /Me after P6-07")]
    public async Task SC7a_Regression_SuperAdminSignIn_AndMe()
    {
        var adminToken = await SignInGetTokenAsync(SuperAdminUserName, SuperAdminPassword);
        adminToken.Should().NotBeNullOrWhiteSpace(
            "SC-7a regression: seeded superadmin sign-in must return a non-empty token after P6-07 changes");

        var (resp, root, body) = await GetAsync(MeUrl, bearer: adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-7a regression: superadmin GET /Me must return 200 after P6-07 OnTokenValidated is wired; body: {0}", body);

        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("SC-7a regression: successed must be true; body: {0}", body);
    }

    [Fact(DisplayName = "SC-7b Regression_FreshParent_SignIn_AndMe: newly registered parent can still sign in and call /Me after P6-07")]
    public async Task SC7b_Regression_FreshParent_SignIn_AndMe()
    {
        var (token, _) = await RegisterParentGetTokenAndIdAsync("sc7b");
        token.Should().NotBeNullOrWhiteSpace(
            "SC-7b regression: register-parent must return a non-empty token after P6-07 changes");

        var (resp, root, body) = await GetAsync(MeUrl, bearer: token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-7b regression: freshly-registered parent GET /Me must return 200 after P6-07; body: {0}", body);

        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("SC-7b regression: successed must be true; body: {0}", body);
    }

    [Fact(DisplayName = "SC-7c Regression_NoToken_Me_Returns401: GET /Me without a token still returns 401 (not broken by P6-07 wiring)")]
    public async Task SC7c_Regression_NoToken_Me_Returns401()
    {
        var (resp, _, body) = await GetAsync(MeUrl, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "SC-7c regression: GET /Me without a bearer token must still return 401 after P6-07; body: {0}", body);
    }

    [Fact(DisplayName = "SC-7d Regression_GarbageToken_Me_Returns401: GET /Me with a garbage bearer token returns 401 (JWT middleware still rejects malformed tokens)")]
    public async Task SC7d_Regression_GarbageToken_Me_Returns401()
    {
        var (resp, _, body) = await GetAsync(MeUrl, bearer: "this.is.not.a.valid.jwt.at.all");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "SC-7d regression: a malformed bearer token must still yield 401 after P6-07; body: {0}", body);
    }

    // ============================================================================
    // Additional: sign-out without a bearer must still return 401 (Sign-Out is [Authorize])
    // ============================================================================

    [Fact(DisplayName = "Additional_SignOut_WithoutBearer_Returns401: Sign-Out is [Authorize]; anonymous call → 401")]
    public async Task Additional_SignOut_WithoutBearer_Returns401()
    {
        var (resp, _, body) = await PostJsonAsync(SignOutUrl, new { }, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Additional: Sign-Out without a bearer token must return 401 (endpoint is [Authorize]); body: {0}", body);
    }

    // ============================================================================
    // Additional SC-5b: after reactivation, a NEW sign-in works (access is restored)
    // ============================================================================

    [Fact(DisplayName = "SC-5b AfterReactivate_NewSignIn_Returns200: after admin reactivates the user, a new sign-in authenticates successfully")]
    public async Task SC5b_AfterReactivate_NewSignIn_Returns200()
    {
        // Arrange: register a parent, sign in, suspend.
        var email = UniqueEmail("sc5b");
        var (targetToken, targetUserId) = await RegisterParentGetTokenAndIdAsync("sc5b", email: email);
        var adminToken = await SignInGetTokenAsync(SuperAdminUserName, SuperAdminPassword);

        var (suspendResp, _, suspendBody) = await PostJsonAsync(
            $"{AdminUsersBase}/{targetUserId}/suspend",
            new { Reason = "P6-07 reactivation test" },
            bearer: adminToken);
        suspendResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-5b arrange: suspend must succeed; body: {0}", suspendBody);

        // Confirm suspension killed the token.
        var (killedResp, _, killedBody) = await GetAsync(MeUrl, bearer: targetToken);
        killedResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "SC-5b arrange: token must be 401 after suspension; body: {0}", killedBody);

        // Act: reactivate the account.
        var (reactResp, reactRoot, reactBody) = await PostJsonAsync(
            $"{AdminUsersBase}/{targetUserId}/reactivate",
            new { Reason = "P6-07 reactivation test — restore" },
            bearer: adminToken);
        reactResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-5b: ReactivateAccount must return 200; body: {0}", reactBody);
        TryProp(reactRoot, "successed", out var rrs).Should().BeTrue("body: {0}", reactBody);
        rrs.GetBoolean().Should().BeTrue("SC-5b: reactivate successed must be true; body: {0}", reactBody);

        // Assert: a fresh sign-in after reactivation must succeed (access restored).
        var newToken = await SignInGetTokenAsync(email, DefaultPassword);
        newToken.Should().NotBeNullOrWhiteSpace(
            "SC-5b: sign-in after reactivation must return a token (account is active again)");

        var (newResp, _, newBody) = await GetAsync(MeUrl, bearer: newToken);
        newResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SC-5b: GET /Me with the post-reactivation token must return 200; body: {0}", newBody);
    }

    // ============================================================================
    // Helpers
    // ============================================================================

    private static string UniqueEmail(string tag = "")
        => $"p607_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@revoke.test";

    /// <summary>
    /// Case-insensitive property lookup — handles Newtonsoft (camelCase) and
    /// System.Text.Json (PascalCase) serialisation paths.
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

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetAsync(string url, string? bearer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var response = await _client.SendAsync(request);
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON — leave root default */ }
        }
        return (response, root, bodyStr);
    }

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        PostJsonAsync(string url, object body, string? bearer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var response = await _client.SendAsync(request);
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON — leave root default */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>
    /// Signs in with the given credentials and returns the access token.
    /// Asserts success inline.
    /// </summary>
    private async Task<string> SignInGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await PostJsonAsync(SignInUrl, new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var at).Should().BeTrue("body: {0}", body);
        return at.GetString()!;
    }

    /// <summary>
    /// Registers a new parent by email (or generates a unique email) and returns
    /// (accessToken, userId). Asserts success inline.
    /// </summary>
    private async Task<(string Token, int UserId)> RegisterParentGetTokenAndIdAsync(
        string tag, string? email = null)
    {
        email ??= UniqueEmail(tag);
        var (resp, root, body) = await PostJsonAsync(RegisterParentUrl,
            new { Email = email, Password = DefaultPassword, AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Register-Parent must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var at).Should().BeTrue("body: {0}", body);
        TryProp(data, "userId", out var uid).Should().BeTrue(
            "register-parent must return a userId in data; body: {0}", body);
        return (at.GetString()!, uid.GetInt32());
    }

    /// <summary>
    /// Registers a parent by explicit email (without capturing tokens). Used when we only need
    /// the account created so we can sign in separately with a different credential state.
    /// </summary>
    private async Task RegisterParentByEmailAsync(string email)
    {
        var (resp, _, body) = await PostJsonAsync(RegisterParentUrl,
            new { Email = email, Password = DefaultPassword, AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Register-Parent (by email) must succeed; body: {0}", body);
    }

    /// <summary>
    /// Generates a real Identity password-reset token by resolving UserManager from the
    /// factory's service scope. Avoids the email round-trip (same pattern as
    /// P1_12_BE6_PasswordReset_Tests.GenerateResetTokenViaUserManagerAsync).
    /// </summary>
    private async Task<string> GenerateResetTokenViaUserManagerAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull("user must exist to generate a password-reset token; email: {0}", email);
        return await userManager.GeneratePasswordResetTokenAsync(user!);
    }

    /// <summary>
    /// Extracts a named claim from a JWT without signature validation.
    /// Returns null if the token is unreadable or the claim is absent.
    /// </summary>
    private static string? ExtractClaim(string token, string claimType)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token)) return null;
            var jwt = handler.ReadJwtToken(token);
            return jwt.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
        }
        catch { return null; }
    }

    /// <summary>
    /// Re-signs a JWT with the test signing key but a past expiry. Preserved here for
    /// future token-lifecycle tests but not required by SC-1..SC-7 (which use live tokens).
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
            var signingKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(TestSigningKey));
            var expiredToken = new JwtSecurityToken(
                issuer: originalJwt.Issuer,
                audience: originalJwt.Audiences.FirstOrDefault(),
                claims: claimsToKeep,
                notBefore: DateTime.UtcNow.AddMinutes(-60),
                expires: DateTime.UtcNow.AddMinutes(-30),
                signingCredentials: new SigningCredentials(
                    signingKey, SecurityAlgorithms.HmacSha256Signature));
            return handler.WriteToken(expiredToken);
        }
        catch
        {
            return originalToken + "_expired_suffix";
        }
    }
}
