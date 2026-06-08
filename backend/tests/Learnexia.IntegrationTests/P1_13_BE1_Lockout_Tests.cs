using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P1-13 BE-1 — Account Lockout integration tests.
///
/// Implements BE-TC-01 through BE-TC-09 from docs/qc/P1-13/backend-test-cases.md 1:1.
///
/// Endpoint under test: [AllowAnonymous] POST /api/Users/Authentication/Sign-In
/// Configuration under test: MaxFailedAccessAttempts=5, DefaultLockoutTimeSpan=5 min,
///                           AllowedForNewUsers=true, lockoutOnFailure: true in SignInCommandHandler.
///
/// Key messages (localized keys):
///   LoginInvalidCredentials  (en) = "Invalid username or password."
///   LoginTooManyFailedAttempts (en) = "Account temporarily locked due to too many failed login attempts. Please try again later."
///   LoginTooManyFailedAttempts (ar) = "تم قفل الحساب مؤقتاً بسبب محاولات تسجيل دخول فاشلة كثيرة. يرجى المحاولة لاحقاً."
///
/// Isolation: every test registers its OWN unique parent so lockout counters never bleed
/// between tests. AllowedForNewUsers=true ensures freshly-registered users are lockable.
///
/// CAPTCHA is disabled/fake in the CaptchaWebAppFactory used here (ICaptchaVerifier = FakeCaptchaVerifier
/// returning true), which allows register-parent to work without a real CAPTCHA token.
/// We share the CaptchaWebAppFactory/CaptchaTests collection to get CAPTCHA-disabled behavior.
/// </summary>
[Collection("CaptchaTests")]
public sealed class P1_13_BE1_Lockout_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";

    // -------------------------------------------------------------------------
    // Expected localized messages (verbatim from SharedResources.en-US.resx)
    // -------------------------------------------------------------------------
    private const string MsgInvalidCredentials = "Invalid username or password.";
    private const string MsgTooManyFailedAttempts = "Account temporarily locked due to too many failed login attempts. Please try again later.";
    private const string MsgTooManyFailedAttemptsAr = "تم قفل الحساب مؤقتاً بسبب محاولات تسجيل دخول فاشلة كثيرة. يرجى المحاولة لاحقاً.";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly CaptchaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_13_BE1_Lockout_Tests(CaptchaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        _factory.FakeVerifier.SetResult(true); // CAPTCHA no-op for all register calls
        await _factory.ApplyMigrationsAndSeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string UniqueEmail(string tag)
        => $"p113_lock_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@locktest.test";

    /// <summary>
    /// Case-insensitive JSON property lookup.
    /// Controller path: Newtonsoft -> camelCase; middleware 422 path: System.Text.Json -> PascalCase.
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
                  object? body = null, string? bearerToken = null,
                  string? acceptLanguage = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearerToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        if (acceptLanguage is not null)
            request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);

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

    /// <summary>
    /// Registers a unique parent account with CAPTCHA disabled (fake=true).
    /// Returns the email so the caller can use it for sign-in.
    /// </summary>
    private async Task<string> RegisterFreshParentAsync(string tag, string password = "Str0ng@Pass")
    {
        var email = UniqueEmail(tag);
        _factory.FakeVerifier.SetResult(true);
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = password, AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: fresh parent registration must succeed; email={0}; body: {1}", email, body);
        return email;
    }

    /// <summary>
    /// Extracts the message string from a sign-in 400 response.
    /// </summary>
    private static string? GetMessage(JsonElement root, string body)
    {
        TryProp(root, "message", out var msgProp).Should().BeTrue(
            "400 response must contain a 'message' key; body: {0}", body);
        return msgProp.GetString();
    }

    /// <summary>
    /// Hammer the sign-in endpoint N times with a wrong password for the given email.
    /// All requests carry Accept-Language: en-US so the message strings are in English.
    /// Returns (statusCode, message) of the last attempt.
    /// </summary>
    private async Task<(int StatusCode, string? Message)> SendWrongPasswordAttemptsAsync(
        string email, int count, string wrongPassword = "Wr0ng!Password99")
    {
        int lastStatus = 0;
        string? lastMessage = null;
        for (var i = 0; i < count; i++)
        {
            var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
                new { UserName = email, Password = wrongPassword },
                acceptLanguage: "en-US");
            lastStatus = (int)resp.StatusCode;
            TryProp(root, "message", out var msgProp);
            lastMessage = msgProp.ValueKind != JsonValueKind.Undefined ? msgProp.GetString() : null;
        }
        return (lastStatus, lastMessage);
    }

    // =========================================================================
    // BE-TC-01 — Wrong password below threshold returns invalid-credentials, not locked
    // =========================================================================

    [Fact(DisplayName = "BE-TC-01: Wrong password (1st attempt) → 400 LoginInvalidCredentials; NOT locked message")]
    public async Task BeTc01_WrongPasswordBelowThreshold_Returns400InvalidCredentials()
    {
        var email = await RegisterFreshParentAsync("tc01");

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = "Wrong#1" },
            acceptLanguage: "en-US");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-01: single wrong-password attempt must return 400; body: {0}", body);

        TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
        succProp.GetBoolean().Should().BeFalse("body: {0}", body);

        var msg = GetMessage(root, body);
        msg.Should().Be(MsgInvalidCredentials,
            "BE-TC-01: below-threshold wrong password must return LoginInvalidCredentials, not locked message; body: {0}", body);
        msg.Should().NotBe(MsgTooManyFailedAttempts,
            "BE-TC-01: account must NOT be locked after a single wrong-password attempt; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-02 — Each failed attempt below threshold stays invalid-credentials (attempts 1–4)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-02: Attempts 1–4 all return 400 + LoginInvalidCredentials (boundary — none trigger lock)")]
    public async Task BeTc02_Attempts1Through4_StayInvalidCredentials_NeverLocked()
    {
        var email = await RegisterFreshParentAsync("tc02");

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
                new { UserName = email, Password = $"Wrong#Attempt{attempt}" },
                acceptLanguage: "en-US");

            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "BE-TC-02: attempt #{0} must return 400; body: {1}", attempt, body);

            TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
            succProp.GetBoolean().Should().BeFalse("body: {0}", body);

            var msg = GetMessage(root, body);
            msg.Should().Be(MsgInvalidCredentials,
                "BE-TC-02: attempt #{0} must return LoginInvalidCredentials (not locked); body: {1}", attempt, body);
            msg.Should().NotBe(MsgTooManyFailedAttempts,
                "BE-TC-02: account must NOT be locked at attempt #{0} (threshold is 5); body: {1}", attempt, body);
        }
    }

    // =========================================================================
    // BE-TC-03 — Correct password still works while under the threshold
    // =========================================================================

    [Fact(DisplayName = "BE-TC-03: 3 wrong + correct password → 200; correct auth works while below threshold")]
    public async Task BeTc03_CorrectPasswordWorksWhileBelowThreshold()
    {
        const string password = "Str0ng@Pass";
        var email = await RegisterFreshParentAsync("tc03", password);

        // 3 wrong-password attempts (below threshold)
        await SendWrongPasswordAttemptsAsync(email, 3);

        // Correct password should still succeed
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = password },
            acceptLanguage: "en-US");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-03: correct password after 3 wrong attempts must return 200; account not yet locked; body: {0}", body);

        TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
        succProp.GetBoolean().Should().BeTrue(
            "BE-TC-03: Successed must be true on correct-password sign-in below threshold; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", body);
        tokenProp.GetString().Should().NotBeNullOrWhiteSpace(
            "BE-TC-03: accessToken must be a non-empty JWT on success; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-04 — 5th consecutive failure locks the account (boundary: record exact attempt number)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-04: 5th consecutive wrong-password attempt locks the account → 400 LoginTooManyFailedAttempts")]
    public async Task BeTc04_FifthConsecutiveFailure_LocksAccount()
    {
        var email = await RegisterFreshParentAsync("tc04");

        // Attempt 1–4: must be LoginInvalidCredentials
        for (var i = 1; i <= 4; i++)
        {
            var (r, root, b) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
                new { UserName = email, Password = $"Wrong#{i}" },
                acceptLanguage: "en-US");
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest, "attempt {0}; body: {1}", i, b);
            TryProp(root, "message", out var m).Should().BeTrue("body: {0}", b);
            m.GetString().Should().Be(MsgInvalidCredentials,
                "BE-TC-04: attempt #{0} must be invalid-credentials; body: {1}", i, b);
        }

        // Attempt 5: must trigger lockout
        var (resp5, root5, body5) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = "Wrong#5" },
            acceptLanguage: "en-US");

        resp5.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-04: 5th attempt must return 400; body: {0}", body5);

        TryProp(root5, "successed", out var succ5).Should().BeTrue("body: {0}", body5);
        succ5.GetBoolean().Should().BeFalse("body: {0}", body5);

        TryProp(root5, "message", out var msg5Prop).Should().BeTrue("body: {0}", body5);
        var msg5 = msg5Prop.GetString();

        // The account must be locked after the 5th attempt (MaxFailedAccessAttempts=5).
        // ASP.NET Identity: IncrementAccessFailedCount then checks >= Max → locks.
        // Observed boundary is recorded here: LOCKS ON ATTEMPT 5.
        msg5.Should().Be(MsgTooManyFailedAttempts,
            "BE-TC-04: 5th consecutive wrong-password attempt must return LoginTooManyFailedAttempts " +
            "(observed lockout boundary = attempt 5); body: {0}", body5);
    }

    // =========================================================================
    // BE-TC-05 — Locked account rejects even the CORRECT password
    // =========================================================================

    [Fact(DisplayName = "BE-TC-05: Correct password on a locked account → 400 LoginTooManyFailedAttempts (not 200, not invalid-creds)")]
    public async Task BeTc05_LockedAccount_RejectsCorrectPassword()
    {
        const string password = "Str0ng@Pass";
        var email = await RegisterFreshParentAsync("tc05", password);

        // Lock the account with 5 wrong-password attempts
        await SendWrongPasswordAttemptsAsync(email, 5);

        // Now try the CORRECT password — must still be rejected because IsLockedOut is checked first
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = password },
            acceptLanguage: "en-US");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-05: correct password on a locked account must return 400 (lockout takes precedence); body: {0}", body);

        TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
        succProp.GetBoolean().Should().BeFalse(
            "BE-TC-05: Successed must be false — lockout takes precedence over valid credentials; body: {0}", body);

        var msg = GetMessage(root, body);
        msg.Should().Be(MsgTooManyFailedAttempts,
            "BE-TC-05: must return LoginTooManyFailedAttempts (not 200, not LoginInvalidCredentials); body: {0}", body);
        msg.Should().NotBe(MsgInvalidCredentials,
            "BE-TC-05: must NOT be LoginInvalidCredentials — lockout check precedes password check; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-06 — Attempts beyond the threshold stay locked
    // =========================================================================

    [Fact(DisplayName = "BE-TC-06: 2 more wrong-password attempts after lockout → still 400 LoginTooManyFailedAttempts")]
    public async Task BeTc06_AttemptsAfterLockout_StayLocked()
    {
        var email = await RegisterFreshParentAsync("tc06");

        // Lock the account
        await SendWrongPasswordAttemptsAsync(email, 5);

        // Two more wrong-password attempts after lockout — should still be locked
        for (var extraAttempt = 1; extraAttempt <= 2; extraAttempt++)
        {
            var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
                new { UserName = email, Password = $"ExtraWrong#{extraAttempt}" },
                acceptLanguage: "en-US");

            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "BE-TC-06: extra attempt #{0} after lockout must return 400; body: {1}", extraAttempt, body);

            var msg = GetMessage(root, body);
            msg.Should().Be(MsgTooManyFailedAttempts,
                "BE-TC-06: extra attempt #{0} must still return LoginTooManyFailedAttempts; body: {1}", extraAttempt, body);
        }
    }

    // =========================================================================
    // BE-TC-07 — Successful sign-in resets the failed-attempt counter
    // =========================================================================

    [Fact(DisplayName = "BE-TC-07: 4 wrong + correct-pass resets counter; 4 more wrong after that → still invalid-creds (not locked)")]
    public async Task BeTc07_SuccessfulSignInResetsCounter()
    {
        const string password = "Str0ng@Pass";
        var email = await RegisterFreshParentAsync("tc07", password);

        // Step 1: 4 wrong-password attempts (one below threshold)
        await SendWrongPasswordAttemptsAsync(email, 4);

        // Step 2: correct-password sign-in — must succeed and reset counter
        var (successResp, successRoot, successBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = password },
            acceptLanguage: "en-US");

        successResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-07 step 2: correct password after 4 wrong attempts must return 200; body: {0}", successBody);

        TryProp(successRoot, "successed", out var succProp).Should().BeTrue("body: {0}", successBody);
        succProp.GetBoolean().Should().BeTrue(
            "BE-TC-07 step 2: Successed must be true after counter reset; body: {0}", successBody);

        // Step 3: 4 more wrong-password attempts — if counter was NOT reset, the cumulative 8 wrong
        // attempts would have exceeded the threshold long ago and the account would be locked.
        // The 4th of this second run should still be LoginInvalidCredentials (not locked),
        // proving the successful sign-in in step 2 reset the counter to 0.
        for (var i = 1; i <= 4; i++)
        {
            var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
                new { UserName = email, Password = $"SecondRoundWrong#{i}" },
                acceptLanguage: "en-US");

            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "BE-TC-07 step 3: attempt #{0} must return 400; body: {1}", i, body);

            var msg = GetMessage(root, body);
            msg.Should().Be(MsgInvalidCredentials,
                "BE-TC-07 step 3: attempt #{0} must be LoginInvalidCredentials " +
                "(counter was reset by the success in step 2, so 4 fresh attempts do not reach threshold 5); " +
                "body: {1}", i, body);
            msg.Should().NotBe(MsgTooManyFailedAttempts,
                "BE-TC-07 step 3: attempt #{0} must NOT be locked after counter reset; body: {1}", i, body);
        }
    }

    // =========================================================================
    // BE-TC-08 — Locked-account message is localized in Arabic
    // =========================================================================

    [Fact(DisplayName = "BE-TC-08: Locked account response with Accept-Language: ar-EG → Arabic LoginTooManyFailedAttempts message")]
    public async Task BeTc08_LockedMessage_IsLocalizedInArabic()
    {
        var email = await RegisterFreshParentAsync("tc08");

        // Lock the account using English (default) requests
        await SendWrongPasswordAttemptsAsync(email, 5);

        // Now sign in with Accept-Language: ar-EG on the locked account
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = "Wr0ng!Password99" },
            acceptLanguage: "ar-EG");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-08: locked account with ar-EG header must still return 400; body: {0}", body);

        TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
        succProp.GetBoolean().Should().BeFalse("body: {0}", body);

        var msg = GetMessage(root, body);
        msg.Should().Be(MsgTooManyFailedAttemptsAr,
            "BE-TC-08: with Accept-Language: ar-EG the message must be the Arabic LoginTooManyFailedAttempts; " +
            "body: {0}", body);
    }

    // =========================================================================
    // BE-TC-09 — Lockout auto-expiry documented (5-min window) — observation-only, not run in CI
    // =========================================================================

    [Fact(DisplayName = "BE-TC-09: OBSERVATION-ONLY — lockout auto-expiry after 5-min window (not asserted in CI; design expectation documented)")]
    public async Task BeTc09_LockoutAutoExpiry_ObservationOnly()
    {
        // Per the test case spec (BE-TC-09, Priority P2):
        // The 5-min DefaultLockoutTimeSpan means the account auto-unlocks after the window elapses.
        // A real 5-minute wait is not feasible in CI and would make the suite flaky/slow.
        // We assert the "inside-window-still-locked" half and skip the "after-window-unlocked" half.
        //
        // Design expectation (mitigates lockout-DoS, audit finding #3):
        //   - Attacker can lock a victim's account for 5 min with 5 bad passwords.
        //   - Account auto-unlocks without admin intervention after DefaultLockoutTimeSpan.
        //   - To assert the full expiry in CI, inject a clock seam (ISystemClock / DateTimeOffset)
        //     so tests can advance time without waiting.

        var email = await RegisterFreshParentAsync("tc09");

        // Lock the account
        await SendWrongPasswordAttemptsAsync(email, 5);

        // Confirm locked immediately after
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = "Wr0ng!Password99" },
            acceptLanguage: "en-US");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-09: account must be locked immediately after 5 failed attempts; body: {0}", body);

        var msg = GetMessage(root, body);
        msg.Should().Be(MsgTooManyFailedAttempts,
            "BE-TC-09: locked message must be LoginTooManyFailedAttempts immediately after lockout; body: {0}", body);

        // Auto-expiry after 5 min: NOT ASSERTED IN CI. Documented as design expectation only.
        // The lockout window expires after DefaultLockoutTimeSpan (5 min) and the account unlocks.
        // To verify: advance the host clock past LockoutEnd using a TestSystemClock seam, then sign in
        // with the correct password and assert 200 + valid JWT. This requires adding a clock seam
        // analogous to how P4-03 exposes factory.TestClock for streak tests. (Q3 open question.)
    }
}
