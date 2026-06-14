using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// Integration tests for the self-scoped language-update endpoint added in
/// branch fix/frontend-rtl-alignment-polish (HANDOFF 2026-06-13):
///
///   [Authorize]  PUT  /api/Users/Account/Language
///   Command:     UpdateMyPreferredLanguageCommand { UserPreferredLanguage }
///   Handler:     UpdateMyPreferredLanguageCommandHandler
///   Validator:   UpdateMyPreferredLanguageCommandValidator
///
/// REGRESSION TARGET: a parent (non-admin) calling this endpoint must get 200, NOT 403.
/// Previously the only language-write endpoint was UserManagementController.UpdateUserLanguage
/// which is [AdminOnly] — parents got 403, mislabelled "No internet" in the UI.
///
/// Test cases:
///   TC-1  Happy path — each valid code ("ar","en","ar-EG","en-US") → 200, Successed=true, persists
///   TC-2  Parent (NON-admin) gets 200, not 403 (explicit regression proof)
///   TC-3  Invalid language codes → 422, Successed=false
///   TC-4  Unauthenticated → 401
///   TC-5  Self-scope: second user unaffected after first updates
///   TC-6  Admin token also succeeds ([Authorize] = any authenticated user)
/// </summary>
[Collection("IntegrationTests")]
public sealed class FrontendRTL_UpdateLanguage_Tests : IAsyncLifetime
{
    // -----------------------------------------------------------------------
    // URLs
    // -----------------------------------------------------------------------
    private const string LanguageUrl    = "api/Users/Account/Language";
    private const string MeUrl          = "api/Users/Me";
    private const string RegisterUrl    = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl      = "api/Users/Authentication/Sign-In";

    private const string SuperAdminUser = "superadmin";
    private const string SuperAdminPass = "123Pa$$word!";

    // -----------------------------------------------------------------------
    // Infrastructure
    // -----------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public FrontendRTL_UpdateLanguage_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // -----------------------------------------------------------------------
    // Low-level helpers (mirrors P1_12_AccountProfile_Tests style)
    // -----------------------------------------------------------------------

    private static string UniqueEmail(string tag = "")
        => $"lang_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@lang.test";

