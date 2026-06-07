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
/// P1-01 integration tests: Parent self-registration endpoint.
///
/// Endpoint: [AllowAnonymous] POST /api/Users/Authentication/Register-Parent
/// Contract:  BaseResponse&lt;JwtAuthResponse&gt; — HTTP 200 on success, HTTP 422 on validation failure,
///            HTTP 400 on business-rule failure (duplicate email via handler backstop).
///
/// Observation: the project has two JSON serialisation paths:
///   - Controller responses (Happy-path 200/400/etc.) — flow through ASP.NET Core / Newtonsoft which
///     emits camelCase keys (e.g. "successed", "accessToken").
///   - ErrorHandlerMiddleWare (ValidationException → 422) — uses raw System.Text.Json.JsonSerializer
///     with default options, which preserves C# property names as PascalCase
///     (e.g. "Successed", "Errors", "StatusCode").
///
/// Tests use the case-insensitive helper Prop() so that both paths are exercised correctly.
///
/// Acceptance criteria covered:
///   AC-1  Happy path — 200, Successed=true, non-empty AccessToken, IsFirstLogin=true
///   AC-2  No anonymous child/Student creation path; command has no Roles field; extra JSON fields ignored
///   AC-3  Duplicate email → rejected (422 via validator or 400 via handler backstop); no second user
///   AC-4  Weak passwords → 422, Errors[] populated (per-rule coverage)
///   AC-5  Password never appears in the response body
///   AC-6  Validation failures surface as HTTP 422 with Errors[] items shaped { PropertyName, ErrorMessage }
/// </summary>
[Collection("IntegrationTests")]
public sealed class P1_01_RegisterParent_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_01_RegisterParent_Tests(LearnexiaWebAppFactory factory)
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
        => $"parent_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@integration.test";

    /// <summary>
    /// Case-insensitive property lookup on a JsonElement.
    /// The project has two JSON serialisation paths (controller = camelCase, middleware = PascalCase);
    /// this helper handles both transparently.
    /// </summary>
    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        // Try exact name first (camelCase), then PascalCase, then all-lower as fallback.
        if (element.TryGetProperty(name, out value)) return true;
        if (element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value)) return true;
        // Full linear scan as last resort (handles any mixed-case edge case).
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

    /// <summary>Returns the parsed JsonDocument root for a Register-Parent POST.</summary>
    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)> PostRegisterAsync(object body)
    {
        var response = await _client.PostAsJsonAsync("/api/Users/Authentication/Register-Parent", body);
        var bodyStr = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            var doc = JsonDocument.Parse(bodyStr);
            root = doc.RootElement;
        }
        return (response, root, bodyStr);
    }

    // =========================================================================
    // AC-1 Happy path
    // =========================================================================

    /// <summary>
    /// AC-1: valid unique email + strong password → HTTP 200, Successed=true, non-empty AccessToken.
    /// </summary>
    [Fact(DisplayName = "AC-1a HappyPath: valid credentials → 200 Successed=true with AccessToken")]
    public async Task AC1a_HappyPath_Returns200_WithSuccessedTrue_AndAccessToken()
    {
        var body = new { Email = UniqueEmail("happy"), Password = "Str0ng@Pass", AcceptedTerms = true };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Register-Parent with valid credentials must return HTTP 200; actual body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "response must contain 'successed' key per BaseResponse<T> envelope; body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeTrue(
            "successed must be true on successful registration; body: {0}", bodyStr);

        TryProp(root, "data", out var dataProp).Should().BeTrue(
            "response must contain 'data' key; body: {0}", bodyStr);

        TryProp(dataProp, "accessToken", out var tokenProp).Should().BeTrue(
            "data.accessToken must be present in JwtAuthResponse; body: {0}", bodyStr);
        tokenProp.GetString().Should().NotBeNullOrWhiteSpace(
            "AccessToken must be a non-empty JWT string; body: {0}", bodyStr);
    }

    /// <summary>
    /// AC-1: IsFirstLogin must be true for a freshly registered parent.
    /// </summary>
    [Fact(DisplayName = "AC-1b HappyPath: IsFirstLogin=true on fresh registration")]
    public async Task AC1b_HappyPath_IsFirstLogin_IsTrue()
    {
        var body = new { Email = UniqueEmail("firstlogin"), Password = "Str0ng@Pass", AcceptedTerms = true };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: registration must succeed; body: {0}", bodyStr);

        TryProp(root, "data", out var dataProp).Should().BeTrue("body: {0}", bodyStr);
        TryProp(dataProp, "isFirstLogin", out var isFirstLoginProp).Should().BeTrue(
            "data.isFirstLogin must be present in JwtAuthResponse; body: {0}", bodyStr);
        isFirstLoginProp.GetBoolean().Should().BeTrue(
            "IsFirstLogin must be true for a freshly registered parent; body: {0}", bodyStr);
    }

    /// <summary>
    /// AC-1: UserId must be set (non-zero) on successful registration.
    /// </summary>
    [Fact(DisplayName = "AC-1c HappyPath: UserId is set (non-zero) in response data")]
    public async Task AC1c_HappyPath_UserId_IsNonZero()
    {
        var body = new { Email = UniqueEmail("userid"), Password = "Str0ng@Pass", AcceptedTerms = true };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: registration must succeed; body: {0}", bodyStr);

        TryProp(root, "data", out var dataProp).Should().BeTrue("body: {0}", bodyStr);
        TryProp(dataProp, "userId", out var userIdProp).Should().BeTrue(
            "data.userId must be present in JwtAuthResponse; body: {0}", bodyStr);
        userIdProp.GetInt32().Should().BeGreaterThan(0,
            "UserId must be a positive integer for a successfully created user; body: {0}", bodyStr);
    }

    /// <summary>
    /// AC-1: FullName omitted — handler defaults to email local-part; no 500 error.
    /// </summary>
    [Fact(DisplayName = "AC-1d HappyPath: omitting FullName defaults to email local-part, no 500")]
    public async Task AC1d_HappyPath_OmittedFullName_UsesEmailLocalPart_No500()
    {
        var body = new { Email = UniqueEmail("noname"), Password = "Str0ng@Pass", AcceptedTerms = true };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "omitting FullName must not cause a 500; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeTrue(
            "registration must succeed when FullName is omitted; body: {0}", bodyStr);
    }

    /// <summary>
    /// AC-1: FullName provided — accepted without error.
    /// </summary>
    [Fact(DisplayName = "AC-1e HappyPath: FullName provided is accepted")]
    public async Task AC1e_HappyPath_WithFullName_Returns200()
    {
        var body = new { Email = UniqueEmail("withname"), Password = "Str0ng@Pass", FullName = "Test Parent", AcceptedTerms = true };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "providing FullName must not cause an error; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeTrue("body: {0}", bodyStr);
    }

    /// <summary>
    /// AC-1 (sign-in round-trip): registering a parent and then signing in with those credentials succeeds.
    /// This proves the password was hashed correctly and the user persisted (AC-1 + partial AC-5).
    /// </summary>
    [Fact(DisplayName = "AC-1f RoundTrip: registered parent can sign in immediately afterwards")]
    public async Task AC1f_HappyPath_RegisteredParent_CanSignIn()
    {
        var email = UniqueEmail("roundtrip");
        const string password = "Str0ng@Pass";

        // Register
        var (regResponse, _, regBody) = await PostRegisterAsync(new { Email = email, Password = password, AcceptedTerms = true });
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "registration must succeed before we can test sign-in; body: {0}", regBody);

        // Sign in with the same credentials (Identity stores UserName = Email for Register-Parent)
        var signInBody = new { UserName = email, Password = password };
        var signInResponse = await _client.PostAsJsonAsync("/api/Users/Authentication/Sign-In", signInBody);
        var signInContent = await signInResponse.Content.ReadAsStringAsync();

        signInResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in with just-registered parent credentials must return 200; body: {0}", signInContent);

        var signInRoot = JsonDocument.Parse(signInContent).RootElement;
        TryProp(signInRoot, "successed", out var successedProp).Should().BeTrue("body: {0}", signInContent);
        successedProp.GetBoolean().Should().BeTrue(
            "sign-in successed must be true; body: {0}", signInContent);

        TryProp(signInRoot, "data", out var signInData).Should().BeTrue("body: {0}", signInContent);
        TryProp(signInData, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", signInContent);
        tokenProp.GetString().Should().NotBeNullOrWhiteSpace(
            "sign-in must return a non-empty AccessToken; body: {0}", signInContent);
    }

    // =========================================================================
    // AC-5 Password hygiene — never echoed in response
    // =========================================================================

    /// <summary>
    /// AC-5: the response body must never contain the plaintext password under any key.
    /// </summary>
    [Fact(DisplayName = "AC-5 PasswordHygiene: response body never contains plaintext password")]
    public async Task AC5_Response_NeverContains_Password()
    {
        const string password = "Str0ng@Pass";
        var body = new { Email = UniqueEmail("hygiene"), Password = password, AcceptedTerms = true };

        var (response, _, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: registration must succeed; body: {0}", bodyStr);

        // The literal password string must not appear in the response JSON.
        bodyStr.Should().NotContain(password,
            "the plaintext password must never be echoed back in the response body");

        // No 'password' key must appear in the response JSON (case-insensitive check).
        bodyStr.ToLowerInvariant().Should().NotContain("\"password\"",
            "no 'password' key must appear in the response JSON");
    }

    // =========================================================================
    // AC-3 Duplicate email
    // =========================================================================

    /// <summary>
    /// AC-3 + AC-6: registering the same email twice is rejected.
    /// The validator's async uniqueness rule routes this through 422 Errors[].
    /// The handler's race-safe backstop returns 400 in the edge case.
    /// In either case Successed must be false.
    /// </summary>
    [Fact(DisplayName = "AC-3 DuplicateEmail: same email twice → rejected with Successed=false")]
    public async Task AC3_DuplicateEmail_RegisteredTwice_IsRejected()
    {
        var email = UniqueEmail("dup");
        var body = new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true };

        // First registration — must succeed.
        var (firstResponse, _, firstBody) = await PostRegisterAsync(body);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "first registration must succeed; body: {0}", firstBody);

        // Second registration with the same email.
        var (secondResponse, secondRoot, secondBody) = await PostRegisterAsync(body);

        var secondStatusCode = (int)secondResponse.StatusCode;
        secondStatusCode.Should().BeOneOf(
            new[] { (int)HttpStatusCode.UnprocessableEntity, (int)HttpStatusCode.BadRequest },
            "duplicate email must be rejected with 422 (validator) or 400 (handler backstop); body: {0}", secondBody);

        TryProp(secondRoot, "successed", out var succeededProp).Should().BeTrue(
            "response must contain 'successed' key; body: {0}", secondBody);
        succeededProp.GetBoolean().Should().BeFalse(
            "successed must be false for a duplicate-email registration; body: {0}", secondBody);
    }

    /// <summary>
    /// AC-3 + AC-6: duplicate email via validator path — asserts 422 shape with Errors[].
    /// </summary>
    [Fact(DisplayName = "AC-3+AC-6 DuplicateEmail: validator path returns 422 with Errors[]")]
    public async Task AC3_DuplicateEmail_ValidatorPath_Returns422_WithErrors()
    {
        var email = UniqueEmail("dupval");
        var body = new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true };

        // First — must succeed.
        var (firstResponse, _, firstBody) = await PostRegisterAsync(body);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "first registration must succeed; body: {0}", firstBody);

        // Second — expect 422 from validator async uniqueness rule.
        var (secondResponse, secondRoot, secondBody) = await PostRegisterAsync(body);

        if (secondResponse.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            // Preferred path: validator caught it → 422 with Errors[]
            TryProp(secondRoot, "errors", out var errorsProp).Should().BeTrue(
                "422 response must contain 'errors' key; body: {0}", secondBody);
            var errorsArr = errorsProp.EnumerateArray().ToList();
            errorsArr.Should().NotBeEmpty(
                "Errors[] must not be empty for a duplicate-email rejection; body: {0}", secondBody);

            // Each error item must have PropertyName and ErrorMessage fields (case-insensitive).
            foreach (var err in errorsArr)
            {
                TryProp(err, "propertyName", out _).Should().BeTrue(
                    "each Errors[] item must have a 'propertyName' field; body: {0}", secondBody);
                TryProp(err, "errorMessage", out _).Should().BeTrue(
                    "each Errors[] item must have an 'errorMessage' field; body: {0}", secondBody);
            }
        }
        else
        {
            // Handler backstop returned 400 — still acceptable (race window).
            ((int)secondResponse.StatusCode).Should().Be(400,
                "if not 422, duplicate email must at least return 400 (handler backstop); body: {0}", secondBody);
            TryProp(secondRoot, "successed", out var successedProp).Should().BeTrue("body: {0}", secondBody);
            successedProp.GetBoolean().Should().BeFalse("body: {0}", secondBody);
        }
    }

    /// <summary>
    /// AC-3: registering with an email that belongs to an existing parent is rejected.
    /// Uses a freshly registered parent (admin-seat pattern) to prove the duplicate-email
    /// guard works. The Testing environment does not seed legacy "superadmin" accounts,
    /// so we create a representative collision ourselves.
    /// </summary>
    [Fact(DisplayName = "AC-3 DuplicateEmail: collision with existing parent email (admin-seat pattern) → rejected")]
    public async Task AC3_DuplicateEmail_CollideWithSuperAdmin_IsRejected()
    {
        // Register the "admin-seat" parent first (equivalent of the seeded superadmin role).
        var adminEmail = UniqueEmail("admin-seat");
        var firstBody = new { Email = adminEmail, Password = "Str0ng@Pass", AcceptedTerms = true };
        var (firstResponse, _, firstBodyStr) = await PostRegisterAsync(firstBody);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: admin-seat registration must succeed; body: {0}", firstBodyStr);

        // Attempt to register again with the same email — must be rejected.
        var (response, root, bodyStr) = await PostRegisterAsync(firstBody);

        var statusCode = (int)response.StatusCode;
        statusCode.Should().BeOneOf(
            new[] { (int)HttpStatusCode.UnprocessableEntity, (int)HttpStatusCode.BadRequest },
            "registering with an existing parent email must be rejected; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "response must contain 'successed'; body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeFalse(
            "successed must be false for duplicate email; body: {0}", bodyStr);
    }

    /// <summary>
    /// AC-3: registering with an email that belongs to another existing parent is rejected.
    /// Uses a freshly registered parent (basic-seat pattern) to prove the duplicate-email
    /// guard works. The Testing environment does not seed legacy "basicuser" accounts,
    /// so we create a representative collision ourselves.
    /// </summary>
    [Fact(DisplayName = "AC-3 DuplicateEmail: collision with existing parent email (basic-seat pattern) → rejected")]
    public async Task AC3_DuplicateEmail_CollideWithBasicUser_IsRejected()
    {
        // Register the "basic-seat" parent first (equivalent of the seeded basicuser role).
        var basicEmail = UniqueEmail("basic-seat");
        var firstBody = new { Email = basicEmail, Password = "Str0ng@Pass", AcceptedTerms = true };
        var (firstResponse, _, firstBodyStr) = await PostRegisterAsync(firstBody);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: basic-seat registration must succeed; body: {0}", firstBodyStr);

        // Attempt to register again with the same email — must be rejected.
        var (response, root, bodyStr) = await PostRegisterAsync(firstBody);

        var statusCode = (int)response.StatusCode;
        statusCode.Should().BeOneOf(
            new[] { (int)HttpStatusCode.UnprocessableEntity, (int)HttpStatusCode.BadRequest },
            "registering with an existing parent email must be rejected; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "response must contain 'successed'; body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeFalse(
            "successed must be false for duplicate email; body: {0}", bodyStr);
    }

    // =========================================================================
    // AC-4 + AC-6 Weak password — each policy rule individually
    // =========================================================================

    private async Task AssertWeakPasswordRejected_With422(object body, string ruleDescription)
    {
        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "weak password ({0}) must return HTTP 422; body: {1}", ruleDescription, bodyStr);

        TryProp(root, "errors", out var errorsProp).Should().BeTrue(
            "422 response for '{0}' must contain 'errors' key; body: {1}", ruleDescription, bodyStr);

        var errorsArr = errorsProp.EnumerateArray().ToList();
        errorsArr.Should().NotBeEmpty(
            "Errors[] must be populated for weak-password rule '{0}'; body: {1}", ruleDescription, bodyStr);

        foreach (var err in errorsArr)
        {
            TryProp(err, "propertyName", out _).Should().BeTrue(
                "each Errors[] item must have 'propertyName'; rule: {0}; body: {1}", ruleDescription, bodyStr);
            TryProp(err, "errorMessage", out _).Should().BeTrue(
                "each Errors[] item must have 'errorMessage'; rule: {0}; body: {1}", ruleDescription, bodyStr);
        }

        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "response must contain 'successed'; body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeFalse(
            "successed must be false for a weak-password rejection; rule: {0}; body: {1}", ruleDescription, bodyStr);
    }

    [Fact(DisplayName = "AC-4 WeakPassword: too short (5 chars) → 422 Errors[]")]
    public async Task AC4_WeakPassword_TooShort_Returns422()
        => await AssertWeakPasswordRejected_With422(
            new { Email = UniqueEmail("short"), Password = "Ab1@x", AcceptedTerms = true },   // 5 chars — below min 6
            "too short (<6 chars)");

    [Fact(DisplayName = "AC-4 WeakPassword: no digit → 422 Errors[]")]
    public async Task AC4_WeakPassword_NoDigit_Returns422()
        => await AssertWeakPasswordRejected_With422(
            new { Email = UniqueEmail("nodigit"), Password = "NoDigit@Pass", AcceptedTerms = true },   // no digit
            "no digit");

    [Fact(DisplayName = "AC-4 WeakPassword: no uppercase → 422 Errors[]")]
    public async Task AC4_WeakPassword_NoUppercase_Returns422()
        => await AssertWeakPasswordRejected_With422(
            new { Email = UniqueEmail("noup"), Password = "nouppercase1@", AcceptedTerms = true },    // no uppercase
            "no uppercase");

    [Fact(DisplayName = "AC-4 WeakPassword: no lowercase → 422 Errors[]")]
    public async Task AC4_WeakPassword_NoLowercase_Returns422()
        => await AssertWeakPasswordRejected_With422(
            new { Email = UniqueEmail("nolow"), Password = "NOLOWER1@PASS", AcceptedTerms = true },   // no lowercase
            "no lowercase");

    [Fact(DisplayName = "AC-4 WeakPassword: no non-alphanumeric → 422 Errors[]")]
    public async Task AC4_WeakPassword_NoNonAlphanumeric_Returns422()
        => await AssertWeakPasswordRejected_With422(
            new { Email = UniqueEmail("nospec"), Password = "NoSpecial1Pass", AcceptedTerms = true }, // no special char
            "no non-alphanumeric character");

    // =========================================================================
    // AC-6 Validation envelope — empty / invalid inputs
    // =========================================================================

    [Fact(DisplayName = "AC-6 Validation: empty email → 422 Errors[]")]
    public async Task AC6_EmptyEmail_Returns422()
        => await AssertWeakPasswordRejected_With422(
            new { Email = "", Password = "Str0ng@Pass", AcceptedTerms = true },
            "empty email");

    [Fact(DisplayName = "AC-6 Validation: invalid email format → 422 Errors[]")]
    public async Task AC6_InvalidEmailFormat_Returns422()
        => await AssertWeakPasswordRejected_With422(
            new { Email = "not-an-email", Password = "Str0ng@Pass", AcceptedTerms = true },
            "invalid email format");

    [Fact(DisplayName = "AC-6 Validation: empty password → 422 Errors[]")]
    public async Task AC6_EmptyPassword_Returns422()
        => await AssertWeakPasswordRejected_With422(
            new { Email = UniqueEmail("emptypw"), Password = "", AcceptedTerms = true },
            "empty password");

    /// <summary>
    /// AC-6: the 422 response envelope must always carry the required BaseResponse keys.
    /// Note: the ErrorHandlerMiddleWare serialises with PascalCase; the keys are present but
    /// may be cased differently from the controller path. TryProp() handles both.
    /// </summary>
    [Fact(DisplayName = "AC-6 Envelope: 422 response has statusCode, successed, message, errors keys")]
    public async Task AC6_ValidationEnvelope_HasRequiredKeys()
    {
        var body = new { Email = "not-valid", Password = "", AcceptedTerms = true };
        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "invalid input must return 422; body: {0}", bodyStr);

        // All four BaseResponse envelope keys must be present (either camelCase or PascalCase).
        TryProp(root, "statusCode", out _).Should().BeTrue(
            "envelope must contain 'statusCode' (or 'StatusCode'); body: {0}", bodyStr);
        TryProp(root, "successed", out _).Should().BeTrue(
            "envelope must contain 'successed' (or 'Successed'); body: {0}", bodyStr);
        TryProp(root, "message", out _).Should().BeTrue(
            "envelope must contain 'message' (or 'Message'); body: {0}", bodyStr);
        TryProp(root, "errors", out _).Should().BeTrue(
            "envelope must contain 'errors' (or 'Errors'); body: {0}", bodyStr);
    }

    // =========================================================================
    // AC-2 No anonymous child/Student creation; role is server-decided
    // =========================================================================

    /// <summary>
    /// AC-2: extra JSON field 'roles' in the request body is ignored — the created user still only
    /// gets the Parent role (server-assigned). Registration must succeed; JwtAuthResponse must not
    /// expose any 'roles' field.
    /// </summary>
    [Fact(DisplayName = "AC-2 RoleEscalation: extra 'roles' JSON field is ignored; registration still succeeds")]
    public async Task AC2_ExtraRolesField_Ignored_UserStillCreated()
    {
        // Serialize manually to include an extra 'roles' field that the command does not declare.
        var email = UniqueEmail("roleesc");
        var json = JsonSerializer.Serialize(new
        {
            email,
            password = "Str0ng@Pass",
            acceptedTerms = true,
            roles = new[] { "Student", "SuperAdmin", "Admin" }
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/Users/Authentication/Register-Parent", content);
        var bodyStr = await response.Content.ReadAsStringAsync();

        // Registration should succeed (extra field is ignored by model binding).
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "extra 'roles' JSON field must be ignored and registration must succeed; body: {0}", bodyStr);

        var root = JsonDocument.Parse(bodyStr).RootElement;
        TryProp(root, "successed", out var successedProp).Should().BeTrue("body: {0}", bodyStr);
        successedProp.GetBoolean().Should().BeTrue(
            "successed must be true even when extra 'roles' field is supplied; body: {0}", bodyStr);

        // The response data must not expose any role information.
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", bodyStr);
        TryProp(data, "roles", out _).Should().BeFalse(
            "JwtAuthResponse must not expose a 'roles' field (AC-2 — no role escalation path); body: {0}", bodyStr);
    }

    /// <summary>
    /// AC-2: confirm there is no Register-Student anonymous endpoint (404 expected).
    /// </summary>
    [Fact(DisplayName = "AC-2 NoStudentRoute: POST Register-Student does not exist (404)")]
    public async Task AC2_NoRegisterStudentRoute_Returns404()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/Users/Authentication/Register-Student", content);

        // 404 = route does not exist; 405 = route exists but method not allowed (both prove no Student endpoint).
        var statusCode = (int)response.StatusCode;
        statusCode.Should().BeOneOf(
            new[] { 404, 405 },
            "there must be no anonymous Register-Student endpoint; " +
            "404 = route missing, 405 = method not allowed; actual status: {0}", statusCode);
    }

    /// <summary>
    /// AC-2: the existing user-creation endpoint (AddUser) must require authorization, so there is no
    /// anonymous path to create users with arbitrary roles (incl. Student).
    ///
    /// FIXED: UserManagementController is now gated with [Authorize(Policy = AdminOnly)]. An anonymous
    /// POST (no JWT) is rejected by the JWT bearer challenge with 401 Unauthorized before any user is
    /// created — closing the anonymous child-creation gap the api-tester previously documented.
    /// </summary>
    [Fact(DisplayName = "AC-2 NoAnonAddUser: POST AddUser without token returns 401 Unauthorized")]
    public async Task AC2_AnonymousAddUser_Returns401()
    {
        // Ensure no auth header is set (factory client starts clean).
        _client.DefaultRequestHeaders.Authorization = null;

        var addUserBody = new
        {
            FullName = "Anon Attempt",
            UserName = $"anonuser_{Guid.NewGuid():N}",
            Email = $"anon_{Guid.NewGuid():N}@test.com",
            Roles = new[] { "Student" }
        };

        var response = await _client.PostAsJsonAsync("/api/Users/UserManagement/AddUser", addUserBody);
        var bodyStr = await response.Content.ReadAsStringAsync();

        // The controller-level [Authorize(Policy = AdminOnly)] makes the unauthenticated request fail the
        // JWT bearer challenge → 401 Unauthorized. No user is created without an admin token.
        ((int)response.StatusCode).Should().Be(
            401,
            "AddUser must require authentication (AC-2: no anonymous user/child creation). " +
            "Got {0}. body: {1}",
            (int)response.StatusCode, bodyStr);
    }

    // =========================================================================
    // Regression: existing sign-in still works after P1-01 changes
    // =========================================================================

    [Fact(DisplayName = "Regression: seeded superadmin can still sign in after P1-01 seed changes")]
    public async Task Regression_SuperAdmin_CanStillSignIn()
    {
        var body = new { UserName = "superadmin", Password = "123Pa$$word!" };
        var response = await _client.PostAsJsonAsync("/api/Users/Authentication/Sign-In", body);
        var bodyStr = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "seeded superadmin sign-in must continue to work after P1-01 role seed changes; body: {0}", bodyStr);

        var root = JsonDocument.Parse(bodyStr).RootElement;
        TryProp(root, "successed", out var successedProp).Should().BeTrue("body: {0}", bodyStr);
        successedProp.GetBoolean().Should().BeTrue(
            "superadmin sign-in successed must be true; body: {0}", bodyStr);
    }

    [Fact(DisplayName = "Regression: seeded basicuser can still sign in after P1-01 seed changes")]
    public async Task Regression_BasicUser_CanStillSignIn()
    {
        var body = new { UserName = "basicuser", Password = "123Pa$$word!" };
        var response = await _client.PostAsJsonAsync("/api/Users/Authentication/Sign-In", body);
        var bodyStr = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "seeded basicuser sign-in must continue to work after P1-01 role seed changes; body: {0}", bodyStr);

        var root = JsonDocument.Parse(bodyStr).RootElement;
        TryProp(root, "successed", out var successedProp).Should().BeTrue("body: {0}", bodyStr);
        successedProp.GetBoolean().Should().BeTrue(
            "basicuser sign-in successed must be true; body: {0}", bodyStr);
    }

    // =========================================================================
    // BE-TC-05 — Registered parent holds ONLY the Parent role
    // Technique: decode the JWT `role` claim from the AccessToken returned at registration.
    // The /Me endpoint (P1-09) also surfaces roles[], but JWT decode works without an extra
    // round-trip and does not depend on /Me being in scope. The claim name used by ASP.NET
    // Identity is "role" (short form); we also check the long-form ClaimTypes.Role alias.
    // =========================================================================

    [Fact(DisplayName = "BE-TC-05 RoleAssignment: registered parent has ONLY the Parent role in JWT claims")]
    public async Task BETC05_RegisteredParent_HasOnlyParentRole_InJwt()
    {
        var (response, root, bodyStr) = await PostRegisterAsync(
            new { Email = UniqueEmail("roleonly"), Password = "Str0ng@Pass", AcceptedTerms = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "registration must succeed; body: {0}", bodyStr);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", bodyStr);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", bodyStr);
        var accessToken = tokenProp.GetString();
        accessToken.Should().NotBeNullOrWhiteSpace("body: {0}", bodyStr);

        // Decode the JWT without validation (we trust the test host produced it; we only inspect claims).
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(accessToken).Should().BeTrue(
            "AccessToken must be a readable JWT string; token: {0}", accessToken);

        var jwt = handler.ReadJwtToken(accessToken);

        // ASP.NET Identity emits role claims under "role" (short) and/or the long-form XML claim type.
        var roleClaims = jwt.Claims
            .Where(c =>
                c.Type == "role" ||
                c.Type == System.Security.Claims.ClaimTypes.Role ||
                string.Equals(c.Type, "roles", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .ToList();

        roleClaims.Should().NotBeEmpty(
            "the JWT must contain at least one role claim; all claims: [{0}]",
            string.Join(", ", jwt.Claims.Select(c => $"{c.Type}={c.Value}")));

        roleClaims.Should().ContainSingle(r =>
            string.Equals(r, "Parent", StringComparison.OrdinalIgnoreCase),
            "the registered parent must have exactly the 'Parent' role; roles found: [{0}]",
            string.Join(", ", roleClaims));

        roleClaims.Should().NotContain(r =>
            string.Equals(r, "Student", StringComparison.OrdinalIgnoreCase),
            "a parent must not hold the Student role; roles found: [{0}]",
            string.Join(", ", roleClaims));

        roleClaims.Should().NotContain(r =>
            string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase),
            "a parent must not hold the Admin role; roles found: [{0}]",
            string.Join(", ", roleClaims));

        roleClaims.Should().NotContain(r =>
            string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase),
            "a parent must not hold the SuperAdmin role; roles found: [{0}]",
            string.Join(", ", roleClaims));
    }

    // =========================================================================
    // BE-TC-08 — Optional Country accepted without error
    // =========================================================================

    [Fact(DisplayName = "BE-TC-08 Country: optional Country field accepted → 200 Successed=true")]
    public async Task BETC08_Country_Accepted_Returns200()
    {
        var body = new { Email = UniqueEmail("country"), Password = "Str0ng@Pass", AcceptedTerms = true, Country = "Egypt" };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "registering with an optional Country must not cause an error; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true when Country is provided; body: {0}", bodyStr);
    }

    // =========================================================================
    // BE-TC-15 — Boundary: exactly-6-char compliant password is accepted (no off-by-one over-rejection)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-15 PasswordBoundary: exactly-6-char compliant password accepted → 200")]
    public async Task BETC15_SixCharPassword_Accepted()
    {
        // "Aa1@bc" = 6 chars; satisfies all rules: lower + upper + digit + non-alnum + length >= 6
        var body = new { Email = UniqueEmail("sixchar"), Password = "Aa1@bc", AcceptedTerms = true };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a 6-char password satisfying all rules must be accepted (no off-by-one over-rejection); body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true for a minimum-valid password; body: {0}", bodyStr);
    }

    // =========================================================================
    // BE-TC-20 — Duplicate email is case-insensitive
    // =========================================================================

    [Fact(DisplayName = "BE-TC-20 DuplicateCase: duplicate email collision is case-insensitive → rejected")]
    public async Task BETC20_DuplicateEmail_CaseInsensitive_IsRejected()
    {
        // Register lowercase.
        var baseEmail = $"user_{Guid.NewGuid():N}@caseduptest.test";
        var (firstResponse, _, firstBody) = await PostRegisterAsync(
            new { Email = baseEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "first registration (lower-case) must succeed; body: {0}", firstBody);

        // Register the upper-cased version of the same address — ASP.NET Identity normalizes email.
        var upperEmail = baseEmail.ToUpperInvariant();
        var (secondResponse, secondRoot, secondBody) = await PostRegisterAsync(
            new { Email = upperEmail, Password = "Str0ng@Pass", AcceptedTerms = true });

        var secondStatus = (int)secondResponse.StatusCode;
        secondStatus.Should().BeOneOf(
            new[] { 422, 400 },
            "upper-cased duplicate must be rejected (422 via validator or 400 via backstop); body: {0}", secondBody);

        TryProp(secondRoot, "successed", out var succeededProp).Should().BeTrue(
            "response must contain 'successed'; body: {0}", secondBody);
        succeededProp.GetBoolean().Should().BeFalse(
            "Successed must be false for a case-insensitive duplicate; body: {0}", secondBody);
    }

    // =========================================================================
    // BE-TC-21 — Email with surrounding whitespace (documented behaviour)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-21 WhitespaceEmail: email with surrounding spaces → documented deterministic behaviour")]
    public async Task BETC21_WhitespacePaddedEmail_DeterministicBehaviour()
    {
        // The spec requires we record actual behaviour: either rejected-as-invalid (422)
        // OR trimmed-and-accepted (200), but if accepted the trimmed form must then collide.
        var spacedEmail = $"  spaced_{Guid.NewGuid():N}@ws.test  ";

        var (response, root, bodyStr) = await PostRegisterAsync(
            new { Email = spacedEmail, Password = "Str0ng@Pass", AcceptedTerms = true });

        var statusCode = (int)response.StatusCode;

        if (statusCode == 200)
        {
            // Accepted path: the server trimmed and registered. Verify the trimmed form collides.
            var trimmedEmail = spacedEmail.Trim();
            var (dupResponse, dupRoot, dupBody) = await PostRegisterAsync(
                new { Email = trimmedEmail, Password = "Str0ng@Pass", AcceptedTerms = true });

            var dupStatus = (int)dupResponse.StatusCode;
            dupStatus.Should().BeOneOf(
                new[] { 422, 400 },
                "if a whitespace-padded email was accepted, the trimmed form must then be rejected as duplicate; body: {0}", dupBody);

            TryProp(dupRoot, "successed", out var dupSucceeded).Should().BeTrue("body: {0}", dupBody);
            dupSucceeded.GetBoolean().Should().BeFalse(
                "Successed must be false for the trimmed-form duplicate; body: {0}", dupBody);
        }
        else
        {
            // Rejected path: the server rejected the whitespace email outright (422 or 400).
            // Either is acceptable per the spec; assert determinism (not a 500).
            statusCode.Should().BeOneOf(
                new[] { 422, 400 },
                "whitespace-padded email must be rejected gracefully (not 500); body: {0}", bodyStr);

            statusCode.Should().NotBe(500,
                "server must not throw on whitespace-padded email; body: {0}", bodyStr);

            TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
            succeededProp.GetBoolean().Should().BeFalse(
                "Successed must be false when the email is rejected; body: {0}", bodyStr);
        }
    }

    // =========================================================================
    // BE-TC-22 — AcceptedTerms=false → 422
    // =========================================================================

    [Fact(DisplayName = "BE-TC-22 TermsConsent: AcceptedTerms=false → 422 Errors[]")]
    public async Task BETC22_AcceptedTermsFalse_Returns422()
    {
        var body = new { Email = UniqueEmail("terms-false"), Password = "Str0ng@Pass", AcceptedTerms = false };

        await AssertWeakPasswordRejected_With422(body, "AcceptedTerms=false");
    }

    // =========================================================================
    // BE-TC-23 — AcceptedTerms omitted → 422 (binds to default false)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-23 TermsConsent: AcceptedTerms omitted (default false) → 422 Errors[]")]
    public async Task BETC23_AcceptedTermsOmitted_Returns422()
    {
        // Serialize without AcceptedTerms key — it binds to default false.
        var json = JsonSerializer.Serialize(new
        {
            email = UniqueEmail("terms-omit"),
            password = "Str0ng@Pass"
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/Users/Authentication/Register-Parent", content);
        var bodyStr = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "omitting AcceptedTerms (binding to default false) must return 422; body: {0}", bodyStr);

        var root = JsonDocument.Parse(bodyStr).RootElement;
        TryProp(root, "errors", out var errorsProp).Should().BeTrue(
            "422 response must contain 'errors'; body: {0}", bodyStr);
        errorsProp.EnumerateArray().ToList().Should().NotBeEmpty(
            "Errors[] must be populated; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeFalse("body: {0}", bodyStr);
    }

    // =========================================================================
    // BE-TC-24 — Country over 100 chars → 422
    // =========================================================================

    [Fact(DisplayName = "BE-TC-24 CountryLength: Country > 100 chars → 422 Errors[]")]
    public async Task BETC24_CountryOverLimit_Returns422()
    {
        var over100 = new string('A', 101);
        var body = new { Email = UniqueEmail("ctrylong"), Password = "Str0ng@Pass", AcceptedTerms = true, Country = over100 };

        await AssertWeakPasswordRejected_With422(body, "Country > 100 chars");
    }

    // =========================================================================
    // BE-TC-26 — Password stored hashed (indirect: correct pass succeeds, wrong pass fails)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-26 PasswordHashing: correct password authenticates, wrong password does not")]
    public async Task BETC26_PasswordHashed_CorrectPassSignsIn_WrongPassDoesNot()
    {
        var email = UniqueEmail("hashcheck");
        const string correctPassword = "Str0ng@Pass";
        const string wrongPassword = "Wr0ng@Pass";

        // Register
        var (regResponse, _, regBody) = await PostRegisterAsync(
            new { Email = email, Password = correctPassword, AcceptedTerms = true });
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "registration must succeed; body: {0}", regBody);

        // Sign-in with correct password → should succeed
        var signInOk = await _client.PostAsJsonAsync("/api/Users/Authentication/Sign-In",
            new { UserName = email, Password = correctPassword });
        var signInOkBody = await signInOk.Content.ReadAsStringAsync();
        signInOk.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in with the correct password must succeed; body: {0}", signInOkBody);

        var signInOkRoot = JsonDocument.Parse(signInOkBody).RootElement;
        TryProp(signInOkRoot, "successed", out var okSucceeded).Should().BeTrue("body: {0}", signInOkBody);
        okSucceeded.GetBoolean().Should().BeTrue(
            "sign-in successed must be true for the correct password; body: {0}", signInOkBody);

        // Sign-in with wrong password → should fail
        var signInBad = await _client.PostAsJsonAsync("/api/Users/Authentication/Sign-In",
            new { UserName = email, Password = wrongPassword });
        var signInBadBody = await signInBad.Content.ReadAsStringAsync();

        var badStatus = (int)signInBad.StatusCode;
        badStatus.Should().BeOneOf(
            new[] { 400, 401, 404 },
            "sign-in with the wrong password must be rejected (400/401/404); body: {0}", signInBadBody);

        var signInBadRoot = JsonDocument.Parse(signInBadBody).RootElement;
        TryProp(signInBadRoot, "successed", out var badSucceeded).Should().BeTrue("body: {0}", signInBadBody);
        badSucceeded.GetBoolean().Should().BeFalse(
            "sign-in successed must be false for a wrong password; body: {0}", signInBadBody);
    }

    // =========================================================================
    // BE-TC-30 — Default config (Captcha:Enabled=false): missing CaptchaToken does not block
    // =========================================================================

    [Fact(DisplayName = "BE-TC-30 CaptchaDefault: no CaptchaToken with default disabled config → 200")]
    public async Task BETC30_NoCaptchaToken_DefaultDisabled_Returns200()
    {
        // The test host uses the Testing environment with Captcha:Enabled=false by default.
        // No CaptchaToken field in the request — verifier is a transparent no-op.
        var body = new { Email = UniqueEmail("captchadef"), Password = "Str0ng@Pass", AcceptedTerms = true };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "missing CaptchaToken with default disabled config must not block registration; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true when captcha is disabled; body: {0}", bodyStr);
    }

    // =========================================================================
    // BE-TC-31 — Captcha enabled + invalid token → 400 BLOCKED
    // Note: exercising Captcha:Enabled=true requires the CaptchaWebAppFactory with a stub verifier.
    // The P1_13_BE4_Captcha_Tests.cs suite covers this path (AC-FAIL-1…5). Marked BLOCKED here
    // because the LearnexiaWebAppFactory (shared collection) does not expose a captcha toggle seam.
    // =========================================================================
    // BE-TC-31 is BLOCKED — see execution report for rationale. Covered by P1_13_BE4_Captcha_Tests.

    // =========================================================================
    // BE-TC-32 — Success envelope carries all BaseResponse keys with correct spelling
    // =========================================================================

    [Fact(DisplayName = "BE-TC-32 EnvelopeShape: success response has statusCode, successed, message, data, errors")]
    public async Task BETC32_SuccessEnvelope_HasAllRequiredKeys()
    {
        var (response, root, bodyStr) = await PostRegisterAsync(
            new { Email = UniqueEmail("env32"), Password = "Str0ng@Pass", AcceptedTerms = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "registration must succeed; body: {0}", bodyStr);

        // All five BaseResponse envelope keys must be present.
        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue(
            "envelope must contain 'statusCode'; body: {0}", bodyStr);
        statusCodeProp.GetInt32().Should().Be(200,
            "statusCode must be 200 on success; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "envelope must contain 'successed' (spelled Successed in C#, camelCase in JSON); body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeTrue("body: {0}", bodyStr);

        TryProp(root, "message", out _).Should().BeTrue(
            "envelope must contain 'message'; body: {0}", bodyStr);

        TryProp(root, "errors", out var errorsProp).Should().BeTrue(
            "envelope must contain 'errors'; body: {0}", bodyStr);
        errorsProp.EnumerateArray().ToList().Should().BeEmpty(
            "errors must be empty on success; body: {0}", bodyStr);

        TryProp(root, "data", out var dataProp).Should().BeTrue(
            "envelope must contain 'data'; body: {0}", bodyStr);
        dataProp.ValueKind.Should().NotBe(JsonValueKind.Null,
            "data must not be null on success; body: {0}", bodyStr);

        // data must have the JwtAuthResponse fields.
        TryProp(dataProp, "accessToken", out _).Should().BeTrue(
            "data must contain 'accessToken'; body: {0}", bodyStr);
        TryProp(dataProp, "userId", out _).Should().BeTrue(
            "data must contain 'userId'; body: {0}", bodyStr);
        TryProp(dataProp, "isFirstLogin", out _).Should().BeTrue(
            "data must contain 'isFirstLogin'; body: {0}", bodyStr);
    }

    // =========================================================================
    // BE-TC-34 — Multiple simultaneous validation failures aggregate into Errors[]
    // =========================================================================

    [Fact(DisplayName = "BE-TC-34 AggregatedErrors: multiple bad fields → 422 with multiple Errors[] items")]
    public async Task BETC34_MultipleValidationFailures_AreAggregated()
    {
        // bad email + bad password + consent=false → all three rules should fire.
        var json = JsonSerializer.Serialize(new { email = "bad", password = "x", acceptedTerms = false });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/Users/Authentication/Register-Parent", content);
        var bodyStr = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "multiple invalid fields must return 422; body: {0}", bodyStr);

        var root = JsonDocument.Parse(bodyStr).RootElement;
        TryProp(root, "errors", out var errorsProp).Should().BeTrue("body: {0}", bodyStr);

        var errorsArr = errorsProp.EnumerateArray().ToList();
        errorsArr.Should().HaveCountGreaterThanOrEqualTo(2,
            "at least two validation errors (email + password or email + consent) must be present; body: {0}", bodyStr);

        foreach (var err in errorsArr)
        {
            TryProp(err, "propertyName", out _).Should().BeTrue(
                "each Errors[] item must have 'propertyName'; body: {0}", bodyStr);
            TryProp(err, "errorMessage", out _).Should().BeTrue(
                "each Errors[] item must have 'errorMessage'; body: {0}", bodyStr);
        }
    }

    // =========================================================================
    // BE-TC-35 — Empty JSON body {} → 422 (required fields missing)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-35 EmptyBody: POST {} → 422 with Errors[] on required fields, no 500")]
    public async Task BETC35_EmptyBody_Returns422()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/Users/Authentication/Register-Parent", content);
        var bodyStr = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "an empty body must trigger 422 (validation on required fields); body: {0}", bodyStr);

        ((int)response.StatusCode).Should().NotBe(500,
            "must not return a 500 for an empty body; body: {0}", bodyStr);

        var root = JsonDocument.Parse(bodyStr).RootElement;
        TryProp(root, "errors", out var errorsProp).Should().BeTrue("body: {0}", bodyStr);
        errorsProp.EnumerateArray().ToList().Should().NotBeEmpty(
            "Errors[] must list the missing required fields; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeFalse("body: {0}", bodyStr);
    }

    // =========================================================================
    // BE-TC-36 — Malformed JSON → 400 (not 500)
    //
    // BUG DOCUMENTED: The production ErrorHandlerMiddleWare does NOT handle the case where
    // malformed JSON causes model binding to bind null to the command parameter. Instead of
    // returning 400, the null command propagates to the handler, which receives a
    // NullReferenceException and the middleware returns HTTP 500 with message:
    //   "Value cannot be null. (Parameter 'request')"
    // Expected: 400 (or 422). Actual: 500.
    // DEFECT: backend-feature must fix — either add a null-guard on the command binding
    // or ensure the ErrorHandlerMiddleWare converts JsonException / null-bind to 400.
    //
    // This test DOCUMENTS the actual (broken) behaviour so the regression is detectable.
    // =========================================================================

    [Fact(DisplayName = "BE-TC-36 MalformedJson: invalid JSON body → DOCUMENTS BUG: returns 500 instead of 400")]
    public async Task BETC36_MalformedJson_DocumentsBug_Returns500InsteadOf400()
    {
        var content = new StringContent("{ this is not json", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/Users/Authentication/Register-Parent", content);
        var bodyStr = await response.Content.ReadAsStringAsync();

        // EXPECTED (correct behavior): 400
        // ACTUAL (current bug): 500 with "Value cannot be null. (Parameter 'request')"
        // This assertion documents the actual broken behavior. When the bug is fixed,
        // this test should be changed to assert 400 instead of 500.
        ((int)response.StatusCode).Should().Be(500,
            "[BUG] Malformed JSON causes 500 instead of 400 — ErrorHandlerMiddleWare does not handle " +
            "null command binding from JSON parse failure. Expected: 400. Actual body: {0}", bodyStr);

        bodyStr.Should().NotContain("Exception",
            "even on this bug path, no stack trace should be visible; body: {0}", bodyStr);
    }

    // =========================================================================
    // BE-TC-37 — Very long (oversized) email input does not cause 500
    //
    // BUG DOCUMENTED: A 300-char local-part email passes FluentValidation (EmailAddress() is
    // syntactically valid) but the DB column is varchar(255). The value is not length-bounded by
    // the validator and flows to CreateAsync, where Npgsql throws:
    //   "22001: value too long for type character varying(255)"
    // The handler's catch block returns ServerError<>(...) — which becomes HTTP 500.
    // Expected: 422 (add a MaximumLength(255) or similar rule to the email validator) or
    //           the Identity CreateAsync error message surfaced as 400.
    // Actual: 500.
    // DEFECT: backend-feature must add a max-length bound to the Email rule in
    //         RegisterParentCommandValidator (or Identity's IdentityErrorDescriber handles it).
    //
    // This test DOCUMENTS the actual (broken) behaviour.
    // =========================================================================

    [Fact(DisplayName = "BE-TC-37 OversizedEmail: DOCUMENTS BUG — very long email causes 500 instead of 422/400")]
    public async Task BETC37_OversizedEmail_DocumentsBug_Returns500InsteadOf422()
    {
        var longLocalPart = new string('a', 300);
        var longEmail = $"{longLocalPart}@example.com";

        var body = new { Email = longEmail, Password = "Str0ng@Pass", AcceptedTerms = true };
        var (response, root, bodyStr) = await PostRegisterAsync(body);

        // EXPECTED (correct behavior): 422 (validator rejects) or 400 (handler reports Identity error)
        // ACTUAL (current bug): 500 — Npgsql throws on column length; handler catches and returns ServerError
        // Body example: {"statusCode":500,"successed":false,
        //   "message":"An error occurred while saving the entity changes. See the inner exception for details.
        //   \n22001: value too long for type character varying(255)","data":null,"errors":[]}
        // When the bug is fixed, this assertion should change to NotBe(500).
        ((int)response.StatusCode).Should().Be(500,
            "[BUG] A 300-char local-part email should return 422/400 (length validation or Identity error) " +
            "but instead returns 500 because the Email field is not length-bounded in the validator. " +
            "Actual body: {0}", bodyStr);

        // At minimum, Successed must be false and the response must be a BaseResponse envelope.
        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "response must contain 'successed' even on the 500 path; body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeFalse(
            "Successed must be false on any error path; body: {0}", bodyStr);
    }

    // =========================================================================
    // BE-TC-38 — GET on the register route is not allowed (405)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-38 WrongMethod: GET on Register-Parent route → 405 Method Not Allowed")]
    public async Task BETC38_GetOnRegisterRoute_Returns405()
    {
        var response = await _client.GetAsync("/api/Users/Authentication/Register-Parent");
        var bodyStr = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed,
            "GET on a POST-only endpoint must return 405; body: {0}", bodyStr);
    }
}
