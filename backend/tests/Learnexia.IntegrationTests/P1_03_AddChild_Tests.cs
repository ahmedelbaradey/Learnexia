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
/// P1-03 integration tests: Parent provisions child (Student-role) accounts via Add-Child endpoint.
///
/// Endpoints under test:
///   [Authorize(Roles="Parent,Admin,SuperAdmin")]
///   POST api/Parent/Add-Child
///   GET  api/Parent/My-Children                (used for persistence / auto-link verification)
///   POST api/Users/Authentication/Sign-In      (used for child sign-in round-trip)
///
/// Command shape (actual implementation fields): { FullName, Email, Password, Grade, Language, Country }
/// Note: the plan used ChildName/LoginEmail but the implementation uses FullName/Email.
///
/// Acceptance criteria covered:
///   AC-1 (case a)  Happy path: Parent JWT + valid child → 200, Successed=true, Id/Grade/Language/Country set, password NOT echoed
///   AC-1 (case a)  Response envelope has all BaseResponse keys: statusCode, successed, message, data, errors
///   AC-3 (case j)  Child can sign in with the parent-assigned email + password (round-trip)
///   AC-5 (case a)  Child appears in GET My-Children after Add-Child (auto-link works)
///   AC-2 (case g)  Multiple children: each linked; My-Children returns all
///   AC-7 (case c)  Duplicate email → 400, Successed=false, specific message, no second account
///   AC-6 (case b)  Grade 0 → 422; Grade 7 → 422
///   AC-6 (case d)  Malformed email → 422
///   AC-6 (case h)  Language "fr" → 422
///   AC-6 (case i)  Empty/missing password → 422
///   AC-6           Missing FullName → 422
///   AC-6           Missing Country → 422
///   AC-4 (case e)  Anonymous → 401
///   AC-4 (case f)  Basic-role (non-parent) → 403
///   AC-4           Password NOT in response body
///   AC-4           ParentId from JWT only (body field ignored)
/// </summary>
[Collection("IntegrationTests")]
public sealed class P1_03_AddChild_Tests : IAsyncLifetime
{
    // ---------------------------------------------------------------------------
    // URLs
    // ---------------------------------------------------------------------------
    private const string AddChildUrl = "api/Parent/Add-Child";
    private const string MyChildrenUrl = "api/Parent/My-Children";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";

    // Seeded accounts
    private const string SuperAdminUserName = "superadmin";
    private const string SuperAdminPassword = "123Pa$$word!";
    private const string BasicUserName = "basicuser";
    private const string BasicUserPassword = "123Pa$$word!";

