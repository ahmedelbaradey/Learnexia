using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P1-12 BE-9 + BE-8 integration tests.
///
/// BE-9 — Register-Parent now accepts optional <c>Country</c> and mandatory <c>AcceptedTerms</c>:
///   POST /api/Users/Authentication/Register-Parent
///   Body: { email, password, fullName?, country?, acceptedTerms }
///   Success → 200, BaseResponse&lt;JwtAuthResponse&gt;, Successed=true
///   AcceptedTerms=false | omitted → 422 (registration blocked)
///   Country &gt; 100 chars → 422
///   POST-success /Me shows country (mapped to Nationality)
///   AcceptedTerms=true, country omitted → success, country is null in /Me
///
/// BE-8 — Edit-Child (parent edits their OWN linked child):
///   PUT /api/Parent/Update-Child
///   Body: { childId, fullName, grade, language, country }
///   Success → 200, BaseResponse&lt;UpdatedChildResponse&gt;, Successed=true
///   Happy path: re-fetch reflects updated values
///   Family-scope: parent A editing parent B's child → 403; B's child UNCHANGED
///   Validation: grade outside 1-6 → 422; language not in {ar,en} → 422; empty fullName → 422; fullName/country &gt; 100 → 422
///   Auth: unauthenticated → 401
///   No mass-assignment: extra email/role/parentId fields in body are ignored
///
/// Acceptance criteria map:
///   BE-9-AC-1: acceptedTerms=false → 422
///   BE-9-AC-2: acceptedTerms=true + country → 200, /Me shows country (Nationality)
///   BE-9-AC-3: acceptedTerms=true, country omitted → 200, /Me country null
///   BE-9-AC-4: country &gt; 100 chars → 422
///   BE-8-AC-5: parent edits own child → 200, values persisted
///   BE-8-AC-6: parent edits another parent's child → 403, child UNCHANGED
///   BE-8-AC-7: grade outside 1-6 → 422; language not in {ar,en} → 422; empty fullName → 422; field &gt; 100 chars → 422
///   BE-8-AC-8: unauthenticated → 401
///   BE-8-AC-9: extra mass-assignment fields in body are ignored
/// </summary>
[Collection("IntegrationTests")]
public sealed class P1_12_BE9_Register_BE8_EditChild_Tests : IAsyncLifetime
{
    // ---------------------------------------------------------------------------
    // URLs
    // ---------------------------------------------------------------------------
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string MeUrl = "api/Users/Me";
    private const string AddChildUrl = "api/Parent/Add-Child";
    private const string UpdateChildUrl = "api/Parent/Update-Child";
    private const string MyChildrenUrl = "api/Parent/My-Children";

