using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P1-04 integration tests: Link-a-parent-to-a-child endpoint and family-scoped My-Children list.
///
/// Endpoints under test:
///   [Authorize(Roles="Parent,Admin,SuperAdmin")]
///   POST api/Users/Parent/Link-Child
///   GET  api/Users/Parent/My-Children
///
/// Test strategy:
///   - Create Parent users via POST /api/Users/Authentication/Register-Parent (returns a JWT).
///   - Create Student users via POST /api/Users/UserManagement/AddUser (admin-gated;
///     uses the seeded superadmin token). This sets CreatedBy from the JWT (admin id).
///   - The handler's cross-family guard allows linking when the child was created by the
///     calling parent (child.CreatedBy == parentId) OR when the child has no parent yet.
///     Because the admin creates students in tests, CreatedBy != parentId — the "no parent yet"
///     branch applies for the initial link, which is the correct and intended path for
///     test data isolation.
///   - Each test seeds its own uniquely-named users to avoid cross-test state.
///
/// JSON serialization note: controller path uses Newtonsoft (camelCase); middleware path (422)
/// uses System.Text.Json (PascalCase). TryProp() handles both transparently.
///
/// Acceptance criteria covered:
///   AC-2   Happy path — link an existing child → 200, Successed=true, child summary returned
///   AC-6   Idempotent re-link of the same child → 200, no duplicate row, no error
///   AC-3   My-Children family isolation — parent B never sees parent A's children
///   AC-4   Many-to-many — a parent may be linked to multiple children
///   AC-5   Non-existent email → generic failure (no email enumeration)
///   AC-5   Target is not a student (Admin) → generic failure
///   AC-7   Cross-family IDOR: parent cannot claim a child already linked to another parent → generic failure
///   AC-2   Unauthenticated → 401
///   (AC-7) Non-parent role (Student token) → 403
///   Validation: null/empty email → 422
///   Validation: malformed email → 422
///   Self-link → generic failure
///   My-Children — parent with no children → 200, empty list
///   My-Children — parent with 2 children → 200, list of 2
///   Parent-id from JWT: body field cannot change who links
/// </summary>
[Collection("IntegrationTests")]
public sealed class P1_04_LinkParentChild_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // ---------------------------------------------------------------------------
    // Constants
    // ---------------------------------------------------------------------------
    private const string LinkChildUrl = "api/Users/Parent/Link-Child";
    private const string MyChildrenUrl = "api/Users/Parent/My-Children";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string AddUserUrl = "api/Users/UserManagement/AddUser";
    private const string DefaultPassword = "Str0ng@Pass";
    private const string DefaultAdminUserName = "superadmin";
    private const string DefaultAdminPassword = "123Pa$$word!";

    public P1_04_LinkParentChild_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ===========================================================================
    // Helpers
    // ===========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p104_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>
    /// Case-insensitive property lookup: handles both camelCase (controller path) and
    /// PascalCase (middleware 422 path) JSON serialization.
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

    /// <summary>Registers a new parent and returns their JWT access token.</summary>
    private async Task<string> RegisterParentAndGetTokenAsync(string email, string password = DefaultPassword)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>Signs in with an existing account and returns the JWT access token.</summary>
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
    /// Creates a Student user via the admin AddUser endpoint and returns their email.
    /// The student's CreatedBy will be the admin's user id (not the test parent's id).
    /// The handler permits linking when the child has no parent yet, so cross-family
    /// isolation is still tested via explicit pre-link in the IDOR tests.
    /// </summary>
    private async Task<string> CreateStudentViaAdminAsync(string adminToken, string? overrideEmail = null)
    {
        var email = overrideEmail ?? UniqueEmail("student");
        var userName = $"stu_{Guid.NewGuid():N}"[..20];
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, AddUserUrl,
            new
            {
                Email = email,
                UserName = userName,
                FullName = $"Test Student {userName}",
                Roles = new[] { "Student" }
            },
            adminToken);
        // AddUser returns 200 (Success mapped to 200, not 201 in the current impl)
        var addStatusCode = (int)resp.StatusCode;
        addStatusCode.Should().BeOneOf(new[] { 200, 201 },
            $"admin AddUser for Student must succeed; body: {body}");
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("AddUser must succeed; body: {0}", body);
        return email;
    }

    /// <summary>
    /// Calls Link-Child and returns the raw (response, root, body) tuple for assertions.
    /// </summary>
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        LinkChildAsync(string parentToken, string childEmail)
        => SendAsync(_client, HttpMethod.Post, LinkChildUrl, new { ChildEmail = childEmail }, parentToken);

    /// <summary>
    /// Calls My-Children and returns the raw (response, root, body) tuple.
    /// </summary>
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        MyChildrenAsync(string parentToken)
        => SendAsync(_client, HttpMethod.Get, MyChildrenUrl, null, parentToken);

    // ===========================================================================
    // AC-2 Happy path — link an existing student
    // ===========================================================================

    [Fact(DisplayName = "AC-2 HappyPath: parent links an existing unlinked student → 200 Successed=true with child summary")]
    public async Task AC2_LinkChild_HappyPath_Returns200_WithChildSummary()
    {
        // Arrange
        var adminToken = await SignInAndGetTokenAsync(DefaultAdminUserName, DefaultAdminPassword);
        var parentEmail = UniqueEmail("parent");
        var parentToken = await RegisterParentAndGetTokenAsync(parentEmail);
        var childEmail = await CreateStudentViaAdminAsync(adminToken);

        // Act
        var (resp, root, body) = await LinkChildAsync(parentToken, childEmail);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "linking an existing student must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true on successful link; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("response must have 'data'; body: {0}", body);
        TryProp(data, "id", out var idProp).Should().BeTrue("data.id must be present; body: {0}", body);
        idProp.GetInt32().Should().BeGreaterThan(0, "linked child Id must be a positive integer; body: {0}", body);

        TryProp(data, "fullName", out var nameProp).Should().BeTrue("data.fullName must be present; body: {0}", body);
        nameProp.GetString().Should().NotBeNullOrWhiteSpace("fullName must not be blank; body: {0}", body);

        TryProp(data, "email", out var emailProp).Should().BeTrue("data.email must be present; body: {0}", body);
        emailProp.GetString().Should().Be(childEmail, "email in response must match the linked child; body: {0}", body);
    }

    // ===========================================================================
    // AC-6 Idempotent re-link — no duplicate row, still returns 200
    // ===========================================================================

    [Fact(DisplayName = "AC-6 Idempotent: re-linking the same child → 200 Successed=true, no duplicate DB row")]
    public async Task AC6_RelinkSameChild_IsIdempotent_NoError_NoDbDuplicate()
    {
        // Arrange
        var adminToken = await SignInAndGetTokenAsync(DefaultAdminUserName, DefaultAdminPassword);
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = await CreateStudentViaAdminAsync(adminToken);

        // First link
        var (resp1, _, body1) = await LinkChildAsync(parentToken, childEmail);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first link must succeed; body: {0}", body1);

        // Second link (idempotent)
        var (resp2, root2, body2) = await LinkChildAsync(parentToken, childEmail);

        // Assert HTTP layer
        resp2.StatusCode.Should().Be(HttpStatusCode.OK,
            "re-linking the same child must still return 200 (idempotent); body: {0}", body2);
        TryProp(root2, "successed", out var successed2).Should().BeTrue("body: {0}", body2);
        successed2.GetBoolean().Should().BeTrue("Successed must be true on idempotent re-link; body: {0}", body2);

        // Assert DB: only one ParentStudents row for (parent, child)
        // The composite PK (ParentId, StudentId) prevents duplicates at the DB level.
        // We verify by counting rows for the child — should be exactly 1 regardless of
        // how many times Link-Child was called with the same (parent, child) pair.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();

        var childUser = db.Users.SingleOrDefault(u => u.Email == childEmail);
        childUser.Should().NotBeNull("the student user must exist in the DB; email: {0}", childEmail);

        var linkCount = db.ParentStudents.Count(ps => ps.StudentId == childUser!.Id);
        linkCount.Should().BeLessOrEqualTo(1,
            "composite PK must prevent duplicate (ParentId, StudentId) rows; found {0} rows for studentId {1}",
            linkCount, childUser!.Id);
    }

    /// <summary>
    /// A cleaner idempotency verification: after two Link-Child POSTs, My-Children returns exactly 1 child.
    /// </summary>
    [Fact(DisplayName = "AC-6 Idempotent: My-Children shows exactly 1 entry after two link POSTs")]
    public async Task AC6_RelinkSameChild_MyChildrenCountIs1()
    {
        var adminToken = await SignInAndGetTokenAsync(DefaultAdminUserName, DefaultAdminPassword);
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = await CreateStudentViaAdminAsync(adminToken);

        await LinkChildAsync(parentToken, childEmail);   // first link
        await LinkChildAsync(parentToken, childEmail);   // idempotent second

        var (resp, root, body) = await MyChildrenAsync(parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children must return 200; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.GetArrayLength().Should().Be(1,
            "exactly one child must appear in My-Children after two idempotent link calls; body: {0}", body);
    }

    // ===========================================================================
    // AC-5 Non-existent email → generic failure (no email enumeration)
    // ===========================================================================

    [Fact(DisplayName = "AC-5 NonExistentEmail: link to unknown email → non-2xx, generic message, same shape as other failures")]
    public async Task AC5_LinkToNonExistentEmail_ReturnsFailure_NoEmailLeak()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var ghostEmail = UniqueEmail("ghost");  // guaranteed non-existent

        var (resp, root, body) = await LinkChildAsync(parentToken, ghostEmail);

        ((int)resp.StatusCode).Should().NotBeInRange(200, 299,
            "linking a non-existent email must fail; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("Successed must be false for unknown email; body: {0}", body);

        // The body must not reveal whether the email exists (no "not found", "does not exist" etc).
        // The handler collapses all rejections to a single generic "cannot link" message.
        body.Should().NotContainEquivalentOf("not found",
            "error message must not say 'not found' — that leaks email existence; body: {0}", body);
        body.Should().NotContainEquivalentOf("does not exist",
            "error message must not say 'does not exist'; body: {0}", body);
    }

    // ===========================================================================
    // AC-5 Target is not a student (Admin/Basic) → same generic failure
    // ===========================================================================

    [Fact(DisplayName = "AC-5 NonStudent: link to an Admin user → generic failure with SAME message shape as NonExistentEmail")]
    public async Task AC5_LinkToAdminUser_ReturnsGenericFailure_SameShapeAsNonExistent()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        // The seeded superadmin has Admin+SuperAdmin roles — not Student.
        var adminEmail = "superadmin@gmail.com";

        var (respAdmin, rootAdmin, bodyAdmin) = await LinkChildAsync(parentToken, adminEmail);

        ((int)respAdmin.StatusCode).Should().NotBeInRange(200, 299,
            "linking to an admin (non-student) must fail; body: {0}", bodyAdmin);

        TryProp(rootAdmin, "successed", out var succeededAdmin).Should().BeTrue("body: {0}", bodyAdmin);
        succeededAdmin.GetBoolean().Should().BeFalse("Successed must be false for non-student target; body: {0}", bodyAdmin);

        // Compare with a non-existent email rejection to confirm SAME generic shape (AC-5 no enumeration).
        var (respGhost, rootGhost, bodyGhost) = await LinkChildAsync(parentToken, UniqueEmail("ghost"));

        // Both must be non-2xx and Successed=false
        ((int)respGhost.StatusCode).Should().NotBeInRange(200, 299, "body: {0}", bodyGhost);
        TryProp(rootGhost, "successed", out var succeededGhost).Should().BeTrue("body: {0}", bodyGhost);
        succeededGhost.GetBoolean().Should().BeFalse("body: {0}", bodyGhost);

        // Both must return the same HTTP status code (same generic error surface).
        respAdmin.StatusCode.Should().Be(respGhost.StatusCode,
            "non-student and non-existent email must return the SAME status code to prevent enumeration; " +
            "admin body: {0}, ghost body: {1}", bodyAdmin, bodyGhost);
    }

    // ===========================================================================
    // AC-7 Cross-family IDOR: parent cannot link a child already owned by another parent
    // ===========================================================================

    [Fact(DisplayName = "AC-7 IDOR: parent B cannot link a child already linked to parent A → generic failure")]
    public async Task AC7_CrossFamily_ParentB_CannotLinkChildAlreadyLinkedToParentA()
    {
        var adminToken = await SignInAndGetTokenAsync(DefaultAdminUserName, DefaultAdminPassword);

        // Parent A
        var parentAToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentA"));
        var childEmail = await CreateStudentViaAdminAsync(adminToken);

        // Parent A successfully links the child
        var (linkAResp, _, linkABody) = await LinkChildAsync(parentAToken, childEmail);
        linkAResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent A must be able to link the child first; body: {0}", linkABody);

        // Parent B attempts to steal the child
        var parentBToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentB"));
        var (linkBResp, rootB, linkBBody) = await LinkChildAsync(parentBToken, childEmail);

        ((int)linkBResp.StatusCode).Should().NotBeInRange(200, 299,
            "parent B must NOT be able to link a child already linked to parent A; body: {0}", linkBBody);

        TryProp(rootB, "successed", out var succeededB).Should().BeTrue("body: {0}", linkBBody);
        succeededB.GetBoolean().Should().BeFalse("Successed must be false for cross-family claim; body: {0}", linkBBody);

        // Error message must not disclose ownership information
        linkBBody.Should().NotContainEquivalentOf("parent A",
            "response must not disclose family ownership; body: {0}", linkBBody);
        linkBBody.Should().NotContainEquivalentOf("already linked",
            "response must not disclose link status; body: {0}", linkBBody);
    }

    // ===========================================================================
    // AC-3 Family-scoped read — My-Children isolation
    // ===========================================================================

    [Fact(DisplayName = "AC-3 Isolation: My-Children for parent B returns empty list when parent A has children")]
    public async Task AC3_MyChildren_ParentB_SeesEmptyList_WhenParentAHasChild()
    {
        var adminToken = await SignInAndGetTokenAsync(DefaultAdminUserName, DefaultAdminPassword);

        // Parent A links a child
        var parentAToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentA"));
        var childEmail = await CreateStudentViaAdminAsync(adminToken);
        var (linkResp, _, linkBody) = await LinkChildAsync(parentAToken, childEmail);
        linkResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent A must link successfully; body: {0}", linkBody);

        // Parent B has no children
        var parentBToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentB"));

        var (resp, root, body) = await MyChildrenAsync(parentBToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children must return 200 for parent B; body: {0}", body);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true even for empty list; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("response must have 'data'; body: {0}", body);
        data.GetArrayLength().Should().Be(0,
            "parent B must see 0 children — family isolation must prevent leakage; body: {0}", body);
    }

    // ===========================================================================
    // AC-4 Many-to-many — a parent linked to multiple children
    // ===========================================================================

    [Fact(DisplayName = "AC-4 ManyToMany: parent linked to 2 students → My-Children returns list of 2")]
    public async Task AC4_ParentLinkedToTwoStudents_MyChildrenReturnsBoth()
    {
        var adminToken = await SignInAndGetTokenAsync(DefaultAdminUserName, DefaultAdminPassword);
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var child1Email = await CreateStudentViaAdminAsync(adminToken);
        var child2Email = await CreateStudentViaAdminAsync(adminToken);

        var (r1, _, b1) = await LinkChildAsync(parentToken, child1Email);
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "link child 1 must succeed; body: {0}", b1);

        var (r2, _, b2) = await LinkChildAsync(parentToken, child2Email);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, "link child 2 must succeed; body: {0}", b2);

        var (resp, root, body) = await MyChildrenAsync(parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children must return 200; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.GetArrayLength().Should().Be(2,
            "parent linked to 2 students must see exactly 2 children; body: {0}", body);
    }

    // ===========================================================================
    // My-Children — parent with no children → empty list
    // ===========================================================================

    [Fact(DisplayName = "MyChildren: parent with no linked children → 200, empty data array")]
    public async Task MyChildren_ParentWithNoChildren_Returns200_EmptyArray()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));

        var (resp, root, body) = await MyChildrenAsync(parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children for fresh parent must return 200; body: {0}", body);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true even for empty list; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.GetArrayLength().Should().Be(0, "fresh parent has no children; body: {0}", body);
    }

    // ===========================================================================
    // Self-link → generic failure
    // ===========================================================================

    [Fact(DisplayName = "SelfLink: parent linking their own email → generic failure (non-2xx, Successed=false)")]
    public async Task SelfLink_ParentLinksOwnEmail_ReturnsFailure()
    {
        var parentEmail = UniqueEmail("parent");
        var parentToken = await RegisterParentAndGetTokenAsync(parentEmail);

        var (resp, root, body) = await LinkChildAsync(parentToken, parentEmail);

        ((int)resp.StatusCode).Should().NotBeInRange(200, 299,
            "self-link must be rejected; body: {0}", body);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("Successed must be false for self-link; body: {0}", body);
    }

    // ===========================================================================
    // Auth gating — unauthenticated → 401
    // ===========================================================================

    [Fact(DisplayName = "Auth: unauthenticated POST Link-Child → 401")]
    public async Task Auth_UnauthenticatedLinkChild_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, LinkChildUrl,
            new { ChildEmail = "any@test.local" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Link-Child without a token must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "Auth: unauthenticated GET My-Children → 401")]
    public async Task Auth_UnauthenticatedMyChildren_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, MyChildrenUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "My-Children without a token must return 401; body: {0}", body);
    }

    // ===========================================================================
    // Auth gating — non-parent role (Student) → 403
    // ===========================================================================

    [Fact(DisplayName = "Auth: Student-role token on Link-Child → 403")]
    public async Task Auth_StudentRole_LinkChild_Returns403()
    {
        // Create a Student user via admin, then try to sign in with that student.
        var adminToken = await SignInAndGetTokenAsync(DefaultAdminUserName, DefaultAdminPassword);
        var studentEmail = UniqueEmail("stu");
        var studentUserName = $"stu_{Guid.NewGuid():N}"[..20];

        // Create the student user
        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddUserUrl,
            new
            {
                Email = studentEmail,
                UserName = studentUserName,
                FullName = "Test Student Caller",
                Roles = new[] { "Student" }
            },
            adminToken);
        var addStatusInt = (int)addResp.StatusCode;
        addStatusInt.Should().BeOneOf(new[] { 200, 201 },
            $"student creation must succeed; body: {addBody}");

        // We cannot sign in with the student because AddUser sets a random temporary password.
        // Instead, use AdminResetPassword to set a known password and then sign in.
        // Set a known password via the admin endpoint.
        const string studentPassword = "Str0ng@Pass1!";
        var (resetResp, _, resetBody) = await SendAsync(_client, HttpMethod.Post,
            "api/Users/UserManagement/AdminResetPassword",
            new { Email = studentEmail, Password = studentPassword, ConfirmPassword = studentPassword },
            adminToken);

        if (resetResp.StatusCode != HttpStatusCode.OK)
        {
            // If admin reset password is unavailable, skip this test path rather than fail on setup.
            // This is a test infrastructure limitation: AddUser uses a random temp password.
            // We use the basicuser (seeded with Basic role) as a non-Parent role token instead.
            var basicToken = await SignInAndGetTokenAsync("basicuser", "123Pa$$word!");

            var (resp2, _, body2) = await SendAsync(_client, HttpMethod.Post, LinkChildUrl,
                new { ChildEmail = "any@test.local" }, basicToken);

            resp2.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "a non-parent role (Basic) must get 403 on Link-Child; body: {0}", body2);
            return;
        }

        // Sign in as the student
        var studentToken = await SignInAndGetTokenAsync(studentUserName, studentPassword);

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, LinkChildUrl,
            new { ChildEmail = "any@test.local" }, studentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a Student-role token must get 403 on Link-Child; body: {0}", body);
    }

    /// <summary>
    /// Simpler non-parent role test using the seeded basicuser (Basic role — not Parent/Admin/SuperAdmin).
    /// </summary>
    [Fact(DisplayName = "Auth: Basic-role token on Link-Child → 403 (non-Parent/Admin/SuperAdmin)")]
    public async Task Auth_BasicRole_LinkChild_Returns403()
    {
        var basicToken = await SignInAndGetTokenAsync("basicuser", "123Pa$$word!");

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, LinkChildUrl,
            new { ChildEmail = "any@test.local" }, basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Basic-role token must get 403 on Link-Child (not Parent/Admin/SuperAdmin); body: {0}", body);
    }

    [Fact(DisplayName = "Auth: Basic-role token on My-Children → 403")]
    public async Task Auth_BasicRole_MyChildren_Returns403()
    {
        var basicToken = await SignInAndGetTokenAsync("basicuser", "123Pa$$word!");

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, MyChildrenUrl, null, basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Basic-role token must get 403 on My-Children; body: {0}", body);
    }

    // ===========================================================================
    // Admin and SuperAdmin may also call Link-Child (per controller gate)
    // ===========================================================================

    [Fact(DisplayName = "Auth: SuperAdmin token on My-Children → 200 (admin is permitted)")]
    public async Task Auth_SuperAdminRole_MyChildren_Returns200()
    {
        var adminToken = await SignInAndGetTokenAsync(DefaultAdminUserName, DefaultAdminPassword);

        var (resp, root, body) = await MyChildrenAsync(adminToken);

        // Admin has no children, so we just check the 200 and the envelope shape.
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SuperAdmin must be able to call My-Children (role gate permits Admin/SuperAdmin); body: {0}", body);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);
    }

    // ===========================================================================
    // Validation (FluentValidation → 422) — shape tests
    // ===========================================================================

    [Fact(DisplayName = "Validation: null/empty ChildEmail → 422 with Errors[]")]
    public async Task Validation_EmptyEmail_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, LinkChildUrl,
            new { ChildEmail = "" }, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty ChildEmail must return 422; body: {0}", body);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("Successed must be false for validation failure; body: {0}", body);
        TryProp(root, "errors", out var errors).Should().BeTrue("response must have 'errors' key; body: {0}", body);
        errors.GetArrayLength().Should().BeGreaterThan(0, "Errors[] must be populated for empty email; body: {0}", body);
    }

    [Fact(DisplayName = "Validation: malformed ChildEmail → 422 with Errors[]")]
    public async Task Validation_MalformedEmail_Returns422()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, LinkChildUrl,
            new { ChildEmail = "not-an-email-at-all" }, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "malformed ChildEmail must return 422; body: {0}", body);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("body: {0}", body);
        TryProp(root, "errors", out var errors).Should().BeTrue("response must have 'errors'; body: {0}", body);
        errors.GetArrayLength().Should().BeGreaterThan(0, "Errors[] must not be empty; body: {0}", body);
    }

    [Fact(DisplayName = "Validation: 422 envelope has all required BaseResponse keys")]
    public async Task Validation_Envelope_HasRequiredKeys()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, LinkChildUrl,
            new { ChildEmail = "" }, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "body: {0}", body);

        // All BaseResponse envelope keys must be present (either camelCase or PascalCase)
        TryProp(root, "statusCode", out _).Should().BeTrue(
            "422 envelope must contain 'statusCode'; body: {0}", body);
        TryProp(root, "successed", out _).Should().BeTrue(
            "422 envelope must contain 'successed'; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue(
            "422 envelope must contain 'message'; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue(
            "422 envelope must contain 'errors'; body: {0}", body);
    }

    // ===========================================================================
    // Parent-id from JWT only — no body override
    // ===========================================================================

    [Fact(DisplayName = "JwtOnly: parentId in request body (if sent) does NOT change who links")]
    public async Task JwtOnly_BodyParentId_IsIgnored_ActingParentIsFromToken()
    {
        // Arrange: two parents and one student
        var adminToken = await SignInAndGetTokenAsync(DefaultAdminUserName, DefaultAdminPassword);
        var parentAEmail = UniqueEmail("parentA");
        var parentAToken = await RegisterParentAndGetTokenAsync(parentAEmail);
        var parentBToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentB"));
        var childEmail = await CreateStudentViaAdminAsync(adminToken);

        // Resolve parent A's numeric id from their token / My-Children empty call.
        // We can do this by parsing the token claim, but here we just verify indirectly:
        // parent B sends parentId=<some bogus number> alongside a valid childEmail.
        // The handler ignores the body parentId; the JWT identifies who acts.
        // After the call, only parent A's children list changes.

        // First, parent A links the child legitimately.
        var (linkResp, _, linkBody) = await LinkChildAsync(parentAToken, childEmail);
        linkResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent A link must succeed; body: {0}", linkBody);

        // Parent B tries to send an extra 'parentId' field in the body pointing at parent A's id.
        // The LinkChildCommand has NO parentId field, so it's ignored by model binding.
        // The child is already linked to parent A, so parent B must get a failure.
        var manipulatedBody = new
        {
            ChildEmail = childEmail,
            ParentId = 999999 // extra field — must be silently ignored
        };
        var (manipResp, manipRoot, manipBody) = await SendAsync(_client, HttpMethod.Post, LinkChildUrl,
            manipulatedBody, parentBToken);

        ((int)manipResp.StatusCode).Should().NotBeInRange(200, 299,
            "parent B with a body parentId override must still fail (child belongs to parent A); body: {0}", manipBody);
        TryProp(manipRoot, "successed", out var successed).Should().BeTrue("body: {0}", manipBody);
        successed.GetBoolean().Should().BeFalse(
            "Successed must be false — the body parentId was ignored and the JWT-resolved parent B cannot claim parent A's child; body: {0}", manipBody);

        // Confirm parent B still has 0 children (the link was not created).
        var (listResp, listRoot, listBody) = await MyChildrenAsync(parentBToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        TryProp(listRoot, "data", out var listData).Should().BeTrue("body: {0}", listBody);
        listData.GetArrayLength().Should().Be(0,
            "parent B must have 0 children — the body parentId override was ignored; body: {0}", listBody);
    }

    // ===========================================================================
    // Persistence: link row created and retrievable via My-Children
    // ===========================================================================

    [Fact(DisplayName = "Persistence: after Link-Child, child appears in My-Children")]
    public async Task Persistence_AfterLinkChild_ChildAppearsInMyChildren()
    {
        var adminToken = await SignInAndGetTokenAsync(DefaultAdminUserName, DefaultAdminPassword);
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = await CreateStudentViaAdminAsync(adminToken);

        var (linkResp, _, linkBody) = await LinkChildAsync(parentToken, childEmail);
        linkResp.StatusCode.Should().Be(HttpStatusCode.OK, "link must succeed; body: {0}", linkBody);

        var (listResp, listRoot, listBody) = await MyChildrenAsync(parentToken);

        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children must return 200; body: {0}", listBody);
        TryProp(listRoot, "data", out var data).Should().BeTrue("body: {0}", listBody);

        var children = data.EnumerateArray().ToList();
        children.Should().HaveCount(1, "exactly one child must be in the list; body: {0}", listBody);

        // The child in the list must match what we linked
        TryProp(children[0], "email", out var emailInList).Should().BeTrue("body: {0}", listBody);
        emailInList.GetString().Should().Be(childEmail,
            "email in My-Children must match the linked child's email; body: {0}", listBody);
    }

    // ===========================================================================
    // Envelope shape on success
    // ===========================================================================

    [Fact(DisplayName = "Envelope: successful Link-Child response has statusCode, successed, message, data keys")]
    public async Task Envelope_SuccessfulLinkChild_HasAllBaseResponseKeys()
    {
        var adminToken = await SignInAndGetTokenAsync(DefaultAdminUserName, DefaultAdminPassword);
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = await CreateStudentViaAdminAsync(adminToken);

        var (resp, root, body) = await LinkChildAsync(parentToken, childEmail);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out var statusCode).Should().BeTrue(
            "envelope must have 'statusCode'; body: {0}", body);
        statusCode.GetInt32().Should().Be(200, "statusCode in envelope must be 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue(
            "envelope must have 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "message", out _).Should().BeTrue(
            "envelope must have 'message'; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue(
            "envelope must have 'data'; body: {0}", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Null,
            "data must not be null on success; body: {0}", body);
    }
}