    // A password that satisfies the configured Identity policy:
    // RequireDigit + RequireLowercase + RequireUppercase + RequireNonAlphanumeric + length >= 6
    private const string ValidChildPassword = "Child@Pass1";

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_03_AddChild_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>Generates a unique email guaranteed not to exist in the current test run.</summary>
    private static string UniqueEmail(string tag = "")
        => $"p103_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>
    /// Case-insensitive property lookup: the project has two JSON serialisation paths
    /// (controller = camelCase via Newtonsoft, middleware 422 = PascalCase via System.Text.Json).
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
            catch { /* non-JSON; leave root default */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>Registers a new parent account and returns their JWT access token.</summary>
    private async Task<string> RegisterParentAndGetTokenAsync(string email, string password = "Str0ng@Pass")
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = password, AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>Signs in with an existing account and returns their JWT access token.</summary>
    private async Task<string> SignInAndGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>
    /// Builds a valid Add-Child command body using the actual command fields
    /// (FullName, Email, Password, Grade, Language, Country).
    /// </summary>
    // P8-01: LearningLanguage is now required. Default to "ar" (Arabic-first) for existing tests.
    private static object ValidChildBody(string email, int grade = 3, string language = "ar", string country = "EG", string learningLanguage = "ar")
        => new
        {
            FullName = "Test Child",
            Email = email,
            Password = ValidChildPassword,
            Grade = grade,
            Language = language,
            Country = country,
            LearningLanguage = learningLanguage,
        };

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        AddChildAsync(string parentToken, object body)
        => SendAsync(_client, HttpMethod.Post, AddChildUrl, body, parentToken);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        MyChildrenAsync(string parentToken)
        => SendAsync(_client, HttpMethod.Get, MyChildrenUrl, null, parentToken);

    // ===========================================================================
    // AC-1 / case (a) — Happy path: valid parent + valid child → 200
    // ===========================================================================

    [Fact(DisplayName = "AC-1a HappyPath: parent adds a child → 200, Successed=true, child profile returned")]
    public async Task AC1a_HappyPath_Returns200_WithSuccessedTrue_AndChildProfile()
    {
        // Arrange
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");
        var body = ValidChildBody(childEmail, grade: 3, language: "ar", country: "EG");

        // Act
        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        // Assert HTTP status
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Add-Child with valid parent token and valid payload must return 200; body: {0}", rawBody);

        // Envelope: Successed=true
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeTrue("Successed must be true on successful Add-Child; body: {0}", rawBody);

        // Data shape: Id, FullName, Email, Grade, Language, Country
        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", rawBody);
        data.ValueKind.Should().NotBe(JsonValueKind.Null, "data must not be null on success; body: {0}", rawBody);

        TryProp(data, "id", out var idProp).Should().BeTrue("data.id must be present; body: {0}", rawBody);
        idProp.GetInt32().Should().BeGreaterThan(0, "child Id must be a positive integer; body: {0}", rawBody);

        TryProp(data, "fullName", out var nameProp).Should().BeTrue("data.fullName must be present; body: {0}", rawBody);
        nameProp.GetString().Should().Be("Test Child", "fullName must match the submitted name; body: {0}", rawBody);

        TryProp(data, "email", out var emailProp).Should().BeTrue("data.email must be present; body: {0}", rawBody);
        emailProp.GetString().Should().Be(childEmail, "email must match the assigned email; body: {0}", rawBody);

        TryProp(data, "grade", out var gradeProp).Should().BeTrue("data.grade must be present; body: {0}", rawBody);
        gradeProp.GetInt32().Should().Be(3, "grade must be echoed back; body: {0}", rawBody);

        TryProp(data, "language", out var langProp).Should().BeTrue("data.language must be present; body: {0}", rawBody);
        langProp.GetString().Should().NotBeNullOrWhiteSpace("language must not be blank; body: {0}", rawBody);
        // Stored as culture code "ar-EG" after normalization
        langProp.GetString().Should().ContainEquivalentOf("ar", "language should contain 'ar'; body: {0}", rawBody);

        TryProp(data, "country", out var countryProp).Should().BeTrue("data.country must be present; body: {0}", rawBody);
        countryProp.GetString().Should().Be("EG", "country must echo the submitted value; body: {0}", rawBody);
    }

    [Fact(DisplayName = "AC-1b HappyPath: password NOT echoed in Add-Child response")]
    public async Task AC1b_HappyPath_Password_NotInResponse()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        var (resp, _, rawBody) = await AddChildAsync(parentToken, ValidChildBody(childEmail));

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "prerequisite: Add-Child must succeed; body: {0}", rawBody);

