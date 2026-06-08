using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P1-02 integration tests: Stay signed in — token refresh and sign-out.
///
/// Endpoints under test (Identity AuthenticationController):
///   [AllowAnonymous] POST /api/Users/Authentication/Sign-In
///   [AllowAnonymous] POST /api/Users/Authentication/Register-Parent
///   [AllowAnonymous] POST /api/Users/Authentication/Refresh-Token
///   [Authorize]      POST /api/Users/Authentication/Sign-Out
///
/// IDistributedCache: Program.cs wires in-memory fallback when Redis connection string is empty
/// (appsettings.json has "Redis": ""). Tests run without a Redis container; the in-memory cache
/// is scoped to the single WebApplicationFactory process, making single-process refresh/sign-out
/// flows fully testable with isolation.
///
/// JSON serialization dual-path note (same as P1-01):
///   - Controller responses (200/400/401/etc.) → Newtonsoft → camelCase keys.
///   - ErrorHandlerMiddleWare (ValidationException → 422) → System.Text.Json → PascalCase keys.
/// The TryProp() helper handles both paths with a case-insensitive fallback.
///
/// Acceptance criteria covered:
///   AC-1  Happy path Refresh-Token: valid refresh token → 200, new non-empty access token.
///   AC-2  Sign-in and Register-Parent return a non-empty refreshToken.tokenString.
///   AC-3  Sign-Out revokes refresh token; subsequent /Refresh-Token with old token → 401.
///   AC-4  Expired/revoked/garbage refresh token → 401 (not 500); Successed=false.
///   AC-5  Tampered refresh token (single char change) → 401 (match validation enforced).
///   AC-6  Access-token lifetime ≈ 30 min; refresh token lifetime ≈ 7 days.
///   AC-7  Missing accessToken or refreshToken → 422 with Errors[].
///   AC-8  Rotation: new refreshToken.tokenString differs from the one sent in the request.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P1_02_RefreshAndSignOut_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_02_RefreshAndSignOut_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p102_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@integration.test";

    /// <summary>
    /// Case-insensitive property lookup on a JsonElement.
    /// Controller path = camelCase; middleware 422 path = PascalCase. This helper covers both.
    /// </summary>
    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        if (element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value)) return true;
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

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)> PostJsonAsync(string path, object body)
    {
        var response = await _client.PostAsJsonAsync(path, body);
        var bodyStr = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            var doc = JsonDocument.Parse(bodyStr);
            root = doc.RootElement;
        }
        return (response, root, bodyStr);
    }

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)> PostJsonWithBearerAsync(string path, object body, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(body);
        var response = await _client.SendAsync(request);
        var bodyStr = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            var doc = JsonDocument.Parse(bodyStr);
            root = doc.RootElement;
        }
        return (response, root, bodyStr);
    }

    /// <summary>
    /// Registers a new parent, returns (accessToken, refreshTokenString).
    /// Asserts success inline so test-level failures report cleanly.
    /// </summary>
    private async Task<(string AccessToken, string RefreshTokenString)> RegisterAndGetTokensAsync(string tag = "")
    {
        var email = UniqueEmail(tag);
        var (response, root, body) = await PostJsonAsync("/api/Users/Authentication/Register-Parent",
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Register-Parent must succeed to bootstrap the P1-02 test; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var at).Should().BeTrue("body: {0}", body);
        TryProp(data, "refreshToken", out var rt).Should().BeTrue("body: {0}", body);
        TryProp(rt, "tokenString", out var ts).Should().BeTrue("body: {0}", body);

        return (at.GetString()!, ts.GetString()!);
    }

    /// <summary>
    /// Signs in the seeded superadmin, returns (accessToken, refreshTokenString).
    /// </summary>
    private async Task<(string AccessToken, string RefreshTokenString)> SignInSuperAdminAsync()
    {
        var (response, root, body) = await PostJsonAsync("/api/Users/Authentication/Sign-In",
            new { UserName = "superadmin", Password = "123Pa$$word!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Seeded superadmin sign-in must succeed; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var at).Should().BeTrue("body: {0}", body);
        TryProp(data, "refreshToken", out var rt).Should().BeTrue("body: {0}", body);
        TryProp(rt, "tokenString", out var ts).Should().BeTrue("body: {0}", body);

        return (at.GetString()!, ts.GetString()!);
    }

    // =========================================================================
    // AC-2: Sign-in and Registration return a non-empty refresh token
    // =========================================================================

    [Fact(DisplayName = "AC-2a SignIn_ReturnsRefreshToken: POST Sign-In includes non-empty refreshToken.tokenString")]
    public async Task AC2a_SignIn_ReturnsRefreshToken()
    {
        var (response, root, body) = await PostJsonAsync("/api/Users/Authentication/Sign-In",
            new { UserName = "superadmin", Password = "123Pa$$word!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("sign-in successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("response must contain 'data'; body: {0}", body);

        // refreshToken object must be present
        TryProp(data, "refreshToken", out var refreshToken).Should().BeTrue(
            "AC-2: Sign-In response data must include 'refreshToken' object (fix: GenerateRefreshToken() now called in GetJwtToken); body: {0}", body);

        // tokenString must be non-empty
        TryProp(refreshToken, "tokenString", out var tokenString).Should().BeTrue(
            "refreshToken must contain 'tokenString'; body: {0}", body);
        tokenString.GetString().Should().NotBeNullOrWhiteSpace(
            "AC-2: refreshToken.tokenString must be non-empty on sign-in; body: {0}", body);
    }

    [Fact(DisplayName = "AC-2b Register_ReturnsRefreshToken: POST Register-Parent includes non-empty refreshToken.tokenString")]
    public async Task AC2b_Register_ReturnsRefreshToken()
    {
        var email = UniqueEmail("regrefresh");
        var (response, root, body) = await PostJsonAsync("/api/Users/Authentication/Register-Parent",
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "refreshToken", out var refreshToken).Should().BeTrue(
            "AC-2: Register-Parent response data must include 'refreshToken' object; body: {0}", body);

        TryProp(refreshToken, "tokenString", out var tokenString).Should().BeTrue(
            "refreshToken must contain 'tokenString'; body: {0}", body);
        tokenString.GetString().Should().NotBeNullOrWhiteSpace(
            "AC-2: refreshToken.tokenString must be non-empty on registration; body: {0}", body);
    }

    // =========================================================================
    // AC-1: Happy path Refresh — returns 200 with a new access token
    // =========================================================================

    [Fact(DisplayName = "AC-1 HappyPath_Refresh_Returns200_NewAccessToken: valid refresh token → 200 with new access token")]
    public async Task AC1_HappyPath_Refresh_Returns200_NewAccessToken()
    {
        // Step 1: sign in to get an access + refresh token pair.
        var (accessToken, refreshTokenStr) = await SignInSuperAdminAsync();

        accessToken.Should().NotBeNullOrWhiteSpace("prerequisite: sign-in must return access token");
        refreshTokenStr.Should().NotBeNullOrWhiteSpace("prerequisite: sign-in must return refresh token");

        // Step 2: the access token is 30-min. For the refresh to succeed, ValidateDetails checks
        // jwtToken.ValidTo > DateTime.Now. Since the token was just issued it is NOT expired yet,
        // which would normally return "TokenIsRunning" (400). We need to use a token that is
        // already expired in its claims but whose signature is still valid, OR call Refresh-Token
        // which requires an expired access token per the current ValidateDetails guard.
        //
        // Per the brief: "TokenIsRunning" = access token is still valid → 400 BadRequest (not 401).
        // To test the happy-path 200, we need an expired access token. We test this
        // by constructing a token with a past expiry using the same issuer/audience/signing key.
        // Since we can't easily forge a valid expired JWT in-process without the private key,
        // we test the "TokenIsRunning" path explicitly (which is 400 not 500, a correctness test)
        // and test the happy-path via a separate flow where we verify the full rotation.
        //
        // The correct test sequence that WILL reach 200 is to use a deliberately expired token.
        // We'll craft one by decoding/re-encoding with the known test secret.
        var expiredAccessToken = CreateExpiredAccessToken(accessToken);

        var (refreshResponse, refreshRoot, refreshBody) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAccessToken, RefreshToken = refreshTokenStr });

        // The endpoint must return 200 (not 500) for the happy path with a valid but expired token.
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "AC-1: Refresh-Token with a valid (expired) access token and matching refresh token must return 200; body: {0}", refreshBody);

        TryProp(refreshRoot, "successed", out var successed).Should().BeTrue("body: {0}", refreshBody);
        successed.GetBoolean().Should().BeTrue(
            "AC-1: Successed must be true on successful token refresh; body: {0}", refreshBody);

        TryProp(refreshRoot, "data", out var data).Should().BeTrue("body: {0}", refreshBody);
        TryProp(data, "accessToken", out var newAccessToken).Should().BeTrue("body: {0}", refreshBody);
        newAccessToken.GetString().Should().NotBeNullOrWhiteSpace(
            "AC-1: new access token must be non-empty after a successful refresh; body: {0}", refreshBody);
    }

    [Fact(DisplayName = "AC-1 TokenIsRunning_Returns400: refreshing with a freshly-minted (non-expired) access token → 400 BadRequest")]
    public async Task AC1_TokenIsRunning_NeverReturns500()
    {
        // ValidateDetails now compares jwtToken.ValidTo against DateTime.UtcNow (both UTC). A freshly
        // signed-in access token is NOT yet expired, so the guard correctly returns the
        // "TokenIsRunning" sentinel and the endpoint must reject the refresh with 400 BadRequest
        // ("Token is not expired.") instead of pointlessly issuing a new token. This pins the fix:
        // previously DateTime.Now (local) on a UTC+ host made the token look expired → spurious 200.
        var (accessToken, refreshTokenStr) = await SignInSuperAdminAsync();

        var (response, root, body) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = accessToken, RefreshToken = refreshTokenStr });

        // Must never be 500 (internal server error would be a regression).
        ((int)response.StatusCode).Should().NotBe(500,
            "AC-1: /Refresh-Token must never return 500 regardless of token expiry state; body: {0}", body);

        // Correct behavior after the UtcNow fix: a still-valid access token is rejected with 400.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "AC-1: refreshing with a freshly-minted, non-expired access token must return 400 (TokenIsRunning); body: {0}", body);

        // Envelope shape must be valid: Successed=false on the rejection.
        TryProp(root, "successed", out var successed).Should().BeTrue(
            "response must contain 'successed' key per BaseResponse<T>; body: {0}", body);
        successed.GetBoolean().Should().BeFalse(
            "AC-1: Successed must be false when the access token is still running; body: {0}", body);
    }

    // =========================================================================
    // AC-8: Rotation — new refresh token differs from the one supplied
    // =========================================================================

    [Fact(DisplayName = "AC-8 Rotation_NewRefreshTokenDiffersFromOld: refreshed token string differs from the request's")]
    public async Task AC8_Rotation_NewRefreshTokenDiffersFromOld()
    {
        var (accessToken, oldRefreshTokenStr) = await SignInSuperAdminAsync();

        oldRefreshTokenStr.Should().NotBeNullOrWhiteSpace("prerequisite: must have a refresh token from sign-in");

        var expiredAccessToken = CreateExpiredAccessToken(accessToken);

        var (refreshResponse, refreshRoot, refreshBody) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAccessToken, RefreshToken = oldRefreshTokenStr });

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "rotation test requires a 200 from /Refresh-Token; body: {0}", refreshBody);

        TryProp(refreshRoot, "data", out var data).Should().BeTrue("body: {0}", refreshBody);
        TryProp(data, "refreshToken", out var newRt).Should().BeTrue(
            "rotated response must contain a refreshToken object; body: {0}", refreshBody);
        TryProp(newRt, "tokenString", out var newTokenString).Should().BeTrue(
            "rotated refreshToken must contain tokenString; body: {0}", refreshBody);

        var newRefreshStr = newTokenString.GetString();
        newRefreshStr.Should().NotBeNullOrWhiteSpace(
            "AC-8: new refresh token must be non-empty after rotation; body: {0}", refreshBody);
        newRefreshStr.Should().NotBe(oldRefreshTokenStr,
            "AC-8: rotated refresh token must differ from the one submitted in the request (old one is now invalid); body: {0}", refreshBody);
    }

    // =========================================================================
    // AC-3: Sign-Out → 200, subsequent refresh with old token → 401
    // =========================================================================

    [Fact(DisplayName = "AC-3a SignOut_Returns200_Successed: POST Sign-Out with Bearer → 200 Successed=true")]
    public async Task AC3a_SignOut_Returns200_Successed()
    {
        var (accessToken, _) = await RegisterAndGetTokensAsync("signout200");

        var (response, root, body) = await PostJsonWithBearerAsync(
            "/api/Users/Authentication/Sign-Out", new { }, accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "AC-3: Sign-Out must return 200 OK; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "AC-3: Sign-Out Successed must be true; body: {0}", body);
    }

    [Fact(DisplayName = "AC-3b SignOut_InvalidatesRefreshToken: after Sign-Out, old refresh token → 401")]
    public async Task AC3b_SignOut_InvalidatesRefreshToken()
    {
        // Step 1: register a fresh parent and capture tokens.
        var (accessToken, refreshTokenStr) = await RegisterAndGetTokensAsync("signoutrevoke");

        // Step 2: sign out.
        var (signOutResponse, _, signOutBody) = await PostJsonWithBearerAsync(
            "/api/Users/Authentication/Sign-Out", new { }, accessToken);
        signOutResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "Sign-Out must succeed before testing revocation; body: {0}", signOutBody);

        // Step 3: attempt to refresh using the now-revoked refresh token.
        // We need an expired access token; use our helper.
        var expiredAccessToken = CreateExpiredAccessToken(accessToken);

        var (refreshResponse, refreshRoot, refreshBody) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAccessToken, RefreshToken = refreshTokenStr });

        // Must be 401, not 200, not 500.
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "AC-3: after Sign-Out the revoked refresh token must not be exchangeable (expect 401); body: {0}", refreshBody);

        TryProp(refreshRoot, "successed", out var successed).Should().BeTrue("body: {0}", refreshBody);
        successed.GetBoolean().Should().BeFalse(
            "AC-3: Successed must be false when refresh token was revoked by sign-out; body: {0}", refreshBody);
    }

    // =========================================================================
    // AC-4: Expired/revoked/garbage refresh token → 401, not 500
    // =========================================================================

    [Fact(DisplayName = "AC-4 GarbageRefreshToken_Returns401_NotServerError: arbitrary refresh token string → 401")]
    public async Task AC4_GarbageRefreshToken_Returns401_NotServerError()
    {
        var (accessToken, _) = await SignInSuperAdminAsync();
        var expiredAccessToken = CreateExpiredAccessToken(accessToken);

        // Supply a completely garbage refresh token — the stored entry won't match.
        var (response, root, body) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAccessToken, RefreshToken = "this-is-garbage-and-will-not-match-anything" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "AC-4: a garbage refresh token must return 401, not 500; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse(
            "AC-4: Successed must be false for a garbage refresh token; body: {0}", body);
    }

    [Fact(DisplayName = "AC-4 Envelope_401_HasCorrectShape: 401 response is a valid BaseResponse envelope")]
    public async Task AC4_Envelope_401_HasCorrectShape()
    {
        var (accessToken, _) = await SignInSuperAdminAsync();
        var expiredAccessToken = CreateExpiredAccessToken(accessToken);

        var (response, root, body) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAccessToken, RefreshToken = "garbage-token-for-envelope-check" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);

        // Validate the BaseResponse<T> envelope keys are all present.
        TryProp(root, "statusCode", out _).Should().BeTrue(
            "401 response must contain 'statusCode' per BaseResponse<T> envelope; body: {0}", body);
        TryProp(root, "successed", out _).Should().BeTrue(
            "401 response must contain 'successed' per BaseResponse<T> envelope; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue(
            "401 response must contain 'message' per BaseResponse<T> envelope; body: {0}", body);
    }

    // =========================================================================
    // AC-5: Tampered refresh token → 401 (match validation enforced)
    // =========================================================================

    [Fact(DisplayName = "AC-5 TamperedRefreshToken_Returns401: one-char modification of a valid refresh token → 401")]
    public async Task AC5_TamperedRefreshToken_Returns401()
    {
        var (accessToken, refreshTokenStr) = await RegisterAndGetTokensAsync("tamper");

        // Mutate one character so the string is structurally similar but NOT equal to the stored token.
        refreshTokenStr.Should().NotBeNullOrWhiteSpace("prerequisite: refresh token must be present");
        var tamperedToken = refreshTokenStr[..^1] + (refreshTokenStr[^1] == 'A' ? 'B' : 'A');

        var expiredAccessToken = CreateExpiredAccessToken(accessToken);

        var (response, root, body) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAccessToken, RefreshToken = tamperedToken });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "AC-5: a tampered refresh token (one char off) must return 401; the stored value check must reject arbitrary strings; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse(
            "AC-5: Successed must be false for a tampered refresh token; body: {0}", body);
    }

    // =========================================================================
    // AC-6: Token policy — access token ≈ 30 min, refresh token ≈ 7 days
    // =========================================================================

    [Fact(DisplayName = "AC-6 AccessTokenLifetime_Is30Min: decoded access token ValidTo is ≈ 30 min from now")]
    public async Task AC6_AccessTokenLifetime_Is30Min()
    {
        var before = DateTime.UtcNow;
        var (accessToken, _) = await SignInSuperAdminAsync();
        var after = DateTime.UtcNow;

        // Decode without validation (we just need the claims).
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(accessToken).Should().BeTrue(
            "the returned accessToken must be a readable JWT; token: {0}", accessToken);

        var jwt = handler.ReadJwtToken(accessToken);

        // ValidTo is the exp claim in UTC. We compare it against the midpoint of the issuance window.
        // The token is generated with DateTime.Now.AddMinutes(30) — which on a UTC-offset machine is
        // stored in UTC in the JWT exp. We verify remaining lifetime is close to 30 min.
        //
        // Note: GenerateJwtToken uses DateTime.Now (local), so the stored exp UTC value depends on
        // the host timezone. We account for this by comparing against the known midpoint ±2 min.
        var midpointOfIssuance = before + (after - before) / 2;
        var remainingLifetime = jwt.ValidTo - midpointOfIssuance;

        // Allow generous ±2 min tolerance to cover local timezone offsets in the test environment.
        remainingLifetime.TotalMinutes.Should().BeInRange(28.0, 32.0,
            "AC-6: access token ValidTo must be approximately 30 minutes from issuance " +
            "(JwtSettings.AccessTokenExpireMinutes=30); jwt.ValidTo={0}, midpoint={1}",
            jwt.ValidTo.ToString("o"), midpointOfIssuance.ToString("o"));
    }

    [Fact(DisplayName = "AC-6 RefreshTokenExpiry_Is7Days: sign-in refreshToken.expireAt ≈ now + 7 days")]
    public async Task AC6_RefreshTokenExpiry_Is7Days()
    {
        var (response, root, body) = await PostJsonAsync("/api/Users/Authentication/Sign-In",
            new { UserName = "superadmin", Password = "123Pa$$word!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "refreshToken", out var refreshToken).Should().BeTrue("body: {0}", body);
        TryProp(refreshToken, "expireAt", out var expireAtProp).Should().BeTrue(
            "AC-6: refreshToken.expireAt must be present; body: {0}", body);

        var expireAt = expireAtProp.GetDateTime();
        var expectedExpiry = DateTime.UtcNow.AddDays(7);

        // Allow ±60 seconds for processing.
        var diff = Math.Abs((expireAt - expectedExpiry).TotalSeconds);
        diff.Should().BeLessThan(60,
            "AC-6: refresh token expiry must be approximately 7 days from now (JwtSettings.RefreshTokenExpireDate=7); body: {0}", body);
    }

    // =========================================================================
    // AC-7: Missing AccessToken or RefreshToken → 422 with Errors[]
    // =========================================================================

    [Fact(DisplayName = "AC-7a MissingAccessToken_Returns422: empty AccessToken → 422 Errors[]")]
    public async Task AC7a_MissingAccessToken_Returns422()
    {
        var (response, root, body) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = "", RefreshToken = "some-refresh-token" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "AC-7: empty AccessToken must return 422 (RefreshTokenValidatior fires via ValidationBehavior); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);

        TryProp(root, "errors", out var errors).Should().BeTrue(
            "AC-7: 422 response must include 'errors' key; body: {0}", body);
        var errArray = errors.EnumerateArray().ToList();
        errArray.Should().NotBeEmpty("AC-7: Errors[] must not be empty; body: {0}", body);
    }

    [Fact(DisplayName = "AC-7b MissingRefreshToken_Returns422: empty RefreshToken → 422 Errors[]")]
    public async Task AC7b_MissingRefreshToken_Returns422()
    {
        var (response, root, body) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = "some-access-token", RefreshToken = "" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "AC-7: empty RefreshToken must return 422 (RefreshTokenValidatior fires via ValidationBehavior); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("body: {0}", body);

        TryProp(root, "errors", out var errors).Should().BeTrue(
            "AC-7: 422 response must include 'errors' key; body: {0}", body);
        errors.EnumerateArray().ToList().Should().NotBeEmpty("body: {0}", body);
    }

    [Fact(DisplayName = "AC-7c / BE-TC-24 BothMissing_Returns422: both AccessToken and RefreshToken empty → 422, one error per field")]
    public async Task AC7c_BothMissing_Returns422()
    {
        var (response, root, body) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = "", RefreshToken = "" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "BE-TC-24: both fields empty must return 422; body: {0}", body);

        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", body);
        var errArray = errors.EnumerateArray().ToList();
        errArray.Should().NotBeEmpty("BE-TC-24: Errors[] must contain at least one item; body: {0}", body);

        // Each error item must have PropertyName and ErrorMessage.
        foreach (var err in errArray)
        {
            TryProp(err, "propertyName", out _).Should().BeTrue(
                "BE-TC-24: each Errors[] item must have 'propertyName'; body: {0}", body);
            TryProp(err, "errorMessage", out _).Should().BeTrue(
                "BE-TC-24: each Errors[] item must have 'errorMessage'; body: {0}", body);
        }

        // BE-TC-24 specific: errors must reference BOTH AccessToken and RefreshToken fields.
        var propertyNames = errArray
            .Select(e =>
            {
                TryProp(e, "propertyName", out var pn);
                return pn.GetString() ?? string.Empty;
            })
            .ToList();

        propertyNames.Should().Contain(p =>
            string.Equals(p, "AccessToken", StringComparison.OrdinalIgnoreCase),
            "BE-TC-24: Errors[] must include an entry for 'AccessToken' when it is empty; body: {0}", body);

        propertyNames.Should().Contain(p =>
            string.Equals(p, "RefreshToken", StringComparison.OrdinalIgnoreCase),
            "BE-TC-24: Errors[] must include an entry for 'RefreshToken' when it is empty; body: {0}", body);
    }

    // =========================================================================
    // Auth: Sign-Out requires Bearer token (401 without one)
    // =========================================================================

    [Fact(DisplayName = "Auth_SignOut_WithoutBearer_Returns401: Sign-Out without Authorization header → 401")]
    public async Task Auth_SignOut_WithoutBearer_Returns401()
    {
        // Do NOT set an Authorization header.
        var content = JsonContent.Create(new { });
        var response = await _client.PostAsync("/api/Users/Authentication/Sign-Out", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Sign-Out is [Authorize] and must return 401 without a valid Bearer token");
    }

    // =========================================================================
    // Replay attack: old token after rotation is rejected
    // =========================================================================

    [Fact(DisplayName = "AC-8 Replay_OldRefreshToken_After_Rotation_Returns401: replaying the original refresh token after rotation → 401")]
    public async Task AC8_Replay_OldRefreshToken_After_Rotation_Returns401()
    {
        // 1. Sign in → get original tokens.
        var (accessToken, originalRefreshToken) = await RegisterAndGetTokensAsync("replay");
        var expiredAt1 = CreateExpiredAccessToken(accessToken);

        // 2. Refresh once (rotates the token).
        var (firstRefreshResponse, firstRefreshRoot, firstRefreshBody) = await PostJsonAsync(
            "/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAt1, RefreshToken = originalRefreshToken });

        firstRefreshResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "first refresh must succeed to set up the rotation test; body: {0}", firstRefreshBody);

        TryProp(firstRefreshRoot, "data", out var newData).Should().BeTrue("body: {0}", firstRefreshBody);
        TryProp(newData, "accessToken", out var newAt).Should().BeTrue("body: {0}", firstRefreshBody);
        var newAccessToken = newAt.GetString()!;

        // 3. Replay the ORIGINAL refresh token (which was rotated away).
        var expiredAt2 = CreateExpiredAccessToken(newAccessToken);

        var (replayResponse, replayRoot, replayBody) = await PostJsonAsync(
            "/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAt2, RefreshToken = originalRefreshToken });

        replayResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "AC-8: replaying the original (rotated-away) refresh token must return 401; body: {0}", replayBody);

        TryProp(replayRoot, "successed", out var successed).Should().BeTrue("body: {0}", replayBody);
        successed.GetBoolean().Should().BeFalse(
            "AC-8: Successed must be false when the rotated-away token is replayed; body: {0}", replayBody);
    }

    // =========================================================================
    // Regression: P1-01 sign-in still works (smoke)
    // =========================================================================

    [Fact(DisplayName = "Regression_P1_01: seeded superadmin sign-in still works after P1-02 changes")]
    public async Task Regression_P1_01_SuperAdmin_CanStillSignIn()
    {
        var (response, root, body) = await PostJsonAsync("/api/Users/Authentication/Sign-In",
            new { UserName = "superadmin", Password = "123Pa$$word!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Regression: seeded superadmin sign-in must continue to work; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var at).Should().BeTrue("body: {0}", body);
        at.GetString().Should().NotBeNullOrWhiteSpace(
            "access token must be non-empty on regression sign-in; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-06 — New access token from refresh is a readable, valid JWT
    // =========================================================================

    [Fact(DisplayName = "BE-TC-06 NewAccessToken_IsReadableJwt: refreshed access token is a valid JWT with same identity")]
    public async Task BeTc06_NewAccessToken_IsReadableJwt()
    {
        // Precondition: seam guard — BE-TC-05 logic: expired token must reach 200.
        var (accessToken, refreshTokenStr) = await SignInSuperAdminAsync();
        var expiredAccessToken = CreateExpiredAccessToken(accessToken);

        var (refreshResponse, refreshRoot, refreshBody) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAccessToken, RefreshToken = refreshTokenStr });

        // Seam guard: if this fails, the expired-JWT forge is broken — dependent cases below are unreliable.
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-06 seam guard (BE-TC-05): expired-token forge must reach 200; if 401 the test signing key drifted. body: {0}", refreshBody);

        TryProp(refreshRoot, "data", out var data).Should().BeTrue("body: {0}", refreshBody);
        TryProp(data, "accessToken", out var newAtProp).Should().BeTrue("body: {0}", refreshBody);

        var newAccessToken = newAtProp.GetString();
        newAccessToken.Should().NotBeNullOrWhiteSpace("BE-TC-06: new access token must be non-empty; body: {0}", refreshBody);

        // New access token must be a readable JWT (parseable by JwtSecurityTokenHandler).
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(newAccessToken).Should().BeTrue(
            "BE-TC-06: new access token from refresh must be a valid readable JWT; token: {0}", newAccessToken);

        // Decode both tokens and verify the identity claim (Id) is the same user.
        var originalJwt = handler.ReadJwtToken(accessToken);
        var newJwt = handler.ReadJwtToken(newAccessToken);

        var originalUserId = originalJwt.Claims.FirstOrDefault(c => c.Type == "Id")?.Value;
        var newUserId = newJwt.Claims.FirstOrDefault(c => c.Type == "Id")?.Value;

        originalUserId.Should().NotBeNullOrWhiteSpace("original token must carry an 'Id' claim");
        newUserId.Should().Be(originalUserId,
            "BE-TC-06: refreshed access token must carry the same user Id claim as the original; body: {0}", refreshBody);

        TryProp(refreshRoot, "successed", out var successed).Should().BeTrue("body: {0}", refreshBody);
        successed.GetBoolean().Should().BeTrue("BE-TC-06: successed must be true; body: {0}", refreshBody);
    }

    // =========================================================================
    // BE-TC-07 — Refresh preserves identity claims (same user)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-07 Refresh_PreservesIdentityClaims: new token carries same Id and NameIdentifier")]
    public async Task BeTc07_Refresh_PreservesIdentityClaims()
    {
        var (accessToken, refreshTokenStr) = await RegisterAndGetTokensAsync("idclaims");
        var expiredAccessToken = CreateExpiredAccessToken(accessToken);

        var (refreshResponse, refreshRoot, refreshBody) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAccessToken, RefreshToken = refreshTokenStr });

        // Seam guard.
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-07 seam guard: must reach 200 to test claim preservation; body: {0}", refreshBody);

        TryProp(refreshRoot, "data", out var data).Should().BeTrue("body: {0}", refreshBody);
        TryProp(data, "accessToken", out var newAtProp).Should().BeTrue("body: {0}", refreshBody);

        var newAccessToken = newAtProp.GetString()!;
        var handler = new JwtSecurityTokenHandler();

        var originalJwt = handler.ReadJwtToken(accessToken);
        var newJwt = handler.ReadJwtToken(newAccessToken);

        // Id claim: uniquely identifies the DB user record.
        var originalId = originalJwt.Claims.FirstOrDefault(c => c.Type == "Id")?.Value;
        var newId = newJwt.Claims.FirstOrDefault(c => c.Type == "Id")?.Value;
        originalId.Should().NotBeNullOrWhiteSpace("original token must carry an 'Id' claim");
        newId.Should().Be(originalId,
            "BE-TC-07: refreshed token must carry the same user 'Id' claim; body: {0}", refreshBody);

        // NameIdentifier: username identity claim.
        var originalNameId = originalJwt.Claims.FirstOrDefault(c =>
            c.Type == System.Security.Claims.ClaimTypes.NameIdentifier ||
            c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        var newNameId = newJwt.Claims.FirstOrDefault(c =>
            c.Type == System.Security.Claims.ClaimTypes.NameIdentifier ||
            c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        originalNameId.Should().NotBeNullOrWhiteSpace("original token must carry a NameIdentifier claim");
        newNameId.Should().Be(originalNameId,
            "BE-TC-07: refreshed token must carry the same NameIdentifier claim; body: {0}", refreshBody);
    }

    // =========================================================================
    // BE-TC-10 — No stored token (signed out) → 401 RefreshTokenNotFound
    // =========================================================================

    [Fact(DisplayName = "BE-TC-10 NoStoredToken_Returns401: after sign-out, refresh token → 401 (RefreshTokenNotFound)")]
    public async Task BeTc10_NoStoredToken_Returns401()
    {
        // Step 1: register a fresh parent and capture tokens.
        var (accessToken, refreshTokenStr) = await RegisterAndGetTokensAsync("nostoredtoken");

        // Step 2: sign out (clears the stored cache entry).
        var (signOutResponse, _, signOutBody) = await PostJsonWithBearerAsync(
            "/api/Users/Authentication/Sign-Out", new { }, accessToken);
        signOutResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-10 setup: Sign-Out must succeed to clear the stored entry; body: {0}", signOutBody);

        // Step 3: forge an expired access token and attempt to refresh.
        var expiredAccessToken = CreateExpiredAccessToken(accessToken);

        var (refreshResponse, refreshRoot, refreshBody) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAccessToken, RefreshToken = refreshTokenStr });

        // Must be 401 (RefreshTokenNotFound path) — not 500, not 200.
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "BE-TC-10: refreshing after sign-out (no stored entry) must return 401; body: {0}", refreshBody);

        ((int)refreshResponse.StatusCode).Should().NotBe(500,
            "BE-TC-10: must never be 500; body: {0}", refreshBody);

        TryProp(refreshRoot, "successed", out var successed).Should().BeTrue("body: {0}", refreshBody);
        successed.GetBoolean().Should().BeFalse(
            "BE-TC-10: successed must be false when no stored token exists; body: {0}", refreshBody);
    }

    // =========================================================================
    // BE-TC-15 — Revoked (signed-out) refresh token → 401 (explicit behavioural contract)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-15 RevokedRefreshToken_Returns401: signed-out token cannot be re-exchanged")]
    public async Task BeTc15_RevokedRefreshToken_Returns401()
    {
        // Step 1: register a fresh parent; capture token pair.
        var (accessToken, refreshTokenStr) = await RegisterAndGetTokensAsync("revoked");

        // Step 2: sign out → revokes the stored refresh token.
        var (signOutResponse, _, signOutBody) = await PostJsonWithBearerAsync(
            "/api/Users/Authentication/Sign-Out", new { }, accessToken);
        signOutResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-15: Sign-Out must return 200 before revocation test; body: {0}", signOutBody);

        // Step 3: forge expired access token; attempt refresh with the pre-sign-out refresh token.
        var expiredAccessToken = CreateExpiredAccessToken(accessToken);

        var (refreshResponse, refreshRoot, refreshBody) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAccessToken, RefreshToken = refreshTokenStr });

        // The revoked (deleted) refresh token must be rejected with 401.
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "BE-TC-15: revoked (sign-out deleted) refresh token must not be re-exchangeable; must 401; body: {0}", refreshBody);

        TryProp(refreshRoot, "successed", out var successed).Should().BeTrue("body: {0}", refreshBody);
        successed.GetBoolean().Should().BeFalse(
            "BE-TC-15: successed must be false when refresh token was revoked by sign-out; body: {0}", refreshBody);
    }

    // =========================================================================
    // BE-TC-17 — Rotation: new refresh token carries a 7-day expireAt
    // =========================================================================

    [Fact(DisplayName = "BE-TC-17 Rotation_NewRefreshToken_Carries7DayExpiry: rotated token has fresh 7-day expireAt")]
    public async Task BeTc17_Rotation_NewRefreshToken_Carries7DayExpiry()
    {
        var (accessToken, refreshTokenStr) = await SignInSuperAdminAsync();
        var expiredAccessToken = CreateExpiredAccessToken(accessToken);

        var before = DateTime.UtcNow;

        var (refreshResponse, refreshRoot, refreshBody) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAccessToken, RefreshToken = refreshTokenStr });

        var after = DateTime.UtcNow;

        // Seam guard.
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-17 seam guard: expired-token forge must reach 200; body: {0}", refreshBody);

        TryProp(refreshRoot, "data", out var data).Should().BeTrue("body: {0}", refreshBody);
        TryProp(data, "refreshToken", out var newRt).Should().BeTrue("body: {0}", refreshBody);
        TryProp(newRt, "expireAt", out var expireAtProp).Should().BeTrue(
            "BE-TC-17: rotated refreshToken must carry 'expireAt'; body: {0}", refreshBody);

        var expireAt = expireAtProp.GetDateTime();
        var midpoint = before + (after - before) / 2;
        var expectedExpiry = midpoint.AddDays(7);

        // Rotated token must carry a FRESH 7-day window (not a residual from the original issuance).
        var diffSeconds = Math.Abs((expireAt - expectedExpiry).TotalSeconds);
        diffSeconds.Should().BeLessThan(60,
            "BE-TC-17: rotated refresh token expireAt must be ≈ UtcNow + 7 days (±60 s); " +
            "actual={0}, expected≈{1}; body: {2}", expireAt.ToString("o"), expectedExpiry.ToString("o"), refreshBody);
    }

    // =========================================================================
    // BE-TC-26 — Sign-Out with malformed/garbage Bearer → 401
    // =========================================================================

    [Fact(DisplayName = "BE-TC-26 SignOut_MalformedBearer_Returns401: Sign-Out with garbage JWT → 401")]
    public async Task BeTc26_SignOut_MalformedBearer_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Users/Authentication/Sign-Out");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-jwt-this-is-garbage");
        request.Content = JsonContent.Create(new { });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "BE-TC-26: Sign-Out with an unparseable/invalid Bearer token must return 401; " +
            "the JWT middleware must reject it before reaching the handler, never 500");

        ((int)response.StatusCode).Should().NotBe(500,
            "BE-TC-26: must never be 500 for a malformed Bearer token");
    }

    // =========================================================================
    // BE-TC-27 — Sign-Out is idempotent (second sign-out with same token no 500)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-27 SignOut_Idempotent_SecondCallNoServerError: double sign-out never 500")]
    public async Task BeTc27_SignOut_Idempotent_SecondCallNoServerError()
    {
        // Register a parent so the access token is fresh and within its 30-min TTL.
        var (accessToken, _) = await RegisterAndGetTokensAsync("idempotent");

        // First sign-out: must succeed.
        var (firstResponse, _, firstBody) = await PostJsonWithBearerAsync(
            "/api/Users/Authentication/Sign-Out", new { }, accessToken);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-27: first Sign-Out must succeed; body: {0}", firstBody);

        // Second sign-out with the SAME (still within 30-min TTL) access token.
        // The access token is still valid (Live-Token Window per Lead Decision 5).
        // RemoveAsync on an absent cache key must not throw → must not 500.
        var (secondResponse, _, secondBody) = await PostJsonWithBearerAsync(
            "/api/Users/Authentication/Sign-Out", new { }, accessToken);

        // Accept either 200 (idempotent success) or 401 (if the framework rejects the already-used
        // access token), but NEVER 500.
        ((int)secondResponse.StatusCode).Should().NotBe(500,
            "BE-TC-27: second Sign-Out must never return 500; RemoveAsync on absent key must be safe; body: {0}", secondBody);

        var acceptableStatuses = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized };
        acceptableStatuses.Should().Contain(secondResponse.StatusCode,
            "BE-TC-27: second Sign-Out must be either 200 (idempotent) or 401 (token rejected), never 500; body: {0}", secondBody);
    }

    // =========================================================================
    // BE-TC-03 — Refresh token persistence round-trip through cache
    // (Explicit persistence test — the happy-path canary BE-TC-05 proves the same
    //  thing implicitly, but this test isolates the persistence contract.)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-03 RefreshToken_PersistenceRoundTrip: issued token is retrievable at /Refresh-Token")]
    public async Task BeTc03_RefreshToken_PersistenceRoundTrip()
    {
        // Step 1: register a parent → capture access + refresh token.
        var (accessToken, refreshTokenStr) = await RegisterAndGetTokensAsync("persistence");

        refreshTokenStr.Should().NotBeNullOrWhiteSpace("prerequisite: Register-Parent must return a refresh token");
        accessToken.Should().NotBeNullOrWhiteSpace("prerequisite: Register-Parent must return an access token");

        // Step 2: forge an expired access token (same claims, past exp).
        var expiredAccessToken = CreateExpiredAccessToken(accessToken);

        // Step 3: call Refresh-Token with the original refresh token — if the token was actually
        // persisted to the cache during registration, this must succeed (200). If it was only
        // returned in the body and never stored, it fails with RefreshTokenNotFound (401).
        var (refreshResponse, refreshRoot, refreshBody) = await PostJsonAsync("/api/Users/Authentication/Refresh-Token",
            new { AccessToken = expiredAccessToken, RefreshToken = refreshTokenStr });

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-03: the refresh token issued at Register-Parent must be persisted in the cache; " +
            "if this 401s, the token was returned in the body but never stored; body: {0}", refreshBody);

        TryProp(refreshRoot, "successed", out var successed).Should().BeTrue("body: {0}", refreshBody);
        successed.GetBoolean().Should().BeTrue("BE-TC-03: successed must be true on persistence round-trip; body: {0}", refreshBody);
    }

    // =========================================================================
    // Helpers — expired JWT construction
    // =========================================================================

    /// <summary>
    /// Constructs a JWT that is syntactically identical to the supplied token but with the
    /// expiry claim (exp) set to the past, using the known test signing key from appsettings.json.
    ///
    /// WHY: The Refresh-Token endpoint calls ValidateDetails which checks jwtToken.ValidTo > DateTime.Now.
    /// A freshly-minted token is not expired, so it returns "TokenIsRunning" (400). To exercise the
    /// 200 happy-path and the 401 error paths we need an expired-but-otherwise-valid JWT.
    ///
    /// HOW: We re-sign with the well-known test secret from appsettings.json. In production the secret
    /// is different, but in the integration test environment this is the configured signing key.
    /// </summary>
    private static string CreateExpiredAccessToken(string originalToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var originalJwt = handler.ReadJwtToken(originalToken);

            // Strip exp and iat from the original claims to avoid duplicates when we set them below.
            var claimsToKeep = originalJwt.Claims
                .Where(c => c.Type != "exp" && c.Type != "iat" && c.Type != "nbf")
                .ToList();

            // Re-sign with the known test secret and a past expiry.
            var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.ASCII.GetBytes("CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789"));

            var expiredToken = new JwtSecurityToken(
                issuer: originalJwt.Issuer,
                audience: originalJwt.Audiences.FirstOrDefault(),
                claims: claimsToKeep,
                notBefore: DateTime.UtcNow.AddMinutes(-60),
                expires: DateTime.UtcNow.AddMinutes(-30), // expired 30 min ago
                signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    signingKey,
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature));

            return handler.WriteToken(expiredToken);
        }
        catch
        {
            // If token manipulation fails, return a clearly invalid token that should
            // still exercise the error path (will hit AlgorithmIsWrong or RefreshTokenNotFound).
            return originalToken + "_expired_suffix";
        }
    }
}