    /// <summary>
    /// Case-insensitive property lookup — handles both camelCase (Newtonsoft controller path)
    /// and PascalCase (middleware / System.Text.Json path).
    /// </summary>
    private static bool TryProp(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (el.TryGetProperty(pascal, out value)) return true;
        foreach (var prop in el.EnumerateObject())
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
        var req = new HttpRequestMessage(method, url);
        if (body is not null)
            req.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearerToken is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var resp    = await client.SendAsync(req);
        var rawBody = await resp.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            try { root = JsonDocument.Parse(rawBody).RootElement; }
            catch { /* non-JSON body; leave root default */ }
        }
        return (resp, root, rawBody);
    }

    /// <summary>
    /// Registers a new parent and returns (accessToken, userId).
    /// </summary>
    private async Task<(string Token, int UserId)> RegisterParentAsync(string email,
        string password = "Str0ng@Pass", string? fullName = null)
    {
        var payload = fullName is not null
            ? (object)new { Email = email, Password = password, FullName = fullName, AcceptedTerms = true }
            : new { Email = email, Password = password, AcceptedTerms = true };

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterUrl, payload);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tok).Should().BeTrue("body: {0}", body);
        TryProp(data, "userId", out var uid).Should().BeTrue("body: {0}", body);
        return (tok.GetString()!, uid.GetInt32());
    }

    /// <summary>
    /// Signs in with username+password and returns the access token.
    /// </summary>
    private async Task<string> SignInAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tok).Should().BeTrue("body: {0}", body);
        return tok.GetString()!;
    }

    /// <summary>
    /// PUTs a language code and returns the full (Response, Root, Body) triple.
    /// </summary>
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        PutLanguageAsync(string token, string langCode)
        => SendAsync(_client, HttpMethod.Put, LanguageUrl,
                     new { UserPreferredLanguage = langCode }, token);

    /// <summary>
    /// GETs /api/Users/Me and returns the full triple.
    /// </summary>
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetMeAsync(string token)
        => SendAsync(_client, HttpMethod.Get, MeUrl, null, token);

    // -----------------------------------------------------------------------
    // Shared assertion: validates a 422 error response has the correct shape.
    // -----------------------------------------------------------------------
    private static void AssertValidationEnvelope(JsonElement root, string rawBody)
    {
        root.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "422 must have a JSON body; raw: {0}", rawBody);

        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue(
            "422 body must have 'statusCode'; body: {0}", rawBody);
        statusCodeProp.GetInt32().Should().Be(422,
            "statusCode inside 422 body must be 422; body: {0}", rawBody);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "422 body must have 'successed'; body: {0}", rawBody);
        succeededProp.GetBoolean().Should().BeFalse(
            "Successed must be false on validation failure; body: {0}", rawBody);

        TryProp(root, "errors", out var errorsProp).Should().BeTrue(
            "422 body must have 'errors'; body: {0}", rawBody);
        errorsProp.ValueKind.Should().Be(JsonValueKind.Array,
            "errors must be a JSON array; body: {0}", rawBody);
        errorsProp.GetArrayLength().Should().BeGreaterThan(0,
            "errors must be non-empty on validation failure; body: {0}", rawBody);
    }

    // =======================================================================
    // TC-1 + TC-2: Happy path — valid language codes, and the NON-admin parent
    // must get 200, NOT 403 (explicit regression proof).
    // =======================================================================

    [Theory(DisplayName = "TC-1: PUT Language with valid code → 200, Successed=true, BaseResponse envelope")]
    [InlineData("ar")]
    [InlineData("en")]
    [InlineData("ar-EG")]
    [InlineData("en-US")]
    public async Task HappyPath_ValidLanguageCode_Returns200(string langCode)
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail($"happy-{langCode.Replace("-", "")}"));

        var (resp, root, body) = await PutLanguageAsync(token, langCode);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PUT Language with valid code '{0}' must return 200; body: {1}", langCode, body);

        // --- BaseResponse<string> envelope shape ---
        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue(
            "envelope must contain 'statusCode'; body: {0}", body);
        statusCodeProp.GetInt32().Should().Be(200,
            "envelope statusCode must be 200; body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "envelope must contain 'successed'; body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true for valid language code; body: {0}", body);

        TryProp(root, "message", out _).Should().BeTrue(
            "envelope must contain 'message'; body: {0}", body);

        TryProp(root, "errors", out _).Should().BeTrue(
            "envelope must contain 'errors'; body: {0}", body);
    }

    [Fact(DisplayName = "TC-2 (REGRESSION): Non-admin PARENT calling PUT Language → 200, NOT 403")]
    public async Task Regression_ParentUser_Gets200_Not403()
    {
        // A freshly-registered parent has role 'Parent', not 'Admin'. The old endpoint
        // (UserManagementController.UpdateUserLanguage, AdminOnly) returned 403 for parents.
        // The new endpoint (AccountController.UpdateLanguage, [Authorize]) must return 200.
        var (token, _) = await RegisterParentAsync(UniqueEmail("regression-parent"));

        var (resp, _, body) = await PutLanguageAsync(token, "ar");

        resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "a non-admin PARENT must NOT get 403; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "a non-admin PARENT must get 200 from the self-scoped language endpoint; body: {0}", body);
    }

    // =======================================================================
    // TC-1 (persistence): change persists — verify via GET /api/Users/Me
    // =======================================================================

    [Theory(DisplayName = "TC-1 (persist): PUT Language persists to DB — GET /Me reflects the new preferredLanguage")]
    [InlineData("ar")]
    [InlineData("en")]
    [InlineData("ar-EG")]
    [InlineData("en-US")]
    public async Task HappyPath_ValidLanguageCode_PersistsToDB(string langCode)
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail($"persist-{langCode.Replace("-", "")}"));

        // Apply the language change
        var (putResp, _, putBody) = await PutLanguageAsync(token, langCode);
        putResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PUT Language prerequisite must succeed; code='{0}'; body: {1}", langCode, putBody);

        // Verify via /Me — PreferredLanguage in MeResponse must match
        var (meResp, meRoot, meBody) = await GetMeAsync(token);
        meResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Me after language update must succeed; body: {0}", meBody);

        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);

        TryProp(meData, "preferredLanguage", out var langProp).Should().BeTrue(
            "/Me data must contain 'preferredLanguage'; body: {0}", meBody);
        langProp.GetString().Should().Be(langCode,
            "/Me must reflect the language '{0}' persisted by PUT Language; body: {1}",
            langCode, meBody);
    }

    // =======================================================================
    // TC-3: Invalid language codes → 422, Successed=false, errors array non-empty
    // =======================================================================

    [Theory(DisplayName = "TC-3: PUT Language with invalid/empty code → 422, Successed=false")]
    [InlineData("fr",   "unsupported ISO code")]
    [InlineData("",     "empty string")]
    [InlineData("xx",   "made-up two-letter code")]
    [InlineData("zh",   "valid ISO but not in allowed set")]
    [InlineData("de-DE","valid BCP-47 but not in allowed set")]
    public async Task Validation_InvalidLanguageCode_Returns422(string langCode, string reason)
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail($"val-{Guid.NewGuid():N}"));

        var (resp, root, body) = await PutLanguageAsync(token, langCode);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "PUT Language with '{0}' ({1}) must return 422; body: {2}", langCode, reason, body);

        AssertValidationEnvelope(root, body);
    }

    [Fact(DisplayName = "TC-3-null: PUT Language with null UserPreferredLanguage → 422")]
    public async Task Validation_NullLanguageCode_Returns422()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("val-null"));

        // Sending the field as null (NotEmpty rule fires)
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Put, LanguageUrl,
            new { UserPreferredLanguage = (string?)null }, token);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "PUT Language with null value must return 422; body: {0}", body);

        AssertValidationEnvelope(root, body);
    }

    // =======================================================================
    // TC-4: Unauthenticated → 401
    // =======================================================================

    [Fact(DisplayName = "TC-4-a: PUT Language without bearer token → 401")]
    public async Task Auth_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Put, LanguageUrl,
            new { UserPreferredLanguage = "ar" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "PUT Language without a token must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "TC-4-b: PUT Language with an invalid/garbage JWT → 401")]
    public async Task Auth_InvalidToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Put, LanguageUrl,
            new { UserPreferredLanguage = "en" }, "this.is.not.a.valid.jwt");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "PUT Language with invalid token must return 401; body: {0}", body);
    }

    // =======================================================================
    // TC-5: Self-scope — the change applies only to the calling user.
    //       A second registered user is unaffected after the first updates.
    // =======================================================================

    [Fact(DisplayName = "TC-5: Self-scope — PUT Language only mutates the CALLING user's preferredLanguage")]
    public async Task SelfScope_OnlyCallerIsAffected()
    {
        // Register two independent parents
        var (tokenA, _) = await RegisterParentAsync(UniqueEmail("scope-a"));
        var (tokenB, _) = await RegisterParentAsync(UniqueEmail("scope-b"));

        // Establish a known initial language for User B (so we can detect mutation)
        var (putBInit, _, putBInitBody) = await PutLanguageAsync(tokenB, "ar");
        putBInit.StatusCode.Should().Be(HttpStatusCode.OK,
            "User B initial language PUT must succeed; body: {0}", putBInitBody);

        // User A changes to "en"
        var (putA, _, putABody) = await PutLanguageAsync(tokenA, "en");
        putA.StatusCode.Should().Be(HttpStatusCode.OK,
            "User A PUT Language to 'en' must succeed; body: {0}", putABody);

        // User A's /Me must show "en"
        var (meARespA, meARootA, meABodyA) = await GetMeAsync(tokenA);
        meARespA.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", meABodyA);
        TryProp(meARootA, "data", out var meDataA).Should().BeTrue("body: {0}", meABodyA);
        TryProp(meDataA, "preferredLanguage", out var langA).Should().BeTrue("body: {0}", meABodyA);
        langA.GetString().Should().Be("en",
            "User A's preferredLanguage must be 'en' after their own PUT; body: {0}", meABodyA);

        // User B's /Me must still show "ar" — untouched by User A's update
        var (meRespB, meRootB, meBodyB) = await GetMeAsync(tokenB);
        meRespB.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", meBodyB);
        TryProp(meRootB, "data", out var meDataB).Should().BeTrue("body: {0}", meBodyB);
        TryProp(meDataB, "preferredLanguage", out var langB).Should().BeTrue("body: {0}", meBodyB);
        langB.GetString().Should().Be("ar",
            "User B's preferredLanguage must remain 'ar' — unchanged by User A's PUT; body: {0}", meBodyB);
    }

    // =======================================================================
    // TC-6: Admin token also succeeds ([Authorize] allows ANY authenticated user)
    // =======================================================================

    [Fact(DisplayName = "TC-6: Admin user calling PUT Language also gets 200 (endpoint is [Authorize], not [AdminOnly])")]
    public async Task AdminToken_AlsoSucceeds()
    {
        // Sign in as the seeded superadmin
        var adminToken = await SignInAsync(SuperAdminUser, SuperAdminPass);

        var (resp, root, body) = await PutLanguageAsync(adminToken, "en-US");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "An admin user must also be able to PUT Language (endpoint is [Authorize], not restricted to parents); body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true for admin token on PUT Language; body: {0}", body);
    }
}
