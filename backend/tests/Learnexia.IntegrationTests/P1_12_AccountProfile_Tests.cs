using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P1-12 integration tests: Account Profile (GET/PUT /api/Users/Account/Profile) and enriched
/// GET /api/Users/Me endpoints.
///
/// Endpoints under test:
///   [Authorize] GET  /api/Users/Account/Profile   → BaseResponse&lt;AccountProfileResponse&gt;
///   [Authorize] PUT  /api/Users/Account/Profile   → BaseResponse&lt;AccountProfileResponse&gt;
///   [Authorize] GET  /api/Users/Me                → BaseResponse&lt;MeResponse&gt; (enriched with phone/country/avatarUrl)
///
/// AccountProfileResponse shape: { fullName, phone, country, avatarUrl }
///   phone   → User.PhoneNumber
///   country → User.Nationality
///   avatar  → User.AvatarUrl
///
/// Acceptance criteria covered:
///   AC-1 Auth        Anonymous GET/PUT → 401 (no body leak on 401)
///   AC-2 Happy-read  Authenticated GET returns BaseResponse envelope (Successed=true) + correct fields
///   AC-3 Happy-update PUT with {fullName, phone, country} → 200/Successed=true; subsequent GET + /Me reflect persisted values
///   AC-4 Validation  Empty fullName → 422; fullName &gt; 100 chars → 422; malformed phone → 422; country &gt; 100 chars → 422
///   AC-5 Self-scope  PUT only mutates the caller's own row; a second user's profile is unaffected
///   AC-6 Me-enrich   Authenticated /Me includes phone/country/avatarUrl and existing fields (id, roles, fullName, preferredLanguage, isFirstLogin, hasChildren)
/// </summary>
[Collection("IntegrationTests")]
public sealed class P1_12_AccountProfile_Tests : IAsyncLifetime
{
    // ---------------------------------------------------------------------------
    // URLs
    // ---------------------------------------------------------------------------
    private const string ProfileUrl = "api/Users/Account/Profile";
    private const string MeUrl = "api/Users/Me";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_12_AccountProfile_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------------------
    // Helpers — mirrored from the existing P1_09_Me_Tests style
    // ---------------------------------------------------------------------------

    private static string UniqueEmail(string tag = "")
        => $"p112_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@profile.test";

    /// <summary>
    /// Case-insensitive property lookup that handles both the controller (camelCase Newtonsoft) and
    /// middleware (PascalCase System.Text.Json) serialisation paths.
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
    /// Registers a new parent with a unique email and optional fullName, returning (accessToken, userId).
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

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetProfileAsync(string bearerToken)
        => SendAsync(_client, HttpMethod.Get, ProfileUrl, null, bearerToken);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        PutProfileAsync(string bearerToken, object body)
        => SendAsync(_client, HttpMethod.Put, ProfileUrl, body, bearerToken);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetMeAsync(string bearerToken)
        => SendAsync(_client, HttpMethod.Get, MeUrl, null, bearerToken);

    // ===========================================================================
    // AC-1: Auth — unauthenticated GET/PUT → 401
    // ===========================================================================

