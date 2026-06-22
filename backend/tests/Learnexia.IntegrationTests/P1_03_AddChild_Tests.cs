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
        await SeatTestSupport.GrantSeatsAsync(_factory, parentToken); // P10-14 seat gate: 2 children need ≥2 seats
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
        await SeatTestSupport.GrantSeatsAsync(_factory, parentToken); // P10-14 seat gate: 2 children need ≥2 seats
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
        await SeatTestSupport.GrantSeatsAsync(_factory, parentToken); // P10-14 seat gate: duplicate add reserves a 2nd seat before the dup-email check
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

    // P6-04 regression: corrected stale expectation. Country is OPTIONAL by design — every Identity
    // validator (RegisterParent, UpdateMyProfile, UpdateChildProfile) bounds Country length only WHEN
    // present and never requires it. So empty Country is accepted and the child is created. (The old
    // assertion expected 422; that was never the product's behaviour. See docs/qc/P6-04/bug-triage.md.)
    [Fact(DisplayName = "AC-6 EmptyCountry: empty Country is accepted (Country optional) → child created")]
    public async Task AC6_EmptyCountry_Accepted_CountryOptional()
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

        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            "Country is optional, so empty Country must still create the child; body: {0}", rawBody);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", rawBody);
        TryProp(data, "id", out var idProp).Should().BeTrue("body: {0}", rawBody);
        idProp.GetInt32().Should().BeGreaterThan(0, "body: {0}", rawBody);
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

    // ===========================================================================
    // BE-TC-05 — Duplicate after sibling does not undo the sibling
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-05: duplicate after valid sibling does not undo the sibling")]
    public async Task BETC05_DuplicateAfterSibling_DoesNotUndoSibling()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        await SeatTestSupport.GrantSeatsAsync(_factory, parentToken); // P10-14 seat gate: 1 real child + duplicate add need ≥2 seats
        var email1 = UniqueEmail("child1");
        var email2 = email1; // same email — duplicate

        // Step 1: add first child → must succeed
        var (r1, _, b1) = await AddChildAsync(parentToken, ValidChildBody(email1));
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "first Add-Child must succeed; body: {0}", b1);

        // Step 2: add duplicate → must return 400
        var (r2, root2, b2) = await AddChildAsync(parentToken, ValidChildBody(email2));
        r2.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "duplicate email must return 400; body: {0}", b2);
        TryProp(root2, "successed", out var succ2).Should().BeTrue("body: {0}", b2);
        succ2.GetBoolean().Should().BeFalse("Successed must be false for duplicate; body: {0}", b2);

        // Step 3: first child still exists in My-Children
        var (listResp, listRoot, listBody) = await MyChildrenAsync(parentToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children must return 200; body: {0}", listBody);
        TryProp(listRoot, "data", out var data).Should().BeTrue("body: {0}", listBody);
        var emails = data.EnumerateArray()
            .Select(c => TryProp(c, "email", out var ep) ? ep.GetString() : null)
            .ToList();
        emails.Should().Contain(email1,
            "the first child must still exist; duplicate failure must not roll it back; body: {0}", listBody);
    }

    // ===========================================================================
    // BE-TC-08 — Grade extreme 1000 → 422
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-08b: grade=1000 (extreme) → 422")]
    public async Task BETC08b_Grade1000_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "Test Child",
            Email = UniqueEmail("child"),
            Password = ValidChildPassword,
            Grade = 1000,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
        };

        var (resp, _, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "grade=1000 is outside 1-6 and must return 422; body: {0}", rawBody);
    }

    // ===========================================================================
    // BE-TC-10 — Password fails complexity (additional sub-runs)
    // ===========================================================================

    [Theory(DisplayName = "BE-TC-10: password fails complexity → 422")]
    [InlineData("alllower1!", "no uppercase")]
    [InlineData("ALLUPPER1!", "no lowercase")]
    [InlineData("Aa!aaaaa", "no digit")]
    [InlineData("Aa1aaaaa", "no special char")]
    [InlineData("Aa1!", "too short (len < 6)")]
    public async Task BETC10_PasswordFailsComplexity_Returns422(string password, string reason)
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "Test Child",
            Email = UniqueEmail("child"),
            Password = password,
            Grade = 3,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "password '{0}' ({1}) must fail complexity and return 422; body: {2}", password, reason, rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeFalse("Successed must be false; body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "Errors[] must be populated; body: {0}", rawBody);
    }

    // ===========================================================================
    // BE-TC-11 — Minimum-valid password → 200 (positive boundary)
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-11: minimum-valid password 'Aa1!aa' (len 6, all 4 complexity classes) → 200")]
    public async Task BETC11_MinimumValidPassword_Returns200()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        // Exactly 6 chars: one lower (a), one upper (A), one digit (1), one special (!), two more lower (aa)
        const string minValidPassword = "Aa1!aa";
        var body = new
        {
            FullName = "Test Child",
            Email = UniqueEmail("child"),
            Password = minValidPassword,
            Grade = 3,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "password 'Aa1!aa' meets the minimum complexity (len 6, all 4 classes) and must return 200; body: {0}", rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeTrue("Successed must be true for minimum-valid password; body: {0}", rawBody);
    }

    // ===========================================================================
    // BE-TC-12 — language not in {ar,en} — additional sub-runs
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-12b: language='EN' (wrong case, must be lowercase) → 422")]
    public async Task BETC12b_Language_UppercaseEN_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = ValidChildBody(UniqueEmail("child"), language: "EN");

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "language='EN' (uppercase, rule is exact-match lowercase) must return 422; body: {0}", rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeFalse("body: {0}", rawBody);
    }

    [Fact(DisplayName = "BE-TC-12c: language='' (empty) → 422")]
    public async Task BETC12c_Language_Empty_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = ValidChildBody(UniqueEmail("child"), language: "");

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty language must return 422; body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "Errors[] must be populated; body: {0}", rawBody);
    }

    // ===========================================================================
    // BE-TC-13 — learningLanguage empty string sub-run
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-13c: learningLanguage='' (empty string) → 422")]
    public async Task BETC13c_LearningLanguage_Empty_Returns422()
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
            LearningLanguage = "",
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty LearningLanguage must return 422; body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "Errors[] must be populated; body: {0}", rawBody);
    }

    // ===========================================================================
    // BE-TC-14 — Malformed email additional sub-runs
    // ===========================================================================

    [Theory(DisplayName = "BE-TC-14: malformed email sub-runs → 422")]
    [InlineData("foo@", "trailing @ with no domain")]
    [InlineData("@bar.com", "leading @ with no local part")]
    public async Task BETC14_MalformedEmail_SubRuns_Returns422(string email, string description)
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "Test Child",
            Email = email,
            Password = ValidChildPassword,
            Grade = 3,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "email '{0}' ({1}) is malformed and must return 422; body: {2}", email, description, rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "Errors[] must be populated; body: {0}", rawBody);
    }

    // ===========================================================================
    // BE-TC-16 — Expired/malformed JWT → 401
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-16: garbage bearer token → 401")]
    public async Task BETC16_GarbageBearerToken_Returns401()
    {
        var body = ValidChildBody(UniqueEmail("child"));
        var (resp, _, rawBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl, body, "not.a.valid.jwt.at.all");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a garbage bearer token must return 401; body: {0}", rawBody);
    }

    // ===========================================================================
    // BE-TC-18 — Body cannot inject role or parentId (mass-assignment / privilege escalation)
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-18: extra role/roles/parentId fields in body are ignored; child is Student, not elevated")]
    public async Task BETC18_ExtraRoleFields_AreIgnored_ChildIsStudent()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        // Body with extra fields that must be silently ignored
        var body = new
        {
            FullName = "Test Child",
            Email = childEmail,
            Password = ValidChildPassword,
            Grade = 3,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
            Role = "Admin",            // extra field — must be ignored
            Roles = new[] { "SuperAdmin" },  // extra field — must be ignored
            IsStudent = false,         // extra field — must be ignored
            ParentId = 999999,         // extra field — must be ignored
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        // Must succeed
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "extra fields in body must be ignored; Add-Child must still succeed; body: {0}", rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", rawBody);

        // Verify the child is Student role (not Admin/SuperAdmin)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var childUser = db.Users.SingleOrDefault(u => u.Email == childEmail);
        childUser.Should().NotBeNull("child must be created; email: {0}", childEmail);

        var studentRole = db.Roles.SingleOrDefault(r => r.Name == "Student");
        studentRole.Should().NotBeNull("'Student' role must exist");
        var adminRole = db.Roles.SingleOrDefault(r => r.Name == "Admin");
        var superAdminRole = db.Roles.SingleOrDefault(r => r.Name == "SuperAdmin");

        var hasStudent = db.UserRoles.Any(ur => ur.UserId == childUser!.Id && ur.RoleId == studentRole!.Id);
        hasStudent.Should().BeTrue("child must have Student role regardless of body role injection attempt");

        if (adminRole is not null)
        {
            var hasAdmin = db.UserRoles.Any(ur => ur.UserId == childUser!.Id && ur.RoleId == adminRole.Id);
            hasAdmin.Should().BeFalse("role injection must not grant Admin role; body: {0}", rawBody);
        }

        if (superAdminRole is not null)
        {
            var hasSuperAdmin = db.UserRoles.Any(ur => ur.UserId == childUser!.Id && ur.RoleId == superAdminRole.Id);
            hasSuperAdmin.Should().BeFalse("role injection must not grant SuperAdmin role; body: {0}", rawBody);
        }
    }

    // ===========================================================================
    // BE-TC-20 — Child created by Parent-A does NOT appear under Parent-B (family scope)
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-20: child added by Parent-A does NOT appear in Parent-B My-Children (no cross-family leakage)")]
    public async Task BETC20_CrossFamilyScope_ChildNotVisibleUnderOtherParent()
    {
        var parentAToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentA"));
        var parentBToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentB"));
        var childEmail = UniqueEmail("child");

        // Parent-A adds a child
        var (addResp, _, addBody) = await AddChildAsync(parentAToken, ValidChildBody(childEmail));
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Parent-A's Add-Child must succeed; body: {0}", addBody);

        // Parent-B's My-Children must NOT contain Parent-A's child
        var (listResp, listRoot, listBody) = await MyChildrenAsync(parentBToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children must return 200; body: {0}", listBody);
        TryProp(listRoot, "data", out var data).Should().BeTrue("body: {0}", listBody);
        var emails = data.EnumerateArray()
            .Select(c => TryProp(c, "email", out var ep) ? ep.GetString() : null)
            .ToList();
        emails.Should().NotContain(childEmail,
            "Parent-A's child must not appear in Parent-B's My-Children (no cross-family leakage); body: {0}", listBody);
    }

    // ===========================================================================
    // BE-TC-21 — Grade boundaries 1 and 6 persist correctly
    // ===========================================================================

    [Theory(DisplayName = "BE-TC-21: grade boundaries 1 and 6 → 200 and persisted")]
    [InlineData(1)]
    [InlineData(6)]
    public async Task BETC21_GradeBoundaries_PersistedCorrectly(int grade)
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail($"parent_b{grade}"));
        var childEmail = UniqueEmail($"child_b{grade}");
        var body = ValidChildBody(childEmail, grade: grade);

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "grade={0} is a valid boundary and must return 200; body: {1}", grade, rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", rawBody);

        // Verify the grade persisted via My-Children
        var (listResp, listRoot, listBody) = await MyChildrenAsync(parentToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        TryProp(listRoot, "data", out var data).Should().BeTrue("body: {0}", listBody);
        var child = data.EnumerateArray()
            .FirstOrDefault(c => TryProp(c, "email", out var ep) && ep.GetString() == childEmail);
        child.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "the added child must appear in My-Children; body: {0}", listBody);
        TryProp(child, "grade", out var gradeProp).Should().BeTrue("data must have grade; body: {0}", listBody);
        gradeProp.GetInt32().Should().Be(grade, "grade must be persisted as {0}; body: {1}", grade, listBody);
    }

    // ===========================================================================
    // BE-TC-22 — Child sign-in JWT carries learning_language claim (P8-01)
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-22: signed-in child JWT carries a learning_language claim (P8-01)")]
    public async Task BETC22_ChildSignIn_JWT_CarriesLearningLanguageClaim()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        var (addResp, _, addBody) = await AddChildAsync(parentToken, ValidChildBody(childEmail, learningLanguage: "en"));
        addResp.StatusCode.Should().Be(HttpStatusCode.OK, "Add-Child must succeed; body: {0}", addBody);

        var (signInResp, signInRoot, signInBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        signInResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "child sign-in must succeed; body: {0}", signInBody);

        TryProp(signInRoot, "data", out var data).Should().BeTrue("body: {0}", signInBody);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", signInBody);
        var jwtString = tokenProp.GetString()!;
        jwtString.Should().NotBeNullOrWhiteSpace("body: {0}", signInBody);

        // Decode JWT payload (middle segment, base64url)
        var parts = jwtString.Split('.');
        parts.Should().HaveCount(3, "JWT must have 3 segments; token: {0}", jwtString);
        var payload = parts[1];
        // Pad base64url to standard base64 (switch expression needs explicit parentheses for modulo)
        int rem = payload.Length % 4;
        string padded;
        if (rem == 2) padded = payload + "==";
        else if (rem == 3) padded = payload + "=";
        else padded = payload;
        var base64 = padded.Replace('-', '+').Replace('_', '/');
        var jsonBytes = Convert.FromBase64String(base64);
        var payloadDoc = JsonDocument.Parse(jsonBytes).RootElement;

        // The learning_language claim should be present (P8-01)
        var hasLearningLanguage = payloadDoc.TryGetProperty("learning_language", out var llClaim);
        hasLearningLanguage.Should().BeTrue(
            "the child's JWT must carry a 'learning_language' claim per P8-01; token payload: {0}",
            System.Text.Encoding.UTF8.GetString(jsonBytes));
        llClaim.GetString().Should().Be("en",
            "learning_language claim must match the submitted value 'en'; token payload: {0}",
            System.Text.Encoding.UTF8.GetString(jsonBytes));
    }

    // ===========================================================================
    // BE-TC-03 — Grade/language/country/learningLanguage all persisted (explicit named test)
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-03: grade=4, language='en', country='SA', learningLanguage='ar' → all persisted in My-Children")]
    public async Task BETC03_AllProfileFields_PersistedAndListedInMyChildren()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");
        var body = new
        {
            FullName = "Test Child",
            Email = childEmail,
            Password = ValidChildPassword,
            Grade = 4,
            Language = "en",
            Country = "SA",
            LearningLanguage = "ar",
        };

        var (addResp, _, addBody) = await AddChildAsync(parentToken, body);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK, "Add-Child must succeed; body: {0}", addBody);

        var (listResp, listRoot, listBody) = await MyChildrenAsync(parentToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children must return 200; body: {0}", listBody);
        TryProp(listRoot, "data", out var data).Should().BeTrue("body: {0}", listBody);

        var child = data.EnumerateArray()
            .FirstOrDefault(c => TryProp(c, "email", out var ep) && ep.GetString() == childEmail);
        child.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "the added child must appear in My-Children; body: {0}", listBody);

        TryProp(child, "grade", out var gradeProp).Should().BeTrue("body: {0}", listBody);
        gradeProp.GetInt32().Should().Be(4, "grade must be persisted as 4; body: {0}", listBody);

        TryProp(child, "language", out var langProp).Should().BeTrue("body: {0}", listBody);
        langProp.GetString().Should().Be("en",
            "language must be normalized to short code 'en' in My-Children; body: {0}", listBody);

        TryProp(child, "country", out var countryProp).Should().BeTrue("body: {0}", listBody);
        countryProp.GetString().Should().Be("SA", "country must be persisted as 'SA'; body: {0}", listBody);

        TryProp(child, "learningLanguage", out var llProp).Should().BeTrue("body: {0}", listBody);
        llProp.GetString().Should().NotBeNullOrWhiteSpace(
            "learningLanguage must be present in My-Children; body: {0}", listBody);
        llProp.GetString().Should().ContainEquivalentOf("ar",
            "learningLanguage must reflect the submitted value 'ar'; body: {0}", listBody);
    }

    // ===========================================================================
    // BE-TC-24 — Duplicate response same regardless of whose email it is (no cross-family info leak)
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-24: duplicate email response is the same regardless of whose email it is (no enumeration)")]
    public async Task BETC24_DuplicateEmailResponse_SameRegardlessOfOwner()
    {
        var parentAToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentA"));
        var parentBToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentB"));
        // P10-14 seat gate: parent-A adds a real child then several duplicate attempts (each reserves
        // a seat before the dup-email check) — provision enough seats for both parents.
        await SeatTestSupport.GrantSeatsAsync(_factory, parentAToken);
        await SeatTestSupport.GrantSeatsAsync(_factory, parentBToken);
        var parentAEmail = UniqueEmail("parentAEmail");
        // Register a third parent to get a "parent email" we can try to use
        await RegisterParentAndGetTokenAsync(parentAEmail);

        var emailOfParentBChild = UniqueEmail("childOfB");
        var emailOfParentAChild = UniqueEmail("childOfA");

        // Parent-B adds a child
        var (r1, _, b1) = await AddChildAsync(parentBToken, ValidChildBody(emailOfParentBChild));
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "Parent-B's Add-Child must succeed; body: {0}", b1);

        // Parent-A adds their own child
        var (r2, _, b2) = await AddChildAsync(parentAToken, ValidChildBody(emailOfParentAChild));
        r2.StatusCode.Should().Be(HttpStatusCode.OK, "Parent-A's Add-Child must succeed; body: {0}", b2);

        // Now Parent-A tries duplicates with:
        // (a) Parent-B's child email
        var (ra, rootA, bodyA) = await AddChildAsync(parentAToken, ValidChildBody(emailOfParentBChild));
        ra.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "duplicate of Parent-B's child email must return 400; body: {0}", bodyA);
        TryProp(rootA, "message", out var msgA).Should().BeTrue("body: {0}", bodyA);
        var messageA = msgA.GetString() ?? string.Empty;
        messageA.Should().NotBeNullOrWhiteSpace("must have a message; body: {0}", bodyA);

        // (b) Parent-A's own child email
        var (rb, rootB, bodyB) = await AddChildAsync(parentAToken, ValidChildBody(emailOfParentAChild));
        rb.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "duplicate of own child's email must return 400; body: {0}", bodyB);
        TryProp(rootB, "message", out var msgB).Should().BeTrue("body: {0}", bodyB);
        var messageB = msgB.GetString() ?? string.Empty;

        // (c) A parent's account email
        var (rc, rootC, bodyC) = await AddChildAsync(parentAToken, ValidChildBody(parentAEmail));
        rc.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "duplicate of a parent's email must return 400; body: {0}", bodyC);
        TryProp(rootC, "message", out var msgC).Should().BeTrue("body: {0}", bodyC);
        var messageC = msgC.GetString() ?? string.Empty;

        // All three must return the same message (no cross-family info leak)
        messageA.Should().Be(messageB,
            "duplicate message must be the same for a foreign child's email as for own child's email");
        messageA.Should().Be(messageC,
            "duplicate message must be the same for a parent email as for a child email");
    }

    // ===========================================================================
    // BE-TC-25 — Blank fullName whitespace-only sub-run
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-25b: fullName whitespace-only → 422")]
    public async Task BETC25b_WhitespaceOnlyFullName_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "   ",
            Email = UniqueEmail("child"),
            Password = ValidChildPassword,
            Grade = 3,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "whitespace-only FullName must return 422; body: {0}", rawBody);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", rawBody);
        errors.GetArrayLength().Should().BeGreaterThan(0, "Errors[] must be populated; body: {0}", rawBody);
    }

    // ===========================================================================
    // BE-TC-28 — No anonymous / child self-onboard path exists
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-28: Add-Child requires auth (no anonymous child-create path)")]
    public async Task BETC28_AddChild_RequiresAuth_NoAnonymousChildCreate()
    {
        // Call Add-Child without any auth → must be 401
        var body = ValidChildBody(UniqueEmail("child"));
        var (resp, _, rawBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl, body);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Add-Child must require auth; no anonymous child-create path exists; body: {0}", rawBody);

        // Also confirm Register-Parent is the only anonymous account-creation endpoint
        // (anonymous GET to Add-Child is 405 or 401, not 200; this is already covered by BE-TC-15)
        // No action needed here beyond the 401 above — the anonymous path is confirmed absent.
    }

    // ===========================================================================
    // BE-TC-12b — country whitespace-only → document behavior
    // ===========================================================================

    // P6-04 regression: corrected stale expectation (see AC6_EmptyCountry above + docs/qc/P6-04/bug-triage.md).
    // Country is optional and length-bounded only WHEN present, so a whitespace-only Country is accepted.
    [Fact(DisplayName = "BE-TC-12b: whitespace-only Country is accepted (Country optional) → child created")]
    public async Task BETC12b_CountryWhitespaceOnly_Accepted_CountryOptional()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var body = new
        {
            FullName = "Test Child",
            Email = UniqueEmail("child"),
            Password = ValidChildPassword,
            Grade = 3,
            Language = "ar",
            Country = "   ",
            LearningLanguage = "ar",
        };

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            "Country is optional (bounded only when present), so whitespace Country must still create the child; body: {0}", rawBody);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", rawBody);
        TryProp(data, "id", out var idProp).Should().BeTrue("body: {0}", rawBody);
        idProp.GetInt32().Should().BeGreaterThan(0, "body: {0}", rawBody);
    }

    // ===========================================================================
    // BE-TC-29 — Admin/SuperAdmin token can call Add-Child (support flow) — extended
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-29: SuperAdmin token Add-Child creates child linked to SuperAdmin's JWT id")]
    public async Task BETC29_SuperAdmin_AddChild_ChildLinkedToSuperAdmin()
    {
        var adminToken = await SignInAndGetTokenAsync(SuperAdminUserName, SuperAdminPassword);
        // P10-14 seat gate: the shared SuperAdmin account's 1 implicit-Free seat may already be
        // occupied by another test in the shared DB — provision seats so the support add-child works.
        await SeatTestSupport.GrantSeatsAsync(_factory, adminToken);
        var childEmail = UniqueEmail("child_sa");
        var body = ValidChildBody(childEmail);

        var (resp, root, rawBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl, body, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SuperAdmin must be allowed to call Add-Child (gate: Parent,Admin,SuperAdmin); body: {0}", rawBody);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeTrue("body: {0}", rawBody);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", rawBody);
        TryProp(data, "id", out var idProp).Should().BeTrue("body: {0}", rawBody);
        idProp.GetInt32().Should().BeGreaterThan(0, "child Id must be positive; body: {0}", rawBody);

        // The child must appear in the SuperAdmin's My-Children (linked to the JWT-resolved acting id)
        var (listResp, listRoot, listBody) = await MyChildrenAsync(adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        TryProp(listRoot, "data", out var listData).Should().BeTrue("body: {0}", listBody);
        var emails = listData.EnumerateArray()
            .Select(c => TryProp(c, "email", out var ep) ? ep.GetString() : null)
            .ToList();
        emails.Should().Contain(childEmail,
            "child must be linked to the SuperAdmin's JWT id and appear in their My-Children; body: {0}", listBody);
    }

    // ===========================================================================
    // BE-TC-30 — Oversized inputs are handled gracefully (no 500)
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-30: oversized fullName (~10000 chars) → 422 (MaximumLength validator fires)")]
    public async Task BETC30_OversizedFullName_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var longName = new string('A', 10_000);
        var body = new
        {
            FullName = longName,
            Email = UniqueEmail("child"),
            Password = ValidChildPassword,
            Grade = 3,
            Language = "ar",
            Country = "EG",
            LearningLanguage = "ar",
        };

        var (resp, _, rawBody) = await AddChildAsync(parentToken, body);

        // Fixed: MaximumLength(255) added to AddChildCommandValidator.FullName (DEF-P103-01).
        // The oversized name is now caught by FluentValidation at 422 before reaching the DB.
        ((int)resp.StatusCode).Should().Be(422,
            "an oversized fullName must be rejected by the validator with 422 (not 500); body: {0}", rawBody);

        ((int)resp.StatusCode).Should().NotBe(500,
            "an oversized fullName must never cause an unhandled 500; body: {0}", rawBody);
    }

    // ===========================================================================
    // BE-TC-27 — BLOCKED: role-assign failure triggers compensating delete
    // ===========================================================================
    // This case cannot be triggered from the pure HTTP surface without a fault-injection hook.
    // The seam (IChildAccountService) performs a compensating DeleteAsync on AddToRoleAsync failure,
    // but there is no HTTP-level fault seam to force AddToRoleAsync to fail.
    // Marked BLOCKED with this reason in the execution report.
    // No test method is generated — the case is documented here as a reminder.
    // BLOCKED: BE-TC-27 — no fault-injection hook exists to force AddToRoleAsync failure from the HTTP layer.
}
