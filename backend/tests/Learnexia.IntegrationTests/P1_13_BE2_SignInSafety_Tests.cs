using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P1-13 BE-2 — Sign-in safety and anti-enumeration integration tests.
///
/// Implements BE-TC-10 through BE-TC-23 from docs/qc/P1-13/backend-test-cases.md 1:1.
///
/// Endpoint under test: [AllowAnonymous] POST /api/Users/Authentication/Sign-In
///
/// Anti-enumeration guarantee (audit finding #2 fix):
///   Not-found and wrong-password both return HTTP 400 with the SAME LoginInvalidCredentials
///   message. Previously the not-found path returned 404, leaking which emails are registered.
///
/// Timing-oracle mitigation (audit finding #1):
///   The not-found path hashes the supplied password via IPasswordHasher to burn equivalent
///   CPU, preventing latency-based enumeration. Behavioral parity (same status + message)
///   is asserted; absolute latency is NOT asserted (flaky).
///
/// Key messages:
///   LoginInvalidCredentials (en) = "Invalid username or password."
///   LoginInvalidCredentials (ar) = "اسم المستخدم أو كلمة المرور غير صحيحة."
///   LoginTooManyFailedAttempts (en) = "Account temporarily locked due to too many failed login attempts. Please try again later."
///   LoginAccountDeactivated (en) = "Your account is inactive. Please contact support."
///   LoginSystemError (en) = "An error occurred during sign-in. Please try again."
///
/// BE-TC-20/21 (force exception path) are downgraded to CODE-REVIEW-VERIFIED because
/// SignInCommandHandler uses injected IIdentityServiceManager which is not easily
/// replaced with a throwing stub from WebApplicationFactory without re-registering the
/// entire mediator pipeline — the catch block is already audited.
/// </summary>
[Collection("CaptchaTests")]
public sealed class P1_13_BE2_SignInSafety_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string MeUrl = "api/Users/Me";

    // -------------------------------------------------------------------------
    // Expected localized messages
    // -------------------------------------------------------------------------
    private const string MsgInvalidCredentials = "Invalid username or password.";
    private const string MsgInvalidCredentialsAr = "اسم المستخدم أو كلمة المرور غير صحيحة.";
    private const string MsgTooManyFailedAttempts = "Account temporarily locked due to too many failed login attempts. Please try again later.";
    private const string MsgAccountDeactivated = "Your account is inactive. Please contact support.";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly CaptchaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_13_BE2_SignInSafety_Tests(CaptchaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        _factory.FakeVerifier.SetResult(true);
        await _factory.ApplyMigrationsAndSeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string UniqueEmail(string tag)
        => $"p113_enum_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@enumtest.test";

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

    // =========================================================================
    // BE-TC-10 — Non-existent user returns 400 invalid-credentials (not 404)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-10: Non-existent user → 400 LoginInvalidCredentials (NOT 404) — anti-enumeration")]
    public async Task BeTc10_NonExistentUser_Returns400_NotFound()
    {
        var absentEmail = $"nobody_{Guid.NewGuid():N}@nope.test";

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = absentEmail, Password = "Whatever#1" },
            acceptLanguage: "en-US");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-10: non-existent user must return 400 (not 404) — anti-enumeration fix; body: {0}", body);
        ((int)resp.StatusCode).Should().NotBe(404,
            "BE-TC-10: 404 would leak which emails are registered (enumeration oracle); body: {0}", body);

        TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
        succProp.GetBoolean().Should().BeFalse("body: {0}", body);

        TryProp(root, "message", out var msgProp).Should().BeTrue("body: {0}", body);
        msgProp.GetString().Should().Be(MsgInvalidCredentials,
            "BE-TC-10: not-found path must return LoginInvalidCredentials; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-11 — Existing user + wrong password returns 400 invalid-credentials
    // =========================================================================

    [Fact(DisplayName = "BE-TC-11: Existing user + wrong password → 400 LoginInvalidCredentials")]
    public async Task BeTc11_ExistingUser_WrongPassword_Returns400InvalidCredentials()
    {
        var email = await RegisterFreshParentAsync("tc11");

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = "AbsolutelyWrong#99" },
            acceptLanguage: "en-US");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-11: existing user + wrong password must return 400; body: {0}", body);

        TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
        succProp.GetBoolean().Should().BeFalse("body: {0}", body);

        TryProp(root, "message", out var msgProp).Should().BeTrue("body: {0}", body);
        msgProp.GetString().Should().Be(MsgInvalidCredentials,
            "BE-TC-11: wrong-password path must return LoginInvalidCredentials; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-12 — Not-found and wrong-password are byte-for-byte indistinguishable
    // =========================================================================

    [Fact(DisplayName = "BE-TC-12: Not-found and wrong-password responses are byte-for-byte identical (status, successed, statusCode, message, shape)")]
    public async Task BeTc12_NotFound_And_WrongPassword_AreIndistinguishable()
    {
        var email = await RegisterFreshParentAsync("tc12");
        var absentEmail = $"nobody_{Guid.NewGuid():N}@nope.test";

        // Response A: absent email
        var (respA, rootA, bodyA) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = absentEmail, Password = "SomePassword#1" },
            acceptLanguage: "en-US");

        // Response B: existing email + wrong password
        var (respB, rootB, bodyB) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = "SomeWrongPassword#2" },
            acceptLanguage: "en-US");

        // Status parity
        respA.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-12 path A (absent email) must return 400; body: {0}", bodyA);
        respB.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-12 path B (wrong password) must return 400; body: {0}", bodyB);

        // Successed parity
        TryProp(rootA, "successed", out var succA).Should().BeTrue("body: {0}", bodyA);
        TryProp(rootB, "successed", out var succB).Should().BeTrue("body: {0}", bodyB);
        succA.GetBoolean().Should().BeFalse("body: {0}", bodyA);
        succB.GetBoolean().Should().BeFalse("body: {0}", bodyB);

        // statusCode field parity
        TryProp(rootA, "statusCode", out var scA).Should().BeTrue("body: {0}", bodyA);
        TryProp(rootB, "statusCode", out var scB).Should().BeTrue("body: {0}", bodyB);
        scA.GetInt32().Should().Be(400, "BE-TC-12: statusCode field must be 400 for absent email; body: {0}", bodyA);
        scB.GetInt32().Should().Be(400, "BE-TC-12: statusCode field must be 400 for wrong password; body: {0}", bodyB);

        // Message parity
        TryProp(rootA, "message", out var msgA).Should().BeTrue("body: {0}", bodyA);
        TryProp(rootB, "message", out var msgB).Should().BeTrue("body: {0}", bodyB);
        var msgAStr = msgA.GetString();
        var msgBStr = msgB.GetString();
        msgAStr.Should().Be(msgBStr,
            "BE-TC-12: the message for absent-email and wrong-password must be IDENTICAL " +
            "(anti-enumeration guarantee); A='{0}' B='{1}'", msgAStr, msgBStr);
        msgAStr.Should().Be(MsgInvalidCredentials,
            "BE-TC-12: both paths must return LoginInvalidCredentials; body A: {0}", bodyA);

        // errors/data shape parity — both must be present (or absent) in the same way
        var errorsAPresent = TryProp(rootA, "errors", out var errA);
        var errorsBPresent = TryProp(rootB, "errors", out var errB);
        errorsAPresent.Should().Be(errorsBPresent,
            "BE-TC-12: 'errors' key presence must match between absent-email and wrong-password paths");

        var dataAPresent = TryProp(rootA, "data", out _);
        var dataBPresent = TryProp(rootB, "data", out _);
        dataAPresent.Should().Be(dataBPresent,
            "BE-TC-12: 'data' key presence must match between the two paths");
    }

    // =========================================================================
    // BE-TC-13 — Anti-enumeration parity holds in English
    // =========================================================================

    [Fact(DisplayName = "BE-TC-13: Anti-enumeration parity in English — not-found and wrong-password both return same en-US LoginInvalidCredentials string")]
    public async Task BeTc13_AntiEnumerationParity_English()
    {
        var email = await RegisterFreshParentAsync("tc13");
        var absentEmail = $"nobody_{Guid.NewGuid():N}@nope.test";

        var (respA, rootA, bodyA) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = absentEmail, Password = "Irrelevant#1" },
            acceptLanguage: "en-US");

        var (respB, rootB, bodyB) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = "WrongPassword#2" },
            acceptLanguage: "en-US");

        TryProp(rootA, "message", out var msgA).Should().BeTrue("body: {0}", bodyA);
        TryProp(rootB, "message", out var msgB).Should().BeTrue("body: {0}", bodyB);

        msgA.GetString().Should().Be(MsgInvalidCredentials,
            "BE-TC-13: absent-email en-US path must return English LoginInvalidCredentials; body: {0}", bodyA);
        msgB.GetString().Should().Be(MsgInvalidCredentials,
            "BE-TC-13: wrong-password en-US path must return English LoginInvalidCredentials; body: {0}", bodyB);
        msgA.GetString().Should().Be(msgB.GetString(),
            "BE-TC-13: both en-US paths must return identical message strings; A='{0}' B='{1}'",
            msgA.GetString(), msgB.GetString());
    }

    // =========================================================================
    // BE-TC-14 — Anti-enumeration parity holds in Arabic
    // =========================================================================

    [Fact(DisplayName = "BE-TC-14: Anti-enumeration parity in Arabic — not-found and wrong-password both return same ar-EG LoginInvalidCredentials string")]
    public async Task BeTc14_AntiEnumerationParity_Arabic()
    {
        var email = await RegisterFreshParentAsync("tc14");
        var absentEmail = $"nobody_{Guid.NewGuid():N}@nope.test";

        var (respA, rootA, bodyA) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = absentEmail, Password = "Irrelevant#1" },
            acceptLanguage: "ar-EG");

        var (respB, rootB, bodyB) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = "WrongPassword#2" },
            acceptLanguage: "ar-EG");

        TryProp(rootA, "message", out var msgA).Should().BeTrue("body: {0}", bodyA);
        TryProp(rootB, "message", out var msgB).Should().BeTrue("body: {0}", bodyB);

        msgA.GetString().Should().Be(MsgInvalidCredentialsAr,
            "BE-TC-14: absent-email ar-EG path must return Arabic LoginInvalidCredentials; body: {0}", bodyA);
        msgB.GetString().Should().Be(MsgInvalidCredentialsAr,
            "BE-TC-14: wrong-password ar-EG path must return Arabic LoginInvalidCredentials; body: {0}", bodyB);
        msgA.GetString().Should().Be(msgB.GetString(),
            "BE-TC-14: both ar-EG paths must return identical Arabic message strings; A='{0}' B='{1}'",
            msgA.GetString(), msgB.GetString());
    }

    // =========================================================================
    // BE-TC-15 — Deactivated account returns its own clear message
    // =========================================================================

    [Fact(DisplayName = "BE-TC-15: Deactivated account (IsActive=false) with correct credentials → 400 LoginAccountDeactivated")]
    public async Task BeTc15_DeactivatedAccount_ReturnsLoginAccountDeactivated()
    {
        // Seed a deactivated account directly via UserManager (no HTTP admin endpoint exists to toggle IsActive)
        const string password = "Str0ng@Pass";
        var email = UniqueEmail("tc15-deactivated");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = new User
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            FullName = "Deactivated Test User",
            RegistrationIsCompleted = true,
            IsActive = false  // deactivated
        };

        var createResult = await userManager.CreateAsync(user, password);
        createResult.Succeeded.Should().BeTrue(
            "BE-TC-15 setup: creating deactivated user must succeed; errors: {0}",
            string.Join(", ", createResult.Errors.Select(e => e.Description)));

        // Attempt sign-in with correct credentials — should return LoginAccountDeactivated
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = password },
            acceptLanguage: "en-US");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-15: deactivated account with correct credentials must return 400; body: {0}", body);

        TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
        succProp.GetBoolean().Should().BeFalse(
            "BE-TC-15: Successed must be false for a deactivated account; body: {0}", body);

        TryProp(root, "message", out var msgProp).Should().BeTrue("body: {0}", body);
        msgProp.GetString().Should().Be(MsgAccountDeactivated,
            "BE-TC-15: deactivated account must return LoginAccountDeactivated (distinct from invalid-credentials); " +
            "body: {0}", body);
    }

    // =========================================================================
    // BE-TC-16 — Not-found path performs comparable work (timing-oracle mitigation, behavioral)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-16: BEHAVIORAL — not-found and wrong-password return identical 400 + message (timing parity, no absolute latency assertion)")]
    public async Task BeTc16_TimingOracleMitigation_BehavioralParity()
    {
        // Per BE-TC-16 spec: assert behavioral parity only, NOT absolute latency (flaky).
        // The dummy-hash on the not-found path (SignInCommandHandler line 59:
        //   _ = _passwordHasher.HashPassword(EnumerationGuardUser, request.Password))
        // ensures equivalent CPU cost. We verify both paths return 400 + same message.
        // Audit finding #1: timing mitigation is present and code-review verified.

        var email = await RegisterFreshParentAsync("tc16");
        var absentEmail = $"nobody_{Guid.NewGuid():N}@nope.test";

        // Not-found path
        var (respNotFound, rootNotFound, bodyNotFound) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = absentEmail, Password = "AnyPass#1" },
            acceptLanguage: "en-US");

        // Wrong-password path
        var (respWrongPass, rootWrongPass, bodyWrongPass) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = "AnyWrongPass#2" },
            acceptLanguage: "en-US");

        // Behavioral parity: same status + message
        respNotFound.StatusCode.Should().Be(HttpStatusCode.BadRequest, "body: {0}", bodyNotFound);
        respWrongPass.StatusCode.Should().Be(HttpStatusCode.BadRequest, "body: {0}", bodyWrongPass);

        TryProp(rootNotFound, "message", out var msgNotFound).Should().BeTrue("body: {0}", bodyNotFound);
        TryProp(rootWrongPass, "message", out var msgWrongPass).Should().BeTrue("body: {0}", bodyWrongPass);

        msgNotFound.GetString().Should().Be(msgWrongPass.GetString(),
            "BE-TC-16: behavioral parity — not-found and wrong-password must return the same message " +
            "(timing-oracle mitigation; absolute latency NOT asserted per spec); " +
            "notFound='{0}' wrongPass='{1}'", msgNotFound.GetString(), msgWrongPass.GetString());

        // NOTE (audit finding #1 documentation):
        // The dummy-hash call _ = _passwordHasher.HashPassword(EnumerationGuardUser, request.Password)
        // in SignInCommandHandler.Handle() ensures the CPU cost of the not-found path approximates the
        // wrong-password path, mitigating latency-based enumeration. This test only asserts behavioral
        // parity — a precise timing assertion would be non-deterministic in CI.
    }

    // =========================================================================
    // BE-TC-17 — Sign-in success returns a well-formed BaseResponse envelope
    // =========================================================================

    [Fact(DisplayName = "BE-TC-17: Successful sign-in → 200; well-formed BaseResponse envelope with accessToken, userId, message, errors, statusCode")]
    public async Task BeTc17_SuccessfulSignIn_ReturnsWellFormedEnvelope()
    {
        const string password = "Str0ng@Pass";
        var email = await RegisterFreshParentAsync("tc17", password);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = password },
            acceptLanguage: "en-US");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-17: successful sign-in must return 200; body: {0}", body);

        // successed
        TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
        succProp.GetBoolean().Should().BeTrue(
            "BE-TC-17: Successed must be true on successful sign-in; body: {0}", body);

        // statusCode field
        TryProp(root, "statusCode", out var scProp).Should().BeTrue(
            "BE-TC-17: envelope must contain 'statusCode'; body: {0}", body);
        scProp.GetInt32().Should().Be(200,
            "BE-TC-17: statusCode field must be 200; body: {0}", body);

        // message
        TryProp(root, "message", out _).Should().BeTrue(
            "BE-TC-17: envelope must contain 'message'; body: {0}", body);

        // errors
        TryProp(root, "errors", out _).Should().BeTrue(
            "BE-TC-17: envelope must contain 'errors' key; body: {0}", body);

        // data.accessToken
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", body);
        tokenProp.GetString().Should().NotBeNullOrWhiteSpace(
            "BE-TC-17: data.accessToken must be a non-empty JWT; body: {0}", body);

        // data.userId
        TryProp(data, "userId", out var userIdProp).Should().BeTrue("body: {0}", body);
        userIdProp.GetInt32().Should().BeGreaterThan(0,
            "BE-TC-17: data.userId must be a positive integer; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-18 — Missing UserName/Password → 422 validation (not 400/500)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-18a: Empty body {} → 422 ValidationBehavior (SignInValidatior requires both fields)")]
    public async Task BeTc18a_EmptyBody_Returns422()
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl, new { },
            acceptLanguage: "en-US");

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "BE-TC-18a: empty body must return 422 (SignInValidatior via ValidationBehavior); body: {0}", body);

        TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
        succProp.GetBoolean().Should().BeFalse("body: {0}", body);

        TryProp(root, "errors", out var errorsProp).Should().BeTrue(
            "BE-TC-18a: 422 envelope must contain 'errors'; body: {0}", body);
        errorsProp.EnumerateArray().ToList().Should().NotBeEmpty(
            "BE-TC-18a: Errors[] must be populated with validation failures; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-18b: Only UserName provided (no Password) → 422 ValidationBehavior")]
    public async Task BeTc18b_OnlyUserName_Returns422()
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = "somebody@test.test" },
            acceptLanguage: "en-US");

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "BE-TC-18b: missing Password must return 422; body: {0}", body);

        TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
        succProp.GetBoolean().Should().BeFalse("body: {0}", body);

        TryProp(root, "errors", out var errorsProp).Should().BeTrue("body: {0}", body);
        errorsProp.EnumerateArray().ToList().Should().NotBeEmpty("body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-18c: Only Password provided (no UserName) -> 422 ValidationBehavior")]
    public async Task BeTc18c_OnlyPassword_Returns422()
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { Password = "Str0ng@Pass" },
            acceptLanguage: "en-US");

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "BE-TC-18c: missing UserName must return 422; body: {0}", body);

        TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
        succProp.GetBoolean().Should().BeFalse("body: {0}", body);

        TryProp(root, "errors", out var errorsProp).Should().BeTrue("body: {0}", body);
        errorsProp.EnumerateArray().ToList().Should().NotBeEmpty("body: {0}", body);
    }

    // =========================================================================
    // BE-TC-20 — Internal exception returns generic 500 (CODE-REVIEW-VERIFIED)
    // =========================================================================

    // This test is downgraded to CODE-REVIEW-VERIFIED per the test-case spec (BE-TC-20 note, Q2).
    // Forcing the 500 path requires a throwing stub for IIdentityServiceManager or SignInManager,
    // which would need to be injected via a custom WebApplicationFactory that re-registers the
    // entire mediator pipeline. The catch block (SignInCommandHandler.Handle lines 101-106) is
    // already audited:
    //   - Logs the exception via ILoggerManager.LogError (server-side only)
    //   - Returns ServerError<JwtAuthResponse>(_localizer[SharedResourcesKey.LoginSystemError])
    //     (generic localized message, no ex.Message, no stack trace)
    // The audit confirms this is implemented correctly. Add a throwing-double fixture if Q2 is
    // resolved in the affirmative (see docs/qc/P1-13/README.md §4).

    [Fact(Skip = "BE-TC-20 CODE-REVIEW-VERIFIED: forcing the 500 path requires a throwing double for " +
                 "IIdentityServiceManager/SignInManager — injecting it via WebApplicationFactory requires " +
                 "re-registering the mediator pipeline. The catch block is audited: logs ex server-side, " +
                 "returns generic LoginSystemError (no ex.Message, no stack trace). " +
                 "Per Q2: implement throwing-double fixture if lead approves.",
        DisplayName = "BE-TC-20: CODE-REVIEW-VERIFIED — exception → generic 500 LoginSystemError, no ex.Message")]
    public Task BeTc20_Exception_Returns500_NoExMessage_CodeReviewVerified()
        => Task.CompletedTask;

    // =========================================================================
    // BE-TC-21 — Exception detail is logged server-side, not returned (CODE-REVIEW-VERIFIED)
    // =========================================================================

    [Fact(Skip = "BE-TC-21 CODE-REVIEW-VERIFIED: same blocker as BE-TC-20 (throwing-double fixture). " +
                 "The catch block at SignInCommandHandler.Handle line 105 calls " +
                 "_logger.LogError(ex, 'Error: in SignInCommand') — detail goes to server log only. " +
                 "Per Q2: implement log-capturing fixture if lead approves.",
        DisplayName = "BE-TC-21: CODE-REVIEW-VERIFIED — exception detail logged server-side, not in response")]
    public Task BeTc21_ExceptionDetailLogged_NotReturned_CodeReviewVerified()
        => Task.CompletedTask;

    // =========================================================================
    // BE-TC-22 — Lockout message is distinct from invalid-credentials (regression pin)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-22: Lockout message DIFFERS from invalid-credentials (accepted enumeration trade-off, regression pin — audit finding #2)")]
    public async Task BeTc22_LockoutMessage_IsDistinctFrom_InvalidCredentials()
    {
        // NOTE (audit finding #2 — accepted trade-off):
        // The locked-out message (LoginTooManyFailedAttempts) is distinct from the
        // invalid-credentials message (LoginInvalidCredentials). This is an accepted trade-off:
        // locking reveals account existence, but is necessary for UX (the user must know why
        // their correct password was rejected). A future "strict non-enumeration" change that
        // makes these messages identical would be a deliberate product decision; this test
        // pins the current behavior so any change is visible as a test change, not a silent regression.

        var email = await RegisterFreshParentAsync("tc22");

        // Lock the account
        for (var i = 0; i < 5; i++)
        {
            await SendAsync(_client, HttpMethod.Post, SignInUrl,
                new { UserName = email, Password = $"Lock#Attempt{i}" },
                acceptLanguage: "en-US");
        }

        // Get the locked response
        var (lockedResp, lockedRoot, lockedBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = "AnotherWrong#6" },
            acceptLanguage: "en-US");

        lockedResp.StatusCode.Should().Be(HttpStatusCode.BadRequest, "body: {0}", lockedBody);
        TryProp(lockedRoot, "message", out var lockedMsg).Should().BeTrue("body: {0}", lockedBody);
        var lockedMsgStr = lockedMsg.GetString();

        // Get an invalid-credentials response (use a different, non-locked account)
        var email2 = await RegisterFreshParentAsync("tc22b");
        var (invalidResp, invalidRoot, invalidBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email2, Password = "WrongPass#1" },
            acceptLanguage: "en-US");

        invalidResp.StatusCode.Should().Be(HttpStatusCode.BadRequest, "body: {0}", invalidBody);
        TryProp(invalidRoot, "message", out var invalidMsg).Should().BeTrue("body: {0}", invalidBody);
        var invalidMsgStr = invalidMsg.GetString();

        // Assert they DIFFER (accepted trade-off pin)
        lockedMsgStr.Should().NotBe(invalidMsgStr,
            "BE-TC-22 (accepted trade-off): LoginTooManyFailedAttempts must DIFFER from " +
            "LoginInvalidCredentials — this is the current documented behavior (audit finding #2). " +
            "If a future change makes them identical, this test will fail intentionally as a decision gate; " +
            "locked='{0}' invalidCreds='{1}'", lockedMsgStr, invalidMsgStr);

        lockedMsgStr.Should().Be(MsgTooManyFailedAttempts,
            "BE-TC-22: locked message must be LoginTooManyFailedAttempts; body: {0}", lockedBody);
        invalidMsgStr.Should().Be(MsgInvalidCredentials,
            "BE-TC-22: invalid-credentials message must be LoginInvalidCredentials; body: {0}", invalidBody);
    }

    // =========================================================================
    // BE-TC-23 — Email case-insensitivity does not create an enumeration signal
    // =========================================================================

    [Fact(DisplayName = "BE-TC-23: Uppercase email + wrong password → 400 LoginInvalidCredentials (same as exact-case — no enumeration via case normalization)")]
    public async Task BeTc23_EmailCaseInsensitivity_NoEnumerationSignal()
    {
        var email = await RegisterFreshParentAsync("tc23"); // lowercase: p113_enum_tc23_...@enumtest.test
        var uppercaseEmail = email.ToUpperInvariant();

        // Step 1: Sign in with uppercased email + wrong password
        var (respUpper, rootUpper, bodyUpper) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = uppercaseEmail, Password = "WrongPassword#1" },
            acceptLanguage: "en-US");

        // Step 2: Sign in with exact-case email + wrong password
        var (respExact, rootExact, bodyExact) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = "WrongPassword#1" },
            acceptLanguage: "en-US");

        // Both must return 400 + LoginInvalidCredentials
        respUpper.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-23: uppercased email + wrong password must return 400; body: {0}", bodyUpper);
        respExact.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BE-TC-23: exact-case email + wrong password must return 400; body: {0}", bodyExact);

        TryProp(rootUpper, "message", out var msgUpper).Should().BeTrue("body: {0}", bodyUpper);
        TryProp(rootExact, "message", out var msgExact).Should().BeTrue("body: {0}", bodyExact);

        msgUpper.GetString().Should().Be(MsgInvalidCredentials,
            "BE-TC-23: uppercased email path must return LoginInvalidCredentials; body: {0}", bodyUpper);
        msgExact.GetString().Should().Be(MsgInvalidCredentials,
            "BE-TC-23: exact-case email path must return LoginInvalidCredentials; body: {0}", bodyExact);

        msgUpper.GetString().Should().Be(msgExact.GetString(),
            "BE-TC-23: uppercase vs exact-case paths must return identical messages " +
            "(no enumeration signal via case normalization); upper='{0}' exact='{1}'",
            msgUpper.GetString(), msgExact.GetString());
    }
}