    [Fact(DisplayName = "AC-1-a: GET /api/Users/Account/Profile without token → 401")]
    public async Task Auth_Get_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, ProfileUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Profile GET without a bearer token must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-1-b: PUT /api/Users/Account/Profile without token → 401")]
    public async Task Auth_Put_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Put, ProfileUrl,
            new { FullName = "Test", Phone = "+20111222333", Country = "EG" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Profile PUT without a bearer token must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-1-c: GET /api/Users/Account/Profile with garbage token → 401")]
    public async Task Auth_Get_InvalidToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, ProfileUrl,
            null, "this.is.not.a.valid.jwt");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Profile GET with invalid bearer token must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-1-d: PUT /api/Users/Account/Profile with garbage token → 401")]
    public async Task Auth_Put_InvalidToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Put, ProfileUrl,
            new { FullName = "Test" }, "this.is.not.a.valid.jwt");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Profile PUT with invalid bearer token must return 401; body: {0}", body);
    }

    // ===========================================================================
    // AC-2: Happy read — GET returns BaseResponse envelope with correct fields
    // ===========================================================================

    [Fact(DisplayName = "AC-2-a: Authenticated GET /Profile → 200 with full BaseResponse envelope (Successed=true)")]
    public async Task HappyRead_Get_Returns200_WithBaseResponseEnvelope()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("get-shape"));

        var (resp, root, body) = await GetProfileAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Profile GET with valid token must return 200; body: {0}", body);

        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue(
            "envelope must have 'statusCode'; body: {0}", body);
        statusCodeProp.GetInt32().Should().Be(200,
            "statusCode in envelope must be 200; body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "envelope must have 'successed'; body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true for authenticated read; body: {0}", body);

        TryProp(root, "message", out _).Should().BeTrue(
            "envelope must have 'message'; body: {0}", body);

        TryProp(root, "errors", out _).Should().BeTrue(
            "envelope must have 'errors'; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue(
            "envelope must have 'data'; body: {0}", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Null,
            "data must not be null on success; body: {0}", body);
    }

    [Fact(DisplayName = "AC-2-b: GET /Profile data has all AccountProfileResponse fields (fullName, phone, country, avatarUrl)")]
    public async Task HappyRead_Get_Data_HasAllProfileFields()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("get-fields"), fullName: "Profile Fields Test");

        var (resp, root, body) = await GetProfileAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // fullName — required field, set at registration
        TryProp(data, "fullName", out var fullNameProp).Should().BeTrue(
            "data must contain 'fullName'; body: {0}", body);
        fullNameProp.GetString().Should().Be("Profile Fields Test",
            "fullName must match what was registered; body: {0}", body);

        // phone — optional; null for a freshly-registered user
        TryProp(data, "phone", out _).Should().BeTrue(
            "data must contain 'phone' key (may be null); body: {0}", body);

        // country — optional; null for a freshly-registered user
        TryProp(data, "country", out _).Should().BeTrue(
            "data must contain 'country' key (may be null); body: {0}", body);

        // avatarUrl — null until BE-4 upload endpoint is implemented
        TryProp(data, "avatarUrl", out _).Should().BeTrue(
            "data must contain 'avatarUrl' key (may be null); body: {0}", body);
    }

    // ===========================================================================
    // AC-3: Happy update — PUT persists values; subsequent GET and /Me reflect them
    // ===========================================================================

    [Fact(DisplayName = "AC-3-a: PUT /Profile returns 200/Successed=true and reflects new values in response")]
    public async Task HappyUpdate_Put_Returns200_WithUpdatedData()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("put-happy"));

        var updateBody = new { FullName = "Updated Full Name", Phone = "+201012345678", Country = "EG" };
        var (resp, root, body) = await PutProfileAsync(token, updateBody);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Profile PUT with valid body must return 200; body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true on successful update; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Null,
            "data must not be null after a successful update; body: {0}", body);

        TryProp(data, "fullName", out var fn).Should().BeTrue("body: {0}", body);
        fn.GetString().Should().Be("Updated Full Name",
            "response data must echo the updated fullName; body: {0}", body);

        TryProp(data, "phone", out var ph).Should().BeTrue("body: {0}", body);
        ph.GetString().Should().Be("+201012345678",
            "response data must echo the updated phone; body: {0}", body);

        TryProp(data, "country", out var co).Should().BeTrue("body: {0}", body);
        co.GetString().Should().Be("EG",
            "response data must echo the updated country; body: {0}", body);
    }

    [Fact(DisplayName = "AC-3-b: After PUT /Profile a subsequent GET /Profile returns the persisted values")]
    public async Task HappyUpdate_SubsequentGet_ReflectsPersistedValues()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("put-persist"));

        var updateBody = new { FullName = "Persisted Name", Phone = "+201099887766", Country = "SA" };
        var (putResp, _, putBody) = await PutProfileAsync(token, updateBody);
        putResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PUT prerequisite must succeed; body: {0}", putBody);

        // Now GET the profile and assert the values were persisted
        var (getResp, getRoot, getBody) = await GetProfileAsync(token);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "subsequent GET must succeed; body: {0}", getBody);

        TryProp(getRoot, "data", out var data).Should().BeTrue("body: {0}", getBody);

        TryProp(data, "fullName", out var fn).Should().BeTrue("body: {0}", getBody);
        fn.GetString().Should().Be("Persisted Name",
            "GET must return the fullName written by PUT; body: {0}", getBody);

        TryProp(data, "phone", out var ph).Should().BeTrue("body: {0}", getBody);
        ph.GetString().Should().Be("+201099887766",
            "GET must return the phone written by PUT; body: {0}", getBody);

        TryProp(data, "country", out var co).Should().BeTrue("body: {0}", getBody);
        co.GetString().Should().Be("SA",
            "GET must return the country written by PUT; body: {0}", getBody);
    }

    [Fact(DisplayName = "AC-3-c: After PUT /Profile GET /api/Users/Me reflects the persisted phone/country values")]
    public async Task HappyUpdate_SubsequentMe_ReflectsPersistedValues()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("put-me-sync"));

        var updateBody = new { FullName = "Me Sync Name", Phone = "+201111234567", Country = "AE" };
        var (putResp, _, putBody) = await PutProfileAsync(token, updateBody);
        putResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PUT prerequisite must succeed; body: {0}", putBody);

        // /Me should now reflect the updated phone and country
        var (meResp, meRoot, meBody) = await GetMeAsync(token);
        meResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Me after PUT must succeed; body: {0}", meBody);

        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);

        TryProp(meData, "phone", out var ph).Should().BeTrue(
            "/Me data must contain 'phone' after profile update; body: {0}", meBody);
        ph.GetString().Should().Be("+201111234567",
            "/Me must reflect the phone persisted by PUT; body: {0}", meBody);

        TryProp(meData, "country", out var co).Should().BeTrue(
            "/Me data must contain 'country' after profile update; body: {0}", meBody);
        co.GetString().Should().Be("AE",
            "/Me must reflect the country persisted by PUT; body: {0}", meBody);

        TryProp(meData, "fullName", out var fn).Should().BeTrue(
            "/Me data must contain 'fullName' after profile update; body: {0}", meBody);
        fn.GetString().Should().Be("Me Sync Name",
            "/Me must reflect the fullName persisted by PUT; body: {0}", meBody);
    }

    [Fact(DisplayName = "AC-3-d: PUT /Profile with only required fullName (phone/country omitted) → 200, phone/country null")]
    public async Task HappyUpdate_RequiredFieldOnly_ClearsOptionals()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("put-minimal"));

        // First set phone + country
        var setBody = new { FullName = "Minimal Test", Phone = "+201055554444", Country = "KW" };
        var (setResp, _, setBody2) = await PutProfileAsync(token, setBody);
        setResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "first PUT must succeed; body: {0}", setBody2);

        // Now update with only fullName — phone/country should be cleared (set to null) per the handler
        // which sets null for whitespace-only or missing optional fields.
        var minimalBody = new { FullName = "Minimal Only Name" };
        var (putResp, putRoot, putBody) = await PutProfileAsync(token, minimalBody);
        putResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PUT with only fullName must succeed; body: {0}", putBody);

        TryProp(putRoot, "successed", out var succeeded).Should().BeTrue("body: {0}", putBody);
        succeeded.GetBoolean().Should().BeTrue("body: {0}", putBody);

        TryProp(putRoot, "data", out var data).Should().BeTrue("body: {0}", putBody);

        TryProp(data, "fullName", out var fn).Should().BeTrue("body: {0}", putBody);
        fn.GetString().Should().Be("Minimal Only Name",
            "fullName must be updated; body: {0}", putBody);

        // phone and country sent as null/absent → handler sets them to null
        TryProp(data, "phone", out var ph).Should().BeTrue("body: {0}", putBody);
        ph.ValueKind.Should().Be(JsonValueKind.Null,
            "phone must be null when not supplied in the PUT body; body: {0}", putBody);

        TryProp(data, "country", out var co).Should().BeTrue("body: {0}", putBody);
        co.ValueKind.Should().Be(JsonValueKind.Null,
            "country must be null when not supplied in the PUT body; body: {0}", putBody);
    }

    // ===========================================================================
    // AC-4: Validation — 422 with BaseResponse error shape
    // ===========================================================================

    [Fact(DisplayName = "AC-4-a: PUT /Profile with empty fullName → 422 with error body")]
    public async Task Validation_EmptyFullName_Returns422()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("val-empty-fn"));

        var (resp, root, body) = await PutProfileAsync(token,
            new { FullName = "", Phone = (string?)null, Country = (string?)null });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty fullName must return 422; body: {0}", body);

        // The ValidationBehavior surfaces errors in the BaseResponse envelope
        AssertValidationBody(root, body);
    }

    [Fact(DisplayName = "AC-4-b: PUT /Profile with fullName > 100 chars → 422 with error body")]
    public async Task Validation_FullNameTooLong_Returns422()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("val-long-fn"));
        var longName = new string('A', 101); // 101 chars, max is 100

        var (resp, root, body) = await PutProfileAsync(token,
            new { FullName = longName });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "fullName > 100 chars must return 422; body: {0}", body);

        AssertValidationBody(root, body);
    }

    [Fact(DisplayName = "AC-4-c: PUT /Profile with malformed phone ('abc') → 422 with error body")]
    public async Task Validation_MalformedPhone_Letters_Returns422()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("val-phone-abc"));

        var (resp, root, body) = await PutProfileAsync(token,
            new { FullName = "Valid Name", Phone = "abc" });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "non-numeric phone must return 422; body: {0}", body);

        AssertValidationBody(root, body);
    }

    [Fact(DisplayName = "AC-4-d: PUT /Profile with too-short phone (< 7 digits) → 422 with error body")]
    public async Task Validation_PhoneTooShort_Returns422()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("val-phone-short"));

        var (resp, root, body) = await PutProfileAsync(token,
            new { FullName = "Valid Name", Phone = "12345" }); // 5 digits; min is 7

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "phone with fewer than 7 digits must return 422; body: {0}", body);

        AssertValidationBody(root, body);
    }

    [Fact(DisplayName = "AC-4-e: PUT /Profile with too-long phone (> 15 digits) → 422 with error body")]
    public async Task Validation_PhoneTooLong_Returns422()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("val-phone-long"));

        var (resp, root, body) = await PutProfileAsync(token,
            new { FullName = "Valid Name", Phone = "1234567890123456" }); // 16 digits; max is 15

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "phone with more than 15 digits must return 422; body: {0}", body);

        AssertValidationBody(root, body);
    }

    [Fact(DisplayName = "AC-4-f: PUT /Profile with country > 100 chars → 422 with error body")]
    public async Task Validation_CountryTooLong_Returns422()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("val-country-long"));
        var longCountry = new string('X', 101); // 101 chars; max is 100

        var (resp, root, body) = await PutProfileAsync(token,
            new { FullName = "Valid Name", Country = longCountry });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "country > 100 chars must return 422; body: {0}", body);

        AssertValidationBody(root, body);
    }

    [Fact(DisplayName = "AC-4-g: PUT /Profile with completely empty body (no fullName at all) → 422")]
    public async Task Validation_MissingFullName_Returns422()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("val-no-fn"));

        // Sending an empty JSON object: fullName will be deserialized as null, which fails "NotEmpty"
        var (resp, root, body) = await PutProfileAsync(token, new { });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "missing fullName must return 422; body: {0}", body);

        AssertValidationBody(root, body);
    }

    // ===========================================================================
    // AC-5: Self-scope / no mass-assignment
    // ===========================================================================

    [Fact(DisplayName = "AC-5-a: PUT /Profile only mutates the calling user's own row")]
    public async Task SelfScope_PutOnlyMutatesCallerProfile()
    {
        // Register two parents
        var (tokenA, _) = await RegisterParentAsync(UniqueEmail("scope-a"), fullName: "Parent A Original");
        var (tokenB, _) = await RegisterParentAsync(UniqueEmail("scope-b"), fullName: "Parent B Original");

        // Parent A updates their profile
        var (putResp, _, putBody) = await PutProfileAsync(tokenA,
            new { FullName = "Parent A Updated", Phone = "+201234567890", Country = "EG" });
        putResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Parent A PUT must succeed; body: {0}", putBody);

        // Parent B's profile must be untouched
        var (bResp, bRoot, bBody) = await GetProfileAsync(tokenB);
        bResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Parent B GET must succeed; body: {0}", bBody);

        TryProp(bRoot, "data", out var bData).Should().BeTrue("body: {0}", bBody);

        TryProp(bData, "fullName", out var bFn).Should().BeTrue("body: {0}", bBody);
        bFn.GetString().Should().Be("Parent B Original",
            "Parent B's fullName must not be changed by Parent A's PUT; body: {0}", bBody);

        TryProp(bData, "phone", out var bPh).Should().BeTrue("body: {0}", bBody);
        bPh.ValueKind.Should().Be(JsonValueKind.Null,
            "Parent B's phone must still be null (was never set); body: {0}", bBody);

        TryProp(bData, "country", out var bCo).Should().BeTrue("body: {0}", bBody);
        bCo.ValueKind.Should().Be(JsonValueKind.Null,
            "Parent B's country must still be null (was never set); body: {0}", bBody);
    }

    [Fact(DisplayName = "AC-5-b: PUT /Profile body cannot contain userId/email/role — only allowed fields are applied")]
    public async Task SelfScope_ExtraFieldsInBody_AreIgnored()
    {
        var (tokenA, userIdA) = await RegisterParentAsync(UniqueEmail("mass-assign"), fullName: "Mass Assign Test");

        // Attempt to send extra fields that could cause mass-assignment; these must be silently ignored
        // because UpdateMyProfileCommand only defines FullName / Phone / Country. The controller binds
        // [FromBody] UpdateMyProfileCommand, which never has id/email/role properties.
        var bodyWithExtras = new
        {
            FullName = "Safe Update",
            Phone = "+201122334455",
            Country = "KW",
            // These extra fields must be ignored by the model binder
            Id = 9999,
            Email = "attacker@evil.com",
            Role = "SuperAdmin",
            UserId = 9999
        };

        var (resp, root, body) = await PutProfileAsync(tokenA, bodyWithExtras);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PUT with extra fields must still return 200 (extras are ignored); body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // The returned profile must reflect the allowed fields only, not the extra ones
        TryProp(data, "fullName", out var fn).Should().BeTrue("body: {0}", body);
        fn.GetString().Should().Be("Safe Update",
            "fullName must be the one we sent; body: {0}", body);

        // Verify the user's data via GET — the id must still be the original user's id
        var (getResp, getRoot, getBody) = await GetProfileAsync(tokenA);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", getBody);

        // The /Me endpoint will reflect the updated fullName for the right user (userIdA), not id=9999
        var (meResp, meRoot, meBody) = await GetMeAsync(tokenA);
        meResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", meBody);
        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);
        TryProp(meData, "id", out var idFromMe).Should().BeTrue("body: {0}", meBody);
        idFromMe.GetInt32().Should().Be(userIdA,
            "/Me must still return the original user's id after PUT with extra fields; body: {0}", meBody);
    }

    // ===========================================================================
    // AC-6: /Me enrichment — phone/country/avatarUrl and all existing fields present
    // ===========================================================================

    [Fact(DisplayName = "AC-6-a: Authenticated GET /Me includes phone, country, avatarUrl (P1-12 enrichment)")]
    public async Task MeEnrichment_ContainsPhoneCountryAvatarUrl()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("me-enrich"));

        var (resp, root, body) = await GetMeAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Me must return 200; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // The three enriched fields must be present in the response (null for a fresh user)
        TryProp(data, "phone", out _).Should().BeTrue(
            "/Me data must contain 'phone' key (may be null); body: {0}", body);

        TryProp(data, "country", out _).Should().BeTrue(
            "/Me data must contain 'country' key (may be null); body: {0}", body);

        TryProp(data, "avatarUrl", out _).Should().BeTrue(
            "/Me data must contain 'avatarUrl' key (may be null); body: {0}", body);
    }

    [Fact(DisplayName = "AC-6-b: Authenticated GET /Me still includes all existing fields (id, roles, fullName, preferredLanguage, isFirstLogin, hasChildren)")]
    public async Task MeEnrichment_ExistingFieldsStillPresent()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("me-existing"), fullName: "Existing Fields Test");

        var (resp, root, body) = await GetMeAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Me must return 200; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // All pre-existing MeResponse fields must still be present
        TryProp(data, "id", out var idProp).Should().BeTrue(
            "/Me data must have 'id'; body: {0}", body);
        idProp.GetInt32().Should().BeGreaterThan(0, "body: {0}", body);

        TryProp(data, "roles", out var rolesProp).Should().BeTrue(
            "/Me data must have 'roles'; body: {0}", body);
        rolesProp.ValueKind.Should().Be(JsonValueKind.Array, "roles must be an array; body: {0}", body);

        TryProp(data, "fullName", out var fnProp).Should().BeTrue(
            "/Me data must have 'fullName'; body: {0}", body);
        fnProp.GetString().Should().Be("Existing Fields Test", "body: {0}", body);

        TryProp(data, "preferredLanguage", out _).Should().BeTrue(
            "/Me data must have 'preferredLanguage'; body: {0}", body);

        TryProp(data, "isFirstLogin", out var isFirstLogin).Should().BeTrue(
            "/Me data must have 'isFirstLogin'; body: {0}", body);
        new[] { JsonValueKind.True, JsonValueKind.False }.Should().Contain(isFirstLogin.ValueKind,
            "isFirstLogin must be boolean; body: {0}", body);

        TryProp(data, "hasChildren", out var hasChildren).Should().BeTrue(
            "/Me data must have 'hasChildren'; body: {0}", body);
        new[] { JsonValueKind.True, JsonValueKind.False }.Should().Contain(hasChildren.ValueKind,
            "hasChildren must be boolean; body: {0}", body);
    }

    [Fact(DisplayName = "AC-6-c: GET /Me phone/country reflect values set via PUT /Profile")]
    public async Task MeEnrichment_PhoneCountryReflectProfileUpdate()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("me-sync-check"));

        // Initially phone/country should be null
        var (initialResp, initialRoot, initialBody) = await GetMeAsync(token);
        initialResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", initialBody);
        TryProp(initialRoot, "data", out var initialData).Should().BeTrue("body: {0}", initialBody);
        TryProp(initialData, "phone", out var initPhone).Should().BeTrue("body: {0}", initialBody);
        initPhone.ValueKind.Should().Be(JsonValueKind.Null,
            "phone should be null before any profile update; body: {0}", initialBody);

        // Set phone and country via PUT
        var updateBody = new { FullName = "Me Sync Test", Phone = "+441234567890", Country = "GB" };
        var (putResp, _, putBody) = await PutProfileAsync(token, updateBody);
        putResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PUT must succeed; body: {0}", putBody);

        // /Me must now reflect the updated values
        var (meResp, meRoot, meBody) = await GetMeAsync(token);
        meResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", meBody);
        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);

        TryProp(meData, "phone", out var mePhone).Should().BeTrue("body: {0}", meBody);
        mePhone.GetString().Should().Be("+441234567890",
            "/Me must reflect the phone set by PUT; body: {0}", meBody);

        TryProp(meData, "country", out var meCountry).Should().BeTrue("body: {0}", meBody);
        meCountry.GetString().Should().Be("GB",
            "/Me must reflect the country set by PUT; body: {0}", meBody);
    }

    [Fact(DisplayName = "AC-6-d: GET /Me avatarUrl is null for a user who has not uploaded an avatar")]
    public async Task MeEnrichment_AvatarUrl_IsNullForFreshUser()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("me-avatar-null"));

        var (resp, root, body) = await GetMeAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "avatarUrl", out var avatarUrl).Should().BeTrue(
            "/Me data must contain 'avatarUrl'; body: {0}", body);
        avatarUrl.ValueKind.Should().Be(JsonValueKind.Null,
            "avatarUrl must be null for a user who has not uploaded an avatar (BE-4 not implemented); body: {0}", body);
    }

    // ===========================================================================
    // Supplemental: GET /Profile for fresh user returns null phone/country/avatarUrl
    // ===========================================================================

    [Fact(DisplayName = "Supplemental: GET /Profile for fresh user has null phone/country/avatarUrl")]
    public async Task GetProfile_FreshUser_HasNullOptionals()
    {
        var (token, _) = await RegisterParentAsync(UniqueEmail("fresh-nulls"), fullName: "Null Optionals Test");

        var (resp, root, body) = await GetProfileAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "phone", out var phone).Should().BeTrue("body: {0}", body);
        phone.ValueKind.Should().Be(JsonValueKind.Null,
            "phone must be null for a fresh user; body: {0}", body);

        TryProp(data, "country", out var country).Should().BeTrue("body: {0}", body);
        country.ValueKind.Should().Be(JsonValueKind.Null,
            "country must be null for a fresh user; body: {0}", body);

        TryProp(data, "avatarUrl", out var avatarUrl).Should().BeTrue("body: {0}", body);
        avatarUrl.ValueKind.Should().Be(JsonValueKind.Null,
            "avatarUrl must be null for a fresh user; body: {0}", body);
    }

    // ===========================================================================
    // Private helpers
    // ===========================================================================

    /// <summary>
    /// Asserts that a 422 response has a valid BaseResponse-shaped error body with at least one error.
    /// Handles both the camelCase (Newtonsoft controller path) and PascalCase (middleware 422 path).
    /// </summary>
    private static void AssertValidationBody(JsonElement root, string rawBody)
    {
        root.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "422 response must have a JSON body; raw body: {0}", rawBody);

        // statusCode
        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue(
            "422 body must have 'statusCode'; body: {0}", rawBody);
        statusCodeProp.GetInt32().Should().Be(422,
            "statusCode in 422 body must be 422; body: {0}", rawBody);

        // successed
        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "422 body must have 'successed'; body: {0}", rawBody);
        succeededProp.GetBoolean().Should().BeFalse(
            "Successed must be false on a validation error; body: {0}", rawBody);

        // errors — array with at least one entry
        TryProp(root, "errors", out var errorsProp).Should().BeTrue(
            "422 body must have 'errors'; body: {0}", rawBody);
        errorsProp.ValueKind.Should().Be(JsonValueKind.Array,
            "errors must be a JSON array; body: {0}", rawBody);
        errorsProp.GetArrayLength().Should().BeGreaterThan(0,
            "errors must have at least one entry on a validation failure; body: {0}", rawBody);
    }
}
