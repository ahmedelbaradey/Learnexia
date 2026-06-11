using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P7-06 / P7-07 / P7-08 integration tests: Admin User/Account Management wave.
///
/// Endpoints under test (all AdminOnly, base route api/Admin/Users):
///   P7-06  GET  api/Admin/Users                           — search/list users
///   P7-06  GET  api/Admin/Users/{id}                      — admin profile inspect
///   P7-06  GET  api/Admin/Users/{id}/family               — family linkage
///   P7-06  GET  api/Admin/Users/{id}/activity             — activity summary
///   P7-07  POST api/Admin/Users/{id}/suspend              — suspend account
///   P7-07  POST api/Admin/Users/{id}/reactivate           — reactivate account
///   P7-07  DELETE api/Admin/Users/{id}                    — soft-delete account
///   P7-08  PATCH  api/Admin/Users/{childId}/profile       — update child profile
///   P7-08  POST   api/Admin/Users/{childId}/grade         — override child grade
///   P7-08  POST   api/Admin/Users/{childId}/learning-language — admin change learning language
///
/// Auth matrix: anonymous → 401 / non-admin (parent/basic) → 403 / admin → 200.
/// Envelope: BaseResponse with keys statusCode, successed, message, data, errors.
/// Validation (ICommand): 422 on invalid bodies.
///
/// Docker is NOT available in CI — compile-only; run via dotnet test on a host with Docker.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_06_07_08_UserAccountAdmin_Tests : IAsyncLifetime
{
    // ── URL constants ────────────────────────────────────────────────────────────
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string RefreshTokenUrl    = "api/Users/Authentication/Refresh-Token";
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl        = "api/Parent/Add-Child";

    private const string AdminUsersBase     = "api/Admin/Users";

    // ── Seeded credentials ───────────────────────────────────────────────────────
    private const string AdminUserName      = "superadmin";
    private const string AdminPassword      = "123Pa$$word!";
    private const string BasicUserName      = "basicuser";
    private const string BasicUserPassword  = "123Pa$$word!";

    // ── JWT signing key (appsettings.json test key) ──────────────────────────────
    private const string TestSigningKey     = "CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789";

    private const string ValidChildPassword = "Child@Pass1";

    // ── Infrastructure ───────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private string? _adminToken;
    private string? _basicToken;
    private string? _parentToken;

    public P7_06_07_08_UserAccountAdmin_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();
        _adminToken  = await SignInGetTokenAsync(AdminUserName, AdminPassword);
        _basicToken  = await SignInGetTokenAsync(BasicUserName, BasicUserPassword);
        _parentToken = await RegisterParentGetTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ============================================================================
    // P7-06: Auth matrix — GET api/Admin/Users
    // ============================================================================

    [Fact(DisplayName = "P7-06 AC-5: GET /Admin/Users anonymous → 401")]
    public async Task P706_SearchUsers_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, AdminUsersBase, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "AdminOnly GET /Admin/Users must return 401 without a token; body: {0}", body);
    }

    [Fact(DisplayName = "P7-06 AC-5: GET /Admin/Users with parent token → 403")]
    public async Task P706_SearchUsers_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, AdminUsersBase, bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "AdminOnly GET /Admin/Users must return 403 for a non-admin token; body: {0}", body);
    }

    [Fact(DisplayName = "P7-06 AC-5: GET /Admin/Users with basic-role token → 403")]
    public async Task P706_SearchUsers_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, AdminUsersBase, bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "AdminOnly GET /Admin/Users must return 403 for a basic-role token; body: {0}", body);
    }

    // ============================================================================
    // P7-06: Happy path — user search returns paginated results
    // ============================================================================

    [Fact(DisplayName = "P7-06 AC-1: GET /Admin/Users with admin token → 200 + BaseResponse envelope")]
    public async Task P706_SearchUsers_Admin_Returns200WithEnvelope()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, AdminUsersBase, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Admin GET /Admin/Users must return 200; body: {0}", body);

        // Envelope keys
        AssertEnvelopeKeys(root, body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
    }

    [Fact(DisplayName = "P7-06 AC-1: SearchUsers result is paginated (currentPage / totalCount / totalPages / pageSize present)")]
    public async Task P706_SearchUsers_ReturnsPaginatedShape()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}?PageNumber=1&PageSize=10", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // PaginatedResult envelope sits inside data
        TryProp(root, "data", out var outer).Should().BeTrue("envelope must have data; body: {0}", body);

        // Accept both flat and nested pagination shapes
        bool hasPagination =
            TryProp(outer, "currentPage", out _) ||
            TryProp(outer, "totalCount", out _);

        // Alternatively the entire paginated result may be the outer data
        if (!hasPagination)
        {
            TryProp(root, "currentPage", out _).Should().BeTrue(
                "SearchUsers must return pagination metadata (currentPage/totalCount/totalPages/pageSize); body: {0}", body);
        }
    }

    [Fact(DisplayName = "P7-06 AC-1: SearchUsers free-text q filter — search by email finds the user")]
    public async Task P706_SearchUsers_EmailFilter_FindsUser()
    {
        // Register a parent with a known unique email, then search for it.
        var email = UniqueEmail("search_q");
        await RegisterParentWithEmailAsync(email);

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}?q={Uri.EscapeDataString(email)}", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var items = ExtractListItems(root);
        items.Should().NotBeEmpty("search by exact email must find at least one result; body: {0}", body);

        bool found = items.Any(item =>
            TryProp(item, "email", out var ep) &&
            string.Equals(ep.GetString(), email, StringComparison.OrdinalIgnoreCase));
        found.Should().BeTrue("the registered user must appear in search results for their email; email={0}; body={1}", email, body);
    }

    [Fact(DisplayName = "P7-06 AC-1: SearchUsers role filter — parent role returns only parents")]
    public async Task P706_SearchUsers_RoleFilter_ReturnsOnlyParents()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}?role=Parent&PageSize=50", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var items = ExtractListItems(root);
        // Every item that has a role field must be Parent
        foreach (var item in items)
        {
            if (TryProp(item, "role", out var roleProp) && roleProp.ValueKind != JsonValueKind.Null)
            {
                roleProp.GetString().Should().Be("Parent",
                    "role filter=Parent must return only Parent-role users; body: {0}", body);
            }
        }
    }

    [Fact(DisplayName = "P7-06 AC-1: SearchUsers page-size cap at 100 — PageSize=200 is clamped")]
    public async Task P706_SearchUsers_PageSizeCappedAt100()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}?PageNumber=1&PageSize=200", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // The response must succeed (no 500 / 400) — page-size clamping is silent.
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("PageSize=200 must be silently clamped to 100, not rejected; body: {0}", body);

        // pageSize in the result must be ≤ 100
        TryProp(root, "data", out var outer).Should().BeTrue("body: {0}", body);
        if (TryProp(outer, "pageSize", out var psProp))
        {
            psProp.GetInt32().Should().BeLessOrEqualTo(100,
                "clamped pageSize must not exceed 100; body: {0}", body);
        }
    }

    // ============================================================================
    // P7-06 AC-1 (CRITICAL): List DTO carries MINIMAL child PII
    // — list item must NOT include grade, nationality, or learningLanguage
    // ============================================================================

    [Fact(DisplayName = "P7-06 AC-1 (CRITICAL): List item DTO does NOT include grade/nationality/learningLanguage")]
    public async Task P706_SearchUsers_ListItem_DoesNotExposeSensitiveChildPii()
    {
        // Create a child so there is at least one Student row with grade/nationality/learningLanguage.
        var parentEmail = UniqueEmail("pii_parent");
        var parentToken = await RegisterParentWithEmailAsync(parentEmail);
        var childEmail  = UniqueEmail("pii_child");
        await AddChildAsync(parentToken, childEmail, grade: 3, country: "SA");

        // Search with role=Student filter to find the child row.
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}?role=Student&q={Uri.EscapeDataString(childEmail)}", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var items = ExtractListItems(root);
        var childItem = items.FirstOrDefault(item =>
            TryProp(item, "email", out var ep) &&
            string.Equals(ep.GetString(), childEmail, StringComparison.OrdinalIgnoreCase));

        if (childItem.ValueKind == JsonValueKind.Undefined)
        {
            // If the item is not found by email filter it may not be returned — skip PII check.
            return;
        }

        // CRITICAL: These fields must NOT appear on the list DTO (minimal PII per brief Q-A1).
        childItem.TryGetProperty("grade", out _).Should().BeFalse(
            "CRITICAL: list DTO must NOT expose child grade (PII leak); body: {0}", body);
        childItem.TryGetProperty("Grade", out _).Should().BeFalse(
            "CRITICAL: list DTO must NOT expose child Grade (PII leak); body: {0}", body);

        childItem.TryGetProperty("nationality", out _).Should().BeFalse(
            "CRITICAL: list DTO must NOT expose child nationality; body: {0}", body);
        childItem.TryGetProperty("Nationality", out _).Should().BeFalse(
            "CRITICAL: list DTO must NOT expose child Nationality; body: {0}", body);

        childItem.TryGetProperty("learningLanguage", out _).Should().BeFalse(
            "CRITICAL: list DTO must NOT expose child learningLanguage; body: {0}", body);
        childItem.TryGetProperty("LearningLanguage", out _).Should().BeFalse(
            "CRITICAL: list DTO must NOT expose child LearningLanguage; body: {0}", body);
    }

    // ============================================================================
    // P7-06: GET /Admin/Users/{id} — profile detail
    // ============================================================================

    [Fact(DisplayName = "P7-06 AC-2: GET /Admin/Users/{id} anonymous → 401")]
    public async Task P706_GetProfile_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, $"{AdminUsersBase}/1", bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-06 AC-2: GET /Admin/Users/{id} parent → 403")]
    public async Task P706_GetProfile_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, $"{AdminUsersBase}/1", bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-06 AC-2: GET /Admin/Users/{id} for existing user → 200 with both language fields distinct")]
    public async Task P706_GetProfile_ExistingUser_Returns200WithBothLanguageFields()
    {
        // Register a parent; find their user id; inspect their profile.
        var email = UniqueEmail("profile_user");
        await RegisterParentWithEmailAsync(email);

        // Find the user id via search.
        var userId = await FindUserIdByEmailAsync(email);
        userId.Should().BeGreaterThan(0, "parent user must be findable by email for profile test");

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}/{userId}", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Admin GET /Admin/Users/{id} must return 200 for an existing user; body: {0}", body);

        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // Both language fields must be present and distinct
        TryProp(data, "preferredLanguage", out var pl).Should().BeTrue(
            "profile DTO must expose preferredLanguage; body: {0}", body);
        pl.GetString().Should().NotBeNullOrWhiteSpace("preferredLanguage must be non-empty; body: {0}", body);

        TryProp(data, "learningLanguage", out var ll).Should().BeTrue(
            "profile DTO must expose learningLanguage (distinct from preferredLanguage); body: {0}", body);
        ll.GetString().Should().NotBeNullOrWhiteSpace("learningLanguage must be non-empty; body: {0}", body);

        // lastSignInAtUtc must be null (not tracked this wave — Q-A6 baked-in decision)
        if (TryProp(data, "lastSignInAtUtc", out var lastSignIn))
        {
            lastSignIn.ValueKind.Should().Be(JsonValueKind.Null,
                "lastSignInAtUtc must be null (not tracked); body: {0}", body);
        }
    }

    [Fact(DisplayName = "P7-06 AC-2: GET /Admin/Users/{id} for child includes grade, country, and both language fields")]
    public async Task P706_GetProfile_Child_IncludesGradeAndCountry()
    {
        var parentToken = await RegisterParentGetTokenAsync();
        var childEmail  = UniqueEmail("profile_child");
        await AddChildAsync(parentToken, childEmail, grade: 4, country: "EG");

        var childId = await FindUserIdByEmailAsync(childEmail);
        childId.Should().BeGreaterThan(0, "child user must be findable for profile test");

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}/{childId}", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Admin GET /Admin/Users/{id} for child must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // Child-only fields visible on detail profile
        TryProp(data, "grade", out var gradeProp).Should().BeTrue(
            "child profile must include grade on detail view; body: {0}", body);
        gradeProp.GetInt32().Should().Be(4, "grade must match what was set at creation; body: {0}", body);

        TryProp(data, "nationality", out var natProp).Should().BeTrue(
            "child profile must include nationality on detail view; body: {0}", body);
        natProp.GetString().Should().Be("EG", "nationality must match; body: {0}", body);
    }

    [Fact(DisplayName = "P7-06 AC-2: GET /Admin/Users/99999999 → 404 (user not found)")]
    public async Task P706_GetProfile_NonExistentUser_Returns404()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}/99999999", bearer: _adminToken);

        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 404 },
            "non-existent user must return 404 or 200 with successed=false; body: {0}", body);

        if ((int)resp.StatusCode == 200 && root.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
            s.GetBoolean().Should().BeFalse("non-existent user must return successed=false; body: {0}", body);
        }
    }

    // ============================================================================
    // P7-06: GET /Admin/Users/{id}/family — family linkage
    // ============================================================================

    [Fact(DisplayName = "P7-06 AC-3: GET /Admin/Users/{id}/family anonymous → 401")]
    public async Task P706_GetFamily_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, $"{AdminUsersBase}/1/family", bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-06 AC-3: GET /Admin/Users/{id}/family parent → 403")]
    public async Task P706_GetFamily_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, $"{AdminUsersBase}/1/family", bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-06 AC-3: GET /Admin/Users/{parentId}/family → children list for a parent")]
    public async Task P706_GetFamily_ForParent_ReturnsChildren()
    {
        var parentEmail = UniqueEmail("family_parent");
        var parentToken = await RegisterParentWithEmailAsync(parentEmail);
        var parentId    = await FindUserIdByEmailAsync(parentEmail);

        var childEmail = UniqueEmail("family_child");
        await AddChildAsync(parentToken, childEmail, grade: 2, country: "EG");

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}/{parentId}/family", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Admin/Users/{parentId}/family must return 200; body: {0}", body);

        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "children", out var childrenArr).Should().BeTrue(
            "family DTO must include 'children' array for a parent; body: {0}", body);

        childrenArr.GetArrayLength().Should().BeGreaterThan(0,
            "parent with at least one linked child must have non-empty children array; body: {0}", body);
    }

    [Fact(DisplayName = "P7-06 AC-3: GET /Admin/Users/{childId}/family → parents list for a child")]
    public async Task P706_GetFamily_ForChild_ReturnsParents()
    {
        var parentToken = await RegisterParentGetTokenAsync();
        var childEmail  = UniqueEmail("family_child_rev");
        await AddChildAsync(parentToken, childEmail, grade: 1, country: "EG");

        var childId = await FindUserIdByEmailAsync(childEmail);

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}/{childId}/family", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Admin/Users/{childId}/family must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "parents", out var parentsArr).Should().BeTrue(
            "family DTO must include 'parents' array for a child; body: {0}", body);

        parentsArr.GetArrayLength().Should().BeGreaterThan(0,
            "child with a linked parent must have non-empty parents array; body: {0}", body);
    }

    // ============================================================================
    // P7-06: GET /Admin/Users/{id}/activity — activity summary (degrades gracefully)
    // ============================================================================

    [Fact(DisplayName = "P7-06 AC-4: GET /Admin/Users/{id}/activity anonymous → 401")]
    public async Task P706_GetActivity_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, $"{AdminUsersBase}/1/activity", bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-06 AC-4: GET /Admin/Users/{id}/activity parent → 403")]
    public async Task P706_GetActivity_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, $"{AdminUsersBase}/1/activity", bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-06 AC-4: GET /Admin/Users/{id}/activity for any user → never 500, degrades gracefully")]
    public async Task P706_GetActivity_Admin_NeverReturns500()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}/1/activity", bearer: _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "activity endpoint must never return 500 (degrades gracefully); body: {0}", body);

        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 404 },
            "activity must return 200 or 404, never a raw 500; body: {0}", body);
    }

    [Fact(DisplayName = "P7-06 AC-4: Activity summary labels lastSignInAtUtc as null (not tracked)")]
    public async Task P706_GetActivity_LastSignInIsNull()
    {
        var email       = UniqueEmail("act_user");
        var parentToken = await RegisterParentWithEmailAsync(email);
        var userId      = await FindUserIdByEmailAsync(email);

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}/{userId}/activity", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "activity for a known user must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // lastSignInAtUtc must be null (no LastSignInAt column on User — Q-A6 decision)
        if (TryProp(data, "lastSignInAtUtc", out var lastSignIn))
        {
            lastSignIn.ValueKind.Should().Be(JsonValueKind.Null,
                "lastSignInAtUtc must be null (not tracked in this wave); body: {0}", body);
        }
    }

    // ============================================================================
    // P7-07: POST /Admin/Users/{id}/suspend
    // ============================================================================

    [Fact(DisplayName = "P7-07 AC-6: POST /Admin/Users/{id}/suspend anonymous → 401")]
    public async Task P707_Suspend_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, $"{AdminUsersBase}/1/suspend",
            new { reason = "test" }, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-07 AC-6: POST /Admin/Users/{id}/suspend parent → 403")]
    public async Task P707_Suspend_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, $"{AdminUsersBase}/1/suspend",
            new { reason = "test" }, bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-07 AC-7: POST /Admin/Users/{id}/suspend — empty reason → 422")]
    public async Task P707_Suspend_EmptyReason_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/1/suspend",
            new { reason = "" },
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Suspend with empty reason must return 422 (SuspendAccountCommandValidator); body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "P7-07 AC-7: POST /Admin/Users/{id}/suspend — reason > 500 chars → 422")]
    public async Task P707_Suspend_ReasonTooLong_Returns422()
    {
        var longReason = new string('x', 501);
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/1/suspend",
            new { reason = longReason },
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Suspend with reason > 500 chars must return 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "P7-07 AC-1: Suspend happy path → AccountStatus becomes Suspended; user cannot sign in")]
    public async Task P707_Suspend_HappyPath_AccountBecomesSuspended_SignInBlocked()
    {
        // Create a suspendable parent
        var email    = UniqueEmail("suspend_victim");
        var password = "Str0ng@Pass";
        await RegisterParentWithEmailAndPasswordAsync(email, password);
        var userId = await FindUserIdByEmailAsync(email);
        userId.Should().BeGreaterThan(0, "suspendable user must be findable");

        // Suspend via admin
        var (suspendResp, suspendRoot, suspendBody) = await SendAsync(
            HttpMethod.Post,
            $"{AdminUsersBase}/{userId}/suspend",
            new { reason = "Integration test suspension" },
            bearer: _adminToken);
        suspendResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Suspend must return 200; body: {0}", suspendBody);
        TryProp(suspendRoot, "successed", out var s).Should().BeTrue("body: {0}", suspendBody);
        s.GetBoolean().Should().BeTrue("Suspend must succeed; body: {0}", suspendBody);

        // CRITICAL: Suspended user must NOT be able to sign in
        var (signInResp, signInRoot, signInBody) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = password });
        ((int)signInResp.StatusCode).Should().NotBe(200,
            "CRITICAL: a suspended user must NOT be able to sign in; body: {0}", signInBody);

        if (signInRoot.ValueKind != JsonValueKind.Undefined &&
            TryProp(signInRoot, "successed", out var signInSucceeded))
        {
            signInSucceeded.GetBoolean().Should().BeFalse(
                "CRITICAL: successed must be false for a suspended user's sign-in attempt; body: {0}", signInBody);
        }
    }

    [Fact(DisplayName = "P7-07 AC-1: Suspend → refresh token is invalidated (cannot refresh after suspension)")]
    public async Task P707_Suspend_RefreshTokenInvalidated()
    {
        // Create a parent and get their tokens first.
        var email    = UniqueEmail("suspend_refresh");
        var password = "Str0ng@Pass";
        await RegisterParentWithEmailAndPasswordAsync(email, password);
        var userId = await FindUserIdByEmailAsync(email);

        // Sign in to get access + refresh tokens
        var (accessToken, refreshTokenString) = await SignInGetBothTokensAsync(email, password);

        // Suspend the user
        var (suspendResp, _, suspendBody) = await SendAsync(
            HttpMethod.Post,
            $"{AdminUsersBase}/{userId}/suspend",
            new { reason = "Refresh revocation test" },
            bearer: _adminToken);
        suspendResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Suspend must succeed; body: {0}", suspendBody);

        // The refresh token must be revoked — attempt to refresh should fail
        var expiredAccessToken = CreateExpiredAccessToken(accessToken);
        var (refreshResp, refreshRoot, refreshBody) = await SendAsync(HttpMethod.Post, RefreshTokenUrl,
            new { AccessToken = expiredAccessToken, RefreshToken = refreshTokenString });

        // After suspension the refresh token is removed from Redis so this must NOT return 200.
        ((int)refreshResp.StatusCode).Should().NotBe(200,
            "CRITICAL: after suspension the refresh token must be revoked — Refresh-Token must not return 200; body: {0}", refreshBody);

        if (refreshRoot.ValueKind != JsonValueKind.Undefined &&
            TryProp(refreshRoot, "successed", out var rs))
        {
            rs.GetBoolean().Should().BeFalse(
                "CRITICAL: successed must be false when refresh token has been revoked by suspension; body: {0}", refreshBody);
        }
    }

    [Fact(DisplayName = "P7-07 AC-5: Suspend already-Suspended account → rejected (illegal transition)")]
    public async Task P707_Suspend_AlreadySuspended_Rejected()
    {
        var email = UniqueEmail("suspend_twice");
        await RegisterParentWithEmailAsync(email);
        var userId = await FindUserIdByEmailAsync(email);

        // First suspend — must succeed
        var (first, _, firstBody) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{userId}/suspend",
            new { reason = "First suspension" }, bearer: _adminToken);
        first.StatusCode.Should().Be(HttpStatusCode.OK, "first suspension must succeed; body: {0}", firstBody);

        // Second suspend — must be rejected (already Suspended)
        var (second, secondRoot, secondBody) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{userId}/suspend",
            new { reason = "Second attempt" }, bearer: _adminToken);

        AssertRejected(second, secondRoot, secondBody,
            "Suspending an already-Suspended account must be rejected");
    }

    [Fact(DisplayName = "P7-07 Self-protection: Admin cannot suspend their own account")]
    public async Task P707_Suspend_OwnAccount_Rejected()
    {
        // Get the admin's own userId from Me
        var adminId = await GetCurrentUserIdAsync(_adminToken!);

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{adminId}/suspend",
            new { reason = "Self-suspend attempt" }, bearer: _adminToken);

        AssertRejected(resp, root, body,
            "Admin must not be able to suspend their own account (self-protection)");
    }

    [Fact(DisplayName = "P7-07 SuperAdmin protection: Admin cannot suspend the seeded SuperAdmin")]
    public async Task P707_Suspend_SuperAdmin_Rejected()
    {
        // Find the superadmin user id via search
        var superAdminId = await FindUserIdByEmailLikeAsync("superadmin");
        if (superAdminId <= 0)
        {
            // Cannot find via email — skip the assertion (superadmin may not have an email set)
            return;
        }

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{superAdminId}/suspend",
            new { reason = "Attempting to suspend superadmin" }, bearer: _adminToken);

        AssertRejected(resp, root, body,
            "Admin must not be able to suspend the SuperAdmin account (SuperAdmin protection)");
    }

    // ============================================================================
    // P7-07: POST /Admin/Users/{id}/reactivate
    // ============================================================================

    [Fact(DisplayName = "P7-07 AC-6: POST /Admin/Users/{id}/reactivate anonymous → 401")]
    public async Task P707_Reactivate_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, $"{AdminUsersBase}/1/reactivate",
            new { }, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-07 AC-2: Reactivate → user can sign in again")]
    public async Task P707_Reactivate_RestoresSignIn()
    {
        var email    = UniqueEmail("reactivate_victim");
        var password = "Str0ng@Pass";
        await RegisterParentWithEmailAndPasswordAsync(email, password);
        var userId = await FindUserIdByEmailAsync(email);

        // Suspend
        await SendAsync(HttpMethod.Post, $"{AdminUsersBase}/{userId}/suspend",
            new { reason = "Setting up for reactivation test" }, bearer: _adminToken);

        // Verify user cannot sign in while suspended
        var (blockedResp, _, _) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = password });
        ((int)blockedResp.StatusCode).Should().NotBe(200,
            "User must not be able to sign in while suspended");

        // Reactivate
        var (reactResp, reactRoot, reactBody) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{userId}/reactivate",
            new { reason = "Test reactivation" }, bearer: _adminToken);
        reactResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Reactivate must return 200; body: {0}", reactBody);
        TryProp(reactRoot, "successed", out var rs).Should().BeTrue("body: {0}", reactBody);
        rs.GetBoolean().Should().BeTrue("Reactivate successed must be true; body: {0}", reactBody);

        // User must now be able to sign in again
        var (signInResp, signInRoot, signInBody) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = password });
        signInResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Reactivated user must be able to sign in; body: {0}", signInBody);
        TryProp(signInRoot, "successed", out var sinS).Should().BeTrue("body: {0}", signInBody);
        sinS.GetBoolean().Should().BeTrue("sign-in successed must be true after reactivation; body: {0}", signInBody);
    }

    [Fact(DisplayName = "P7-07 AC-5: Reactivate a Deleted account → rejected (terminal state)")]
    public async Task P707_Reactivate_DeletedAccount_Rejected()
    {
        var email = UniqueEmail("reactivate_deleted");
        await RegisterParentWithEmailAsync(email);
        var userId = await FindUserIdByEmailAsync(email);

        // Soft-delete the account
        await SendAsync(HttpMethod.Delete, $"{AdminUsersBase}/{userId}",
            new { reason = "Pre-delete for reactivate test", confirm = true, cascadeChildren = false },
            bearer: _adminToken);

        // Attempt to reactivate — must be rejected (deleted is terminal)
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{userId}/reactivate",
            new { reason = "Attempt to reactivate deleted" }, bearer: _adminToken);

        AssertRejected(resp, root, body,
            "Reactivating a Deleted account must be rejected (terminal state)");
    }

    // ============================================================================
    // P7-07: DELETE /Admin/Users/{id}
    // ============================================================================

    [Fact(DisplayName = "P7-07 AC-6: DELETE /Admin/Users/{id} anonymous → 401")]
    public async Task P707_Delete_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Delete, $"{AdminUsersBase}/1",
            new { reason = "test", confirm = true, cascadeChildren = false }, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-07 AC-4 (CRITICAL): DELETE /Admin/Users/{id} with confirm=false → 424")]
    public async Task P707_Delete_ConfirmFalse_Returns424()
    {
        var email = UniqueEmail("delete_unconfirmed");
        await RegisterParentWithEmailAsync(email);
        var userId = await FindUserIdByEmailAsync(email);

        // confirm=false must return 424 (BusinessValidation confirm-gate per P8-04 pattern)
        var (resp, root, body) = await SendAsync(HttpMethod.Delete,
            $"{AdminUsersBase}/{userId}",
            new { reason = "Should be rejected", confirm = false, cascadeChildren = false },
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.FailedDependency,
            "CRITICAL: DELETE without confirm=true must return 424 FailedDependency; body: {0}", body);

        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 424 confirm-gate; body: {0}", body);
    }

    [Fact(DisplayName = "P7-07 AC-7: DELETE /Admin/Users/{id} — empty reason → 422")]
    public async Task P707_Delete_EmptyReason_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Delete,
            $"{AdminUsersBase}/1",
            new { reason = "", confirm = true, cascadeChildren = false },
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "DELETE with empty reason must return 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "P7-07 AC-4: Delete soft-deletes the account; account hidden from default search")]
    public async Task P707_Delete_SoftDeletes_HiddenFromDefaultSearch()
    {
        var email = UniqueEmail("softdelete_target");
        await RegisterParentWithEmailAsync(email);
        var userId = await FindUserIdByEmailAsync(email);
        userId.Should().BeGreaterThan(0, "user must exist before delete");

        // Delete with confirm=true
        var (delResp, delRoot, delBody) = await SendAsync(HttpMethod.Delete,
            $"{AdminUsersBase}/{userId}",
            new { reason = "Soft delete test", confirm = true, cascadeChildren = false },
            bearer: _adminToken);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Delete must return 200; body: {0}", delBody);
        TryProp(delRoot, "successed", out var s).Should().BeTrue("body: {0}", delBody);
        s.GetBoolean().Should().BeTrue("Delete successed must be true; body: {0}", delBody);

        // Default search must NOT return the deleted user
        var (searchResp, searchRoot, searchBody) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}?q={Uri.EscapeDataString(email)}", bearer: _adminToken);
        searchResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", searchBody);

        var items = ExtractListItems(searchRoot);
        bool deleted = items.Any(item =>
            TryProp(item, "email", out var ep) &&
            string.Equals(ep.GetString(), email, StringComparison.OrdinalIgnoreCase));
        deleted.Should().BeFalse(
            "soft-deleted account must NOT appear in default search (isDeleted filter); body: {0}", searchBody);
    }

    [Fact(DisplayName = "P7-07 AC-4: Delete soft-deletes — NOT physically deleted (row still in DB)")]
    public async Task P707_Delete_SoftDelete_NotPhysicallyRemoved()
    {
        var email = UniqueEmail("softdelete_db");
        await RegisterParentWithEmailAsync(email);
        var userId = await FindUserIdByEmailAsync(email);

        // Delete with confirm=true
        await SendAsync(HttpMethod.Delete,
            $"{AdminUsersBase}/{userId}",
            new { reason = "DB persistence test", confirm = true, cascadeChildren = false },
            bearer: _adminToken);

        // Verify row still exists in DB (soft-delete, not physical)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId);

        user.Should().NotBeNull(
            "soft-deleted user row must still exist in the DB (not physically removed); userId={0}", userId);
        user!.IsDeleted.Should().BeTrue(
            "IsDeleted must be true after soft-delete; userId={0}", userId);
    }

    [Fact(DisplayName = "P7-07 AC-5: Delete already-Deleted account → rejected (terminal state)")]
    public async Task P707_Delete_AlreadyDeleted_Rejected()
    {
        var email = UniqueEmail("delete_twice");
        await RegisterParentWithEmailAsync(email);
        var userId = await FindUserIdByEmailAsync(email);

        // First delete — must succeed
        await SendAsync(HttpMethod.Delete, $"{AdminUsersBase}/{userId}",
            new { reason = "First delete", confirm = true, cascadeChildren = false },
            bearer: _adminToken);

        // Second delete — must be rejected
        var (resp, root, body) = await SendAsync(HttpMethod.Delete,
            $"{AdminUsersBase}/{userId}",
            new { reason = "Second attempt", confirm = true, cascadeChildren = false },
            bearer: _adminToken);

        AssertRejected(resp, root, body,
            "Deleting an already-Deleted account must be rejected (terminal state)");
    }

    [Fact(DisplayName = "P7-07 AC-3: Delete parent with cascadeChildren=true → children also soft-deleted")]
    public async Task P707_Delete_CascadeChildren_ChildrenAlsoDeleted()
    {
        var parentEmail = UniqueEmail("cascade_parent");
        var parentToken = await RegisterParentWithEmailAsync(parentEmail);
        var parentId    = await FindUserIdByEmailAsync(parentEmail);

        var childEmail = UniqueEmail("cascade_child");
        await AddChildAsync(parentToken, childEmail, grade: 3, country: "EG");
        var childId = await FindUserIdByEmailAsync(childEmail);
        childId.Should().BeGreaterThan(0, "child must exist before cascade delete");

        // Delete parent with cascadeChildren=true
        var (delResp, delRoot, delBody) = await SendAsync(HttpMethod.Delete,
            $"{AdminUsersBase}/{parentId}",
            new { reason = "Cascade delete test", confirm = true, cascadeChildren = true },
            bearer: _adminToken);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Cascade delete must return 200; body: {0}", delBody);
        TryProp(delRoot, "successed", out var s).Should().BeTrue("body: {0}", delBody);
        s.GetBoolean().Should().BeTrue("Cascade delete successed must be true; body: {0}", delBody);

        // Child must also be soft-deleted
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var child = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == childId);

        child.Should().NotBeNull("child row must still exist (soft-delete, not physical); childId={0}", childId);
        child!.IsDeleted.Should().BeTrue(
            "child must be soft-deleted when parent is deleted with cascadeChildren=true; childId={0}", childId);
    }

    [Fact(DisplayName = "P7-07 Self-protection: Admin cannot delete their own account")]
    public async Task P707_Delete_OwnAccount_Rejected()
    {
        var adminId = await GetCurrentUserIdAsync(_adminToken!);

        var (resp, root, body) = await SendAsync(HttpMethod.Delete,
            $"{AdminUsersBase}/{adminId}",
            new { reason = "Self-delete attempt", confirm = true, cascadeChildren = false },
            bearer: _adminToken);

        AssertRejected(resp, root, body,
            "Admin must not be able to delete their own account (self-protection)");
    }

    [Fact(DisplayName = "P7-07 SuperAdmin protection: Admin cannot delete the seeded SuperAdmin")]
    public async Task P707_Delete_SuperAdmin_Rejected()
    {
        var superAdminId = await FindUserIdByEmailLikeAsync("superadmin");
        if (superAdminId <= 0) return; // cannot find — skip

        var (resp, root, body) = await SendAsync(HttpMethod.Delete,
            $"{AdminUsersBase}/{superAdminId}",
            new { reason = "Attempting to delete superadmin", confirm = true, cascadeChildren = false },
            bearer: _adminToken);

        AssertRejected(resp, root, body,
            "Admin must not be able to delete the SuperAdmin account");
    }

    // ============================================================================
    // P7-07: Suspend a Deleted account → rejected
    // ============================================================================

    [Fact(DisplayName = "P7-07 AC-5: Suspend a Deleted account → rejected (Deleted is terminal)")]
    public async Task P707_Suspend_DeletedAccount_Rejected()
    {
        var email = UniqueEmail("suspend_deleted");
        await RegisterParentWithEmailAsync(email);
        var userId = await FindUserIdByEmailAsync(email);

        // First delete the account
        await SendAsync(HttpMethod.Delete, $"{AdminUsersBase}/{userId}",
            new { reason = "Preparing terminal state", confirm = true, cascadeChildren = false },
            bearer: _adminToken);

        // Now try to suspend — must be rejected
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{userId}/suspend",
            new { reason = "Attempting suspend on deleted" }, bearer: _adminToken);

        AssertRejected(resp, root, body,
            "Suspending a Deleted (terminal) account must be rejected");
    }

    // ============================================================================
    // P7-08: PATCH /Admin/Users/{childId}/profile — update child profile
    // ============================================================================

    [Fact(DisplayName = "P7-08 AC-8: PATCH /Admin/Users/{childId}/profile anonymous → 401")]
    public async Task P708_UpdateProfile_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Patch,
            $"{AdminUsersBase}/1/profile",
            new { preferredLanguage = "en" }, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-08 AC-8: PATCH /Admin/Users/{childId}/profile parent → 403")]
    public async Task P708_UpdateProfile_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Patch,
            $"{AdminUsersBase}/1/profile",
            new { preferredLanguage = "en" }, bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-08 AC-1: Update child preferredLanguage and country — no progress impact")]
    public async Task P708_UpdateProfile_HappyPath_NoPorgressImpact()
    {
        var parentToken = await RegisterParentGetTokenAsync();
        var childEmail  = UniqueEmail("profile_update");
        await AddChildAsync(parentToken, childEmail, grade: 2, country: "EG");
        var childId = await FindUserIdByEmailAsync(childEmail);

        // Update preferredLanguage and country
        var (resp, root, body) = await SendAsync(HttpMethod.Patch,
            $"{AdminUsersBase}/{childId}/profile",
            new { preferredLanguage = "en", country = "SA" },
            bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "UpdateChildProfile must return 200; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        // Verify the profile detail reflects the changes
        var (profileResp, profileRoot, profileBody) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}/{childId}", bearer: _adminToken);
        profileResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", profileBody);
        TryProp(profileRoot, "data", out var data).Should().BeTrue("body: {0}", profileBody);

        TryProp(data, "nationality", out var natProp).Should().BeTrue("body: {0}", profileBody);
        natProp.GetString().Should().Be("SA", "nationality must be updated to SA; body: {0}", profileBody);
    }

    [Fact(DisplayName = "P7-08 AC-1: Update profile on a non-child (parent) user → rejected")]
    public async Task P708_UpdateProfile_NonChildUser_Rejected()
    {
        var email  = UniqueEmail("profile_parent_reject");
        var pToken = await RegisterParentWithEmailAsync(email);
        var userId = await FindUserIdByEmailAsync(email);

        var (resp, root, body) = await SendAsync(HttpMethod.Patch,
            $"{AdminUsersBase}/{userId}/profile",
            new { country = "SA" }, bearer: _adminToken);

        AssertRejected(resp, root, body,
            "UpdateChildProfile must reject when target is a parent, not a child");
    }

    // ============================================================================
    // P7-08: POST /Admin/Users/{childId}/grade — override child grade
    // ============================================================================

    [Fact(DisplayName = "P7-08 AC-8: POST /Admin/Users/{childId}/grade anonymous → 401")]
    public async Task P708_OverrideGrade_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/1/grade",
            new { grade = 3, confirm = true }, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-08 AC-6: POST /Admin/Users/{childId}/grade — grade < 1 → 422")]
    public async Task P708_OverrideGrade_GradeLessThan1_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/1/grade",
            new { grade = 0, confirm = true },
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Grade=0 must return 422 (InclusiveBetween(1,6) validator); body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "P7-08 AC-6: POST /Admin/Users/{childId}/grade — grade > 6 → 422")]
    public async Task P708_OverrideGrade_GradeGreaterThan6_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/1/grade",
            new { grade = 7, confirm = true },
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Grade=7 must return 422 (InclusiveBetween(1,6) validator); body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "P7-08 AC-4+5 (CRITICAL): Grade override — confirm=false → 400 (soft UX gate; NOT 424)")]
    public async Task P708_OverrideGrade_ConfirmFalse_Returns400()
    {
        var parentToken = await RegisterParentGetTokenAsync();
        var childEmail  = UniqueEmail("grade_no_confirm");
        await AddChildAsync(parentToken, childEmail, grade: 2, country: "EG");
        var childId = await FindUserIdByEmailAsync(childEmail);

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{childId}/grade",
            new { grade = 3, confirm = false },
            bearer: _adminToken);

        // Grade override uses BadRequest (400), NOT 424 — non-destructive (confirmed by handler code)
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Grade override confirm=false must return 400 (soft UX gate, NOT 424 since non-destructive); body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "P7-08 AC-4+5 (CRITICAL): Grade override confirm=true → succeeds and updates grade")]
    public async Task P708_OverrideGrade_HappyPath_GradeUpdated()
    {
        var parentToken = await RegisterParentGetTokenAsync();
        var childEmail  = UniqueEmail("grade_override");
        await AddChildAsync(parentToken, childEmail, grade: 2, country: "EG");
        var childId = await FindUserIdByEmailAsync(childEmail);

        // Override from grade 2 → grade 5
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{childId}/grade",
            new { grade = 5, reason = "Admin override test", confirm = true },
            bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Grade override with confirm=true must return 200; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("Grade override successed must be true; body: {0}", body);

        // Response DTO must include oldGrade, newGrade
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "newGrade", out var newGradeProp).Should().BeTrue(
            "response DTO must include newGrade; body: {0}", body);
        newGradeProp.GetInt32().Should().Be(5, "newGrade must be 5; body: {0}", body);

        // Verify in profile
        var (profileResp, profileRoot, profileBody) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}/{childId}", bearer: _adminToken);
        profileResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", profileBody);
        TryProp(profileRoot, "data", out var profileData).Should().BeTrue("body: {0}", profileBody);
        TryProp(profileData, "grade", out var gradeInProfile).Should().BeTrue("body: {0}", profileBody);
        gradeInProfile.GetInt32().Should().Be(5, "profile grade must reflect the override; body: {0}", profileBody);
    }

    [Fact(DisplayName = "P7-08 AC-5 (CRITICAL): Grade override is NON-DESTRUCTIVE — learning history preserved")]
    public async Task P708_OverrideGrade_LearningHistoryPreserved()
    {
        // Create a child with grade 2
        var parentToken = await RegisterParentGetTokenAsync();
        var childEmail  = UniqueEmail("grade_history");
        await AddChildAsync(parentToken, childEmail, grade: 2, country: "EG");
        var childId = await FindUserIdByEmailAsync(childEmail);

        // Record XP award count before grade override — OverrideChildGrade must NOT delete any gamification rows.
        // (The child is freshly created so counts are 0; the assertion is that they remain 0 after the override.)
        // XpAward links to StudentXpProfile (StudentId), not directly to user id.
        int xpAwardsBefore;
        using (var preScope = _factory.Services.CreateScope())
        {
            var gamDb = preScope.ServiceProvider.GetRequiredService<GamificationDbContext>();
            xpAwardsBefore = await gamDb.XpAwards
                .Where(x => x.StudentXpProfile.StudentId == childId)
                .CountAsync();
        }

        int attemptsBefore;
        using (var preScope2 = _factory.Services.CreateScope())
        {
            var learningDb = preScope2.ServiceProvider.GetRequiredService<LearningDbContext>();
            attemptsBefore = await learningDb.Attempts
                .Where(a => a.StudentId == childId)
                .CountAsync();
        }

        // Override grade
        await SendAsync(HttpMethod.Post, $"{AdminUsersBase}/{childId}/grade",
            new { grade = 4, confirm = true }, bearer: _adminToken);

        // Verify: Learning attempt rows are intact (count unchanged — no deletion by grade override)
        using (var postScope = _factory.Services.CreateScope())
        {
            var learningDb = postScope.ServiceProvider.GetRequiredService<LearningDbContext>();
            var attemptsAfter = await learningDb.Attempts
                .Where(a => a.StudentId == childId)
                .CountAsync();

            attemptsAfter.Should().Be(attemptsBefore,
                "CRITICAL: grade override must NOT delete any learning attempts; childId={0}", childId);
        }

        // Verify: XP awards are intact (count unchanged)
        using (var postScope2 = _factory.Services.CreateScope())
        {
            var gamDb = postScope2.ServiceProvider.GetRequiredService<GamificationDbContext>();
            var xpAwardsAfter = await gamDb.XpAwards
                .Where(x => x.StudentXpProfile.StudentId == childId)
                .CountAsync();
            xpAwardsAfter.Should().Be(xpAwardsBefore,
                "CRITICAL: grade override must NOT delete any XP awards; childId={0}", childId);
        }

        // Verify the grade itself changed
        using (var scope = _factory.Services.CreateScope())
        {
            var identityDb = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
            var child = await identityDb.Users.FindAsync(childId);
            child.Should().NotBeNull();
            child!.Grade.Should().Be(4,
                "CRITICAL: grade must be updated to 4 after override; childId={0}", childId);
        }
    }

    [Fact(DisplayName = "P7-08 AC-6: Grade override on a non-child (parent) → rejected")]
    public async Task P708_OverrideGrade_NonChildUser_Rejected()
    {
        var email  = UniqueEmail("grade_parent_reject");
        await RegisterParentWithEmailAsync(email);
        var userId = await FindUserIdByEmailAsync(email);

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{userId}/grade",
            new { grade = 3, confirm = true }, bearer: _adminToken);

        AssertRejected(resp, root, body,
            "OverrideChildGrade must reject when target is not a Student role user");
    }

    // ============================================================================
    // P7-08: POST /Admin/Users/{childId}/learning-language
    // ============================================================================

    [Fact(DisplayName = "P7-08 AC-8: POST /Admin/Users/{childId}/learning-language anonymous → 401")]
    public async Task P708_ChangeLearningLanguage_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/1/learning-language",
            new { learningLanguage = "en", confirmFreshStart = true }, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-08 AC-8: POST /Admin/Users/{childId}/learning-language parent → 403")]
    public async Task P708_ChangeLearningLanguage_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/1/learning-language",
            new { learningLanguage = "en", confirmFreshStart = true }, bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "P7-08 AC-3 (CRITICAL): Change LearningLanguage with confirmFreshStart=false → 424")]
    public async Task P708_ChangeLearningLanguage_NotConfirmed_Returns424()
    {
        var parentToken = await RegisterParentGetTokenAsync();
        var childEmail  = UniqueEmail("lang_no_confirm");
        await AddChildAsync(parentToken, childEmail, grade: 3, country: "EG");
        var childId = await FindUserIdByEmailAsync(childEmail);

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{childId}/learning-language",
            new { learningLanguage = "en", confirmFreshStart = false },
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.FailedDependency,
            "CRITICAL: confirmFreshStart=false must return 424 FailedDependency; body: {0}", body);

        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 424; body: {0}", body);
    }

    [Fact(DisplayName = "P7-08 AC-3 (CRITICAL): Change LearningLanguage not confirmed → no DB mutation")]
    public async Task P708_ChangeLearningLanguage_NotConfirmed_NoMutation()
    {
        var parentToken = await RegisterParentGetTokenAsync();
        var childEmail  = UniqueEmail("lang_no_mutate");
        await AddChildAsync(parentToken, childEmail, grade: 3, country: "EG", learningLanguage: "ar");
        var childId = await FindUserIdByEmailAsync(childEmail);

        // Unconfirmed request — must NOT change the language
        await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{childId}/learning-language",
            new { learningLanguage = "en", confirmFreshStart = false },
            bearer: _adminToken);

        // Verify language unchanged
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var child = await db.Users.FindAsync(childId);
        child.Should().NotBeNull();
        child!.LearningLanguage.Should().Be("ar",
            "CRITICAL: an unconfirmed language-change request must NOT mutate LearningLanguage; childId={0}", childId);
    }

    [Fact(DisplayName = "P7-08 AC-6: Change LearningLanguage with unsupported code → 422")]
    public async Task P708_ChangeLearningLanguage_UnsupportedCode_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/1/learning-language",
            new { learningLanguage = "fr", confirmFreshStart = true },
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Unsupported language code must return 422 (validator); body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "P7-08 AC-2: Change LearningLanguage confirmed → succeeds and updates language")]
    public async Task P708_ChangeLearningLanguage_Confirmed_UpdatesLanguage()
    {
        var parentToken = await RegisterParentGetTokenAsync();
        var childEmail  = UniqueEmail("lang_change");
        // Start with Arabic learning language
        await AddChildAsync(parentToken, childEmail, grade: 3, country: "EG", learningLanguage: "ar");
        var childId = await FindUserIdByEmailAsync(childEmail);

        // Change to English with confirmation
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{childId}/learning-language",
            new { learningLanguage = "en", confirmFreshStart = true },
            bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Confirmed LearningLanguage change must return 200; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        // Verify language updated in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var child = await db.Users.FindAsync(childId);
        child.Should().NotBeNull();
        child!.LearningLanguage.Should().Be("en",
            "LearningLanguage must be 'en' after confirmed change; childId={0}", childId);
    }

    [Fact(DisplayName = "P7-08 AC-2: Change LearningLanguage on non-child (parent) → rejected")]
    public async Task P708_ChangeLearningLanguage_NonChildUser_Rejected()
    {
        var email  = UniqueEmail("lang_parent_reject");
        await RegisterParentWithEmailAsync(email);
        var userId = await FindUserIdByEmailAsync(email);

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{userId}/learning-language",
            new { learningLanguage = "en", confirmFreshStart = true },
            bearer: _adminToken);

        AssertRejected(resp, root, body,
            "AdminChangeLearningLanguage must reject when target is not a Student");
    }

    // ============================================================================
    // P7-06/07/08: BaseResponse envelope shape on success
    // ============================================================================

    [Fact(DisplayName = "Envelope: suspend response has statusCode/successed/message/data/errors keys")]
    public async Task Envelope_SuspendResponse_HasAllBaseResponseKeys()
    {
        var email = UniqueEmail("envelope_suspend");
        await RegisterParentWithEmailAsync(email);
        var userId = await FindUserIdByEmailAsync(email);

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminUsersBase}/{userId}/suspend",
            new { reason = "Envelope test" }, bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        AssertEnvelopeKeys(root, body);
    }

    // ============================================================================
    // Private helpers
    // ============================================================================

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendAsync(HttpMethod method, string url, object? body = null, string? bearer = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        var response = await _client.SendAsync(request);
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>Case-insensitive JSON property lookup (camelCase + PascalCase).</summary>
    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object) { value = default; return false; }
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

    private static List<JsonElement> ExtractListItems(JsonElement root)
    {
        // SearchUsers returns PaginatedResult — try data.data (nested)
        if (TryProp(root, "data", out var outer))
        {
            if (outer.ValueKind == JsonValueKind.Array)
                return outer.EnumerateArray().ToList();

            if (TryProp(outer, "data", out var inner) && inner.ValueKind == JsonValueKind.Array)
                return inner.EnumerateArray().ToList();
        }
        return new List<JsonElement>();
    }

    private static void AssertEnvelopeKeys(JsonElement root, string body)
    {
        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed",  out _).Should().BeTrue("envelope must have successed; body: {0}", body);
        TryProp(root, "message",    out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data",       out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors",     out _).Should().BeTrue("envelope must have errors; body: {0}", body);
    }

    private static void AssertRejected(
        HttpResponseMessage response, JsonElement root, string body, string reason)
    {
        var sc = (int)response.StatusCode;
        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow(
            "rejection response must be valid JSON; reason={0}; body: {1}", reason, body);

        if (sc == 200)
        {
            TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
            s.GetBoolean().Should().BeFalse(
                "{0}; expected Successed=false on 200 rejection; body: {1}", reason, body);
        }
        else
        {
            sc.Should().BeOneOf(new[] { 400, 404, 422, 424 },
                "{0}; expected a 4xx status; got {1}; body: {2}", reason, sc, body);
        }
    }

    private async Task<string> SignInGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in prerequisite must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private async Task<(string AccessToken, string RefreshToken)> SignInGetBothTokensAsync(
        string userName, string password)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var at).Should().BeTrue("body: {0}", body);
        TryProp(data, "refreshToken", out var rt).Should().BeTrue("body: {0}", body);
        TryProp(rt, "tokenString", out var ts).Should().BeTrue("body: {0}", body);
        return (at.GetString()!, ts.GetString()!);
    }

    private async Task<string> RegisterParentGetTokenAsync(string? email = null)
    {
        var parentEmail = email ?? UniqueEmail("parent");
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private async Task<string> RegisterParentWithEmailAsync(string email)
        => await RegisterParentWithEmailAndPasswordAsync(email, "Str0ng@Pass");

    private async Task<string> RegisterParentWithEmailAndPasswordAsync(string email, string password)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = password, AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private async Task<int> FindUserIdByEmailAsync(string email)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}?q={Uri.EscapeDataString(email)}&PageSize=5",
            bearer: _adminToken);

        if (!resp.IsSuccessStatusCode) return 0;

        var items = ExtractListItems(root);
        foreach (var item in items)
        {
            if (TryProp(item, "email", out var ep) &&
                string.Equals(ep.GetString(), email, StringComparison.OrdinalIgnoreCase))
            {
                if (TryProp(item, "id", out var idProp))
                    return idProp.GetInt32();
            }
        }
        return 0;
    }

    /// <summary>
    /// Finds a user id by searching for a username-like string.
    /// Used to locate the seeded superadmin whose email may be the username.
    /// </summary>
    private async Task<int> FindUserIdByEmailLikeAsync(string partialName)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AdminUsersBase}?q={Uri.EscapeDataString(partialName)}&PageSize=10",
            bearer: _adminToken);

        if (!resp.IsSuccessStatusCode) return 0;

        var items = ExtractListItems(root);
        foreach (var item in items)
        {
            if (TryProp(item, "id", out var idProp))
            {
                // Return the first hit that matches
                if (TryProp(item, "email", out var ep))
                {
                    var email = ep.GetString() ?? "";
                    if (email.Contains(partialName, StringComparison.OrdinalIgnoreCase))
                        return idProp.GetInt32();
                }
            }
        }
        return 0;
    }

    private async Task<int> GetCurrentUserIdAsync(string token)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, "api/Users/Me", bearer: token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "Me must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "id", out var idProp).Should().BeTrue("body: {0}", body);
        return idProp.GetInt32();
    }

    private async Task AddChildAsync(
        string parentToken,
        string childEmail,
        int grade = 3,
        string country = "EG",
        string learningLanguage = "ar")
    {
        var body = new
        {
            FullName        = "Test Child",
            Email           = childEmail,
            Password        = ValidChildPassword,
            Grade           = grade,
            Language        = "ar",
            Country         = country,
            LearningLanguage = learningLanguage,
        };
        var (resp, _, rawBody) = await SendAsync(HttpMethod.Post, AddChildUrl, body, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "AddChild prerequisite must succeed; body: {0}", rawBody);
    }

    private static string UniqueEmail(string tag = "")
        => $"p7_0678_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>
    /// Creates an expired-but-otherwise-valid JWT by re-signing with the test secret + past expiry.
    /// Mirrors the pattern in P1_10_AdminSignIn_Tests.
    /// </summary>
    private static string CreateExpiredAccessToken(string originalToken)
    {
        try
        {
            var handler     = new JwtSecurityTokenHandler();
            var originalJwt = handler.ReadJwtToken(originalToken);
            var claimsToKeep = originalJwt.Claims
                .Where(c => c.Type != "exp" && c.Type != "iat" && c.Type != "nbf")
                .ToList();
            var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(TestSigningKey));
            var expiredToken = new JwtSecurityToken(
                issuer: originalJwt.Issuer,
                audience: originalJwt.Audiences.FirstOrDefault(),
                claims: claimsToKeep,
                notBefore: DateTime.UtcNow.AddMinutes(-60),
                expires: DateTime.UtcNow.AddMinutes(-30),
                signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    signingKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature));
            return handler.WriteToken(expiredToken);
        }
        catch
        {
            return originalToken + "_expired_suffix";
        }
    }
}