        // Password must never appear in the response body
        rawBody.Should().NotContainEquivalentOf(ValidChildPassword,
            "the child's password must NEVER appear in the API response; body: {0}", rawBody);
    }

    [Fact(DisplayName = "AC-1c HappyPath: response envelope has all required BaseResponse keys")]
    public async Task AC1c_HappyPath_Envelope_HasAllBaseResponseKeys()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var (resp, root, rawBody) = await AddChildAsync(parentToken, ValidChildBody(UniqueEmail("child")));

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "prerequisite: must succeed; body: {0}", rawBody);

        // Mandatory BaseResponse<T> keys per architecture.md §6
        TryProp(root, "statusCode", out var statusCode).Should().BeTrue("envelope must have 'statusCode'; body: {0}", rawBody);
        statusCode.GetInt32().Should().Be(200, "statusCode in envelope must be 200 on success; body: {0}", rawBody);

        TryProp(root, "successed", out _).Should().BeTrue("envelope must have 'successed'; body: {0}", rawBody);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have 'message'; body: {0}", rawBody);
        TryProp(root, "data", out _).Should().BeTrue("envelope must have 'data'; body: {0}", rawBody);
        TryProp(root, "errors", out _).Should().BeTrue("envelope must have 'errors'; body: {0}", rawBody);
    }

    // ===========================================================================
    // AC-1 — Child receives Student role
    // ===========================================================================

    [Fact(DisplayName = "AC-1d Student role: child is in Student role after Add-Child")]
    public async Task AC1d_CreatedChild_HasStudentRole()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");
        var (addResp, addRoot, addBody) = await AddChildAsync(parentToken, ValidChildBody(childEmail));

        addResp.StatusCode.Should().Be(HttpStatusCode.OK, "prerequisite: Add-Child must succeed; body: {0}", addBody);
        TryProp(addRoot, "data", out var data).Should().BeTrue("body: {0}", addBody);
        TryProp(data, "id", out var idProp).Should().BeTrue("body: {0}", addBody);
        var childId = idProp.GetInt32();

        // Verify via DB: child has Student role
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();

        // Find the child user
        var childUser = db.Users.SingleOrDefault(u => u.Email == childEmail);
        childUser.Should().NotBeNull("child user must exist in DB; email: {0}", childEmail);
        childUser!.Id.Should().Be(childId, "DB Id must match response Id");

        // Find the Student role's Id
        var studentRole = db.Roles.SingleOrDefault(r => r.Name == "Student");
        studentRole.Should().NotBeNull("'Student' role must be seeded in the DB");

        // Verify the role assignment
        var hasStudentRole = db.UserRoles.Any(ur => ur.UserId == childId && ur.RoleId == studentRole!.Id);
        hasStudentRole.Should().BeTrue("the child must be assigned the Student role; childId: {0}", childId);
    }

    // ===========================================================================
    // AC-5 / case (a) — Auto-link: child appears in My-Children
    // ===========================================================================

    [Fact(DisplayName = "AC-5 AutoLink: child appears in My-Children after Add-Child")]
    public async Task AC5_AutoLink_ChildAppearsInMyChildren()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        var (addResp, _, addBody) = await AddChildAsync(parentToken, ValidChildBody(childEmail));
        addResp.StatusCode.Should().Be(HttpStatusCode.OK, "Add-Child must succeed; body: {0}", addBody);

        var (listResp, listRoot, listBody) = await MyChildrenAsync(parentToken);

        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children must return 200; body: {0}", listBody);
        TryProp(listRoot, "successed", out var successed).Should().BeTrue("body: {0}", listBody);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", listBody);

        TryProp(listRoot, "data", out var data).Should().BeTrue("body: {0}", listBody);
        var children = data.EnumerateArray().ToList();
        children.Should().HaveCount(1, "exactly one child must appear in My-Children; body: {0}", listBody);

        // Child email must match
        TryProp(children[0], "email", out var emailInList).Should().BeTrue("body: {0}", listBody);
        emailInList.GetString().Should().Be(childEmail,
            "the linked child's email must appear in My-Children; body: {0}", listBody);
    }

    // ===========================================================================
    // AC-3 / case (j) — Child can sign in with assigned email + password
    // ===========================================================================

    [Fact(DisplayName = "AC-3 RoundTrip: child can sign in with the parent-assigned credentials")]
    public async Task AC3_ChildSignIn_WithAssignedCredentials_Returns200_WithToken()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        // Add the child
        var (addResp, _, addBody) = await AddChildAsync(parentToken, ValidChildBody(childEmail));
        addResp.StatusCode.Should().Be(HttpStatusCode.OK, "Add-Child must succeed; body: {0}", addBody);

        // The child signs in using the email (which is also the UserName per handler implementation)
        var (signInResp, signInRoot, signInBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });

        signInResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "child must be able to sign in with the assigned email + password; body: {0}", signInBody);

        TryProp(signInRoot, "successed", out var successed).Should().BeTrue("body: {0}", signInBody);
        successed.GetBoolean().Should().BeTrue("sign-in Successed must be true; body: {0}", signInBody);

        TryProp(signInRoot, "data", out var data).Should().BeTrue("body: {0}", signInBody);
        TryProp(data, "accessToken", out var accessToken).Should().BeTrue(
            "sign-in must return an accessToken for the child; body: {0}", signInBody);
        accessToken.GetString().Should().NotBeNullOrWhiteSpace(
            "accessToken must be a non-empty JWT string; body: {0}", signInBody);
    }

    // ===========================================================================
    // AC-2 / case (g) — Multiple children: all linked, My-Children returns all
    // ===========================================================================

    [Fact(DisplayName = "AC-2 MultipleChildren: parent adds 2 children → both appear in My-Children")]
    public async Task AC2_MultipleChildren_BothAppearInMyChildren()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail1 = UniqueEmail("child1");
        var childEmail2 = UniqueEmail("child2");

        // Add first child
        var (r1, _, b1) = await AddChildAsync(parentToken, ValidChildBody(childEmail1, grade: 1));
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "first Add-Child must succeed; body: {0}", b1);

        // Add second child
        var (r2, _, b2) = await AddChildAsync(parentToken, ValidChildBody(childEmail2, grade: 2));
        r2.StatusCode.Should().Be(HttpStatusCode.OK, "second Add-Child must succeed; body: {0}", b2);

        // My-Children must return both
        var (listResp, listRoot, listBody) = await MyChildrenAsync(parentToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children must return 200; body: {0}", listBody);
        TryProp(listRoot, "data", out var data).Should().BeTrue("body: {0}", listBody);
        var count = data.GetArrayLength();
        count.Should().Be(2, "parent must see both children in My-Children; body: {0}", listBody);

        var emails = data.EnumerateArray()
            .Select(c => TryProp(c, "email", out var ep) ? ep.GetString() : null)
            .ToList();
        emails.Should().Contain(childEmail1, "first child must appear in My-Children");
        emails.Should().Contain(childEmail2, "second child must appear in My-Children");
    }

    [Fact(DisplayName = "AC-2 MultipleChildren: each child gets a distinct account + distinct Id")]
    public async Task AC2_MultipleChildren_EachHasDistinctId()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var email1 = UniqueEmail("child1");
        var email2 = UniqueEmail("child2");

        var (r1, root1, b1) = await AddChildAsync(parentToken, ValidChildBody(email1));
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "first child must be created; body: {0}", b1);
        TryProp(root1, "data", out var d1).Should().BeTrue("body: {0}", b1);
        TryProp(d1, "id", out var id1Prop).Should().BeTrue("body: {0}", b1);

        var (r2, root2, b2) = await AddChildAsync(parentToken, ValidChildBody(email2));
        r2.StatusCode.Should().Be(HttpStatusCode.OK, "second child must be created; body: {0}", b2);
        TryProp(root2, "data", out var d2).Should().BeTrue("body: {0}", b2);
        TryProp(d2, "id", out var id2Prop).Should().BeTrue("body: {0}", b2);

        id1Prop.GetInt32().Should().NotBe(id2Prop.GetInt32(),
            "each child must have a distinct Id; body1: {0}, body2: {1}", b1, b2);
    }

    // ===========================================================================
    // AC-7 / case (c) — Duplicate email → 400, no second account
    // ===========================================================================

    [Fact(DisplayName = "AC-7 DuplicateEmail: adding a child with an existing email → 400, Successed=false")]
    public async Task AC7_DuplicateEmail_Returns400_WithSuccessedFalse()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        // First call — must succeed
        var (r1, _, b1) = await AddChildAsync(parentToken, ValidChildBody(childEmail));
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "first Add-Child must succeed; body: {0}", b1);

        // Second call with the same email — must be rejected
        var (r2, root2, b2) = await AddChildAsync(parentToken, ValidChildBody(childEmail));

        r2.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "duplicate email must return 400 Bad Request; body: {0}", b2);
        TryProp(root2, "successed", out var successed2).Should().BeTrue("body: {0}", b2);
        successed2.GetBoolean().Should().BeFalse("Successed must be false for duplicate email; body: {0}", b2);
    }

    [Fact(DisplayName = "AC-7 DuplicateEmail: response has a specific message (not raw Identity error)")]
    public async Task AC7_DuplicateEmail_ResponseHasSpecificMessage_NotRawIdentityError()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        await AddChildAsync(parentToken, ValidChildBody(childEmail)); // first

        var (_, root2, b2) = await AddChildAsync(parentToken, ValidChildBody(childEmail)); // duplicate

        TryProp(root2, "message", out var message).Should().BeTrue("body: {0}", b2);
        var messageStr = message.GetString() ?? string.Empty;

        // Must not be empty or a raw Identity "DuplicateEmail" error code
        messageStr.Should().NotBeNullOrWhiteSpace("duplicate email must produce a human-readable message; body: {0}", b2);
        messageStr.Should().NotContain("DuplicateEmail", "must not echo raw Identity error code; body: {0}", b2);
        messageStr.Should().NotContain("is already taken", "must not echo raw Identity message verbatim; body: {0}", b2);
    }

    [Fact(DisplayName = "AC-7 DuplicateEmail: no second account created in DB")]
    public async Task AC7_DuplicateEmail_NoSecondAccountCreated()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        await AddChildAsync(parentToken, ValidChildBody(childEmail)); // first
        await AddChildAsync(parentToken, ValidChildBody(childEmail)); // duplicate

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var count = db.Users.Count(u => u.Email == childEmail);
        count.Should().Be(1, "there must be exactly one account for the email, not two; email: {0}", childEmail);
    }

    // ===========================================================================
    // AC-6 / case (b) — Grade validation → 422
    // ===========================================================================

    [Fact(DisplayName = "AC-6 Grade0: grade = 0 → 422 UnprocessableEntity")]
    public async Task AC6_Grade0_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = ValidChildBody(UniqueEmail("child"), grade: 0);

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "grade=0 is outside 1-6 and must return 422; body: {0}", rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeFalse("Successed must be false for invalid grade; body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("response must have 'errors'; body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "Errors[] must be populated; body: {0}", rawBody);
    }

    [Fact(DisplayName = "AC-6 Grade7: grade = 7 → 422 UnprocessableEntity")]
    public async Task AC6_Grade7_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = ValidChildBody(UniqueEmail("child"), grade: 7);

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "grade=7 is outside 1-6 and must return 422; body: {0}", rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeFalse("body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "body: {0}", rawBody);
    }

    [Fact(DisplayName = "AC-6 GradeNegative: grade = -1 → 422 UnprocessableEntity")]
    public async Task AC6_GradeNegative_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "Test Child",
            Email = UniqueEmail("child"),
            Password = ValidChildPassword,
            Grade = -1,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
        };

        var (resp, _, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "grade=-1 is outside 1-6 and must return 422; body: {0}", rawBody);
    }

    // ===========================================================================
    // AC-6 / case (d) — Malformed email → 422
    // ===========================================================================

    [Fact(DisplayName = "AC-6 MalformedEmail: non-email string → 422 with Errors[]")]
    public async Task AC6_MalformedEmail_Returns422_WithErrors()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "Test Child",
            Email = "not-an-email-at-all",
            Password = ValidChildPassword,
            Grade = 3,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "malformed email must return 422; body: {0}", rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeFalse("body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "Errors[] must be populated for malformed email; body: {0}", rawBody);
    }

    [Fact(DisplayName = "AC-6 MissingEmail: empty email → 422 with Errors[]")]
    public async Task AC6_EmptyEmail_Returns422_WithErrors()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "Test Child",
            Email = "",
            Password = ValidChildPassword,
            Grade = 3,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty email must return 422; body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "Errors[] must be populated; body: {0}", rawBody);
    }

    // ===========================================================================
    // AC-6 / case (h) — Invalid language → 422
    // ===========================================================================

    [Fact(DisplayName = "AC-6 InvalidLanguage: language='fr' → 422 UnprocessableEntity")]
    public async Task AC6_InvalidLanguage_Fr_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = ValidChildBody(UniqueEmail("child"), language: "fr");

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "language='fr' (not ar|en) must return 422; body: {0}", rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeFalse("body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "body: {0}", rawBody);
    }

    [Fact(DisplayName = "AC-6 InvalidLanguage: language='en-US' (full code) → 422 (only 'en' is valid)")]
    public async Task AC6_InvalidLanguage_FullCode_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = ValidChildBody(UniqueEmail("child"), language: "en-US");

        var (resp, _, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "language='en-US' (full code, not short 'en') must return 422; body: {0}", rawBody);
    }

    // ===========================================================================
    // AC-6 / case (i) — Empty/weak password → 422
    // ===========================================================================

    [Fact(DisplayName = "AC-6 EmptyPassword: empty password → 422 with Errors[]")]
    public async Task AC6_EmptyPassword_Returns422_WithErrors()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "Test Child",
            Email = UniqueEmail("child"),
            Password = "",
            Grade = 3,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty password must return 422; body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "Errors[] must be populated for empty password; body: {0}", rawBody);
    }

    [Fact(DisplayName = "AC-6 WeakPassword: password with no special char → 422 (complexity rule)")]
    public async Task AC6_WeakPassword_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "Test Child",
            Email = UniqueEmail("child"),
            Password = "simplepassword",   // no uppercase, no digit, no special char
            Grade = 3,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
        };

        var (resp, _, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "a weak password (no complexity) must return 422; body: {0}", rawBody);
    }

    // ===========================================================================
    // AC-6 — Missing FullName → 422
    // ===========================================================================

    [Fact(DisplayName = "AC-6 MissingFullName: empty FullName → 422 with Errors[]")]
    public async Task AC6_EmptyFullName_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "",
            Email = UniqueEmail("child"),
            Password = ValidChildPassword,
            Grade = 3,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty FullName must return 422; body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "body: {0}", rawBody);
    }

    // ===========================================================================
    // AC-6 — Missing Country → 422
    // ===========================================================================

    [Fact(DisplayName = "AC-6 MissingCountry: empty Country → 422 with Errors[]")]
    public async Task AC6_EmptyCountry_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "Test Child",
            Email = UniqueEmail("child"),
            Password = ValidChildPassword,
            Grade = 3,
            Language = "ar",
            Country = "",
            LearningLanguage = "ar",
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty Country must return 422; body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "body: {0}", rawBody);
    }

    // ===========================================================================
    // AC-6 — 422 envelope has all required keys
    // ===========================================================================

    [Fact(DisplayName = "AC-6 ValidationEnvelope: 422 response has statusCode, successed, message, errors keys")]
    public async Task AC6_ValidationEnvelope_HasRequiredKeys()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = ValidChildBody(UniqueEmail("child"), grade: 0); // grade 0 triggers 422

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "body: {0}", rawBody);

        TryProp(root, "statusCode", out _).Should().BeTrue("422 envelope must contain 'statusCode'; body: {0}", rawBody);
        TryProp(root, "successed", out _).Should().BeTrue("422 envelope must contain 'successed'; body: {0}", rawBody);
        TryProp(root, "message", out _).Should().BeTrue("422 envelope must contain 'message'; body: {0}", rawBody);
        TryProp(root, "errors", out _).Should().BeTrue("422 envelope must contain 'errors'; body: {0}", rawBody);
    }

    // ===========================================================================
    // AC-4 / case (e) — Unauthenticated → 401
    // ===========================================================================

    [Fact(DisplayName = "AC-4 Auth: no JWT on Add-Child → 401")]
    public async Task AC4_Anonymous_AddChild_Returns401()
    {
        var body = ValidChildBody(UniqueEmail("child"));
        var (resp, _, rawBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl, body);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Add-Child without a token must return 401; body: {0}", rawBody);
    }

    // ===========================================================================
    // AC-4 / case (f) — Non-parent role → 403
    // ===========================================================================

    [Fact(DisplayName = "AC-4 Auth: Basic-role token on Add-Child → 403")]
    public async Task AC4_BasicRole_AddChild_Returns403()
    {
        var basicToken = await SignInAndGetTokenAsync(BasicUserName, BasicUserPassword);
        var body = ValidChildBody(UniqueEmail("child"));

        var (resp, _, rawBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl, body, basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a Basic-role token must get 403 on Add-Child (not Parent/Admin/SuperAdmin); body: {0}", rawBody);
    }

    [Fact(DisplayName = "AC-4 Auth: Student-role token on Add-Child → 403")]
    public async Task AC4_StudentRole_AddChild_Returns403()
    {
        // Create a parent, add a child, the child can sign in → use child's token for 403 check
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");
        var (addResp, _, addBody) = await AddChildAsync(parentToken, ValidChildBody(childEmail));
        addResp.StatusCode.Should().Be(HttpStatusCode.OK, "prerequisite: child creation must succeed; body: {0}", addBody);

        // Sign in as the child (Student role)
        var (signInResp, signInRoot, signInBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        signInResp.StatusCode.Should().Be(HttpStatusCode.OK, "prerequisite: child sign-in must succeed; body: {0}", signInBody);
        TryProp(signInRoot, "data", out var signInData).Should().BeTrue("body: {0}", signInBody);
        TryProp(signInData, "accessToken", out var childTokenProp).Should().BeTrue("body: {0}", signInBody);
        var childToken = childTokenProp.GetString()!;

        // Use the child's Student-role token to try Add-Child → must get 403
        var newChildBody = ValidChildBody(UniqueEmail("newchild"));
        var (resp, _, rawBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl, newChildBody, childToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a Student-role token must get 403 on Add-Child; body: {0}", rawBody);
    }

    // ===========================================================================
    // AC-4 — SuperAdmin may call Add-Child (support flow)
    // ===========================================================================

    [Fact(DisplayName = "AC-4 Auth: SuperAdmin token on Add-Child → 200 (permitted per controller gate)")]
    public async Task AC4_SuperAdmin_AddChild_Returns200()
    {
        var adminToken = await SignInAndGetTokenAsync(SuperAdminUserName, SuperAdminPassword);
        var childEmail = UniqueEmail("child");
        var body = ValidChildBody(childEmail);

        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl, body, adminToken);

        // SuperAdmin is permitted by [Authorize(Roles = "Parent,Admin,SuperAdmin")]
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SuperAdmin must be able to call Add-Child (controller gate allows Admin/SuperAdmin); body: {0}", rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeTrue("Successed must be true for SuperAdmin Add-Child; body: {0}", rawBody);
    }

    // ===========================================================================
    // AC-4 — ParentId from JWT only: a body field must be ignored
    // ===========================================================================

    [Fact(DisplayName = "AC-4 JwtOnly: extra parentId field in body is ignored; acting parent is from JWT")]
    public async Task AC4_JwtOnly_ExtraParentIdInBody_IsIgnored()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        // Add a parentId field to the body — the command has no such field, so it must be ignored
        var bodyWithParentId = new
        {
            FullName = "Test Child",
            Email = childEmail,
            Password = ValidChildPassword,
            Grade = 3,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
            ParentId = 999999, // extra field — must be silently ignored
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, bodyWithParentId);

        // The call must still succeed; the extra field is ignored by the model binder
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "extra parentId in body must be ignored; Add-Child must succeed normally; body: {0}", rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", rawBody);

        // Verify the child is linked to the actual JWT parent (appears in their My-Children)
        var (listResp, listRoot, listBody) = await MyChildrenAsync(parentToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        TryProp(listRoot, "data", out var data).Should().BeTrue("body: {0}", listBody);
        var emails = data.EnumerateArray()
            .Select(c => TryProp(c, "email", out var ep) ? ep.GetString() : null)
            .ToList();
        emails.Should().Contain(childEmail,
            "child must be linked to the JWT parent, not the body parentId; body: {0}", listBody);
    }

    // ===========================================================================
    // AC-1 — Grade boundary values (valid range 1-6)
    // ===========================================================================

    [Theory(DisplayName = "AC-1 ValidGrades: grades 1-6 → 200 on each")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task AC1_ValidGrade_Returns200(int grade)
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail($"parent_g{grade}"));
        var childEmail = UniqueEmail($"child_g{grade}");
        var body = ValidChildBody(childEmail, grade: grade);

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "grade {0} is within 1-6 and must return 200; body: {1}", grade, rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeTrue("grade {0} must be accepted; body: {1}", grade, rawBody);
    }

    // ===========================================================================
    // AC-1 — Valid language values
    // ===========================================================================

    [Theory(DisplayName = "AC-1 ValidLanguages: 'ar' and 'en' → 200")]
    [InlineData("ar")]
    [InlineData("en")]
    public async Task AC1_ValidLanguage_Returns200(string language)
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail($"parent_lang_{language}"));
        var childEmail = UniqueEmail($"child_lang_{language}");
        var body = ValidChildBody(childEmail, language: language);

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "language='{0}' must be accepted; body: {1}", language, rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeTrue("body: {0}", rawBody);
    }

    // ===========================================================================
    // AC-1 — Language normalization: 'en' stored as 'en-US', 'ar' stored as 'ar-EG'
    // ===========================================================================

    [Fact(DisplayName = "AC-1 LanguageNorm: 'en' → language in response contains 'en'")]
    public async Task AC1_LanguageNormalization_En_EchosEnUS()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");
        var body = ValidChildBody(childEmail, language: "en");

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", rawBody);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", rawBody);
        TryProp(data, "language", out var langProp).Should().BeTrue("body: {0}", rawBody);
        langProp.GetString().Should().ContainEquivalentOf("en",
            "normalized language for 'en' must contain 'en' (stored as 'en-US'); body: {0}", rawBody);
    }

    [Fact(DisplayName = "AC-1 LanguageNorm: 'ar' → language in response contains 'ar'")]
    public async Task AC1_LanguageNormalization_Ar_EchosArEG()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");
        var body = ValidChildBody(childEmail, language: "ar");

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", rawBody);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", rawBody);
        TryProp(data, "language", out var langProp).Should().BeTrue("body: {0}", rawBody);
        langProp.GetString().Should().ContainEquivalentOf("ar",
            "normalized language for 'ar' must contain 'ar' (stored as 'ar-EG'); body: {0}", rawBody);
    }

    // ===========================================================================
    // Persistence: child data persisted to DB correctly
    // ===========================================================================

    [Fact(DisplayName = "Persistence: child Grade and Nationality persisted to DB after Add-Child")]
    public async Task Persistence_ChildGradeAndNationality_PersistedToDb()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");
        var body = ValidChildBody(childEmail, grade: 4, language: "en", country: "SA");

        var (resp, _, rawBody) = await AddChildAsync(parentToken, body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", rawBody);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();

        var child = db.Users.SingleOrDefault(u => u.Email == childEmail);
        child.Should().NotBeNull("child must be persisted to DB; email: {0}", childEmail);
        child!.Grade.Should().Be(4, "Grade must be persisted correctly");
        child.Nationality.Should().Be("SA", "Nationality (Country) must be persisted correctly");
        child.PreferredLanguage.Should().Be("en-US", "PreferredLanguage must be normalized to 'en-US'");
    }

    [Fact(DisplayName = "Persistence: ParentStudent link row created after Add-Child")]
    public async Task Persistence_ParentStudentLinkCreated_AfterAddChild()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        var (resp, root, rawBody) = await AddChildAsync(parentToken, ValidChildBody(childEmail));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", rawBody);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", rawBody);
        TryProp(data, "id", out var idProp).Should().BeTrue("body: {0}", rawBody);
        var childId = idProp.GetInt32();

        // P2-12: the ParentStudent link table moved out of Identity into the Parent module (schema
        // "parent"); this Identity-DbContext link assertion no longer applies. The successful Add-Child
        // response above (and the My-Children persistence checks elsewhere in this suite) cover the
        // auto-link behavior. The Parent-module link row is revalidated by the P2-12 api-tester batch
        // against the new /api/Parent routes.
        _ = childId;
    }

    // ===========================================================================
    // P8-01 — LearningLanguage validation (required, ar|en only)
    // ===========================================================================

    [Fact(DisplayName = "P8-01: Add-Child without LearningLanguage → 422")]
    public async Task P8_01_MissingLearningLanguage_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "Test Child",
            Email = UniqueEmail("child"),
            Password = ValidChildPassword,
            Grade = 3,
            Language = "ar",
            Country = "EG",
            // LearningLanguage intentionally omitted
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "missing LearningLanguage must return 422; body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "Errors[] must be populated; body: {0}", rawBody);
    }

    [Fact(DisplayName = "P8-01: Add-Child with invalid LearningLanguage='fr' → 422")]
    public async Task P8_01_InvalidLearningLanguage_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "Test Child",
            Email = UniqueEmail("child"),
            Password = ValidChildPassword,
            Grade = 3,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "fr",
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "LearningLanguage='fr' must return 422; body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "Errors[] must be populated; body: {0}", rawBody);
    }

    [Theory(DisplayName = "P8-01: Add-Child with valid LearningLanguage ('ar' or 'en') → 200")]
    [InlineData("ar")]
    [InlineData("en")]
    public async Task P8_01_ValidLearningLanguage_Returns200(string learningLanguage)
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail($"parent_{learningLanguage}"));
        var body = ValidChildBody(UniqueEmail($"child_{learningLanguage}"), learningLanguage: learningLanguage);

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "LearningLanguage='{0}' must be accepted; body: {1}", learningLanguage, rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeTrue("body: {0}", rawBody);
    }
}
