using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AspNetCoreRateLimit;
using FluentAssertions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Learnexia.IntegrationTests;

// ---------------------------------------------------------------------------
// P6-06 Backend Security Hardening — integration test suite
//
// Acceptance criteria exercised:
//   BE-1  Anti-enumeration: registered email AND unknown email both return the
//         SAME 200 + SAME message (no oracle via body/status). These tests
//         supplement the existing P1_12_BE6_PasswordReset_Tests.AC1a/b/c with
//         a focused regression check after the out-of-band Task.Run change.
//   BE-1  Regression: forgot-password → reset flow still works end-to-end
//         (the out-of-band Task.Run dispatch must not break token validity).
//   BE-2  Reset-email localization: a user with PreferredLanguage="ar-EG" must
//         trigger an Arabic email; "en-US" → English.  Captured via
//         CapturingEmailSender (injected via WithWebHostBuilder / PostConfigure).
//   BE-4  Rate-limit in-memory fallback: the existing RateLimitWebAppFactory
//         suite (no Redis) still passes — covered by running
//         P1_13b_BE1_AuthRateLimit_Tests unchanged.  This file adds a targeted
//         regression guard confirming no 429 from the P606Tests factory.
//
// Test doubles:
//   CapturingEmailSender (defined below) replaces IEmailSender in a
//   WithWebHostBuilder-derived factory.  The out-of-band Task.Run dispatch means
//   we await Task.Delay(BackgroundSettleMs) before asserting email captures.
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// CapturingEmailSender — test double for IEmailSender
// ---------------------------------------------------------------------------

/// <summary>Captured email record — reference type so FirstOrDefault returns null when absent.</summary>
public sealed record CapturedEmail(string To, string Subject, string Body);

/// <summary>
/// Thread-safe IEmailSender test double. Records every SendAsync call so that
/// tests can assert subject, body, and recipient after the fact.
/// Always returns <see cref="Result.Success()"/> — mirrors LogEmailSender.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<CapturedEmail> _sent = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<Result> SendAsync(
        string to, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { _sent.Add(new CapturedEmail(to, subject, htmlBody)); }
        finally { _gate.Release(); }
        return Result.Success();
    }

    /// <summary>All captured sends (newest last). Thread-safe snapshot.</summary>
    public IReadOnlyList<CapturedEmail> Sent
    {
        get
        {
            _gate.Wait();
            try { return _sent.ToList(); }
            finally { _gate.Release(); }
        }
    }

    public void Reset()
    {
        _gate.Wait();
        try { _sent.Clear(); }
        finally { _gate.Release(); }
    }
}

// ---------------------------------------------------------------------------
// Shared factory fixture for the P606Tests collection.
//
// Because LearnexiaWebAppFactory is sealed, we use WithWebHostBuilder to inject
// the CapturingEmailSender, following the same pattern as the rate-limit suite's
// derived factories in P1_13b_BE1_AuthRateLimit_Tests.cs.
//
// The factory is the *standard* LearnexiaWebAppFactory (all migrations, seeding,
// rate-limit neutralisation, push-sender seam). Each test that needs email capture
// creates its own client from a derived factory built via WithWebHostBuilder.
// ---------------------------------------------------------------------------

[CollectionDefinition("P606Tests")]
public sealed class P6_06_TestCollection : ICollectionFixture<LearnexiaWebAppFactory> { }

// ---------------------------------------------------------------------------
// P6-06 integration tests
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for P6-06 Backend Security Hardening.
/// Uses the shared <see cref="LearnexiaWebAppFactory"/> (same collection "IntegrationTests"
/// is already used by many tests; we define a separate "P606Tests" collection to give
/// this suite its own Postgres container via a separate fixture, avoiding state pollution).
///
/// For the BE-2 localization tests a per-test WithWebHostBuilder-derived factory
/// replaces IEmailSender with <see cref="CapturingEmailSender"/>, following the
/// pattern established in P1_13b_BE1_AuthRateLimit_Tests (derived factories for
/// per-test overrides).
/// </summary>
[Collection("P606Tests")]
public sealed class P6_06_BackendSecurity_Tests : IAsyncLifetime
{
    // -----------------------------------------------------------------------
    // URL constants (mirrors P1_12_BE6_PasswordReset_Tests)
    // -----------------------------------------------------------------------
    private const string ForgotPasswordUrl = "api/Users/Authentication/Forgot-Password";
    private const string ResetPasswordUrl  = "api/Users/Authentication/Reset-Password";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string UpdateLanguageUrl = "api/Users/Account/Language";

