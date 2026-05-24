using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P1-12 BE-6 integration tests: Password Reset (Forgot-Password + Reset-Password).
///
/// Endpoints under test (both AllowAnonymous, BaseResponse&lt;string&gt;):
///   POST /api/Users/Authentication/Forgot-Password   { email }
///   POST /api/Users/Authentication/Reset-Password    { email, token, newPassword }
///
/// Strategy for obtaining a valid reset token:
///   UserManager&lt;User&gt; is resolved from a service scope on the factory so that tests
///   generate real Identity tokens without needing to receive email. Each test generates
///   its own fresh token (single-use enforcement means re-use fails).
///
/// Acceptance criteria covered:
///   AC-1  Anti-enumeration (forgot): registered email AND non-existent email both return identical 200 + identical body.
///   AC-2  Reset happy path: new password works for sign-in; old password no longer works.
///   AC-3  Session invalidation: refresh token obtained before reset cannot be exchanged after reset.
///   AC-4  Reused token: same reset token used a second time returns generic 400 (single-use).
///   AC-5  Bad/garbage token: returns generic 400 (ResetPasswordInvalidLink).
///   AC-6  Unknown email on reset: returns same generic 400 (no enumeration).
///   AC-7  Weak new password (fails Identity policy): returns generic 400.
///   AC-8  Validation: empty email (forgot) / empty token or newPassword (reset) returns 422.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P1_12_BE6_PasswordReset_Tests : IAsyncLifetime
{
    // ---------------------------------------------------------------------------
    // URLs
    // ---------------------------------------------------------------------------
    private const string ForgotPasswordUrl = "api/Users/Authentication/Forgot-Password";
    private const string ResetPasswordUrl = "api/Users/Authentication/Reset-Password";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string RefreshTokenUrl = "api/Users/Authentication/Refresh-Token";

    // Passwords satisfying ASP.NET Identity policy:
    // RequireDigit + RequireLowercase + RequireUppercase + RequireNonAlphanumeric + MinLength 6
    private const string ValidPassword1 = "Str0ng@Pass";
    private const string ValidPassword2 = "N3w@Secur3!";

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_12_BE6_PasswordReset_Tests(LearnexiaWebAppFactory factory)
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
        => $"p112be6_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@reset.test";

    /// <summary>
    /// Case-insensitive JSON property lookup. Handles both the controller (camelCase via Newtonsoft)
    /// and the ErrorHandlerMiddleWare (PascalCase via System.Text.Json) serialization paths.
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

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)> PostJsonAsync(
        string path, object body)
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

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)> PostJsonWithBearerAsync(
        string path, object body, string accessToken)
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
    /// Registers a new parent and returns the email. Asserts success inline.
    /// </summary>
    private async Task<string> RegisterParentAsync(string tag, string password = ValidPassword1)
    {
        var email = UniqueEmail(tag);
        var (response, _, body) = await PostJsonAsync(RegisterParentUrl,
            new { Email = email, Password = password, AcceptedTerms = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Register-Parent must succeed to bootstrap a P1-12-BE-6 test; body: {0}", body);

        return email;
    }

    /// <summary>
    /// Registers a parent AND returns a sign-in response with access+refresh tokens.
    /// </summary>
    private async Task<(string Email, string AccessToken, string RefreshTokenString)>
        RegisterAndSignInAsync(string tag, string password = ValidPassword1)
    {
        var email = await RegisterParentAsync(tag, password);

        var (signInResp, signInRoot, signInBody) = await PostJsonAsync(SignInUrl,
            new { UserName = email, Password = password });

        signInResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Sign-In must succeed after Register-Parent; body: {0}", signInBody);

        TryProp(signInRoot, "data", out var data).Should().BeTrue("body: {0}", signInBody);
        TryProp(data, "accessToken", out var at).Should().BeTrue("body: {0}", signInBody);
        TryProp(data, "refreshToken", out var rt).Should().BeTrue("body: {0}", signInBody);
        TryProp(rt, "tokenString", out var ts).Should().BeTrue("body: {0}", signInBody);

        return (email, at.GetString()!, ts.GetString()!);
    }

    /// <summary>
    /// Generates a real Identity password-reset token for a user by email using UserManager
    /// resolved from the factory's service scope. Each call mints a fresh single-use token.
    /// </summary>
    private async Task<string> GenerateResetTokenViaUserManagerAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull(
            "user must exist to generate a password reset token; email: {0}", email);

        return await userManager.GeneratePasswordResetTokenAsync(user!);
    }

    /// <summary>
    /// Constructs a JWT that is expired but otherwise structurally valid, using the known
    /// test signing key from appsettings.json. Required for Refresh-Token happy-path tests
    /// because the endpoint rejects non-expired access tokens (TokenIsRunning → 400).
    /// </summary>
    private static string CreateExpiredAccessToken(string originalToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var original = handler.ReadJwtToken(originalToken);

            var claimsToKeep = original.Claims
                .Where(c => c.Type != "exp" && c.Type != "iat" && c.Type != "nbf")
                .ToList();

            var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(
                    "CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789"));

            var expiredToken = new JwtSecurityToken(
                issuer: original.Issuer,
                audience: original.Audiences.FirstOrDefault(),
                claims: claimsToKeep,
                notBefore: DateTime.UtcNow.AddMinutes(-60),
                expires: DateTime.UtcNow.AddMinutes(-30),
                signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    signingKey,
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature));

            return handler.WriteToken(expiredToken);
        }
        catch
        {
            // Fallback: append suffix to exercise the error path.
            return originalToken + "_expired_suffix";
        }
    }

    // ===========================================================================
    // AC-1  Anti-enumeration: Forgot-Password ALWAYS returns identical 200 + body
    // ===========================================================================

    /// <summary>
    /// AC-1: Both a registered email and a non-existent email return 200 Successed=true with
    /// the IDENTICAL response body. This is the headline security property of Forgot-Password.
    /// </summary>
    [Fact(DisplayName = "AC-1a AntiEnum: registered email → 200 Successed=true with generic message")]
    public async Task AC1a_ForgotPassword_RegisteredEmail_Returns200_WithSuccessedTrue()
    {
        var email = await RegisterParentAsync("ac1a");

        var (response, root, body) = await PostJsonAsync(ForgotPasswordUrl, new { Email = email });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Forgot-Password with a registered email must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue(
            "response must have 'successed' key per BaseResponse<T>; body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "AC-1: Forgot-Password with registered email must have Successed=true; body: {0}", body);

        TryProp(root, "message", out var msg).Should().BeTrue(
            "response must have 'message' key; body: {0}", body);
        msg.GetString().Should().NotBeNullOrWhiteSpace(
            "message must be non-empty; body: {0}", body);
    }

    [Fact(DisplayName = "AC-1b AntiEnum: non-existent email → 200 Successed=true with generic message")]
    public async Task AC1b_ForgotPassword_NonExistentEmail_Returns200_WithSuccessedTrue()
    {
        var nonExistentEmail = $"nonexistent_{Guid.NewGuid():N}@nowhere.example";

        var (response, root, body) = await PostJsonAsync(ForgotPasswordUrl,
            new { Email = nonExistentEmail });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Forgot-Password with a non-existent email must return 200 (no enumeration); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue(
            "response must have 'successed' key; body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "AC-1: Forgot-Password with a non-existent email must have Successed=true (identical to registered path); body: {0}", body);

        TryProp(root, "message", out var msg).Should().BeTrue("body: {0}", body);
        msg.GetString().Should().NotBeNullOrWhiteSpace("message must be non-empty; body: {0}", body);
    }

    [Fact(DisplayName = "AC-1c AntiEnum: registered vs non-existent produce IDENTICAL statusCode + successed + message")]
    public async Task AC1c_ForgotPassword_RegisteredVsNonExistent_IdenticalBody()
    {
        // Registered path.
        var registeredEmail = await RegisterParentAsync("ac1c-reg");
        var (regResp, regRoot, regBody) = await PostJsonAsync(ForgotPasswordUrl,
            new { Email = registeredEmail });

        // Non-existent path.
        var ghostEmail = $"ghost_{Guid.NewGuid():N}@nowhere.example";
        var (ghostResp, ghostRoot, ghostBody) = await PostJsonAsync(ForgotPasswordUrl,
            new { Email = ghostEmail });

        // Both must be HTTP 200.
        regResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "registered email must return 200; body: {0}", regBody);
        ghostResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "non-existent email must return 200; body: {0}", ghostBody);

        // statusCode field inside the envelope must match.
        TryProp(regRoot, "statusCode", out var regSc).Should().BeTrue("body: {0}", regBody);
        TryProp(ghostRoot, "statusCode", out var ghostSc).Should().BeTrue("body: {0}", ghostBody);
        regSc.GetInt32().Should().Be(ghostSc.GetInt32(),
            "AC-1: statusCode inside envelope must be identical for registered vs non-existent email (no enumeration). " +
            "registered body: {0}, ghost body: {1}", regBody, ghostBody);

        // successed must match (both true).
        TryProp(regRoot, "successed", out var regSucc).Should().BeTrue("body: {0}", regBody);
        TryProp(ghostRoot, "successed", out var ghostSucc).Should().BeTrue("body: {0}", ghostBody);
        regSucc.GetBoolean().Should().Be(ghostSucc.GetBoolean(),
            "AC-1: Successed must be identical for registered vs non-existent email. " +
            "registered body: {0}, ghost body: {1}", regBody, ghostBody);

        // message must be identical (same localized string).
        TryProp(regRoot, "message", out var regMsg).Should().BeTrue("body: {0}", regBody);
        TryProp(ghostRoot, "message", out var ghostMsg).Should().BeTrue("body: {0}", ghostBody);
        regMsg.GetString().Should().Be(ghostMsg.GetString(),
            "AC-1 HEADLINE: Forgot-Password must return the SAME message for registered and non-existent emails. " +
            "Any difference is an enumeration oracle. registered body: {0}, ghost body: {1}", regBody, ghostBody);
    }

    // ===========================================================================
    // AC-2  Reset happy path: new password works, old password fails
    // ===========================================================================

    [Fact(DisplayName = "AC-2a HappyReset: valid token + new password → 200 Successed=true")]
    public async Task AC2a_ResetPassword_ValidToken_Returns200()
    {
        var email = await RegisterParentAsync("ac2a");
        var token = await GenerateResetTokenViaUserManagerAsync(email);

        var (response, root, body) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = token, NewPassword = ValidPassword2 });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Reset-Password with a valid token must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue(
            "response must have 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "AC-2: Successed must be true on successful password reset; body: {0}", body);
    }

    [Fact(DisplayName = "AC-2b HappyReset: sign-in with NEW password succeeds after reset")]
    public async Task AC2b_AfterReset_NewPassword_SignIn_Succeeds()
    {
        var email = await RegisterParentAsync("ac2b");
        var token = await GenerateResetTokenViaUserManagerAsync(email);

        // Perform reset.
        var (resetResp, _, resetBody) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = token, NewPassword = ValidPassword2 });
        resetResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Reset must succeed before testing new-password sign-in; body: {0}", resetBody);

        // Sign in with the NEW password.
        var (signInResp, signInRoot, signInBody) = await PostJsonAsync(SignInUrl,
            new { UserName = email, Password = ValidPassword2 });

        signInResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "AC-2: sign-in with the new password must succeed after reset; body: {0}", signInBody);

        TryProp(signInRoot, "successed", out var successed).Should().BeTrue("body: {0}", signInBody);
        successed.GetBoolean().Should().BeTrue(
            "AC-2: new password sign-in Successed must be true; body: {0}", signInBody);

        TryProp(signInRoot, "data", out var data).Should().BeTrue("body: {0}", signInBody);
        TryProp(data, "accessToken", out var at).Should().BeTrue("body: {0}", signInBody);
        at.GetString().Should().NotBeNullOrWhiteSpace(
            "AC-2: new password sign-in must return a non-empty access token; body: {0}", signInBody);
    }

    [Fact(DisplayName = "AC-2c HappyReset: sign-in with OLD password fails after reset")]
    public async Task AC2c_AfterReset_OldPassword_SignIn_Fails()
    {
        var email = await RegisterParentAsync("ac2c");
        var token = await GenerateResetTokenViaUserManagerAsync(email);

        // Perform reset.
        var (resetResp, _, resetBody) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = token, NewPassword = ValidPassword2 });
        resetResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Reset must succeed before testing old-password rejection; body: {0}", resetBody);

        // Attempt sign-in with the OLD password.
        var (signInResp, signInRoot, signInBody) = await PostJsonAsync(SignInUrl,
            new { UserName = email, Password = ValidPassword1 });

        // Must NOT return 200; it should be a 4xx (400 or 404 per sign-in handler).
        ((int)signInResp.StatusCode).Should().NotBe(200,
            "AC-2: sign-in with the OLD password must fail after a successful reset; body: {0}", signInBody);

        TryProp(signInRoot, "successed", out var successed).Should().BeTrue("body: {0}", signInBody);
        successed.GetBoolean().Should().BeFalse(
            "AC-2: Successed must be false when signing in with the old password after reset; body: {0}", signInBody);
    }

    // ===========================================================================
    // AC-3  Session invalidation: pre-reset refresh token is revoked
    // ===========================================================================

    [Fact(DisplayName = "AC-3 SessionInvalidation: pre-reset refresh token cannot be exchanged after reset")]
    public async Task AC3_AfterReset_OldRefreshToken_CannotBeExchanged()
    {
        // 1. Register + sign in to obtain a refresh token.
        var (email, accessToken, refreshTokenStr) = await RegisterAndSignInAsync("ac3");

        // 2. Generate a reset token (fresh) and reset the password.
        var resetToken = await GenerateResetTokenViaUserManagerAsync(email);
        var (resetResp, _, resetBody) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = resetToken, NewPassword = ValidPassword2 });
        resetResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Reset must succeed to test session invalidation; body: {0}", resetBody);

        // 3. Try to exchange the pre-reset refresh token (it should be revoked now).
        // We need an expired access token for the Refresh-Token endpoint to reach the
        // token validation logic (non-expired tokens return 400 "TokenIsRunning").
        var expiredAt = CreateExpiredAccessToken(accessToken);

        var (refreshResp, refreshRoot, refreshBody) = await PostJsonAsync(RefreshTokenUrl,
            new { AccessToken = expiredAt, RefreshToken = refreshTokenStr });

        // After a password reset the refresh token entry is deleted from the cache.
        // The endpoint must reject it with 401 (RefreshTokenNotFound), not 200.
        refreshResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "AC-3: a refresh token obtained before the password reset must be rejected (401) after the reset. " +
            "If this is 200 the session was NOT invalidated. body: {0}", refreshBody);

        TryProp(refreshRoot, "successed", out var successed).Should().BeTrue("body: {0}", refreshBody);
        successed.GetBoolean().Should().BeFalse(
            "AC-3: Successed must be false for the revoked pre-reset refresh token; body: {0}", refreshBody);
    }

    // ===========================================================================
    // AC-4  Reused token: same token used twice returns generic 400
    // ===========================================================================

    [Fact(DisplayName = "AC-4 TokenReuse: same reset token used a second time returns 400 (single-use)")]
    public async Task AC4_ResetPassword_TokenUsedTwice_SecondAttemptReturns400()
    {
        var email = await RegisterParentAsync("ac4");

        // Generate ONE token — both uses must use this same token value.
        var token = await GenerateResetTokenViaUserManagerAsync(email);

        // First use: must succeed.
        var (firstResp, _, firstBody) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = token, NewPassword = ValidPassword2 });
        firstResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "First use of the reset token must succeed; body: {0}", firstBody);

        // Second use with the SAME token: must fail (single-use).
        var (secondResp, secondRoot, secondBody) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = token, NewPassword = ValidPassword1 });

        secondResp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "AC-4: reusing a reset token must return 400 (token is already consumed); body: {0}", secondBody);

        TryProp(secondRoot, "successed", out var successed).Should().BeTrue("body: {0}", secondBody);
        successed.GetBoolean().Should().BeFalse(
            "AC-4: Successed must be false when the reset token is reused; body: {0}", secondBody);
    }

    // ===========================================================================
    // AC-5  Bad/garbage token: returns generic 400
    // ===========================================================================

    [Fact(DisplayName = "AC-5 GarbageToken: arbitrary token string → 400 (ResetPasswordInvalidLink)")]
    public async Task AC5_ResetPassword_GarbageToken_Returns400()
    {
        var email = await RegisterParentAsync("ac5");

        var (response, root, body) = await PostJsonAsync(ResetPasswordUrl,
            new
            {
                Email = email,
                Token = "this-is-not-a-valid-token-abcdef1234567890",
                NewPassword = ValidPassword2
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "AC-5: a garbage/invalid reset token must return 400; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue(
            "response must have 'successed' key; body: {0}", body);
        successed.GetBoolean().Should().BeFalse(
            "AC-5: Successed must be false for a garbage token; body: {0}", body);

        // The message must be the generic "invalid link" string (not an internal exception detail).
        TryProp(root, "message", out var msg).Should().BeTrue("body: {0}", body);
        var msgStr = msg.GetString();
        msgStr.Should().NotBeNullOrWhiteSpace("message must be non-empty; body: {0}", body);
        // The message should NOT contain raw exception text or token internals.
        msgStr!.ToLowerInvariant().Should().NotContain("exception",
            "AC-5: error message must not leak exception details; body: {0}", body);
    }

    // ===========================================================================
    // AC-6  Unknown email on Reset-Password: same generic 400 (no enumeration)
    // ===========================================================================

    [Fact(DisplayName = "AC-6 UnknownEmailReset: unknown email + valid-looking token → 400 (no enumeration)")]
    public async Task AC6_ResetPassword_UnknownEmail_Returns400_SameGenericError()
    {
        var unknownEmail = $"nobody_{Guid.NewGuid():N}@nowhere.example";

        var (response, root, body) = await PostJsonAsync(ResetPasswordUrl,
            new
            {
                Email = unknownEmail,
                Token = "some-plausible-token-value-ABCDEFGHIJ",
                NewPassword = ValidPassword2
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "AC-6: Reset-Password with an unknown email must return 400 (no enumeration oracle); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue(
            "response must have 'successed' key; body: {0}", body);
        successed.GetBoolean().Should().BeFalse(
            "AC-6: Successed must be false for an unknown email; body: {0}", body);

        // The response must use the same generic error message as the garbage-token path (AC-5),
        // so that the caller cannot distinguish "no account" from "bad token".
        TryProp(root, "message", out var msg).Should().BeTrue("body: {0}", body);
        msg.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);
    }

    [Fact(DisplayName = "AC-6 AntiEnum: unknown-email 400 message is SAME as garbage-token 400 message")]
    public async Task AC6_ResetPassword_UnknownEmail_And_GarbageToken_SameMessage()
    {
        // Path A: known email + garbage token.
        var email = await RegisterParentAsync("ac6-enum");
        var (garbageResp, garbageRoot, garbageBody) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = "garbage_token_xyz", NewPassword = ValidPassword2 });

        garbageResp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "garbage-token path must return 400; body: {0}", garbageBody);

        TryProp(garbageRoot, "message", out var garbageMsg).Should().BeTrue("body: {0}", garbageBody);

        // Path B: unknown email + any token.
        var unknownEmail = $"nobody_{Guid.NewGuid():N}@nowhere.example";
        var (unknownResp, unknownRoot, unknownBody) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = unknownEmail, Token = "any-token-value", NewPassword = ValidPassword2 });

        unknownResp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "unknown-email path must return 400; body: {0}", unknownBody);

        TryProp(unknownRoot, "message", out var unknownMsg).Should().BeTrue("body: {0}", unknownBody);

        // Both paths must expose the SAME message (no enumeration).
        garbageMsg.GetString().Should().Be(unknownMsg.GetString(),
            "AC-6: Reset-Password must return the SAME message for unknown-email and garbage-token paths. " +
            "A difference is an enumeration oracle. garbage body: {0}, unknown body: {1}",
            garbageBody, unknownBody);
    }

    // ===========================================================================
    // AC-7  Weak new password (fails Identity policy): returns generic 400
    // ===========================================================================

    [Fact(DisplayName = "AC-7a WeakPassword: password too short (5 chars) → 400 (collapsed generic error)")]
    public async Task AC7a_ResetPassword_WeakPassword_TooShort_Returns400()
    {
        var email = await RegisterParentAsync("ac7a");
        var token = await GenerateResetTokenViaUserManagerAsync(email);

        var (response, root, body) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = token, NewPassword = "Ab1@x" }); // 5 chars — below min 6

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "AC-7: weak password (too short) must return 400 via the generic invalid-link collapse; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse(
            "AC-7: Successed must be false for a weak password; body: {0}", body);
    }

    [Fact(DisplayName = "AC-7b WeakPassword: no digit → 400")]
    public async Task AC7b_ResetPassword_WeakPassword_NoDigit_Returns400()
    {
        var email = await RegisterParentAsync("ac7b");
        var token = await GenerateResetTokenViaUserManagerAsync(email);

        var (response, root, body) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = token, NewPassword = "NoDigit@Pass" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "AC-7: weak password (no digit) must return 400; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-7c WeakPassword: no special char → 400")]
    public async Task AC7c_ResetPassword_WeakPassword_NoSpecialChar_Returns400()
    {
        var email = await RegisterParentAsync("ac7c");
        var token = await GenerateResetTokenViaUserManagerAsync(email);

        var (response, root, body) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = token, NewPassword = "NoSpecial1Pass" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "AC-7: weak password (no special char) must return 400; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // ===========================================================================
    // AC-8  Validation: 422 for empty fields (FluentValidation via ValidationBehavior)
    // ===========================================================================

    [Fact(DisplayName = "AC-8a Validation: empty email on Forgot-Password → 422 Errors[]")]
    public async Task AC8a_ForgotPassword_EmptyEmail_Returns422()
    {
        var (response, root, body) = await PostJsonAsync(ForgotPasswordUrl, new { Email = "" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "AC-8: empty email on Forgot-Password must return 422 (ForgotPasswordCommandValidator fires); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);

        TryProp(root, "errors", out var errors).Should().BeTrue(
            "AC-8: 422 response must have 'errors' key; body: {0}", body);
        var errArr = errors.EnumerateArray().ToList();
        errArr.Should().NotBeEmpty("AC-8: Errors[] must not be empty; body: {0}", body);
    }

    [Fact(DisplayName = "AC-8b Validation: invalid email format on Forgot-Password → 422 Errors[]")]
    public async Task AC8b_ForgotPassword_InvalidEmailFormat_Returns422()
    {
        var (response, root, body) = await PostJsonAsync(ForgotPasswordUrl,
            new { Email = "not-an-email-format" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "AC-8: invalid email format on Forgot-Password must return 422; body: {0}", body);

        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", body);
        errors.EnumerateArray().ToList().Should().NotBeEmpty("body: {0}", body);
    }

    [Fact(DisplayName = "AC-8c Validation: empty token on Reset-Password → 422 Errors[]")]
    public async Task AC8c_ResetPassword_EmptyToken_Returns422()
    {
        var (response, root, body) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = "valid@test.com", Token = "", NewPassword = ValidPassword2 });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "AC-8: empty token on Reset-Password must return 422 (ResetPasswordCommandValidator fires); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);

        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", body);
        errors.EnumerateArray().ToList().Should().NotBeEmpty("body: {0}", body);
    }

    [Fact(DisplayName = "AC-8d Validation: empty newPassword on Reset-Password → 422 Errors[]")]
    public async Task AC8d_ResetPassword_EmptyNewPassword_Returns422()
    {
        var (response, root, body) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = "valid@test.com", Token = "some-token", NewPassword = "" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "AC-8: empty newPassword on Reset-Password must return 422; body: {0}", body);

        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", body);
        errors.EnumerateArray().ToList().Should().NotBeEmpty("body: {0}", body);
    }

    [Fact(DisplayName = "AC-8e Validation: empty email on Reset-Password → 422 Errors[]")]
    public async Task AC8e_ResetPassword_EmptyEmail_Returns422()
    {
        var (response, root, body) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = "", Token = "some-token", NewPassword = ValidPassword2 });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "AC-8: empty email on Reset-Password must return 422; body: {0}", body);

        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", body);
        errors.EnumerateArray().ToList().Should().NotBeEmpty("body: {0}", body);
    }

    [Fact(DisplayName = "AC-8f Validation: 422 envelope has statusCode + successed + message + errors keys")]
    public async Task AC8f_ValidationEnvelope_HasRequiredKeys()
    {
        var (response, root, body) = await PostJsonAsync(ForgotPasswordUrl, new { Email = "" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "prerequisite: must be a 422; body: {0}", body);

        // All BaseResponse<T> envelope keys must be present.
        TryProp(root, "statusCode", out _).Should().BeTrue(
            "422 envelope must have 'statusCode' (or 'StatusCode'); body: {0}", body);
        TryProp(root, "successed", out _).Should().BeTrue(
            "422 envelope must have 'successed' (or 'Successed'); body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue(
            "422 envelope must have 'message' (or 'Message'); body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue(
            "422 envelope must have 'errors' (or 'Errors'); body: {0}", body);
    }

    // ===========================================================================
    // Additional: Both endpoints work anonymously (no Bearer required)
    // ===========================================================================

    [Fact(DisplayName = "Auth: Forgot-Password works without Authorization header (AllowAnonymous)")]
    public async Task Auth_ForgotPassword_WorksWithoutBearerToken()
    {
        // Ensure no auth header (factory client starts clean).
        _client.DefaultRequestHeaders.Authorization = null;

        var (response, _, body) = await PostJsonAsync(ForgotPasswordUrl,
            new { Email = UniqueEmail("anon-forgot") });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Forgot-Password is [AllowAnonymous] and must return 200 without a Bearer token; body: {0}", body);
    }

    [Fact(DisplayName = "Auth: Reset-Password works without Authorization header (AllowAnonymous)")]
    public async Task Auth_ResetPassword_WorksWithoutBearerToken()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        // Use a garbage token — the response shape (400 BadRequest) proves the endpoint is reached
        // without a 401, confirming AllowAnonymous works as expected.
        var (response, _, body) = await PostJsonAsync(ResetPasswordUrl,
            new
            {
                Email = UniqueEmail("anon-reset"),
                Token = "any-token",
                NewPassword = ValidPassword2
            });

        // Must NOT be 401 (which would mean [Authorize] is wrongly applied).
        ((int)response.StatusCode).Should().NotBe(401,
            "Reset-Password is [AllowAnonymous] and must not return 401 without a Bearer token; body: {0}", body);
    }

    // ===========================================================================
    // Additional: Envelope shape on success paths
    // ===========================================================================

    [Fact(DisplayName = "Envelope: Forgot-Password 200 response has all BaseResponse<T> keys")]
    public async Task Envelope_ForgotPassword_200_HasRequiredKeys()
    {
        var email = await RegisterParentAsync("env-forgot");
        var (response, root, body) = await PostJsonAsync(ForgotPasswordUrl, new { Email = email });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue(
            "200 response must have 'statusCode'; body: {0}", body);
        TryProp(root, "successed", out _).Should().BeTrue(
            "200 response must have 'successed'; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue(
            "200 response must have 'message'; body: {0}", body);
        // 'data' and 'errors' are optional on success, but 'successed' and 'message' must always be present.
    }

    [Fact(DisplayName = "Envelope: Reset-Password 200 response has statusCode + successed + message keys")]
    public async Task Envelope_ResetPassword_200_HasRequiredKeys()
    {
        var email = await RegisterParentAsync("env-reset");
        var token = await GenerateResetTokenViaUserManagerAsync(email);

        var (response, root, body) = await PostJsonAsync(ResetPasswordUrl,
            new { Email = email, Token = token, NewPassword = ValidPassword2 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue(
            "200 response must have 'statusCode'; body: {0}", body);
        TryProp(root, "successed", out _).Should().BeTrue(
            "200 response must have 'successed'; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue(
            "200 response must have 'message'; body: {0}", body);
    }

    // ===========================================================================
    // Regression: existing auth flows still work
    // ===========================================================================

    [Fact(DisplayName = "Regression: seeded superadmin can sign in after P1-12-BE-6 changes")]
    public async Task Regression_SuperAdmin_CanStillSignIn()
    {
        var (response, root, body) = await PostJsonAsync(SignInUrl,
            new { UserName = "superadmin", Password = "123Pa$$word!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Seeded superadmin sign-in must still work after P1-12-BE-6 changes; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);
    }
}