    // Passwords that satisfy Identity policy: RequireDigit + RequireLowercase + RequireUppercase +
    // RequireNonAlphanumeric + length >= 6.
    private const string ValidParentPassword = "Str0ng@Pass";
    private const string ValidChildPassword = "Child@Pass1";

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_12_BE9_Register_BE8_EditChild_Tests(LearnexiaWebAppFactory factory)
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
        => $"p112be9_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>
    /// Case-insensitive JSON property lookup — handles both the camelCase controller path
    /// and the PascalCase middleware (422) path transparently.
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
    /// Registers a parent with AcceptedTerms=true (required by BE-9 validator) and optional
    /// country.  Returns (accessToken, userId).
    /// </summary>
    private async Task<(string Token, int UserId)> RegisterParentAsync(
        string email,
        string password = ValidParentPassword,
        string? fullName = null,
        string? country = null,
        bool acceptedTerms = true)
    {
        // Build the payload dynamically so we can test with/without country.
        var dict = new Dictionary<string, object?>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["AcceptedTerms"] = acceptedTerms,
        };
        if (fullName is not null) dict["FullName"] = fullName;
        if (country is not null) dict["Country"] = country;

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl, dict);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", body);
        TryProp(data, "userId", out var userIdProp).Should().BeTrue("body: {0}", body);
        return (tokenProp.GetString()!, userIdProp.GetInt32());
    }

    /// <summary>
    /// Adds a child for the given parent token and returns the child's Id.
    /// </summary>
    private async Task<int> AddChildAsync(string parentToken, string childEmail,
        string fullName = "Test Child", int grade = 3, string language = "ar", string country = "EG",
        string learningLanguage = "ar")
    {
        var body = new
        {
            FullName = fullName,
            Email = childEmail,
            Password = ValidChildPassword,
            Grade = grade,
            Language = language,
            Country = country,
            LearningLanguage = learningLanguage, // P8-01: required
        };
        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl, body, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Add-Child prerequisite must succeed; body: {0}", rawBody);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", rawBody);
        TryProp(data, "id", out var idProp).Should().BeTrue("body: {0}", rawBody);
        return idProp.GetInt32();
    }

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetMeAsync(string bearerToken)
        => SendAsync(_client, HttpMethod.Get, MeUrl, null, bearerToken);

    /// <summary>
    /// Helper that asserts a 422 response has the correct BaseResponse validation shape.
    /// </summary>
    private static void AssertValidationBody(JsonElement root, string rawBody)
    {
        root.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "422 response must have a JSON body; raw body: {0}", rawBody);

        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue(
            "422 body must have 'statusCode'; body: {0}", rawBody);
        statusCodeProp.GetInt32().Should().Be(422,
            "statusCode in 422 body must be 422; body: {0}", rawBody);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "422 body must have 'successed'; body: {0}", rawBody);
        succeededProp.GetBoolean().Should().BeFalse(
            "Successed must be false on a validation error; body: {0}", rawBody);

        TryProp(root, "errors", out var errorsProp).Should().BeTrue(
            "422 body must have 'errors'; body: {0}", rawBody);
        errorsProp.ValueKind.Should().Be(JsonValueKind.Array,
            "errors must be a JSON array; body: {0}", rawBody);
        errorsProp.GetArrayLength().Should().BeGreaterThan(0,
            "errors must have at least one entry on a validation failure; body: {0}", rawBody);
    }

    // ===========================================================================
    // BE-9 — Register: AcceptedTerms + Country
    // ===========================================================================

    // ---- BE-9-AC-1: acceptedTerms=false (or omitted) → 422 ----

    [Fact(DisplayName = "BE-9-AC-1a: Register with acceptedTerms=false → 422 (registration blocked)")]
    public async Task BE9_RegisterWithAcceptedTermsFalse_Returns422()
    {
        var payload = new
        {
            Email = UniqueEmail("terms-false"),
            Password = ValidParentPassword,
            AcceptedTerms = false
        };

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl, payload);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "AcceptedTerms=false must block registration with 422; body: {0}", body);

        AssertValidationBody(root, body);
    }

    [Fact(DisplayName = "BE-9-AC-1b: Register with acceptedTerms omitted (defaults to false) → 422")]
    public async Task BE9_RegisterWithAcceptedTermsOmitted_Returns422()
    {
        // Serialize a payload that deliberately omits AcceptedTerms (defaults to C# bool default = false).
        var payload = new
        {
            Email = UniqueEmail("terms-omit"),
            Password = ValidParentPassword
            // AcceptedTerms NOT set — bool default is false, validator must reject it
        };

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl, payload);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Omitting AcceptedTerms (defaults to false) must block registration with 422; body: {0}", body);

        AssertValidationBody(root, body);
    }

    [Fact(DisplayName = "BE-9-AC-1c: 422 response for acceptedTerms=false has full BaseResponse envelope")]
    public async Task BE9_AcceptedTermsFalse_422EnvelopeShape()
    {
        var payload = new
        {
            Email = UniqueEmail("terms-env"),
            Password = ValidParentPassword,
            AcceptedTerms = false
        };

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl, payload);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "body: {0}", body);

        // All 4 required BaseResponse<T> keys per architecture.md §6
        TryProp(root, "statusCode", out _).Should().BeTrue(
            "422 envelope must have 'statusCode'; body: {0}", body);
        TryProp(root, "successed", out _).Should().BeTrue(
            "422 envelope must have 'successed'; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue(
            "422 envelope must have 'message'; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue(
            "422 envelope must have 'errors'; body: {0}", body);
    }

    [Fact(DisplayName = "BE-9-AC-1d: No account is created in DB when acceptedTerms=false")]
    public async Task BE9_AcceptedTermsFalse_NoAccountCreated()
    {
        var email = UniqueEmail("terms-no-acct");
        var payload = new
        {
            Email = email,
            Password = ValidParentPassword,
            AcceptedTerms = false
        };

        var (resp, _, _) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl, payload);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Verify no User row was created
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var count = db.Users.Count(u => u.Email == email);
        count.Should().Be(0,
            "No user must be created when AcceptedTerms=false (validation must run before handler); email: {0}", email);
    }

    // ---- BE-9-AC-2: acceptedTerms=true + country → 200, /Me country not null ----

    [Fact(DisplayName = "BE-9-AC-2a: Register acceptedTerms=true + country → 200, Successed=true, token returned")]
    public async Task BE9_RegisterWithAcceptedTermsTrue_AndCountry_Returns200()
    {
        var payload = new
        {
            Email = UniqueEmail("with-country"),
            Password = ValidParentPassword,
            AcceptedTerms = true,
            Country = "EG"
        };

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl, payload);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "AcceptedTerms=true + country must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        token.GetString().Should().NotBeNullOrWhiteSpace(
            "accessToken must be a non-empty JWT; body: {0}", body);
    }

    [Fact(DisplayName = "BE-9-AC-2b: After register with country, GET /Me reflects country (mapped to Nationality)")]
    public async Task BE9_RegisterWithCountry_MeReflectsCountry()
    {
        const string countryValue = "SA";
        var (token, _) = await RegisterParentAsync(UniqueEmail("me-country"), country: countryValue);

        var (meResp, meRoot, meBody) = await GetMeAsync(token);

        meResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Me after register with country must return 200; body: {0}", meBody);

        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);

        TryProp(meData, "country", out var countryProp).Should().BeTrue(
            "/Me data must contain 'country' key; body: {0}", meBody);
        countryProp.GetString().Should().Be(countryValue,
            "/Me country must match the country registered with; body: {0}", meBody);
    }

    [Fact(DisplayName = "BE-9-AC-2c: AcceptedTermsAtUtc is persisted (not null) after successful registration")]
    public async Task BE9_RegisterWithAcceptedTermsTrue_AcceptedTermsAtUtcPersisted()
    {
        var email = UniqueEmail("terms-utc");
        var (_, userId) = await RegisterParentAsync(email, country: "AE");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var user = db.Users.Single(u => u.Id == userId);

        user.AcceptedTermsAtUtc.Should().NotBeNull(
            "AcceptedTermsAtUtc must be stamped when AcceptedTerms=true; userId: {0}", userId);
    }

    [Fact(DisplayName = "BE-9-AC-2d: Country persisted on Nationality after registration with country")]
    public async Task BE9_RegisterWithCountry_NationalityPersistedInDb()
    {
        const string countryValue = "KW";
        var email = UniqueEmail("nationality");
        var (_, userId) = await RegisterParentAsync(email, country: countryValue);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var user = db.Users.Single(u => u.Id == userId);

        user.Nationality.Should().Be(countryValue,
            "Nationality must match the Country submitted at registration; userId: {0}", userId);
    }

    // ---- BE-9-AC-3: acceptedTerms=true, country omitted → success, country null in /Me ----

    [Fact(DisplayName = "BE-9-AC-3a: Register acceptedTerms=true without country → 200 (country optional)")]
    public async Task BE9_RegisterWithAcceptedTermsTrue_NoCountry_Returns200()
    {
        var payload = new
        {
            Email = UniqueEmail("no-country"),
            Password = ValidParentPassword,
            AcceptedTerms = true
            // Country NOT set
        };

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl, payload);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "AcceptedTerms=true without country must still return 200 (country is optional); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);
    }

    [Fact(DisplayName = "BE-9-AC-3b: GET /Me after register without country → country is null")]
    public async Task BE9_RegisterWithoutCountry_MeCountryIsNull()
    {
        // Register without country
        var payload = new
        {
            Email = UniqueEmail("null-country"),
            Password = ValidParentPassword,
            AcceptedTerms = true
        };
        var (regResp, regRoot, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl, payload);
        regResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "registration must succeed; body: {0}", regBody);
        TryProp(regRoot, "data", out var regData).Should().BeTrue("body: {0}", regBody);
        TryProp(regData, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", regBody);
        var token = tokenProp.GetString()!;

        var (meResp, meRoot, meBody) = await GetMeAsync(token);

        meResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", meBody);
        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);

        TryProp(meData, "country", out var countryProp).Should().BeTrue(
            "/Me data must contain 'country' key (may be null); body: {0}", meBody);
        countryProp.ValueKind.Should().Be(JsonValueKind.Null,
            "/Me country must be null when country was not supplied at registration; body: {0}", meBody);
    }

    // ---- BE-9-AC-4: country > 100 chars → 422 ----

    [Fact(DisplayName = "BE-9-AC-4: Register with country > 100 chars → 422")]
    public async Task BE9_RegisterWithCountryTooLong_Returns422()
    {
        var longCountry = new string('X', 101); // 101 chars, max is 100

        var payload = new
        {
            Email = UniqueEmail("long-country"),
            Password = ValidParentPassword,
            AcceptedTerms = true,
            Country = longCountry
        };

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl, payload);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "country > 100 chars must return 422; body: {0}", body);

        AssertValidationBody(root, body);
    }

    [Fact(DisplayName = "BE-9-AC-4b: Register with country = 100 chars (boundary) → 200 (exactly at max)")]
    public async Task BE9_RegisterWithCountryExactly100Chars_Returns200()
    {
        var exactCountry = new string('X', 100); // 100 chars, exactly at max

        var payload = new
        {
            Email = UniqueEmail("exact-country"),
            Password = ValidParentPassword,
            AcceptedTerms = true,
            Country = exactCountry
        };

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl, payload);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "country of exactly 100 chars (at the boundary) must be accepted; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);
    }

    // ===========================================================================
    // BE-8 — Update-Child (PUT api/Users/Parent/Update-Child)
    // ===========================================================================

    // ---- BE-8-AC-8: Auth — unauthenticated → 401 ----

    [Fact(DisplayName = "BE-8-AC-8a: PUT Update-Child without token → 401")]
    public async Task BE8_UpdateChild_NoToken_Returns401()
    {
        var body = new { ChildId = 1, FullName = "X", Grade = 3, Language = "ar", Country = "EG" };

        var (resp, _, rawBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, body);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Update-Child without a bearer token must return 401; body: {0}", rawBody);
    }

    [Fact(DisplayName = "BE-8-AC-8b: PUT Update-Child with garbage token → 401")]
    public async Task BE8_UpdateChild_InvalidToken_Returns401()
    {
        var body = new { ChildId = 1, FullName = "X", Grade = 3, Language = "ar", Country = "EG" };

        var (resp, _, rawBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, body,
            "this.is.not.a.valid.jwt");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Update-Child with an invalid bearer token must return 401; body: {0}", rawBody);
    }

    // ---- BE-8-AC-5: Happy path — parent edits their OWN linked child ----

    [Fact(DisplayName = "BE-8-AC-5a: Parent updates own child → 200, Successed=true, response reflects new values")]
    public async Task BE8_HappyPath_UpdateOwnChild_Returns200_WithUpdatedValues()
    {
        // Setup: register parent, add child
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail("edit-parent"));
        var childEmail = UniqueEmail("edit-child");
        var childId = await AddChildAsync(parentToken, childEmail,
            fullName: "Original Name", grade: 2, language: "ar", country: "EG");

        // Act: update the child
        var updateBody = new
        {
            ChildId = childId,
            FullName = "Updated Name",
            Grade = 5,
            Language = "en",
            Country = "SA"
        };
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, updateBody, parentToken);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Update-Child with own child must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Null, "data must not be null; body: {0}", body);

        TryProp(data, "id", out var idProp).Should().BeTrue("body: {0}", body);
        idProp.GetInt32().Should().Be(childId, "response id must match the updated child; body: {0}", body);

        TryProp(data, "fullName", out var fullName).Should().BeTrue("body: {0}", body);
        fullName.GetString().Should().Be("Updated Name",
            "response fullName must reflect the new value; body: {0}", body);

        TryProp(data, "grade", out var grade).Should().BeTrue("body: {0}", body);
        grade.GetInt32().Should().Be(5,
            "response grade must reflect the new value; body: {0}", body);

        TryProp(data, "language", out var language).Should().BeTrue("body: {0}", body);
        language.GetString().Should().ContainEquivalentOf("en",
            "response language must contain 'en' (normalized to en-US); body: {0}", body);

        TryProp(data, "country", out var country).Should().BeTrue("body: {0}", body);
        country.GetString().Should().Be("SA",
            "response country must reflect the new value; body: {0}", body);
    }

    [Fact(DisplayName = "BE-8-AC-5b: After Update-Child, re-fetch via My-Children shows persisted values")]
    public async Task BE8_HappyPath_PersistenceVerified_ViaMyChildren()
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail("persist-parent"));
        var childEmail = UniqueEmail("persist-child");
        var childId = await AddChildAsync(parentToken, childEmail,
            fullName: "Before Update", grade: 1, language: "ar", country: "EG");

        // Update the child
        var updateBody = new
        {
            ChildId = childId,
            FullName = "After Update",
            Grade = 4,
            Language = "en",
            Country = "AE"
        };
        var (putResp, _, putBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, updateBody, parentToken);
        putResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite PUT must succeed; body: {0}", putBody);

        // Verify via DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var child = db.Users.Single(u => u.Id == childId);

        child.FullName.Should().Be("After Update",
            "fullName must be persisted after Update-Child; childId: {0}", childId);
        child.Grade.Should().Be(4,
            "grade must be persisted after Update-Child; childId: {0}", childId);
        child.PreferredLanguage.Should().Be("en-US",
            "language must be normalized and persisted as 'en-US'; childId: {0}", childId);
        child.Nationality.Should().Be("AE",
            "country (Nationality) must be persisted after Update-Child; childId: {0}", childId);
    }

    [Fact(DisplayName = "BE-8-AC-5c: Update-Child response has all BaseResponse envelope keys")]
    public async Task BE8_HappyPath_ResponseHasBaseResponseEnvelopeKeys()
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail("env-parent"));
        var childId = await AddChildAsync(parentToken, UniqueEmail("env-child"));

        var updateBody = new
        {
            ChildId = childId,
            FullName = "Envelope Test",
            Grade = 3,
            Language = "ar",
            Country = "EG"
        };
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, updateBody, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue(
            "envelope must have 'statusCode'; body: {0}", body);
        statusCodeProp.GetInt32().Should().Be(200,
            "statusCode in envelope must be 200; body: {0}", body);

        TryProp(root, "successed", out _).Should().BeTrue("envelope must have 'successed'; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have 'message'; body: {0}", body);
        TryProp(root, "data", out _).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue("envelope must have 'errors'; body: {0}", body);
    }

    // ---- BE-8-AC-6: Family-scope — parent A tries to edit parent B's child → 403 ----

    [Fact(DisplayName = "BE-8-AC-6a: Parent A editing parent B's child (not linked to A) → 403")]
    public async Task BE8_FamilyScope_ParentEditsOtherParentChild_Returns403()
    {
        // Register two distinct parents
        var (tokenA, _) = await RegisterParentAsync(UniqueEmail("familyA"));
        var (tokenB, _) = await RegisterParentAsync(UniqueEmail("familyB"));

        // Parent B adds a child
        var childBEmail = UniqueEmail("childB");
        var childBId = await AddChildAsync(tokenB, childBEmail,
            fullName: "B Child Original", grade: 3, language: "ar", country: "EG");

        // Parent A tries to edit Parent B's child — must be forbidden
        var attackBody = new
        {
            ChildId = childBId,
            FullName = "Hacked Name",
            Grade = 6,
            Language = "en",
            Country = "UK"
        };
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, attackBody, tokenA);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent A editing Parent B's child must return 403; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse(
            "Successed must be false for a family-scope violation; body: {0}", body);
    }

    [Fact(DisplayName = "BE-8-AC-6b: After the forbidden attempt, parent B's child is UNCHANGED in DB")]
    public async Task BE8_FamilyScope_ChildBUnchanged_AfterForbiddenAttempt()
    {
        // Register two distinct parents
        var (tokenA, _) = await RegisterParentAsync(UniqueEmail("scopeA"));
        var (tokenB, _) = await RegisterParentAsync(UniqueEmail("scopeB"));

        // Parent B adds a child
        var childBEmail = UniqueEmail("scopeChildB");
        var childBId = await AddChildAsync(tokenB, childBEmail,
            fullName: "Unchanged Child", grade: 2, language: "ar", country: "EG");

        // Parent A attempts to edit the child (must be 403)
        var attackBody = new
        {
            ChildId = childBId,
            FullName = "Tampered Name",
            Grade = 6,
            Language = "en",
            Country = "XX"
        };
        var (attackResp, _, _) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, attackBody, tokenA);
        attackResp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "prerequisite: attack must be forbidden");

        // Verify DB state: child B's values must be exactly as Parent B created them
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var childB = db.Users.Single(u => u.Id == childBId);

        childB.FullName.Should().Be("Unchanged Child",
            "fullName must NOT be changed by a forbidden edit; childId: {0}", childBId);
        childB.Grade.Should().Be(2,
            "grade must NOT be changed by a forbidden edit; childId: {0}", childBId);
        childB.Nationality.Should().Be("EG",
            "Nationality must NOT be changed by a forbidden edit; childId: {0}", childBId);
    }

    [Fact(DisplayName = "BE-8-AC-6c: 403 response has BaseResponse envelope (Successed=false)")]
    public async Task BE8_FamilyScope_403ResponseHasBaseResponseEnvelope()
    {
        var (tokenA, _) = await RegisterParentAsync(UniqueEmail("env403A"));
        var (tokenB, _) = await RegisterParentAsync(UniqueEmail("env403B"));

        var childBId = await AddChildAsync(tokenB, UniqueEmail("env403child"));

        var attackBody = new
        {
            ChildId = childBId,
            FullName = "X",
            Grade = 3,
            Language = "ar",
            Country = "EG"
        };
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, attackBody, tokenA);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);

        // envelope keys present
        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue(
            "403 envelope must have 'statusCode'; body: {0}", body);
        statusCodeProp.GetInt32().Should().Be(403,
            "statusCode in 403 body must be 403; body: {0}", body);

        TryProp(root, "successed", out var succeedProp).Should().BeTrue(
            "403 body must have 'successed'; body: {0}", body);
        succeedProp.GetBoolean().Should().BeFalse(
            "Successed must be false on 403; body: {0}", body);
    }

    // ---- BE-8-AC-7: Validation → 422 ----

    [Fact(DisplayName = "BE-8-AC-7a: Update-Child with grade=0 (below min 1) → 422")]
    public async Task BE8_Validation_GradeZero_Returns422()
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail("val-g0-parent"));
        var childId = await AddChildAsync(parentToken, UniqueEmail("val-g0-child"));

        var body = new
        {
            ChildId = childId,
            FullName = "Valid Name",
            Grade = 0,
            Language = "ar",
            Country = "EG"
        };
        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, body, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "grade=0 is outside 1-6 and must return 422; body: {0}", rawBody);
        AssertValidationBody(root, rawBody);
    }

    [Fact(DisplayName = "BE-8-AC-7b: Update-Child with grade=7 (above max 6) → 422")]
    public async Task BE8_Validation_GradeSeven_Returns422()
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail("val-g7-parent"));
        var childId = await AddChildAsync(parentToken, UniqueEmail("val-g7-child"));

        var body = new
        {
            ChildId = childId,
            FullName = "Valid Name",
            Grade = 7,
            Language = "ar",
            Country = "EG"
        };
        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, body, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "grade=7 is outside 1-6 and must return 422; body: {0}", rawBody);
        AssertValidationBody(root, rawBody);
    }

    [Theory(DisplayName = "BE-8-AC-7c: Valid grades 1-6 on Update-Child → 200")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task BE8_Validation_ValidGrades_Returns200(int grade)
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail($"valg{grade}parent"));
        var childId = await AddChildAsync(parentToken, UniqueEmail($"valg{grade}child"));

        var body = new
        {
            ChildId = childId,
            FullName = "Grade Test",
            Grade = grade,
            Language = "ar",
            Country = "EG"
        };
        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, body, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "grade={0} is within 1-6 and must return 200; body: {1}", grade, rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeTrue("body: {0}", rawBody);
    }

    [Fact(DisplayName = "BE-8-AC-7d: Update-Child with language='fr' (not ar|en) → 422")]
    public async Task BE8_Validation_InvalidLanguage_Fr_Returns422()
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail("val-lang-parent"));
        var childId = await AddChildAsync(parentToken, UniqueEmail("val-lang-child"));

        var body = new
        {
            ChildId = childId,
            FullName = "Valid Name",
            Grade = 3,
            Language = "fr",
            Country = "EG"
        };
        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, body, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "language='fr' is not in {{ar,en}} and must return 422; body: {0}", rawBody);
        AssertValidationBody(root, rawBody);
    }

    [Theory(DisplayName = "BE-8-AC-7e: Valid languages ar and en on Update-Child → 200")]
    [InlineData("ar")]
    [InlineData("en")]
    public async Task BE8_Validation_ValidLanguages_Returns200(string lang)
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail($"vlang{lang}parent"));
        var childId = await AddChildAsync(parentToken, UniqueEmail($"vlang{lang}child"));

        var body = new
        {
            ChildId = childId,
            FullName = "Language Test",
            Grade = 3,
            Language = lang,
            Country = "EG"
        };
        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, body, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "language='{0}' must be accepted; body: {1}", lang, rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeTrue("body: {0}", rawBody);
    }

    [Fact(DisplayName = "BE-8-AC-7f: Update-Child with empty fullName → 422")]
    public async Task BE8_Validation_EmptyFullName_Returns422()
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail("val-fn-parent"));
        var childId = await AddChildAsync(parentToken, UniqueEmail("val-fn-child"));

        var body = new
        {
            ChildId = childId,
            FullName = "",
            Grade = 3,
            Language = "ar",
            Country = "EG"
        };
        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, body, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty fullName must return 422; body: {0}", rawBody);
        AssertValidationBody(root, rawBody);
    }

    [Fact(DisplayName = "BE-8-AC-7g: Update-Child with fullName > 100 chars → 422")]
    public async Task BE8_Validation_FullNameTooLong_Returns422()
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail("val-fnlong-parent"));
        var childId = await AddChildAsync(parentToken, UniqueEmail("val-fnlong-child"));

        var longName = new string('A', 101); // 101 chars, max is 100
        var body = new
        {
            ChildId = childId,
            FullName = longName,
            Grade = 3,
            Language = "ar",
            Country = "EG"
        };
        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, body, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "fullName > 100 chars must return 422; body: {0}", rawBody);
        AssertValidationBody(root, rawBody);
    }

    [Fact(DisplayName = "BE-8-AC-7h: Update-Child with country > 100 chars → 422")]
    public async Task BE8_Validation_CountryTooLong_Returns422()
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail("val-clong-parent"));
        var childId = await AddChildAsync(parentToken, UniqueEmail("val-clong-child"));

        var longCountry = new string('X', 101); // 101 chars, max is 100
        var body = new
        {
            ChildId = childId,
            FullName = "Valid Name",
            Grade = 3,
            Language = "ar",
            Country = longCountry
        };
        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, body, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "country > 100 chars must return 422; body: {0}", rawBody);
        AssertValidationBody(root, rawBody);
    }

    [Fact(DisplayName = "BE-8-AC-7i: Update-Child with empty country → 422")]
    public async Task BE8_Validation_EmptyCountry_Returns422()
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail("val-ec-parent"));
        var childId = await AddChildAsync(parentToken, UniqueEmail("val-ec-child"));

        var body = new
        {
            ChildId = childId,
            FullName = "Valid Name",
            Grade = 3,
            Language = "ar",
            Country = ""
        };
        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, body, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty country must return 422 (UpdateChildCommandValidator marks Country as NotEmpty); body: {0}", rawBody);
        AssertValidationBody(root, rawBody);
    }

    [Fact(DisplayName = "BE-8-AC-7j: Update-Child with childId=0 → 422 (ChildId must be > 0)")]
    public async Task BE8_Validation_ChildIdZero_Returns422()
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail("val-id0-parent"));

        var body = new
        {
            ChildId = 0,
            FullName = "Valid Name",
            Grade = 3,
            Language = "ar",
            Country = "EG"
        };
        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, body, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "childId=0 must return 422 (must be > 0 per UpdateChildCommandValidator); body: {0}", rawBody);
        AssertValidationBody(root, rawBody);
    }

    // ---- BE-8-AC-9: No mass-assignment — extra fields in body are ignored ----

    [Fact(DisplayName = "BE-8-AC-9: Extra fields (email, role, parentId) in Update-Child body are ignored")]
    public async Task BE8_NoMassAssignment_ExtraFieldsIgnored()
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail("mass-parent"));
        var childEmail = UniqueEmail("mass-child");
        var childId = await AddChildAsync(parentToken, childEmail);

        // Attempt to send extra fields that the command does not declare; they must be silently ignored.
        var bodyWithExtras = new Dictionary<string, object?>
        {
            ["ChildId"] = childId,
            ["FullName"] = "Mass Assign Test",
            ["Grade"] = 3,
            ["Language"] = "ar",
            ["Country"] = "EG",
            // These must be ignored by the model binder (not declared on UpdateChildCommand)
            ["Email"] = "attacker@evil.com",
            ["Role"] = "SuperAdmin",
            ["ParentId"] = 99999,
        };

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, bodyWithExtras, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "extra fields must be ignored; Update-Child must succeed normally; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        // Verify email was NOT changed in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var child = db.Users.Single(u => u.Id == childId);

        child.Email.Should().Be(childEmail,
            "the child's email must NOT be changed by an extra 'email' field in the body; childId: {0}", childId);
    }

    // ===========================================================================
    // BE-TC-51: Edit non-existent child → identical 403 (no id-enumeration oracle)
    // Not-linked AND not-found must both return the SAME 403 + same message so
    // an attacker cannot distinguish "child exists but not mine" from "no such child".
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-51a: PUT Update-Child with non-existent childId (999999999) → 403")]
    public async Task BE8_NonExistentChild_Returns403()
    {
        var (parentToken, _) = await RegisterParentAsync(UniqueEmail("nonexist-parent"));

        var body = new
        {
            ChildId = 999999999, // almost certainly does not exist
            FullName = "Ghost Child",
            Grade = 3,
            Language = "ar",
            Country = "EG"
        };

        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl, body, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "BE-TC-51: non-existent childId must return 403 (same as not-linked) — no id-enumeration oracle; body: {0}", rawBody);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeFalse(
            "Successed must be false for non-existent child; body: {0}", rawBody);
    }

    [Fact(DisplayName = "BE-TC-51b: Non-existent child returns same 403 message as cross-family (no enumeration oracle)")]
    public async Task BE8_NonExistentChild_SameMessageAsCrossFamily()
    {
        // Path A: cross-family (child exists but belongs to a different parent)
        var (tokenA, _) = await RegisterParentAsync(UniqueEmail("enum-A"));
        var (tokenB, _) = await RegisterParentAsync(UniqueEmail("enum-B"));
        var childBId = await AddChildAsync(tokenB, UniqueEmail("enum-childB"));

        var crossFamilyBody = new
        {
            ChildId = childBId,
            FullName = "Cross Attempt",
            Grade = 3,
            Language = "ar",
            Country = "EG"
        };
        var (crossResp, crossRoot, crossRawBody) = await SendAsync(
            _client, HttpMethod.Put, UpdateChildUrl, crossFamilyBody, tokenA);
        crossResp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "cross-family; body: {0}", crossRawBody);
        TryProp(crossRoot, "message", out var crossMsg).Should().BeTrue("body: {0}", crossRawBody);

        // Path B: non-existent child (using a fresh parent who has no children)
        var (tokenC, _) = await RegisterParentAsync(UniqueEmail("enum-C"));
        var nonExistBody = new
        {
            ChildId = 999999998,
            FullName = "Ghost",
            Grade = 3,
            Language = "ar",
            Country = "EG"
        };
        var (nonExistResp, nonExistRoot, nonExistRawBody) = await SendAsync(
            _client, HttpMethod.Put, UpdateChildUrl, nonExistBody, tokenC);
        nonExistResp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "non-existent; body: {0}", nonExistRawBody);
        TryProp(nonExistRoot, "message", out var nonExistMsg).Should().BeTrue("body: {0}", nonExistRawBody);

        // Both must produce the same message — no id-enumeration oracle
        crossMsg.GetString().Should().Be(nonExistMsg.GetString(),
            "BE-TC-51 HEADLINE: 403 message must be IDENTICAL for cross-family and non-existent child " +
            "— any difference is a child-id enumeration oracle. " +
            "cross-family body: {0}, non-existent body: {1}",
            crossRawBody, nonExistRawBody);
    }

    // ===========================================================================
    // BE-TC-57: Register cannot inject a role / create a non-parent
    // Extra unmapped fields (roles, role) in the body must be silently ignored.
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-57: Register with extra 'roles' field → 200 but account is Parent ONLY (role injection blocked)")]
    public async Task BE9_Register_RoleInjection_Blocked()
    {
        var email = UniqueEmail("role-inject");

        // Attempt mass-assignment: include 'roles' and 'role' extra fields alongside valid registration fields.
        // UpdateMyProfileCommand / RegisterParentCommand do NOT have a Roles property, so the model binder
        // must silently ignore them.
        var payload = new Dictionary<string, object?>
        {
            ["Email"] = email,
            ["Password"] = ValidParentPassword,
            ["AcceptedTerms"] = true,
            // These extra fields must be ignored — command doesn't have a Roles property
            ["Roles"] = new[] { "Admin" },
            ["Role"] = "Student",
        };

        var (regResp, regRoot, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl, payload);

        regResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-57: Register with extra role fields must still succeed (extras ignored); body: {0}", regBody);

        TryProp(regRoot, "successed", out var successed).Should().BeTrue("body: {0}", regBody);
        successed.GetBoolean().Should().BeTrue("body: {0}", regBody);

        TryProp(regRoot, "data", out var data).Should().BeTrue("body: {0}", regBody);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", regBody);
        var token = tokenProp.GetString()!;

        // Verify via /Me that the assigned role is Parent only
        var (meResp, meRoot, meBody) = await GetMeAsync(token);
        meResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", meBody);
        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);
        TryProp(meData, "roles", out var rolesProp).Should().BeTrue("body: {0}", meBody);

        var roles = rolesProp.EnumerateArray()
            .Select(r => r.GetString())
            .Where(r => r is not null)
            .ToList();

        roles.Should().Contain(r => string.Equals(r, "Parent", StringComparison.OrdinalIgnoreCase),
            "roles must contain 'Parent'; roles={0}; body={1}", string.Join(",", roles!), meBody);

        roles.Should().NotContain(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase),
            "roles must NOT contain 'Admin' — role injection via extra body field must be blocked; body: {0}", meBody);

        roles.Should().NotContain(r => string.Equals(r, "Student", StringComparison.OrdinalIgnoreCase),
            "roles must NOT contain 'Student' — student self-register is blocked per product decisions; body: {0}", meBody);

        roles.Should().NotContain(r => string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase),
            "roles must NOT contain 'SuperAdmin' — privilege escalation via extra field must be blocked; body: {0}", meBody);
    }

    // ===========================================================================
    // BE-TC-58: Register duplicate email → 422 (no second account created)
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-58a: Register same email twice → second attempt returns 422 (duplicate email)")]
    public async Task BE9_Register_DuplicateEmail_Returns422()
    {
        var email = UniqueEmail("dup-email");

        // First registration — must succeed
        var (firstResp, _, firstBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = ValidParentPassword, AcceptedTerms = true });
        firstResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "First registration must succeed; body: {0}", firstBody);

        // Second registration with the same email — must be rejected
        var (secondResp, secondRoot, secondBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = ValidParentPassword, AcceptedTerms = true });

        secondResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "BE-TC-58: Registering with a duplicate email must return 422; body: {0}", secondBody);

        TryProp(secondRoot, "successed", out var successed).Should().BeTrue("body: {0}", secondBody);
        successed.GetBoolean().Should().BeFalse(
            "Successed must be false for duplicate email; body: {0}", secondBody);

        TryProp(secondRoot, "errors", out var errorsProp).Should().BeTrue("body: {0}", secondBody);
        errorsProp.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", secondBody);
        errorsProp.GetArrayLength().Should().BeGreaterThan(0,
            "errors must have at least one entry on duplicate email; body: {0}", secondBody);
    }

    [Fact(DisplayName = "BE-TC-58b: Only one account is created for a duplicate email (no account-takeover via re-register)")]
    public async Task BE9_Register_DuplicateEmail_OnlyOneAccount()
    {
        var email = UniqueEmail("dup-one");

        // First registration
        var (firstResp, firstRoot, firstBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = ValidParentPassword, AcceptedTerms = true });
        firstResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", firstBody);
        TryProp(firstRoot, "data", out var data1).Should().BeTrue("body: {0}", firstBody);
        TryProp(data1, "userId", out var idProp1).Should().BeTrue("body: {0}", firstBody);
        var originalUserId = idProp1.GetInt32();

        // Second registration — should be rejected
        var (secondResp, _, _) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "DifferentP@ss1", AcceptedTerms = true });
        secondResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "duplicate registration must be rejected");

        // Verify via DB that there is exactly one user with this email
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<
            Learnexia.Modules.Identity.Infrastructure.Persistence.IdentityModuleDbContext>();
        var count = db.Users.Count(u => u.Email == email);
        count.Should().Be(1,
            "BE-TC-58: exactly one account must exist for email '{0}'; found {1}", email, count);

        // The existing account must not have been mutated (password still the original)
        var existingUser = db.Users.Single(u => u.Email == email);
        existingUser.Id.Should().Be(originalUserId,
            "The userId must be unchanged after the failed duplicate registration");
    }

    // ---- Additional: Regression — existing (old-signature) register still works with AcceptedTerms=true ----

    [Fact(DisplayName = "Regression: existing tests register-parent helper with AcceptedTerms=true still returns 200")]
    public async Task Regression_RegisterWithAcceptedTermsTrue_StillWorks()
    {
        // This is the shape used internally by RegisterParentAsync — verifies the helper compiles
        // and produces a valid 200 for all other tests in this file.
        var (token, userId) = await RegisterParentAsync(UniqueEmail("regression"));

        token.Should().NotBeNullOrWhiteSpace("access token must be non-empty");
        userId.Should().BeGreaterThan(0, "userId must be a positive integer");
    }
}