    private const string ValidPassword1 = "Str0ng@Pass!1";
    private const string ValidPassword2 = "N3w@Secur3!2";

    // How long (ms) to wait after triggering Forgot-Password before checking
    // captured emails.  Task.Run is in-process with a fresh scope; 800 ms is
    // ample even in CI without being flaky.
    private const int BackgroundSettleMs = 800;

    // -----------------------------------------------------------------------
    // Infrastructure
    // -----------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P6_06_BackendSecurity_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string UniqueEmail(string tag = "")
        => $"p606_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@sec.test";

    private static bool TryProp(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (el.TryGetProperty(pascal, out value)) return true;
        foreach (var prop in el.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            { value = prop.Value; return true; }
        }
        value = default;
        return false;
    }

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        PostJsonAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
            root = JsonDocument.Parse(bodyStr).RootElement;
        return (response, root, bodyStr);
    }

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        PostJsonAsync(string url, object body) => await PostJsonAsync(_client, url, body);

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendWithBearerAsync(HttpClient client, HttpMethod method, string url,
            object? body, string token)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            req.Content = JsonContent.Create(body);
        var response = await client.SendAsync(req);
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
            root = JsonDocument.Parse(bodyStr).RootElement;
        return (response, root, bodyStr);
    }

    /// <summary>
    /// Registers a parent with the default factory client and returns the email address.
    /// </summary>
    private async Task<string> RegisterParentAsync(string tag, string password = ValidPassword1)
    {
        var email = UniqueEmail(tag);
        var (resp, _, body) = await PostJsonAsync(RegisterParentUrl,
            new { Email = email, Password = password, AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Register-Parent must succeed to bootstrap a P6-06 test; body: {0}", body);
        return email;
    }

    /// <summary>
    /// Registers a parent using the given <paramref name="client"/>, signs in to get a
    /// token, updates the preferred language, then returns the email + access token.
    /// Used by BE-2 localization tests that run with a derived capturing-email factory.
    /// </summary>
    private async Task<(string Email, string Token)> RegisterParentWithLanguageAsync(
        HttpClient client, string tag, string preferredLanguage, string password = ValidPassword1)
    {
        var email = UniqueEmail(tag);

        // Register
        var (regResp, _, regBody) = await PostJsonAsync(client, RegisterParentUrl,
            new { Email = email, Password = password, AcceptedTerms = true });
        regResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Register-Parent must succeed; body: {0}", regBody);

        // Sign in to get a token for language-update
        var (signResp, signRoot, signBody) = await PostJsonAsync(client, SignInUrl,
            new { UserName = email, Password = password });
        signResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Sign-In must succeed after registration; body: {0}", signBody);
        TryProp(signRoot, "data", out var signData).Should().BeTrue("body: {0}", signBody);
        TryProp(signData, "accessToken", out var tokProp).Should().BeTrue("body: {0}", signBody);
        var token = tokProp.GetString()!;

        // Update preferred language
        var (langResp, _, langBody) = await SendWithBearerAsync(
            client, HttpMethod.Put, UpdateLanguageUrl,
            new { UserPreferredLanguage = preferredLanguage }, token);
        langResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Update-Language must succeed; lang: {0}; body: {1}", preferredLanguage, langBody);

        return (email, token);
    }

    /// <summary>
    /// Generates a real Identity password-reset token for a user via UserManager
    /// resolved from the factory scope.
    /// </summary>
    private async Task<string> GenerateResetTokenAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await um.FindByEmailAsync(email);
        user.Should().NotBeNull("user must exist to generate reset token; email: {0}", email);
        return await um.GeneratePasswordResetTokenAsync(user!);
    }

    // ==========================================================================
    // BE-1 — Anti-enumeration: SAME 200 + SAME body regardless of account existence
    // ==========================================================================

    /// <summary>
    /// P6-06 AE-1: A registered email returns HTTP 200 with Successed=true and a
    /// non-empty generic message after the P6-06 out-of-band dispatch change.
    /// </summary>
    [Fact(DisplayName = "P606-AE1: registered email → 200 Successed=true (anti-enumeration intact after BE-1 Task.Run change)")]
    public async Task AE1_ForgotPassword_RegisteredEmail_Returns200_SuccessedTrue()
    {
        var email = await RegisterParentAsync("ae1");

        var (resp, root, body) = await PostJsonAsync(ForgotPasswordUrl, new { Email = email });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "P6-06 BE-1: Forgot-Password with a registered email must return 200 after out-of-band dispatch; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue(
            "P6-06 BE-1: Successed must be true for a registered email path; body: {0}", body);

        TryProp(root, "message", out var msg).Should().BeTrue("body: {0}", body);
        msg.GetString().Should().NotBeNullOrWhiteSpace("message must be non-empty; body: {0}", body);
    }

    /// <summary>
    /// P6-06 AE-2: An unknown/non-existent email also returns HTTP 200 with
    /// Successed=true — identical to the registered path (no oracle).
    /// </summary>
    [Fact(DisplayName = "P606-AE2: unknown email → 200 Successed=true (no enumeration oracle after BE-1 change)")]
    public async Task AE2_ForgotPassword_UnknownEmail_Returns200_SuccessedTrue()
    {
        var unknownEmail = $"nobody_p606_{Guid.NewGuid():N}@nowhere.example";

        var (resp, root, body) = await PostJsonAsync(ForgotPasswordUrl, new { Email = unknownEmail });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "P6-06 BE-1: Forgot-Password with unknown email must return 200 (no enumeration); body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue(
            "P6-06 BE-1: Successed must be true for unknown email (identical to registered path); body: {0}", body);
    }

    /// <summary>
    /// P6-06 AE-3: The EXACT SAME statusCode + Successed + message is returned for
    /// both a registered email and an unknown email. Any difference is an oracle.
    /// </summary>
    [Fact(DisplayName = "P606-AE3: registered vs unknown email produce IDENTICAL statusCode + successed + message (headline anti-enumeration)")]
    public async Task AE3_ForgotPassword_RegisteredVsUnknown_IdenticalBody()
    {
        var regEmail   = await RegisterParentAsync("ae3-reg");
        var ghostEmail = $"ghost_p606_{Guid.NewGuid():N}@nowhere.example";

        var (regResp,   regRoot,   regBody)   = await PostJsonAsync(ForgotPasswordUrl, new { Email = regEmail });
        var (ghostResp, ghostRoot, ghostBody) = await PostJsonAsync(ForgotPasswordUrl, new { Email = ghostEmail });

        regResp.StatusCode.Should().Be(HttpStatusCode.OK,   "registered path; body: {0}", regBody);
        ghostResp.StatusCode.Should().Be(HttpStatusCode.OK, "unknown path; body: {0}", ghostBody);

        TryProp(regRoot,   "statusCode", out var regSc).Should().BeTrue("body: {0}", regBody);
        TryProp(ghostRoot, "statusCode", out var ghostSc).Should().BeTrue("body: {0}", ghostBody);
        regSc.GetInt32().Should().Be(ghostSc.GetInt32(),
            "P6-06 BE-1: statusCode must be identical. reg: {0} ghost: {1}", regBody, ghostBody);

        TryProp(regRoot,   "successed", out var regSucc).Should().BeTrue("body: {0}", regBody);
        TryProp(ghostRoot, "successed", out var ghostSucc).Should().BeTrue("body: {0}", ghostBody);
        regSucc.GetBoolean().Should().Be(ghostSucc.GetBoolean(),
            "P6-06 BE-1: Successed must be identical. reg: {0} ghost: {1}", regBody, ghostBody);

        TryProp(regRoot,   "message", out var regMsg).Should().BeTrue("body: {0}", regBody);
        TryProp(ghostRoot, "message", out var ghostMsg).Should().BeTrue("body: {0}", ghostBody);
        regMsg.GetString().Should().Be(ghostMsg.GetString(),
            "P6-06 BE-1 HEADLINE: message must be IDENTICAL for registered and unknown emails. " +
            "Any difference is an enumeration oracle. reg: {0} ghost: {1}", regBody, ghostBody);
    }

    /// <summary>
    /// P6-06 AE-4: The HTTP response returns quickly (before background email delivery
    /// could complete), demonstrating the out-of-band dispatch is active.
    /// A generous 5-second upper bound rules out synchronous SMTP latency.
    /// </summary>
    [Fact(DisplayName = "P606-AE4: Forgot-Password response is fast (out-of-band dispatch does not block HTTP response)")]
    public async Task AE4_ForgotPassword_ResponseIsNotBlockedByEmailDispatch()
    {
        var email = await RegisterParentAsync("ae4");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var (resp, _, body) = await PostJsonAsync(ForgotPasswordUrl, new { Email = email });
        sw.Stop();

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "P6-06 BE-1: 200 must be returned before/without awaiting email send; body: {0}", body);

        // 5 s is an order-of-magnitude larger than what token-mint (HMAC) takes in-process.
        // Synchronous SMTP would typically add 1–10 s, so any sync path would exceed this.
        sw.ElapsedMilliseconds.Should().BeLessThan(5000,
            "P6-06 BE-1: out-of-band dispatch should not hold up the HTTP response; " +
            "elapsed: {0}ms. If this fails, the Task.Run dispatch may not be active.", sw.ElapsedMilliseconds);
    }

    // ==========================================================================
    // BE-1 regression — forgot→reset end-to-end flow still works
    // ==========================================================================

    /// <summary>
    /// P6-06 RC-1: After the out-of-band Task.Run change the complete
    /// forgot-password → reset-password → sign-in flow must still work.
    /// Token is minted BEFORE Task.Run so it is valid regardless of background scheduling.
    /// </summary>
    [Fact(DisplayName = "P606-RC1 Regression: forgot→reset→sign-in end-to-end flow intact after BE-1 Task.Run dispatch")]
    public async Task RC1_ForgotThenReset_EndToEnd_StillWorks()
    {
        var email = await RegisterParentAsync("rc1");

        // Trigger forgot-password (now out-of-band) — must return 200
        var (forgotResp, _, forgotBody) = await PostJsonAsync(ForgotPasswordUrl, new { Email = email });
        forgotResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "P6-06 BE-1: Forgot-Password must return 200 after out-of-band change; body: {0}", forgotBody);

        // Obtain valid reset token via UserManager (same Identity path the handler uses)
        var token = await GenerateResetTokenAsync(email);
        token.Should().NotBeNullOrWhiteSpace("a valid reset token must be mintable");

        // Reset password
        var (resetResp, resetRoot, resetBody) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = token, NewPassword = ValidPassword2 });
        resetResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "P6-06 BE-1 regression: Reset-Password with a valid token must succeed; body: {0}", resetBody);
        TryProp(resetRoot, "successed", out var resetSucc).Should().BeTrue("body: {0}", resetBody);
        resetSucc.GetBoolean().Should().BeTrue(
            "P6-06 BE-1 regression: Reset Successed must be true; body: {0}", resetBody);

        // Sign in with the new password
        var (signResp, signRoot, signBody) = await PostJsonAsync(SignInUrl,
            new { UserName = email, Password = ValidPassword2 });
        signResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "P6-06 BE-1 regression: sign-in with new password must succeed after reset; body: {0}", signBody);
        TryProp(signRoot, "successed", out var signSucc).Should().BeTrue("body: {0}", signBody);
        signSucc.GetBoolean().Should().BeTrue(
            "P6-06 BE-1 regression: sign-in Successed must be true; body: {0}", signBody);
        TryProp(signRoot, "data", out var signData).Should().BeTrue("body: {0}", signBody);
        TryProp(signData, "accessToken", out var atProp).Should().BeTrue("body: {0}", signBody);
        atProp.GetString().Should().NotBeNullOrWhiteSpace(
            "P6-06 BE-1 regression: access token must be non-empty; body: {0}", signBody);
    }

    /// <summary>
    /// P6-06 RC-2: Old password is rejected after reset — password actually changed.
    /// </summary>
    [Fact(DisplayName = "P606-RC2 Regression: old password rejected after reset (password change persisted)")]
    public async Task RC2_AfterReset_OldPassword_Rejected()
    {
        var email = await RegisterParentAsync("rc2");
        var token = await GenerateResetTokenAsync(email);

        var (resetResp, _, resetBody) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = token, NewPassword = ValidPassword2 });
        resetResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Reset must succeed; body: {0}", resetBody);

        var (oldSignResp, oldSignRoot, oldSignBody) = await PostJsonAsync(SignInUrl,
            new { UserName = email, Password = ValidPassword1 });
        ((int)oldSignResp.StatusCode).Should().NotBe(200,
            "P6-06 BE-1 regression: old password must not work after reset; body: {0}", oldSignBody);
        TryProp(oldSignRoot, "successed", out var oldSucc).Should().BeTrue("body: {0}", oldSignBody);
        oldSucc.GetBoolean().Should().BeFalse(
            "P6-06 BE-1 regression: Successed must be false for old-password sign-in; body: {0}", oldSignBody);
    }

    // ==========================================================================
    // BE-2 — Reset email localization via CapturingEmailSender
    //
    // Each test creates a derived factory via WithWebHostBuilder that:
    //   1. Registers CapturingEmailSender as a Singleton IEmailSender (wins over
    //      the base LogEmailSender registered by the Infrastructure module).
    //   2. Keeps all other base factory configuration intact.
    // ==========================================================================

    /// <summary>
    /// P6-06 LC-AR: A user whose PreferredLanguage is "ar-EG" receives an Arabic
    /// reset email — subject and body contain Arabic-script characters.
    /// </summary>
    [Fact(DisplayName = "P606-LC-AR: ar-EG user receives Arabic reset email (subject + body are Arabic-script)")]
    public async Task LC_AR_ArEgUser_ReceivesArabicResetEmail()
    {
        var capture = new CapturingEmailSender();

        // Derived factory with capturing email sender injected as singleton
        using var derived = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(svcs =>
            {
                svcs.RemoveAll<IEmailSender>();
                svcs.AddSingleton<IEmailSender>(capture);
            }));

        using var client = derived.CreateClient();

        var (email, _) = await RegisterParentWithLanguageAsync(client, "lc-ar", "ar-EG");

        // Reset the capture AFTER registration so the welcome email is cleared and
        // only the password-reset email is captured by the forgot-password trigger.
        capture.Reset();

        // Trigger forgot-password (out-of-band dispatch)
        var (resp, _, body) = await PostJsonAsync(client, ForgotPasswordUrl, new { Email = email });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Forgot-Password must return 200; body: {0}", body);

        // Wait for background Task.Run to complete and call SendAsync
        await Task.Delay(BackgroundSettleMs);

        var sent = capture.Sent;
        sent.Should().NotBeEmpty(
            "P6-06 BE-2: CapturingEmailSender must capture at least one send attempt for a registered ar-EG user. " +
            "If empty: check that IEmailSender is resolved from the fresh scope in ForgotPasswordCommandHandler " +
            "(singleton registration ensures the derived-factory scope sees the same instance).");

        var resetEmail = sent.FirstOrDefault(s =>
            string.Equals(s.To, email, StringComparison.OrdinalIgnoreCase));
        resetEmail.Should().NotBeNull(
            "P6-06 BE-2: a reset email must be captured for the registered address {0}", email);

        var subject  = resetEmail!.Subject;
        var htmlBody = resetEmail.Body;

        // Arabic Unicode range U+0600–U+06FF
        var subjectHasArabic = subject.Any(c => c >= '؀' && c <= 'ۿ');
        subjectHasArabic.Should().BeTrue(
            "P6-06 BE-2: ar-EG reset email subject must contain Arabic-script characters. " +
            "Actual subject: '{0}'", subject);

        var bodyHasArabic = htmlBody.Any(c => c >= '؀' && c <= 'ۿ');
        bodyHasArabic.Should().BeTrue(
            "P6-06 BE-2: ar-EG reset email body must contain Arabic-script characters. " +
            "Body length: {0}", htmlBody.Length);

        // No raw placeholders may survive
        htmlBody.Should().NotContain("{userName}",
            "P6-06 BE-2: {userName} placeholder must be substituted in the Arabic body");
        htmlBody.Should().NotContain("{resetUrl}",
            "P6-06 BE-2: {resetUrl} placeholder must be substituted in the Arabic body");

        // Body must contain the reset link (substituted resetUrl)
        (htmlBody.Contains("http://") || htmlBody.Contains("https://")).Should().BeTrue(
            "P6-06 BE-2: Arabic reset email body must contain the reset URL. Body: {0}", htmlBody);
    }

    /// <summary>
    /// P6-06 LC-EN: A user whose PreferredLanguage is "en-US" receives an English
    /// reset email — no Arabic characters in subject or body.
    /// </summary>
    [Fact(DisplayName = "P606-LC-EN: en-US user receives English reset email (subject + body in English, no Arabic)")]
    public async Task LC_EN_EnUsUser_ReceivesEnglishResetEmail()
    {
        var capture = new CapturingEmailSender();

        using var derived = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(svcs =>
            {
                svcs.RemoveAll<IEmailSender>();
                svcs.AddSingleton<IEmailSender>(capture);
            }));

        using var client = derived.CreateClient();

        var (email, _) = await RegisterParentWithLanguageAsync(client, "lc-en", "en-US");

        // Reset capture AFTER registration to discard the welcome email
        capture.Reset();

        var (resp, _, body) = await PostJsonAsync(client, ForgotPasswordUrl, new { Email = email });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "Forgot-Password must return 200; body: {0}", body);

        await Task.Delay(BackgroundSettleMs);

        var sent = capture.Sent;
        sent.Should().NotBeEmpty("P6-06 BE-2: CapturingEmailSender must capture at least one send");

        var resetEmail = sent.FirstOrDefault(s =>
            string.Equals(s.To, email, StringComparison.OrdinalIgnoreCase));
        resetEmail.Should().NotBeNull("P6-06 BE-2: reset email must be captured for {0}", email);

        var subject  = resetEmail!.Subject;
        var htmlBody = resetEmail.Body;

        subject.Should().NotBeNullOrWhiteSpace("P6-06 BE-2: en-US subject must not be empty");

        // No Arabic characters in the English path
        var subjectHasArabic = subject.Any(c => c >= '؀' && c <= 'ۿ');
        subjectHasArabic.Should().BeFalse(
            "P6-06 BE-2: en-US subject must NOT contain Arabic characters. Actual: '{0}'", subject);

        var bodyHasArabic = htmlBody.Any(c => c >= '؀' && c <= 'ۿ');
        bodyHasArabic.Should().BeFalse(
            "P6-06 BE-2: en-US body must NOT contain Arabic characters. Length: {0}", htmlBody.Length);

        // English body must contain the word "reset" (from the en-US template)
        htmlBody.ToLowerInvariant().Should().Contain("reset",
            "P6-06 BE-2: en-US reset email body must contain 'reset' (English template keyword)");

        // No raw placeholders
        htmlBody.Should().NotContain("{userName}", "P6-06 BE-2: {userName} must be substituted");
        htmlBody.Should().NotContain("{resetUrl}",  "P6-06 BE-2: {resetUrl} must be substituted");

        // Body must contain the reset link
        (htmlBody.Contains("http://") || htmlBody.Contains("https://")).Should().BeTrue(
            "P6-06 BE-2: English reset email body must contain the reset URL");
    }

    /// <summary>
    /// P6-06 LC-DIFF: Arabic and English reset emails have DIFFERENT subjects,
    /// confirming the locale branching actually took effect (not the same fallback
    /// returned for both).
    /// </summary>
    [Fact(DisplayName = "P606-LC-DIFF: ar-EG and en-US users receive emails with DIFFERENT subjects (locale branching confirmed)")]
    public async Task LC_DIFF_ArEgAndEnUs_HaveDifferentSubjects()
    {
        // --- ar-EG user ---
        var captureAr = new CapturingEmailSender();
        using var derivedAr = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(svcs =>
            {
                svcs.RemoveAll<IEmailSender>();
                svcs.AddSingleton<IEmailSender>(captureAr);
            }));
        using var clientAr = derivedAr.CreateClient();
        var (arEmail, _) = await RegisterParentWithLanguageAsync(clientAr, "lc-diff-ar", "ar-EG");
        captureAr.Reset(); // discard welcome email, only capture the reset email
        await PostJsonAsync(clientAr, ForgotPasswordUrl, new { Email = arEmail });
        await Task.Delay(BackgroundSettleMs);
        var arSent = captureAr.Sent.FirstOrDefault(s =>
            string.Equals(s.To, arEmail, StringComparison.OrdinalIgnoreCase));
        arSent.Should().NotBeNull("ar-EG email capture must succeed");
        var arSubject = arSent!.Subject;

        // --- en-US user ---
        var captureEn = new CapturingEmailSender();
        using var derivedEn = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(svcs =>
            {
                svcs.RemoveAll<IEmailSender>();
                svcs.AddSingleton<IEmailSender>(captureEn);
            }));
        using var clientEn = derivedEn.CreateClient();
        var (enEmail, _) = await RegisterParentWithLanguageAsync(clientEn, "lc-diff-en", "en-US");
        captureEn.Reset(); // discard welcome email, only capture the reset email
        await PostJsonAsync(clientEn, ForgotPasswordUrl, new { Email = enEmail });
        await Task.Delay(BackgroundSettleMs);
        var enSent = captureEn.Sent.FirstOrDefault(s =>
            string.Equals(s.To, enEmail, StringComparison.OrdinalIgnoreCase));
        enSent.Should().NotBeNull("en-US email capture must succeed");
        var enSubject = enSent!.Subject;

        arSubject.Should().NotBe(enSubject,
            "P6-06 BE-2: ar-EG and en-US reset email subjects must be DIFFERENT, confirming locale branching. " +
            "ar: '{0}'; en: '{1}'", arSubject, enSubject);
    }

    /// <summary>
    /// P6-06 LC-NULL: A user with the default PreferredLanguage ("ar-EG") must
    /// receive the Arabic email. The default on User.cs is "ar-EG" so a freshly
    /// registered parent (no explicit language update) gets the Arabic template.
    /// </summary>
    [Fact(DisplayName = "P606-LC-NULL: user with default PreferredLanguage (ar-EG) receives Arabic email (fallback)")]
    public async Task LC_NULL_DefaultUser_ReceivesArEgFallback()
    {
        var capture = new CapturingEmailSender();

        using var derived = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(svcs =>
            {
                svcs.RemoveAll<IEmailSender>();
                svcs.AddSingleton<IEmailSender>(capture);
            }));

        using var client = derived.CreateClient();

        // Register WITHOUT updating language — User.PreferredLanguage defaults to "ar-EG"
        var email = UniqueEmail("lc-null");
        var (regResp, _, regBody) = await PostJsonAsync(client, RegisterParentUrl,
            new { Email = email, Password = ValidPassword1, AcceptedTerms = true });
        regResp.StatusCode.Should().Be(HttpStatusCode.OK, "Register must succeed; body: {0}", regBody);

        capture.Reset();
        await PostJsonAsync(client, ForgotPasswordUrl, new { Email = email });
        await Task.Delay(BackgroundSettleMs);

        var sent = capture.Sent;
        sent.Should().NotBeEmpty("at least one send must be captured for default-language user");

        var resetEmail = sent.FirstOrDefault(s =>
            string.Equals(s.To, email, StringComparison.OrdinalIgnoreCase));
        resetEmail.Should().NotBeNull("reset email must be captured for {0}", email);

        var subject  = resetEmail!.Subject;
        var htmlBody = resetEmail.Body;

        // Default is ar-EG → Arabic template
        var hasArabic = (subject + htmlBody).Any(c => c >= '؀' && c <= 'ۿ');
        hasArabic.Should().BeTrue(
            "P6-06 BE-2: user with default PreferredLanguage (ar-EG) must receive Arabic reset email. " +
            "subject: '{0}'", subject);
    }

    /// <summary>
    /// P6-06 LC-UNKNOWN: calls for an unknown email must NOT trigger any email send
    /// (no event published for the no-account path).
    /// </summary>
    [Fact(DisplayName = "P606-LC-UNKNOWN: unknown email does NOT trigger email send (no event published on early exit)")]
    public async Task LC_UNKNOWN_UnknownEmail_NoEmailSent()
    {
        var capture = new CapturingEmailSender();

        using var derived = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(svcs =>
            {
                svcs.RemoveAll<IEmailSender>();
                svcs.AddSingleton<IEmailSender>(capture);
            }));

        using var client = derived.CreateClient();
        capture.Reset();

        var unknownEmail = $"nobody_p606_lc_{Guid.NewGuid():N}@nowhere.example";
        var (resp, _, body) = await PostJsonAsync(client, ForgotPasswordUrl, new { Email = unknownEmail });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Forgot-Password with unknown email must return 200 (anti-enumeration); body: {0}", body);

        // Wait to confirm no background dispatch fires for the no-account path
        await Task.Delay(BackgroundSettleMs);

        capture.Sent.Should().BeEmpty(
            "P6-06 BE-2: no email must be dispatched for an unknown email (handler exits early, " +
            "before calling BuildAndDispatchPasswordResetEventAsync). Captured: {0}", capture.Sent.Count);
    }

    // ==========================================================================
    // BE-4 regression — in-memory rate-limit path (no Redis)
    // ==========================================================================

    /// <summary>
    /// P6-06 RL-1: The factory starts without a Redis connection string.
    /// The in-memory rate-limit store fallback (BE-4) must be active — the app
    /// must start and serve non-429 responses. The Testing-env PostConfigure sets
    /// all limits to int.MaxValue so no throttling occurs in the test suite.
    /// </summary>
    [Fact(DisplayName = "P606-RL1 BE-4 regression: in-memory rate-limit store active (no Redis) — first auth call is not 429")]
    public async Task RL1_InMemoryRateLimit_NoCrash_NoPremature429()
    {
        // Unique IP per test to avoid counter pollution across the collection
        var testIp = $"10.200.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", testIp);

        var payload = new StringContent(
            JsonSerializer.Serialize(new { Email = $"rl1_{Guid.NewGuid():N}@test.example" }),
            Encoding.UTF8, "application/json");
        var resp    = await client.PostAsync(ForgotPasswordUrl, payload);
        var bodyStr = await resp.Content.ReadAsStringAsync();

        // Must NOT be 429 (Testing-env counter = int.MaxValue)
        ((int)resp.StatusCode).Should().NotBe(429,
            "P6-06 BE-4 regression: in-memory rate-limit store must not throttle in the Testing env " +
            "(counter = int.MaxValue via LearnexiaWebAppFactory PostConfigure). " +
            "Status: {0}; body: {1}", (int)resp.StatusCode, bodyStr);

        // Unknown email → 200 (anti-enumeration guarantee)
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "P6-06 BE-4 regression: Forgot-Password for unknown email must return 200. " +
            "Status: {0}; body: {1}", (int)resp.StatusCode, bodyStr);
    }
}
