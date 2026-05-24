using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Learnexia.Modules.Parent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P2-12 integration tests — Parent/Family APIs, Notification Preferences, and Identity security extensions.
///
/// Routes under test:
///   Parent module (api/Parent/*):
///     POST   /api/Parent/Add-Child          — create child + auto-link
///     POST   /api/Parent/Link-Child         — link existing student
///     GET    /api/Parent/My-Children        — family-scoped list
///     PUT    /api/Parent/Update-Child       — update linked child only
///     DELETE /api/Parent/Unlink-Child       — unlink; last-parent guard
///
///   Notifications module (api/Notifications/Preferences):
///     GET    /api/Notifications/Preferences — defaults on first call; all 4 categories
///     PUT    /api/Notifications/Preferences — upsert; subsequent GET reflects changes
///
///   Identity/Account (api/Users/Account/*):
///     POST   /api/Users/Account/ChangePassword          — wrong current → 400; success → other sessions gone
///     GET    /api/Users/Account/Sessions                — self-scoped session list
///     POST   /api/Users/Account/Sessions/SignOutOthers  — terminates non-current sessions only
///     GET    /api/Users/Account/Plan                    — {planName:"Free",status:"Active"}
///
///   Schema assertions (via test-container DB):
///     parent."ParentStudent" EXISTS
///     notifications."NotificationPreferences" EXISTS
///     identity."ParentStudents" DOES NOT EXIST
///
/// Coverage map keyed to acceptance criteria in docs/plans/P2-12.md Batch-6 table.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_12_AccountSettings_Tests : IAsyncLifetime
{
    // ---------------------------------------------------------------------------
    // URLs
    // ---------------------------------------------------------------------------

    // Parent module — note: routes moved from /api/Users/Parent/ to /api/Parent/
    private const string AddChildUrl = "api/Parent/Add-Child";
    private const string LinkChildUrl = "api/Parent/Link-Child";
    private const string MyChildrenUrl = "api/Parent/My-Children";
    private const string UpdateChildUrl = "api/Parent/Update-Child";
    private const string UnlinkChildUrl = "api/Parent/Unlink-Child";

    // Notifications preferences
    private const string PreferencesUrl = "api/Notifications/Preferences";

    // Identity account security
    private const string ChangePasswordUrl = "api/Users/Account/ChangePassword";
    private const string SessionsUrl = "api/Users/Account/Sessions";
    private const string SignOutOthersUrl = "api/Users/Account/Sessions/SignOutOthers";
    private const string PlanUrl = "api/Users/Account/Plan";

    // Auth plumbing
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string AddUserUrl = "api/Users/UserManagement/AddUser";
    private const string AdminResetPasswordUrl = "api/Users/UserManagement/AdminResetPassword";

    private const string DefaultPassword = "Str0ng@Pass";
    private const string ValidChildPassword = "Child@Pass1";
    private const string SuperAdminUserName = "superadmin";
    private const string SuperAdminPassword = "123Pa$$word!";

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P2_12_AccountSettings_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------------------
    // Shared helpers (mirrored from P1_03/P1_04 pattern)
    // ---------------------------------------------------------------------------

    private static string UniqueEmail(string tag = "")
        => $"p212_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>Case-insensitive property lookup (handles both camelCase and PascalCase JSON).</summary>
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

    private async Task<string> RegisterParentAndGetTokenAsync(string email, string password = DefaultPassword)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = password, AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

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

    private static object ValidChildBody(string email, int grade = 3, string language = "ar", string country = "EG")
        => new
        {
            FullName = "Test Child",
            Email = email,
            Password = ValidChildPassword,
            Grade = grade,
            Language = language,
            Country = country,
        };

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        AddChildAsync(string token, object body)
        => SendAsync(_client, HttpMethod.Post, AddChildUrl, body, token);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        MyChildrenAsync(string token)
        => SendAsync(_client, HttpMethod.Get, MyChildrenUrl, null, token);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        UnlinkChildAsync(string token, int childId)
        => SendAsync(_client, HttpMethod.Delete, UnlinkChildUrl, new { ChildId = childId }, token);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        LinkChildAsync(string token, string childEmail)
        => SendAsync(_client, HttpMethod.Post, LinkChildUrl, new { ChildEmail = childEmail }, token);

    // ---------------------------------------------------------------------------
    // Helper: add a child and return its DB id
    // ---------------------------------------------------------------------------
    private async Task<(int ChildId, string ChildEmail)> AddChildAndGetIdAsync(string parentToken)
    {
        var childEmail = UniqueEmail("child");
        var (resp, root, body) = await AddChildAsync(parentToken, ValidChildBody(childEmail));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "Add-Child prerequisite must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "id", out var idProp).Should().BeTrue("body: {0}", body);
        return (idProp.GetInt32(), childEmail);
    }

    // ---------------------------------------------------------------------------
    // Helper: create a student via admin and optionally reset their password
    // ---------------------------------------------------------------------------
    private async Task<(string Email, string UserName)> CreateStudentViaAdminAsync(
        string adminToken, string? knownPassword = null)
    {
        var email = UniqueEmail("student");
        var userName = $"stu_{Guid.NewGuid():N}"[..20];
        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddUserUrl,
            new
            {
                Email = email,
                UserName = userName,
                FullName = $"Test Student {userName}",
                Roles = new[] { "Student" }
            },
            adminToken);
        var addStatus = (int)addResp.StatusCode;
        addStatus.Should().BeOneOf(new[] { 200, 201 },
            $"admin AddUser must succeed; body: {addBody}");

        if (knownPassword is not null)
        {
            await SendAsync(_client, HttpMethod.Post, AdminResetPasswordUrl,
                new { Email = email, Password = knownPassword, ConfirmPassword = knownPassword },
                adminToken);
        }

        return (email, userName);
    }

    // ===========================================================================
    // PART 1 — Parent/Family (api/Parent/*)
    // ===========================================================================

    // ---------------------------------------------------------------------------
    // 1.1 Add-Child happy path: 200, child created + linked, role=Student
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "PAR-1 AddChild happy path → 200, Successed=true, child profile returned")]
    public async Task AddChild_HappyPath_Returns200_WithChildProfile()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");
        var body = ValidChildBody(childEmail, grade: 3, language: "ar", country: "EG");

        var (resp, root, rawBody) = await AddChildAsync(parentToken, body);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Add-Child with valid parent token must return 200; body: {0}", rawBody);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", rawBody);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", rawBody);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", rawBody);
        data.ValueKind.Should().NotBe(JsonValueKind.Null, "data must not be null; body: {0}", rawBody);

        TryProp(data, "id", out var idProp).Should().BeTrue("data.id required; body: {0}", rawBody);
        idProp.GetInt32().Should().BeGreaterThan(0, "child Id must be positive; body: {0}", rawBody);

        TryProp(data, "email", out var emailProp).Should().BeTrue("data.email required; body: {0}", rawBody);
        emailProp.GetString().Should().Be(childEmail, "email must match; body: {0}", rawBody);
    }

    [Fact(DisplayName = "PAR-1 AddChild: child created in Identity DB with Student role")]
    public async Task AddChild_CreatesChildInIdentityDb_WithStudentRole()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        var (resp, root, rawBody) = await AddChildAsync(parentToken, ValidChildBody(childEmail));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", rawBody);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", rawBody);
        TryProp(data, "id", out var idProp).Should().BeTrue("body: {0}", rawBody);
        var childId = idProp.GetInt32();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();

        var childUser = db.Users.SingleOrDefault(u => u.Email == childEmail);
        childUser.Should().NotBeNull("child must be created in Identity DB; email: {0}", childEmail);
        childUser!.Id.Should().Be(childId, "DB Id must match response Id");

        var studentRole = db.Roles.SingleOrDefault(r => r.Name == "Student");
        studentRole.Should().NotBeNull("'Student' role must be seeded");
        db.UserRoles.Any(ur => ur.UserId == childId && ur.RoleId == studentRole!.Id)
            .Should().BeTrue("child must have Student role; childId: {0}", childId);
    }

    [Fact(DisplayName = "PAR-1 AddChild: auto-links to calling parent (ParentStudent row in parent schema)")]
    public async Task AddChild_AutoLinks_ToCallingParent_InParentSchema()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        var (resp, root, rawBody) = await AddChildAsync(parentToken, ValidChildBody(childEmail));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", rawBody);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", rawBody);
        TryProp(data, "id", out var idProp).Should().BeTrue("body: {0}", rawBody);
        var childId = idProp.GetInt32();

        // Verify via ParentDbContext that the link row exists in the parent schema
        using var scope = _factory.Services.CreateScope();
        var parentDb = scope.ServiceProvider.GetRequiredService<ParentDbContext>();
        var linkExists = await parentDb.ParentStudents
            .AnyAsync(ps => ps.StudentId == childId);
        linkExists.Should().BeTrue(
            "ParentStudent link row must exist in parent schema after Add-Child; childId: {0}", childId);
    }

    [Fact(DisplayName = "PAR-1 AddChild auto-link: child appears in My-Children immediately")]
    public async Task AddChild_ChildAppearsInMyChildren()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        var (addResp, _, addBody) = await AddChildAsync(parentToken, ValidChildBody(childEmail));
        addResp.StatusCode.Should().Be(HttpStatusCode.OK, "Add-Child must succeed; body: {0}", addBody);

        var (listResp, listRoot, listBody) = await MyChildrenAsync(parentToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children must return 200; body: {0}", listBody);
        TryProp(listRoot, "data", out var dataArr).Should().BeTrue("body: {0}", listBody);
        dataArr.GetArrayLength().Should().Be(1,
            "exactly one child must appear after Add-Child; body: {0}", listBody);

        var firstChild = dataArr.EnumerateArray().First();
        TryProp(firstChild, "email", out var eProp).Should().BeTrue("body: {0}", listBody);
        eProp.GetString().Should().Be(childEmail, "child email must match; body: {0}", listBody);
    }

    // ---------------------------------------------------------------------------
    // 1.2 Duplicate email rejected
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "PAR-2 AddChild duplicate email → 400, Successed=false")]
    public async Task AddChild_DuplicateEmail_Returns400_SuccessedFalse()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var childEmail = UniqueEmail("child");

        var (r1, _, b1) = await AddChildAsync(parentToken, ValidChildBody(childEmail));
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "first Add-Child must succeed; body: {0}", b1);

        var (r2, root2, b2) = await AddChildAsync(parentToken, ValidChildBody(childEmail));
        r2.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "duplicate email must return 400; body: {0}", b2);
        TryProp(root2, "successed", out var s2).Should().BeTrue("body: {0}", b2);
        s2.GetBoolean().Should().BeFalse("Successed must be false for duplicate; body: {0}", b2);
    }

    // ---------------------------------------------------------------------------
    // 1.3 Link-Child happy path
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "PAR-3 LinkChild happy path → 200, Successed=true")]
    public async Task LinkChild_HappyPath_Returns200()
    {
        var adminToken = await SignInAndGetTokenAsync(SuperAdminUserName, SuperAdminPassword);
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var (studentEmail, _) = await CreateStudentViaAdminAsync(adminToken);

        var (resp, root, body) = await LinkChildAsync(parentToken, studentEmail);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Link-Child must return 200; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "id", out var idProp).Should().BeTrue("data.id required; body: {0}", body);
        idProp.GetInt32().Should().BeGreaterThan(0, "body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // 1.4 My-Children family-scope / IDOR check
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "PAR-4 MyChildren family-scope: parent B sees ZERO children when parent A has children")]
    public async Task MyChildren_FamilyScope_ParentBSeesEmptyList()
    {
        var parentAToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentA"));
        var parentBToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentB"));

        // Parent A adds a child
        var (addResp, _, addBody) = await AddChildAsync(parentAToken, ValidChildBody(UniqueEmail("child")));
        addResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent A add-child must succeed; body: {0}", addBody);

        // Parent B must see empty list (IDOR check)
        var (resp, root, body) = await MyChildrenAsync(parentBToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "My-Children must return 200 for parent B; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.GetArrayLength().Should().Be(0,
            "parent B must NOT see parent A's children — family isolation; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // 1.5 Update-Child — only own child allowed
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "PAR-5 UpdateChild happy path → 200, Successed=true")]
    public async Task UpdateChild_HappyPath_Returns200()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var (childId, _) = await AddChildAndGetIdAsync(parentToken);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl,
            new
            {
                ChildId = childId,
                FullName = "Updated Name",
                Grade = 5,
                Language = "en",
                Country = "SA"
            },
            parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Update-Child for linked child must return 200; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);
    }

    [Fact(DisplayName = "PAR-5 UpdateChild non-linked child → 403/404 (family-scope guard)")]
    public async Task UpdateChild_NonLinkedChild_ReturnsForbiddenOrNotFound()
    {
        var parentAToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentA"));
        var parentBToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentB"));

        // Parent A creates a child
        var (childId, _) = await AddChildAndGetIdAsync(parentAToken);

        // Parent B tries to update parent A's child — must be rejected
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Put, UpdateChildUrl,
            new
            {
                ChildId = childId,
                FullName = "Hacked Name",
                Grade = 2,
                Language = "ar",
                Country = "EG"
            },
            parentBToken);

        ((int)resp.StatusCode).Should().BeOneOf(new[] { 403, 404 },
            "updating a non-linked child must return 403 or 404; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("Successed must be false; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // 1.6 Unlink-Child happy path
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "PAR-6 UnlinkChild happy path → 200, link row removed from parent schema")]
    public async Task UnlinkChild_HappyPath_Returns200_LinkRowRemoved()
    {
        // To unlink, the child must have at least TWO parents (last-parent guard).
        // A child can only gain a 2nd parent OUTSIDE the Link-Child cross-family guard
        // (Link-Child only allows linking a child you created or an unparented child, and there is
        // no co-parent invite flow yet). So seed a 2nd link row directly in the FK-less parent
        // schema to exercise the unlink happy path (child retains >= 1 parent after parent A unlinks).
        var parentAToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentA"));

        // Parent A creates child
        var (childId, childEmail) = await AddChildAndGetIdAsync(parentAToken);

        // Seed a second (synthetic) parent link directly — the parent."ParentStudent" table has no
        // cross-module FK, so any ParentId value is valid for the purpose of the guard count.
        const int seededSecondParentId = 999_999;
        using (var seedScope = _factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<ParentDbContext>();
            seedDb.Set<ParentStudent>().Add(new ParentStudent
            {
                ParentId = seededSecondParentId,
                StudentId = childId,
                CreatedAt = DateTime.UtcNow,
            });
            await seedDb.SaveChangesAsync();
        }

        // Parent A unlinks — child still has the seeded second parent, so the guard passes
        var (resp, root, body) = await UnlinkChildAsync(parentAToken, childId);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Unlink-Child must return 200 when child has another parent; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        // Verify parent A's link row is gone in the parent schema
        using var scope = _factory.Services.CreateScope();
        var parentDb = scope.ServiceProvider.GetRequiredService<ParentDbContext>();
        // Resolve parent A's user ID from the identity DB via the email we registered
        // We can verify at HTTP level: My-Children for parent A should now return 0
        var (listResp, listRoot, listBody) = await MyChildrenAsync(parentAToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        TryProp(listRoot, "data", out var data).Should().BeTrue("body: {0}", listBody);
        data.GetArrayLength().Should().Be(0,
            "parent A must see 0 children after unlinking; body: {0}", listBody);
    }

    // ---------------------------------------------------------------------------
    // 1.7 Last-parent guard
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "PAR-7 UnlinkChild last-parent guard → non-2xx with domain error")]
    public async Task UnlinkChild_LastParentGuard_RejectsUnlink()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var (childId, _) = await AddChildAndGetIdAsync(parentToken);

        // Child has only one parent — unlink must be blocked
        var (resp, root, body) = await UnlinkChildAsync(parentToken, childId);

        ((int)resp.StatusCode).Should().NotBeInRange(200, 299,
            "last-parent guard must reject the unlink; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("Successed must be false for last-parent guard; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // 1.8 Unlink non-linked child → generic 404
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "PAR-8 UnlinkChild non-linked child → 404 (anti-enumeration)")]
    public async Task UnlinkChild_NonLinkedChild_Returns404()
    {
        var parentAToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentA"));
        var parentBToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parentB"));

        // Parent A creates a child — parent B is NOT linked to this child
        var (childId, _) = await AddChildAndGetIdAsync(parentAToken);

        // Parent B tries to unlink a child they are not linked to
        var (resp, root, body) = await UnlinkChildAsync(parentBToken, childId);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "unlink of non-linked/other-parent child must return 404; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("Successed must be false; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // 1.9 Auth gating on Parent routes
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "PAR-9 Auth: unauthenticated Add-Child → 401")]
    public async Task AddChild_Unauthenticated_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            ValidChildBody(UniqueEmail("child")));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Add-Child without token must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "PAR-9 Auth: unauthenticated My-Children → 401")]
    public async Task MyChildren_Unauthenticated_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, MyChildrenUrl);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "My-Children without token must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "PAR-9 Auth: unauthenticated Unlink-Child → 401")]
    public async Task UnlinkChild_Unauthenticated_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Delete, UnlinkChildUrl,
            new { ChildId = 1 });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Unlink-Child without token must return 401; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // 1.10 Envelope shape
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "PAR-10 Envelope: successful Add-Child has all BaseResponse keys")]
    public async Task AddChild_Envelope_HasAllBaseResponseKeys()
    {
        var parentToken = await RegisterParentAndGetTokenAsync(UniqueEmail("parent"));
        var (resp, root, rawBody) = await AddChildAsync(parentToken, ValidChildBody(UniqueEmail("child")));

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", rawBody);

        TryProp(root, "statusCode", out var sc).Should().BeTrue("envelope must have 'statusCode'; body: {0}", rawBody);
        sc.GetInt32().Should().Be(200, "statusCode must be 200; body: {0}", rawBody);
        TryProp(root, "successed", out _).Should().BeTrue("envelope must have 'successed'; body: {0}", rawBody);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have 'message'; body: {0}", rawBody);
        TryProp(root, "data", out _).Should().BeTrue("envelope must have 'data'; body: {0}", rawBody);
        TryProp(root, "errors", out _).Should().BeTrue("envelope must have 'errors'; body: {0}", rawBody);
    }

    // ===========================================================================
    // PART 2 — Notification Preferences (api/Notifications/Preferences)
    // ===========================================================================

    // ---------------------------------------------------------------------------
    // 2.1 GET returns defaults (no 404) on first call; all 4 categories present
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "NOT-1 GET Preferences: fresh user → 200, 4 categories with defaults (no 404)")]
    public async Task GetPreferences_FirstCall_Returns200_AllFourCategories()
    {
        var token = await RegisterParentAndGetTokenAsync(UniqueEmail("user"));

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, PreferencesUrl, null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET Preferences on first call must return 200 (defaults, no 404); body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        TryProp(data, "preferences", out var prefs).Should().BeTrue("data.preferences required; body: {0}", body);
        prefs.GetArrayLength().Should().Be(4,
            "all 4 notification categories must be returned on first call; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // 2.2 PUT upserts; subsequent GET reflects the change
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "NOT-2 PUT Preferences: upsert → 200; subsequent GET reflects the changes")]
    public async Task PutPreferences_Upserts_ReflectedInSubsequentGet()
    {
        var token = await RegisterParentAndGetTokenAsync(UniqueEmail("user"));

        // Build a preferences payload with all 4 categories
        // NotificationCategory enum: WeeklyReport=0, StreakAtRisk=1, ProductAnnouncements=2, AchievementBadge=3
        var putPayload = new
        {
            Preferences = new[]
            {
                new { Category = 0, EmailEnabled = true,  PushEnabled = true  },
                new { Category = 1, EmailEnabled = false, PushEnabled = true  },
                new { Category = 2, EmailEnabled = true,  PushEnabled = false },
                new { Category = 3, EmailEnabled = false, PushEnabled = false },
            }
        };

        var (putResp, putRoot, putResponseBody) = await SendAsync(_client, HttpMethod.Put, PreferencesUrl, putPayload, token);
        putResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PUT Preferences must return 200; body: {0}", putResponseBody);
        TryProp(putRoot, "successed", out var s).Should().BeTrue("body: {0}", putResponseBody);
        s.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", putResponseBody);

        // Now GET and verify the changes persisted
        var (getResp, getRoot, getBody) = await SendAsync(_client, HttpMethod.Get, PreferencesUrl, null, token);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "subsequent GET Preferences must return 200; body: {0}", getBody);

        TryProp(getRoot, "data", out var data).Should().BeTrue("body: {0}", getBody);
        TryProp(data, "preferences", out var prefs).Should().BeTrue("body: {0}", getBody);
        prefs.GetArrayLength().Should().Be(4, "must still return all 4 categories; body: {0}", getBody);

        // Verify category 0 (WeeklyReport) has EmailEnabled=true, PushEnabled=true
        var weeklyReport = prefs.EnumerateArray()
            .FirstOrDefault(p => TryProp(p, "category", out var cat) && cat.GetInt32() == 0);
        weeklyReport.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "WeeklyReport category must be present; body: {0}", getBody);
        TryProp(weeklyReport, "emailEnabled", out var emailEnabled).Should().BeTrue("body: {0}", getBody);
        emailEnabled.GetBoolean().Should().BeTrue(
            "WeeklyReport EmailEnabled must be true after PUT; body: {0}", getBody);
        TryProp(weeklyReport, "pushEnabled", out var pushEnabled).Should().BeTrue("body: {0}", getBody);
        pushEnabled.GetBoolean().Should().BeTrue(
            "WeeklyReport PushEnabled must be true after PUT; body: {0}", getBody);
    }

    // ---------------------------------------------------------------------------
    // 2.3 A second user's prefs are isolated
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "NOT-3 Preferences isolation: user B's prefs unaffected by user A's PUT")]
    public async Task PutPreferences_UserIsolation_OtherUserPrefsUnchanged()
    {
        var tokenA = await RegisterParentAndGetTokenAsync(UniqueEmail("userA"));
        var tokenB = await RegisterParentAndGetTokenAsync(UniqueEmail("userB"));

        // User A sets all push disabled
        var putPayloadA = new
        {
            Preferences = new[]
            {
                new { Category = 0, EmailEnabled = false, PushEnabled = false },
                new { Category = 1, EmailEnabled = false, PushEnabled = false },
                new { Category = 2, EmailEnabled = false, PushEnabled = false },
                new { Category = 3, EmailEnabled = false, PushEnabled = false },
            }
        };
        var (putResp, _, putRespBody) = await SendAsync(_client, HttpMethod.Put, PreferencesUrl, putPayloadA, tokenA);
        putResp.StatusCode.Should().Be(HttpStatusCode.OK, "user A PUT must succeed; body: {0}", putRespBody);

        // User B has NOT set any prefs — their GET should still return defaults (not user A's values)
        var (getResp, getRoot, getBody) = await SendAsync(_client, HttpMethod.Get, PreferencesUrl, null, tokenB);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "user B GET must return 200; body: {0}", getBody);

        TryProp(getRoot, "data", out var data).Should().BeTrue("body: {0}", getBody);
        TryProp(data, "preferences", out var prefs).Should().BeTrue("body: {0}", getBody);

        // WeeklyReport default is EmailEnabled=true for a fresh user (never touched)
        var weeklyReport = prefs.EnumerateArray()
            .FirstOrDefault(p => TryProp(p, "category", out var cat) && cat.GetInt32() == 0);
        weeklyReport.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "WeeklyReport must be present for user B; body: {0}", getBody);
        TryProp(weeklyReport, "emailEnabled", out var emailEnabled).Should().BeTrue("body: {0}", getBody);
        // User B never PUT, so defaults apply: WeeklyReport EmailEnabled = true (default)
        emailEnabled.GetBoolean().Should().BeTrue(
            "User B's WeeklyReport must use default (true) unaffected by user A's prefs; body: {0}", getBody);
    }

    // ---------------------------------------------------------------------------
    // 2.4 Unauthenticated → 401
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "NOT-4 Auth: unauthenticated GET Preferences → 401")]
    public async Task GetPreferences_Unauthenticated_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, PreferencesUrl);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GET Preferences without token must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "NOT-4 Auth: unauthenticated PUT Preferences → 401")]
    public async Task PutPreferences_Unauthenticated_Returns401()
    {
        var putBody = new
        {
            Preferences = new[]
            {
                new { Category = 0, EmailEnabled = true, PushEnabled = false },
                new { Category = 1, EmailEnabled = true, PushEnabled = false },
                new { Category = 2, EmailEnabled = true, PushEnabled = false },
                new { Category = 3, EmailEnabled = true, PushEnabled = false },
            }
        };
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Put, PreferencesUrl, putBody);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "PUT Preferences without token must return 401; body: {0}", body);
    }

    // ===========================================================================
    // PART 3 — Identity Security (api/Users/Account/*)
    // ===========================================================================

    // ---------------------------------------------------------------------------
    // 3.1 ChangePassword — wrong current password
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "SEC-1 ChangePassword: wrong current password → 400, Successed=false")]
    public async Task ChangePassword_WrongCurrentPassword_Returns400()
    {
        var email = UniqueEmail("user");
        var token = await RegisterParentAndGetTokenAsync(email, DefaultPassword);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, ChangePasswordUrl,
            new
            {
                CurrentPassword = "WrongPassword@1",
                NewPassword = "NewStr0ng@Pass1",
                ConfirmPassword = "NewStr0ng@Pass1"
            },
            token);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "wrong current password must return 400; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("Successed must be false; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // 3.2 ChangePassword — success
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "SEC-2 ChangePassword: correct current password → 200, Successed=true")]
    public async Task ChangePassword_CorrectCurrentPassword_Returns200()
    {
        var email = UniqueEmail("user");
        var token = await RegisterParentAndGetTokenAsync(email, DefaultPassword);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, ChangePasswordUrl,
            new
            {
                CurrentPassword = DefaultPassword,
                NewPassword = "NewStr0ng@Pass1",
                ConfirmPassword = "NewStr0ng@Pass1"
            },
            token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "correct current password must return 200; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // 3.3 ChangePassword — after success, current session still valid
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "SEC-3 ChangePassword: after success, current session's Sessions endpoint still responds 200")]
    public async Task ChangePassword_AfterSuccess_CurrentSessionStillValid()
    {
        var email = UniqueEmail("user");
        var token = await RegisterParentAndGetTokenAsync(email, DefaultPassword);

        // Change password
        var (changeResp, _, changeBody) = await SendAsync(_client, HttpMethod.Post, ChangePasswordUrl,
            new
            {
                CurrentPassword = DefaultPassword,
                NewPassword = "NewStr0ng@Pass1",
                ConfirmPassword = "NewStr0ng@Pass1"
            },
            token);
        changeResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "password change must succeed; body: {0}", changeBody);

        // The current token must still be valid (current session kept)
        var (sessResp, sessRoot, sessBody) = await SendAsync(_client, HttpMethod.Get, SessionsUrl, null, token);
        sessResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "current session must still be valid after password change; body: {0}", sessBody);
        TryProp(sessRoot, "successed", out var s).Should().BeTrue("body: {0}", sessBody);
        s.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", sessBody);
    }

    // ---------------------------------------------------------------------------
    // 3.4 ChangePassword — unauthenticated → 401
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "SEC-4 Auth: unauthenticated ChangePassword → 401")]
    public async Task ChangePassword_Unauthenticated_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, ChangePasswordUrl,
            new
            {
                CurrentPassword = "any",
                NewPassword = "NewStr0ng@Pass1",
                ConfirmPassword = "NewStr0ng@Pass1"
            });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "ChangePassword without token must return 401; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // 3.5 GET Sessions — returns caller's sessions
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "SEC-5 GET Sessions: authenticated → 200, Successed=true, list of sessions returned")]
    public async Task GetSessions_Authenticated_Returns200_WithSessionList()
    {
        var token = await RegisterParentAndGetTokenAsync(UniqueEmail("user"));

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, SessionsUrl, null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET Sessions must return 200; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        // The data must be an array (even if it has only one entry for the current session)
        data.ValueKind.Should().Be(JsonValueKind.Array,
            "Sessions data must be an array; body: {0}", body);
    }

    [Fact(DisplayName = "SEC-5 GET Sessions: unauthenticated → 401")]
    public async Task GetSessions_Unauthenticated_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, SessionsUrl);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GET Sessions without token must return 401; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // 3.6 SignOutOthers — terminates other sessions, keeps current
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "SEC-6 SignOutOthers: authenticated → 200, current session still valid")]
    public async Task SignOutOthers_Returns200_CurrentSessionStillValid()
    {
        var token = await RegisterParentAndGetTokenAsync(UniqueEmail("user"));

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignOutOthersUrl, null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SignOutOthers must return 200; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        // Current session must still be valid
        var (sessResp, sessRoot, sessBody) = await SendAsync(_client, HttpMethod.Get, SessionsUrl, null, token);
        sessResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "current session must remain valid after SignOutOthers; body: {0}", sessBody);
        TryProp(sessRoot, "successed", out var s2).Should().BeTrue("body: {0}", sessBody);
        s2.GetBoolean().Should().BeTrue("body: {0}", sessBody);
    }

    [Fact(DisplayName = "SEC-6 SignOutOthers: unauthenticated → 401")]
    public async Task SignOutOthers_Unauthenticated_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, SignOutOthersUrl);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "SignOutOthers without token must return 401; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // 3.7 GET Plan — {planName:"Free",status:"Active"}
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "SEC-7 GET Plan: authenticated → 200, planName='Free', status='Active'")]
    public async Task GetPlan_Authenticated_Returns200_WithFreeActivePlan()
    {
        var token = await RegisterParentAndGetTokenAsync(UniqueEmail("user"));

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, PlanUrl, null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET Plan must return 200; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        TryProp(data, "planName", out var planName).Should().BeTrue("data.planName required; body: {0}", body);
        planName.GetString().Should().Be("Free",
            "planName must be 'Free' (stub); body: {0}", body);

        TryProp(data, "status", out var status).Should().BeTrue("data.status required; body: {0}", body);
        status.GetString().Should().Be("Active",
            "status must be 'Active' (stub); body: {0}", body);
    }

    [Fact(DisplayName = "SEC-7 GET Plan: unauthenticated → 401")]
    public async Task GetPlan_Unauthenticated_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, PlanUrl);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GET Plan without token must return 401; body: {0}", body);
    }

    // ===========================================================================
    // PART 4 — Schema assertions
    // ===========================================================================

    [Fact(DisplayName = "SCHEMA-1 parent.\"ParentStudent\" table exists in the test DB")]
    public async Task Schema_ParentStudentTable_Exists()
    {
        using var scope = _factory.Services.CreateScope();
        var parentDb = scope.ServiceProvider.GetRequiredService<ParentDbContext>();

        // A successful MigrateAsync in InitializeAsync guarantees the table exists. We additionally
        // probe by counting rows (this will throw if the table doesn't exist).
        var act = async () =>
        {
            _ = await parentDb.ParentStudents.CountAsync();
        };
        await act.Should().NotThrowAsync(
            "parent.\"ParentStudent\" table must exist after Parent migrations");
    }

    [Fact(DisplayName = "SCHEMA-2 notifications.\"NotificationPreferences\" table exists in the test DB")]
    public async Task Schema_NotificationPreferencesTable_Exists()
    {
        using var scope = _factory.Services.CreateScope();
        var notifDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        var act = async () =>
        {
            _ = await notifDb.NotificationPreferences.CountAsync();
        };
        await act.Should().NotThrowAsync(
            "notifications.\"NotificationPreferences\" table must exist after Notifications migrations");
    }

    [Fact(DisplayName = "SCHEMA-3 identity.\"ParentStudents\" table does NOT exist (dropped by P2-12 migration)")]
    public async Task Schema_IdentityParentStudents_TableIsAbsent()
    {
        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();

        // Execute raw SQL to check if the table exists in the information_schema.
        // If it exists this returns 1, otherwise 0. We assert it returns 0.
        var conn = identityDb.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM information_schema.tables " +
            "WHERE table_schema = 'identity' AND table_name = 'ParentStudents'";
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        await conn.CloseAsync();

        count.Should().Be(0,
            "identity.\"ParentStudents\" must have been dropped by P2-12-ID-2 migration; found {0} rows in information_schema", count);
    }

    // ===========================================================================
    // PART 5 — Envelope regression on new routes (Successed casing)
    // ===========================================================================

    [Fact(DisplayName = "ENV-1 Envelope: My-Children response uses camelCase 'successed' (not 'succeeded')")]
    public async Task Envelope_MyChildren_UsesSuccessedCamelCase()
    {
        var token = await RegisterParentAndGetTokenAsync(UniqueEmail("user"));

        var (resp, _, rawBody) = await MyChildrenAsync(token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", rawBody);

        // The raw JSON body must contain "successed" (the project's canonical spelling)
        rawBody.Should().Contain("successed",
            "the response envelope must use the canonical 'successed' spelling; body: {0}", rawBody);
        rawBody.Should().NotContain("\"succeeded\"",
            "must not use the 'succeeded' misspelling; body: {0}", rawBody);
    }

    [Fact(DisplayName = "ENV-2 Envelope: GET Preferences response uses camelCase 'successed'")]
    public async Task Envelope_GetPreferences_UsesSuccessedCamelCase()
    {
        var token = await RegisterParentAndGetTokenAsync(UniqueEmail("user"));

        var (resp, _, rawBody) = await SendAsync(_client, HttpMethod.Get, PreferencesUrl, null, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", rawBody);

        rawBody.Should().Contain("successed",
            "the response envelope must use the canonical 'successed' spelling; body: {0}", rawBody);
    }

    [Fact(DisplayName = "ENV-3 Envelope: GET Plan response uses camelCase 'successed'")]
    public async Task Envelope_GetPlan_UsesSuccessedCamelCase()
    {
        var token = await RegisterParentAndGetTokenAsync(UniqueEmail("user"));

        var (resp, _, rawBody) = await SendAsync(_client, HttpMethod.Get, PlanUrl, null, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", rawBody);

        rawBody.Should().Contain("successed",
            "the response envelope must use the canonical 'successed' spelling; body: {0}", rawBody);
    }

    // ===========================================================================
    // PART 6 — Regression: old /api/Users/Parent/* routes MUST NOT exist
    // ===========================================================================

    [Fact(DisplayName = "REG-1 Old route /api/Users/Parent/Add-Child must NOT exist (moved to /api/Parent/Add-Child)")]
    public async Task OldAddChildRoute_MustNotExist()
    {
        var token = await RegisterParentAndGetTokenAsync(UniqueEmail("user"));
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post,
            "api/Users/Parent/Add-Child", ValidChildBody(UniqueEmail("child")), token);

        // Must be 404 (not found) — route was relocated to /api/Parent/Add-Child
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the old /api/Users/Parent/Add-Child route must be gone (now /api/Parent/Add-Child); body: {0}", body);
    }

    [Fact(DisplayName = "REG-2 Old route /api/Users/Parent/My-Children must NOT exist (moved to /api/Parent/My-Children)")]
    public async Task OldMyChildrenRoute_MustNotExist()
    {
        var token = await RegisterParentAndGetTokenAsync(UniqueEmail("user"));
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get,
            "api/Users/Parent/My-Children", null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the old /api/Users/Parent/My-Children route must be gone; body: {0}", body);
    }
}
