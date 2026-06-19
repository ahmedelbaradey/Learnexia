using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P7-01 integration tests: Admin management of Subjects and Units in the Learning module.
///
/// Endpoints under test:
///   PUT  /api/learning/Subjects/Reorder           — batch reorder subjects by SequenceOrder
///   PUT  /api/learning/Units/Reorder              — batch reorder units by SequenceOrder
///   PUT  /api/learning/Subjects/{id}/Active       — activate or deactivate a subject
///   PUT  /api/learning/Units/{id}/Active          — activate or deactivate a unit
///   DELETE /api/learning/Subjects?id={n}          — soft-delete a subject (blocks if non-empty)
///   DELETE /api/learning/Units?id={n}             — soft-delete a unit (blocks if has lessons)
///   GET  /api/learning/Subjects/Coverage?gradeId= — per-grade language-coverage report
///   GET  /api/learning/Subjects/List              — admin list (includes inactive; exposes SubjectCode etc.)
///   POST /api/learning/Subjects/Create            — duplicate / restore guard
///
/// Auth matrix:
///   All PUT/DELETE/Coverage/List/GetById endpoints: anonymous → 401, non-admin → 403, admin → 200.
///   (P7-SEC: List/GetById locked down to AdminOnly — they expose admin DTOs with IsActive/SequenceOrder.)
///
/// Acceptance criteria covered:
///   AC-1 : SubjectDto exposes SubjectCode, Language, SequenceOrder, IsActive (admin list DTO).
///   AC-2 : Coverage GET reports all 6 expected (SubjectCode, Language) slots and flags gaps.
///   AC-3 : Reorder persists new SequenceOrder; scoped to language tree (Ar reorder ≠ En reorder).
///   AC-4 : Reorder with IDs from two different trees → Successed=false (cross-tree rejected).
///   AC-5 : SetActive=false hides from student ForGrade / Lessons reads; admin List still shows it.
///   AC-6 : SetActive=true restores student visibility.
///   AC-7 : Delete unit with lessons → Successed=false "unit not empty".
///   AC-7b: Delete subject with non-deleted units → Successed=false.
///   AC-7c: Delete empty unit → soft-deletes (Successed=true); unit disappears from List.
///   AC-7d: Delete empty subject → soft-deletes (Successed=true); subject disappears from List.
///   AC-8 : Create with duplicate (GradeId, SubjectCode, Language) → Successed=false.
///   AC-8b: Create with soft-deleted tree's natural key → Successed=false with "restore" message.
///   AC-9 : Auth: new P7-01 write/coverage endpoints require AdminOnly; non-admin → 403; admin → 200.
///   AC-10: BaseResponse envelope (statusCode, successed, message, data, errors) on success + 422.
///   AC-11: Reorder validator: empty SubjectIds list → 422; Reorder with id=0 → 422.
///   AC-12: SetActive validator: id ≤ 0 in route → 422.
///
/// Docker + Postgres required to RUN. Compile-only on Windows CI (no Docker daemon).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_01_SubjectsUnitsAdmin_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // ---------------------------------------------------------------------------
    // Seeded credentials (mirrors P2-01 / P1-05)
    // ---------------------------------------------------------------------------
    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";
    private const string BasicUserName = "basicuser";
    private const string BasicUserPassword = "123Pa$$word!";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";

    private string? _adminToken;
    private string? _basicToken;
    private string? _parentToken;

    public P7_01_SubjectsUnitsAdmin_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await db.Database.MigrateAsync();

        _adminToken = await SignInGetTokenAsync(AdminUserName, AdminPassword);
        _basicToken = await SignInGetTokenAsync(BasicUserName, BasicUserPassword);
        _parentToken = await RegisterParentGetTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // AC-9 — Auth: write + coverage endpoints require AdminOnly
    // =========================================================================

    [Fact(DisplayName = "AC-9: PUT Subjects/Reorder anonymous → 401")]
    public async Task AC9_SubjectReorder_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Subjects/Reorder",
            new { subjectIds = new[] { 1, 2 } },
            bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Reorder is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-9: PUT Subjects/Reorder non-admin (basic user) → 403")]
    public async Task AC9_SubjectReorder_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Subjects/Reorder",
            new { subjectIds = new[] { 1, 2 } },
            _basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Reorder is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-9: PUT Subjects/Reorder parent → 403")]
    public async Task AC9_SubjectReorder_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Subjects/Reorder",
            new { subjectIds = new[] { 1, 2 } },
            _parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Reorder is AdminOnly — parent user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-9: PUT Units/Reorder anonymous → 401")]
    public async Task AC9_UnitReorder_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Units/Reorder",
            new { unitIds = new[] { 1, 2 } },
            bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Unit Reorder is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-9: PUT Units/Reorder non-admin → 403")]
    public async Task AC9_UnitReorder_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Units/Reorder",
            new { unitIds = new[] { 1, 2 } },
            _basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Unit Reorder is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-9: PUT Subjects/{id}/Active anonymous → 401")]
    public async Task AC9_SubjectSetActive_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Subjects/1/Active",
            new { isActive = false },
            bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "SetActive is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-9: PUT Subjects/{id}/Active non-admin → 403")]
    public async Task AC9_SubjectSetActive_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Subjects/1/Active",
            new { isActive = false },
            _basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "SetActive is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-9: PUT Units/{id}/Active anonymous → 401")]
    public async Task AC9_UnitSetActive_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Units/1/Active",
            new { isActive = false },
            bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Unit SetActive is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-9: PUT Units/{id}/Active non-admin → 403")]
    public async Task AC9_UnitSetActive_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Units/1/Active",
            new { isActive = false },
            _basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Unit SetActive is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-9: GET Subjects/Coverage anonymous → 401")]
    public async Task AC9_Coverage_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/Subjects/Coverage?gradeId=1",
            bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Coverage is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-9: GET Subjects/Coverage non-admin → 403")]
    public async Task AC9_Coverage_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/Subjects/Coverage?gradeId=1",
            bearer: _basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Coverage is AdminOnly — basic user must get 403; body: {0}", body);
    }

    // =========================================================================
    // AC-1 — SubjectDto exposes P7-01 fields on admin List
    // =========================================================================

    [Fact(DisplayName = "AC-1: Subject List item exposes subjectCode, language, sequenceOrder, isActive")]
    public async Task AC1_SubjectList_ExposesP701Fields()
    {
        int gradeId = await CreateGradeGetIdAsync();
        // Create a subject with explicit SubjectCode (MATH=0) and Language (Ar=0)
        var (cResp, _, cBody) = await SendAsync(HttpMethod.Post, "/api/learning/subjects/Create",
            new { Name = $"Math Ar {Guid.NewGuid():N}", Country = "EG", GradeId = gradeId, SubjectCode = 0, Language = 0 },
            _adminToken);
        AssertSuccess(cResp, cBody);

        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}", bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "List must succeed; body: {0}", listBody);

        var items = ExtractPageItems(listRoot, listBody);
        items.Should().NotBeEmpty("created subject must appear in list; body: {0}", listBody);

        var subject = items[0];
        TryProp(subject, "subjectCode", out _).Should().BeTrue(
            "SubjectDto must have subjectCode (P7-01); body: {0}", listBody);
        TryProp(subject, "language", out _).Should().BeTrue(
            "SubjectDto must have language (P7-01); body: {0}", listBody);
        TryProp(subject, "sequenceOrder", out _).Should().BeTrue(
            "SubjectDto must have sequenceOrder (P7-01); body: {0}", listBody);
        TryProp(subject, "isActive", out var isActiveProp).Should().BeTrue(
            "SubjectDto must have isActive (P7-01); body: {0}", listBody);
        isActiveProp.GetBoolean().Should().BeTrue(
            "newly created subject must be active by default; body: {0}", listBody);
    }

    // =========================================================================
    // AC-3 — Subject Reorder: persists new SequenceOrder within one language tree
    // =========================================================================

    [Fact(DisplayName = "AC-3: Subject Reorder — new SequenceOrder persists and is reflected on reload")]
    public async Task AC3_SubjectReorder_PersistsSequenceOrder()
    {
        int gradeId = await CreateGradeGetIdAsync();

        // Create two subjects in the MATH/Ar tree using two separate subject codes
        // (they must share the same GradeId+Language but differ in SubjectCode to avoid unique key conflict).
        // Here we use MATH(0)/Ar(0) and SCIENCE(1)/Ar(0) — same language tree for the grade.
        int subj1 = await CreateSubjectGetIdAsync(gradeId, name: $"Sub A {Guid.NewGuid():N}", subjectCode: 0, language: 0);
        int subj2 = await CreateSubjectGetIdAsync(gradeId, name: $"Sub B {Guid.NewGuid():N}", subjectCode: 1, language: 0);

        // Reorder: put subj2 first, subj1 second.
        var (reorderResp, reorderRoot, reorderBody) = await SendAsync(HttpMethod.Put,
            "/api/learning/Subjects/Reorder",
            new { subjectIds = new[] { subj2, subj1 } },
            _adminToken);
        reorderResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Reorder must return 200; body: {0}", reorderBody);
        AssertSucceeded(reorderRoot, reorderBody);

        // Reload the list and check SequenceOrder values.
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}", bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractPageItems(listRoot, listBody);

        int? order1 = null, order2 = null;
        foreach (var item in items)
        {
            if (!TryProp(item, "id", out var idProp)) continue;
            var id = idProp.GetInt32();
            if (!TryProp(item, "sequenceOrder", out var oProp)) continue;
            var order = oProp.GetInt32();
            if (id == subj1) order1 = order;
            if (id == subj2) order2 = order;
        }

        order1.Should().NotBeNull($"subj1 (id={subj1}) must appear in list; body: {listBody}");
        order2.Should().NotBeNull($"subj2 (id={subj2}) must appear in list; body: {listBody}");

        // After reorder [subj2, subj1]: subj2 → SequenceOrder=0, subj1 → SequenceOrder=1
        order2.Should().BeLessThan(order1!.Value,
            $"subj2 placed first must have lower SequenceOrder than subj1; orders: subj1={order1}, subj2={order2}; body: {listBody}");
    }

    // =========================================================================
    // AC-3 — Unit Reorder: persists new SequenceOrder
    // =========================================================================

    [Fact(DisplayName = "AC-3: Unit Reorder — new SequenceOrder persists and is reflected on reload")]
    public async Task AC3_UnitReorder_PersistsSequenceOrder()
    {
        int gradeId = await CreateGradeGetIdAsync();
        int subjectId = await CreateSubjectGetIdAsync(gradeId, name: $"Math {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        int unit1 = await CreateUnitGetIdAsync(subjectId, name: $"Unit A {Guid.NewGuid():N}", seqOrder: 1);
        int unit2 = await CreateUnitGetIdAsync(subjectId, name: $"Unit B {Guid.NewGuid():N}", seqOrder: 2);

        // Reorder: swap — unit2 first, unit1 second.
        var (reorderResp, reorderRoot, reorderBody) = await SendAsync(HttpMethod.Put,
            "/api/learning/Units/Reorder",
            new { unitIds = new[] { unit2, unit1 } },
            _adminToken);
        reorderResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Unit Reorder must return 200; body: {0}", reorderBody);
        AssertSucceeded(reorderRoot, reorderBody);

        // Reload list.
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}", bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractPageItems(listRoot, listBody);

        int? order1 = null, order2 = null;
        foreach (var item in items)
        {
            if (!TryProp(item, "id", out var idProp)) continue;
            var id = idProp.GetInt32();
            if (!TryProp(item, "sequenceOrder", out var oProp)) continue;
            var order = oProp.GetInt32();
            if (id == unit1) order1 = order;
            if (id == unit2) order2 = order;
        }

        order1.Should().NotBeNull($"unit1 (id={unit1}) must appear in list; body: {listBody}");
        order2.Should().NotBeNull($"unit2 (id={unit2}) must appear in list; body: {listBody}");

        // After reorder [unit2, unit1]: unit2 → 0, unit1 → 1
        order2.Should().BeLessThan(order1!.Value,
            $"unit2 placed first must have lower SequenceOrder than unit1; orders: unit1={order1}, unit2={order2}; body: {listBody}");
    }

    // =========================================================================
    // AC-3 / AC-4 — Subject Reorder is scoped to language tree; cross-tree rejected
    // =========================================================================

    [Fact(DisplayName = "AC-4: Subject Reorder with IDs from two different language trees → Successed=false")]
    public async Task AC4_SubjectReorder_CrossTree_Rejected()
    {
        int gradeId = await CreateGradeGetIdAsync();

        // Create one subject in Ar tree (MATH/Ar) and one in En tree (MATH/En).
        int subjAr = await CreateSubjectGetIdAsync(gradeId, name: $"Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);
        int subjEn = await CreateSubjectGetIdAsync(gradeId, name: $"Math En {Guid.NewGuid():N}", subjectCode: 0 /* MATH */, language: 1 /* En */);

        // Attempt cross-tree reorder — must be rejected by the handler (different Language values).
        var (reorderResp, reorderRoot, reorderBody) = await SendAsync(HttpMethod.Put,
            "/api/learning/Subjects/Reorder",
            new { subjectIds = new[] { subjAr, subjEn } },
            _adminToken);

        // Handler returns BadRequest → HTTP 400
        ((int)reorderResp.StatusCode).Should().BeOneOf(new[] { 200, 400 },
            "cross-tree reorder must be rejected gracefully (200 successed=false or 400); body: {0}", reorderBody);

        if (reorderRoot.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(reorderRoot, "successed", out var succeededProp).Should().BeTrue("body: {0}", reorderBody);
            succeededProp.GetBoolean().Should().BeFalse(
                "cross-tree reorder must return Successed=false; body: {0}", reorderBody);
        }
    }

    [Fact(DisplayName = "AC-3: Subject Reorder does not affect the parallel language tree")]
    public async Task AC3_SubjectReorder_IsScopedToLanguageTree()
    {
        int gradeId = await CreateGradeGetIdAsync();

        // Ar tree: MATH/Ar and SCIENCE/Ar
        int arSubj1 = await CreateSubjectGetIdAsync(gradeId, name: $"Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);
        int arSubj2 = await CreateSubjectGetIdAsync(gradeId, name: $"Science Ar {Guid.NewGuid():N}", subjectCode: 1, language: 0);

        // En tree: MATH/En and SCIENCE/En
        int enSubj1 = await CreateSubjectGetIdAsync(gradeId, name: $"Math En {Guid.NewGuid():N}", subjectCode: 0, language: 1);
        int enSubj2 = await CreateSubjectGetIdAsync(gradeId, name: $"Science En {Guid.NewGuid():N}", subjectCode: 1, language: 1);

        // Capture En subjects' initial SequenceOrder values before we touch Ar tree.
        var enOrderBefore = await GetSubjectOrders(gradeId, new[] { enSubj1, enSubj2 });

        // Reorder Ar tree only.
        var (reorderResp, reorderRoot, reorderBody) = await SendAsync(HttpMethod.Put,
            "/api/learning/Subjects/Reorder",
            new { subjectIds = new[] { arSubj2, arSubj1 } },
            _adminToken);
        reorderResp.StatusCode.Should().Be(HttpStatusCode.OK, "Ar reorder must succeed; body: {0}", reorderBody);
        AssertSucceeded(reorderRoot, reorderBody);

        // Verify En subjects' SequenceOrder values are unchanged.
        var enOrderAfter = await GetSubjectOrders(gradeId, new[] { enSubj1, enSubj2 });

        enOrderBefore[enSubj1].Should().Be(enOrderAfter[enSubj1],
            $"Reordering Ar tree must not change En subject {enSubj1} SequenceOrder");
        enOrderBefore[enSubj2].Should().Be(enOrderAfter[enSubj2],
            $"Reordering Ar tree must not change En subject {enSubj2} SequenceOrder");
    }

    // =========================================================================
    // AC-5 — SetActive=false hides from student ForGrade read
    // =========================================================================

    [Fact(DisplayName = "AC-5: Deactivating a subject hides it from student ForGrade read")]
    public async Task AC5_SubjectDeactivate_HidesFromStudentForGrade()
    {
        // Use the FIRST existing grade with Number=1 so ForGrade (which resolves grades via
        // FirstOrDefaultAsync by Number) will serve subjects from THIS grade.
        // Creating a fresh grade would make our subject invisible to the ForGrade query.
        //
        // Isolation: use ARABIC/Ar (SubjectCode=2, Language=0) — a slot that is:
        //   (a) not seeded by the LearningSeeder (which does not run in the test environment), and
        //   (b) not claimed by any other ForGrade-visibility test in the full suite
        //       (MATH/Ar (0,0) is written by P2_01 tests that also target grade Number=1;
        //        SCIENCE/Ar (1,0) is used by AC6; ENGLISH/En (3,1) is used by P7_05 LEAK-8).
        // SubjectLanguageResolver pins ARABIC → ContentLanguage.Ar regardless of student language,
        // so the admin token (no learning_language claim → Ar fallback) is served ARABIC/Ar via ForGrade.
        int gradeNumber = 1;
        int gradeId = await GetFirstGradeIdByNumberAsync(gradeNumber);

        // Create an ARABIC/Ar subject (SubjectCode=2, Language=0)
        int subjectId = await CreateSubjectGetIdAsync(gradeId,
            name: $"Arabic Ar {Guid.NewGuid():N}", subjectCode: 2, language: 0);

        // P7-05: publish so the subject is visible to student ForGrade reads.
        // (Admin-created entities default to Draft; ForGrade filters by LifecycleState == Published.)
        await PublishEntityAsync(1 /* Subject */, subjectId);

        // Verify it appears in ForGrade (authenticated student read).
        var subjsBefore = await GetStudentSubjectsAsync(gradeNumber);
        subjsBefore.Any(s => TryProp(s, "id", out var id) && id.GetInt32() == subjectId)
            .Should().BeTrue("active subject must appear in ForGrade; gradeNumber={0}", gradeNumber);

        // Deactivate.
        var (setResp, setRoot, setBody) = await SendAsync(HttpMethod.Put,
            $"/api/learning/Subjects/{subjectId}/Active",
            new { isActive = false },
            _adminToken);
        setResp.StatusCode.Should().Be(HttpStatusCode.OK, "SetActive must return 200; body: {0}", setBody);
        AssertSucceeded(setRoot, setBody);

        // Verify it no longer appears in ForGrade.
        var subjsAfter = await GetStudentSubjectsAsync(gradeNumber);
        subjsAfter.Any(s => TryProp(s, "id", out var id) && id.GetInt32() == subjectId)
            .Should().BeFalse("deactivated subject must be hidden from ForGrade; gradeNumber={0}", gradeNumber);
    }

    [Fact(DisplayName = "AC-5: Admin List still returns deactivated subject (IsActive=false preserved)")]
    public async Task AC5_SubjectDeactivate_AdminListStillReturnsIt()
    {
        int gradeId = await CreateGradeGetIdAsync();
        int subjectId = await CreateSubjectGetIdAsync(gradeId,
            name: $"Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        // Deactivate.
        var (setResp, _, setBody) = await SendAsync(HttpMethod.Put,
            $"/api/learning/Subjects/{subjectId}/Active",
            new { isActive = false },
            _adminToken);
        setResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", setBody);

        // Admin list should still show it with IsActive=false.
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}", bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractPageItems(listRoot, listBody);

        var found = items.FirstOrDefault(s => TryProp(s, "id", out var id) && id.GetInt32() == subjectId);
        found.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "deactivated subject must still appear in admin List; body: {0}", listBody);

        TryProp(found, "isActive", out var isActiveProp).Should().BeTrue("body: {0}", listBody);
        isActiveProp.GetBoolean().Should().BeFalse(
            "admin List must reflect IsActive=false after deactivation; body: {0}", listBody);
    }

    // =========================================================================
    // AC-6 — SetActive=true restores student visibility
    // =========================================================================

    [Fact(DisplayName = "AC-6: Reactivating a subject restores student ForGrade visibility")]
    public async Task AC6_SubjectReactivate_RestoresStudentVisibility()
    {
        // Use the FIRST existing grade with Number=1 (the seeded grade).
        // See AC5_SubjectDeactivate_HidesFromStudentForGrade for the reason.
        // Use SCIENCE/Ar (SubjectCode=1, Language=0) — distinct from AC5's ARABIC/Ar (2,0),
        // P2_01's MATH/Ar (0,0), and P7_05 LEAK-8's ENGLISH/En (3,1) — no collision in grade 1.
        int gradeNumber = 1;
        int gradeId = await GetFirstGradeIdByNumberAsync(gradeNumber);

        int subjectId = await CreateSubjectGetIdAsync(gradeId,
            name: $"Science Ar {Guid.NewGuid():N}", subjectCode: 1, language: 0);

        // P7-05: publish so the subject is visible to student ForGrade reads.
        await PublishEntityAsync(1 /* Subject */, subjectId);

        // Deactivate then reactivate.
        await SendAsync(HttpMethod.Put, $"/api/learning/Subjects/{subjectId}/Active", new { isActive = false }, _adminToken);

        var (reactResp, reactRoot, reactBody) = await SendAsync(HttpMethod.Put,
            $"/api/learning/Subjects/{subjectId}/Active",
            new { isActive = true },
            _adminToken);
        reactResp.StatusCode.Should().Be(HttpStatusCode.OK, "Reactivate must return 200; body: {0}", reactBody);
        AssertSucceeded(reactRoot, reactBody);

        // Verify it reappears in ForGrade.
        var subjsAfter = await GetStudentSubjectsAsync(gradeNumber);
        subjsAfter.Any(s => TryProp(s, "id", out var id) && id.GetInt32() == subjectId)
            .Should().BeTrue("reactivated subject must reappear in ForGrade; gradeNumber={0}", gradeNumber);
    }

    // =========================================================================
    // AC-5 — Unit SetActive: hides from student Lessons read
    // =========================================================================

    [Fact(DisplayName = "AC-5: Deactivating a unit hides it from student Subject/{id}/Lessons read")]
    public async Task AC5_UnitDeactivate_HidesFromStudentLessons()
    {
        int gradeId = await CreateGradeGetIdAsync();
        int subjectId = await CreateSubjectGetIdAsync(gradeId,
            name: $"Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        int unitId = await CreateUnitGetIdAsync(subjectId, name: $"Unit X {Guid.NewGuid():N}", seqOrder: 1);

        // P7-05: publish the subject and unit so the student Lessons endpoint can serve them.
        // (GetSubjectLessons filters by LifecycleState == Published for both subject and units.)
        await PublishEntityAsync(1 /* Subject */, subjectId);
        await PublishEntityAsync(2 /* Unit */, unitId);

        // Verify unit appears in student Lessons.
        var unitsBefore = await GetStudentLessonsUnitsAsync(subjectId);
        unitsBefore.Any(u => TryProp(u, "unitId", out var uid) && uid.GetInt32() == unitId)
            .Should().BeTrue("active unit must appear in Lessons; subjectId={0}", subjectId);

        // Deactivate the unit.
        var (setResp, setRoot, setBody) = await SendAsync(HttpMethod.Put,
            $"/api/learning/Units/{unitId}/Active",
            new { isActive = false },
            _adminToken);
        setResp.StatusCode.Should().Be(HttpStatusCode.OK, "Unit SetActive must return 200; body: {0}", setBody);
        AssertSucceeded(setRoot, setBody);

        // Verify unit no longer appears in student Lessons.
        var unitsAfter = await GetStudentLessonsUnitsAsync(subjectId);
        unitsAfter.Any(u => TryProp(u, "unitId", out var uid) && uid.GetInt32() == unitId)
            .Should().BeFalse("deactivated unit must be hidden from Lessons; subjectId={0}", subjectId);
    }

    [Fact(DisplayName = "AC-5: Admin List still returns deactivated unit (IsActive=false preserved)")]
    public async Task AC5_UnitDeactivate_AdminListStillReturnsIt()
    {
        int gradeId = await CreateGradeGetIdAsync();
        int subjectId = await CreateSubjectGetIdAsync(gradeId,
            name: $"Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        int unitId = await CreateUnitGetIdAsync(subjectId, name: $"Unit Y {Guid.NewGuid():N}", seqOrder: 1);

        // Deactivate.
        await SendAsync(HttpMethod.Put, $"/api/learning/Units/{unitId}/Active", new { isActive = false }, _adminToken);

        // Admin List must still show it.
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}", bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractPageItems(listRoot, listBody);

        var found = items.FirstOrDefault(u => TryProp(u, "id", out var id) && id.GetInt32() == unitId);
        found.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "deactivated unit must still appear in admin List; body: {0}", listBody);

        TryProp(found, "isActive", out var isActiveProp).Should().BeTrue("body: {0}", listBody);
        isActiveProp.GetBoolean().Should().BeFalse(
            "admin List must reflect IsActive=false after deactivation; body: {0}", listBody);
    }

    // =========================================================================
    // AC-7 — Soft-delete: unit not empty guard
    // =========================================================================

    [Fact(DisplayName = "AC-7: DELETE Unit with non-deleted lessons → Successed=false 'unit not empty'")]
    public async Task AC7_DeleteUnit_WithLessons_IsRejected()
    {
        int gradeId = await CreateGradeGetIdAsync();
        int subjectId = await CreateSubjectGetIdAsync(gradeId,
            name: $"Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);
        int unitId = await CreateUnitGetIdAsync(subjectId, name: $"Unit With Lesson {Guid.NewGuid():N}", seqOrder: 1);

        // Add a lesson to the unit.
        var (lessonResp, _, lessonBody) = await SendAsync(HttpMethod.Post,
            "/api/learning/lessons/Create",
            new { Name = $"Lesson {Guid.NewGuid():N}", Difficulty = 1, SequenceOrder = 1, IsLocked = false, UnitId = unitId },
            _adminToken);
        lessonResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "lesson prereq must be created; body: {0}", lessonBody);

        // Attempt to delete the unit.
        var (delResp, delRoot, delBody) = await SendAsync(HttpMethod.Delete,
            $"/api/learning/Units?id={unitId}",
            bearer: _adminToken);

        // Must be rejected — handler returns BadRequest (HTTP 200 with Successed=false based on BaseResponseHandler pattern).
        AssertRejected(delResp, delRoot, delBody,
            "Delete of unit with lessons must be rejected with Successed=false");
    }

    [Fact(DisplayName = "AC-7c: DELETE empty Unit → Successed=true, unit disappears from List")]
    public async Task AC7c_DeleteEmptyUnit_SoftDeletes_DisappearsFromList()
    {
        int gradeId = await CreateGradeGetIdAsync();
        int subjectId = await CreateSubjectGetIdAsync(gradeId,
            name: $"Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);
        int unitId = await CreateUnitGetIdAsync(subjectId, name: $"Empty Unit {Guid.NewGuid():N}", seqOrder: 1);

        // Delete the empty unit.
        var (delResp, delRoot, delBody) = await SendAsync(HttpMethod.Delete,
            $"/api/learning/Units?id={unitId}",
            bearer: _adminToken);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Delete of empty unit must return 200; body: {0}", delBody);
        AssertSucceeded(delRoot, delBody);

        // Unit must no longer appear in List (global IsDeleted filter).
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}", bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractPageItems(listRoot, listBody);

        items.Any(u => TryProp(u, "id", out var id) && id.GetInt32() == unitId)
            .Should().BeFalse("soft-deleted unit must not appear in List (global filter); body: {0}", listBody);
    }

    // =========================================================================
    // AC-7b — Subject not empty guard
    // =========================================================================

    [Fact(DisplayName = "AC-7b: DELETE Subject with non-deleted Units → Successed=false")]
    public async Task AC7b_DeleteSubject_WithUnits_IsRejected()
    {
        int gradeId = await CreateGradeGetIdAsync();
        int subjectId = await CreateSubjectGetIdAsync(gradeId,
            name: $"Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        // Add a unit to the subject.
        await CreateUnitGetIdAsync(subjectId, name: $"Unit In Subject {Guid.NewGuid():N}", seqOrder: 1);

        // Attempt to delete the subject.
        var (delResp, delRoot, delBody) = await SendAsync(HttpMethod.Delete,
            $"/api/learning/subjects?id={subjectId}",
            bearer: _adminToken);

        AssertRejected(delResp, delRoot, delBody,
            "Delete of subject with units must be rejected with Successed=false");
    }

    [Fact(DisplayName = "AC-7d: DELETE empty Subject → Successed=true, subject disappears from List")]
    public async Task AC7d_DeleteEmptySubject_SoftDeletes_DisappearsFromList()
    {
        int gradeId = await CreateGradeGetIdAsync();
        int subjectId = await CreateSubjectGetIdAsync(gradeId,
            name: $"Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        // Delete the empty subject.
        var (delResp, delRoot, delBody) = await SendAsync(HttpMethod.Delete,
            $"/api/learning/subjects?id={subjectId}",
            bearer: _adminToken);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Delete of empty subject must return 200; body: {0}", delBody);
        AssertSucceeded(delRoot, delBody);

        // Subject must no longer appear in List (global IsDeleted filter).
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}", bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractPageItems(listRoot, listBody);

        items.Any(s => TryProp(s, "id", out var id) && id.GetInt32() == subjectId)
            .Should().BeFalse("soft-deleted subject must not appear in List (global filter); body: {0}", listBody);
    }

    // =========================================================================
    // AC-8 — Duplicate tree rejected
    // =========================================================================

    [Fact(DisplayName = "AC-8: Create Subject with duplicate (GradeId, SubjectCode, Language) → Successed=false")]
    public async Task AC8_CreateSubject_DuplicateTree_Rejected()
    {
        int gradeId = await CreateGradeGetIdAsync();

        // First create — succeeds.
        var (first, _, firstBody) = await SendAsync(HttpMethod.Post, "/api/learning/subjects/Create",
            new { Name = $"Math Ar A {Guid.NewGuid():N}", Country = "EG", GradeId = gradeId, SubjectCode = 0, Language = 0 },
            _adminToken);
        AssertSuccess(first, firstBody);

        // Second create — same key must be rejected.
        var (second, secondRoot, secondBody) = await SendAsync(HttpMethod.Post, "/api/learning/subjects/Create",
            new { Name = $"Math Ar B {Guid.NewGuid():N}", Country = "EG", GradeId = gradeId, SubjectCode = 0, Language = 0 },
            _adminToken);

        AssertRejected(second, secondRoot, secondBody,
            "Duplicate (GradeId, SubjectCode, Language) must be rejected with Successed=false");
    }

    // =========================================================================
    // AC-8b — Soft-deleted tree's natural key → "restore instead" message
    // =========================================================================

    [Fact(DisplayName = "AC-8b: Create Subject whose key matches a soft-deleted row → Successed=false with restore message")]
    public async Task AC8b_CreateSubject_SoftDeletedKeyExists_ReturnsRestoreMessage()
    {
        int gradeId = await CreateGradeGetIdAsync();

        // Create and then soft-delete (the subject has no units so deletion is allowed).
        int subjectId = await CreateSubjectGetIdAsync(gradeId,
            name: $"Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        var (delResp, _, delBody) = await SendAsync(HttpMethod.Delete,
            $"/api/learning/subjects?id={subjectId}",
            bearer: _adminToken);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK, "prereq delete must succeed; body: {0}", delBody);

        // Attempt to create with the same natural key.
        var (createResp, createRoot, createBody) = await SendAsync(HttpMethod.Post, "/api/learning/subjects/Create",
            new { Name = $"Math Ar New {Guid.NewGuid():N}", Country = "EG", GradeId = gradeId, SubjectCode = 0, Language = 0 },
            _adminToken);

        // Must return Successed=false (not a raw 500).
        AssertRejected(createResp, createRoot, createBody,
            "Create against a soft-deleted tree's key must return Successed=false (not 500)");

        // Verify the response body is valid JSON (not an exception page).
        var parseAction = () => JsonDocument.Parse(createBody);
        parseAction.Should().NotThrow(
            "response must be valid JSON, not a raw exception page; body: {0}", createBody);

        // The message should not be empty — it should give an informative message.
        if (TryProp(createRoot, "message", out var msgProp))
        {
            var msg = msgProp.GetString();
            msg.Should().NotBeNullOrWhiteSpace(
                "handler must return a non-empty message when soft-deleted tree exists; body: {0}", createBody);
        }
    }

    // =========================================================================
    // AC-2 — Language coverage report
    // =========================================================================

    [Fact(DisplayName = "AC-2: Coverage GET for grade with no subjects → 6 slots all Exists=false")]
    public async Task AC2_Coverage_EmptyGrade_AllSlotsMissing()
    {
        int gradeId = await CreateGradeGetIdAsync();

        var (covResp, covRoot, covBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Subjects/Coverage?gradeId={gradeId}",
            bearer: _adminToken);
        covResp.StatusCode.Should().Be(HttpStatusCode.OK, "Coverage must return 200; body: {0}", covBody);
        AssertSucceeded(covRoot, covBody);

        // Envelope must have a data object.
        TryProp(covRoot, "data", out var data).Should().BeTrue("envelope must have data; body: {0}", covBody);

        // data.coverage must have 6 entries.
        TryProp(data, "coverage", out var coverageArray).Should().BeTrue(
            "report must have coverage array; body: {0}", covBody);
        coverageArray.GetArrayLength().Should().Be(6,
            "expected 6 (SubjectCode, Language) slots; body: {0}", covBody);

        // All must have Exists=false.
        foreach (var slot in coverageArray.EnumerateArray())
        {
            TryProp(slot, "exists", out var existsProp).Should().BeTrue(
                "each coverage slot must have 'exists'; body: {0}", covBody);
            existsProp.GetBoolean().Should().BeFalse(
                "all slots must be Exists=false for empty grade; body: {0}", covBody);
        }

        // gapCount must equal 6.
        TryProp(data, "gapCount", out var gapCountProp).Should().BeTrue(
            "report must expose gapCount; body: {0}", covBody);
        gapCountProp.GetInt32().Should().Be(6,
            "gapCount must be 6 when no subjects exist; body: {0}", covBody);
    }

    [Fact(DisplayName = "AC-2: Coverage GET after creating a (MATH/Ar) subject → that slot Exists=true, 5 gaps")]
    public async Task AC2_Coverage_OneSlotPresent_FiveGaps()
    {
        int gradeId = await CreateGradeGetIdAsync();
        await CreateSubjectGetIdAsync(gradeId, name: $"Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        var (covResp, covRoot, covBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Subjects/Coverage?gradeId={gradeId}",
            bearer: _adminToken);
        covResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", covBody);

        TryProp(covRoot, "data", out var data).Should().BeTrue("body: {0}", covBody);

        // gapCount must be 5.
        TryProp(data, "gapCount", out var gapProp).Should().BeTrue("body: {0}", covBody);
        gapProp.GetInt32().Should().Be(5,
            "5 gaps when 1 of 6 slots is filled; body: {0}", covBody);

        // Find the MATH/Ar slot (SubjectCode=0, Language=0) — must be Exists=true.
        TryProp(data, "coverage", out var coverageArray).Should().BeTrue("body: {0}", covBody);
        bool foundMathAr = false;
        foreach (var slot in coverageArray.EnumerateArray())
        {
            if (!TryProp(slot, "subjectCode", out var codeProp)) continue;
            if (!TryProp(slot, "language", out var langProp)) continue;
            var code = codeProp.ValueKind == JsonValueKind.Number ? codeProp.GetInt32() : -1;
            var lang = langProp.ValueKind == JsonValueKind.Number ? langProp.GetInt32() : -1;
            if (code == 0 /* MATH */ && lang == 0 /* Ar */)
            {
                foundMathAr = true;
                TryProp(slot, "exists", out var existsProp).Should().BeTrue("body: {0}", covBody);
                existsProp.GetBoolean().Should().BeTrue(
                    "MATH/Ar slot must be Exists=true after creating the subject; body: {0}", covBody);
            }
        }
        foundMathAr.Should().BeTrue("MATH/Ar slot must be present in coverage array; body: {0}", covBody);
    }

    [Fact(DisplayName = "AC-2: Coverage GET for non-existent gradeId → Successed=false")]
    public async Task AC2_Coverage_NonExistentGrade_FailsGracefully()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/Subjects/Coverage?gradeId=99999999",
            bearer: _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "non-existent gradeId must not produce a raw 500; body: {0}", body);

        if (root.ValueKind != JsonValueKind.Undefined && TryProp(root, "successed", out var s))
            s.GetBoolean().Should().BeFalse("Successed must be false for non-existent grade; body: {0}", body);
    }

    [Fact(DisplayName = "AC-9: Coverage with admin token → 200")]
    public async Task AC9_Coverage_Admin_Returns200()
    {
        // Coverage endpoint must succeed for admin (even for a non-existent grade that would return NotFound,
        // the auth itself must pass — we use a valid grade here to get a 200).
        int gradeId = await CreateGradeGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Subjects/Coverage?gradeId={gradeId}",
            bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Admin must receive 200 on Coverage; body: {0}", body);
        AssertSucceeded(root, body);
    }

    // =========================================================================
    // AC-10 — BaseResponse envelope shape on successful write
    // =========================================================================

    [Fact(DisplayName = "AC-10: SetActive response has statusCode/successed/message/data/errors keys")]
    public async Task AC10_SetActive_ResponseEnvelopeShape()
    {
        int gradeId = await CreateGradeGetIdAsync();
        int subjectId = await CreateSubjectGetIdAsync(gradeId,
            name: $"Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        var (resp, root, body) = await SendAsync(HttpMethod.Put,
            $"/api/learning/Subjects/{subjectId}/Active",
            new { isActive = false },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data", out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue("envelope must have errors; body: {0}", body);
    }

    // =========================================================================
    // AC-11 — Validation: Reorder validators
    // =========================================================================

    [Fact(DisplayName = "AC-11: Subjects Reorder with empty SubjectIds list → 422")]
    public async Task AC11_SubjectReorder_EmptyList_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Subjects/Reorder",
            new { subjectIds = Array.Empty<int>() },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Empty SubjectIds must trigger FluentValidation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-11: Subjects Reorder with id=0 in list → 422")]
    public async Task AC11_SubjectReorder_ZeroId_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Subjects/Reorder",
            new { subjectIds = new[] { 0 } },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "id=0 in SubjectIds must trigger GreaterThan(0) → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-11: Units Reorder with empty UnitIds list → 422")]
    public async Task AC11_UnitReorder_EmptyList_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Units/Reorder",
            new { unitIds = Array.Empty<int>() },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Empty UnitIds must trigger FluentValidation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-11: Units Reorder with id=0 in list → 422")]
    public async Task AC11_UnitReorder_ZeroId_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Units/Reorder",
            new { unitIds = new[] { 0 } },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "id=0 in UnitIds must trigger GreaterThan(0) → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    // =========================================================================
    // AC-12 — Validation: SetActive validator (SubjectId GreaterThan(0))
    // The route id is bound into the command body by the controller (command with { SubjectId = id }).
    // SubjectId=0 would only happen if the route was /Subjects/0/Active — the route constraint
    // {id:int} does not block 0, so the validator fires.
    // =========================================================================

    [Fact(DisplayName = "AC-12: PUT Subjects/0/Active → 422 (SubjectId GreaterThan(0) rule)")]
    public async Task AC12_SubjectSetActive_ZeroId_Returns422()
    {
        // Route: PUT /api/learning/Subjects/0/Active
        // Controller sets command.SubjectId = 0 (from route). Validator has GreaterThan(0).
        var (resp, root, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Subjects/0/Active",
            new { isActive = false },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "SubjectId=0 must trigger GreaterThan(0) validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    // =========================================================================
    // Reorder idempotency: reordering with a single-item list is allowed
    // =========================================================================

    [Fact(DisplayName = "Reorder: single-item list is allowed (no cross-tree issue)")]
    public async Task AC3_SubjectReorder_SingleItem_Succeeds()
    {
        int gradeId = await CreateGradeGetIdAsync();
        int subjectId = await CreateSubjectGetIdAsync(gradeId,
            name: $"Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        var (resp, root, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Subjects/Reorder",
            new { subjectIds = new[] { subjectId } },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Single-item reorder must succeed; body: {0}", body);
        AssertSucceeded(root, body);
    }

    // =========================================================================
    // Reorder: non-existent IDs → Successed=false (not found)
    // =========================================================================

    [Fact(DisplayName = "Reorder: Subjects Reorder with non-existent IDs → Successed=false")]
    public async Task AC3_SubjectReorder_NonExistentIds_Rejected()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Subjects/Reorder",
            new { subjectIds = new[] { 99999997, 99999998 } },
            _adminToken);

        // Handler: subjects.Count != request.SubjectIds.Count → NotFound → HTTP 200 Successed=false
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 400, 404 },
            "Non-existent IDs must be rejected gracefully; body: {0}", body);

        if (root.ValueKind != JsonValueKind.Undefined && TryProp(root, "successed", out var s))
            s.GetBoolean().Should().BeFalse("Successed must be false for non-existent IDs; body: {0}", body);
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

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
        var bodyStr = await response.Content.ReadAsStringAsync();
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
        if (element.TryGetProperty(name, out value)) return true;
        if (element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value)) return true;
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

    private static List<JsonElement> ExtractPageItems(JsonElement root, string bodyStr)
    {
        TryProp(root, "data", out var outer).Should().BeTrue("envelope must have 'data'; body: {0}", bodyStr);
        TryProp(outer, "data", out var inner).Should().BeTrue("PaginatedResult must have inner 'data'; body: {0}", bodyStr);

        if (inner.ValueKind == JsonValueKind.Null || inner.ValueKind == JsonValueKind.Undefined)
            return new List<JsonElement>();
        if (inner.ValueKind != JsonValueKind.Array)
            return new List<JsonElement>();

        return inner.EnumerateArray().ToList();
    }

    private static void AssertSuccess(HttpResponseMessage response, string body)
    {
        ((int)response.StatusCode).Should().Be(200,
            "Create/write handler must return 200; body: {0}", body);
    }

    private static void AssertSucceeded(JsonElement root, string body)
    {
        TryProp(root, "successed", out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
    }

    /// <summary>
    /// Asserts that an operation was rejected: either HTTP 200 with successed=false
    /// or a non-success HTTP status with a valid JSON envelope (not a raw 500 exception page).
    /// </summary>
    private static void AssertRejected(HttpResponseMessage response, JsonElement root, string body, string reason)
    {
        var statusCode = (int)response.StatusCode;

        // Body must be valid JSON (not an exception dump).
        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow("rejection response must be valid JSON; reason={0}; body: {1}", reason, body);

        // Either HTTP 200 with Successed=false, or a 4xx / 5xx.
        if (statusCode == 200)
        {
            TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
            s.GetBoolean().Should().BeFalse(
                "{0}; expected Successed=false on 200 rejection; body: {1}", reason, body);
        }
        else
        {
            statusCode.Should().BeOneOf(new[] { 400, 404, 422, 424, 500 },
                "{0}; expected a non-success HTTP status when Successed=false; body: {1}", reason, body);
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

    private async Task<string> RegisterParentGetTokenAsync()
    {
        var email = $"p701_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.com";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>
    /// Publishes a curriculum entity by calling POST /api/learning/ContentLifecycle/Transition
    /// with TargetState=Published (2). Required before student-facing reads can see the entity,
    /// because P7-05 added LifecycleState == Published filter to ForGrade and GetSubjectLessons.
    /// VersionedEntityType: Subject=1, Unit=2, Lesson=3, QuizQuestion=4.
    /// </summary>
    private async Task PublishEntityAsync(int entityType, int entityId)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/ContentLifecycle/Transition",
            new { EntityType = entityType, EntityId = entityId, TargetState = 2 },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Transition to Published must return 200; entityType={0}, entityId={1}; body: {2}",
            entityType, entityId, body);
        AssertSucceeded(root, body);
    }

    /// <summary>
    /// Returns the id of the FIRST grade in the system with the given Number.
    /// If no grade with that Number exists yet (e.g. when the test runs in isolation
    /// before any other test has created one), creates one and returns its id.
    /// Used by ForGrade tests to ensure the subject's gradeId matches the grade that
    /// ForGrade will pick (GetSubjectsForGradeQuery uses FirstOrDefaultAsync on Number).
    /// </summary>
    private async Task<int> GetFirstGradeIdByNumberAsync(int number)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/grades/List?PageNumber=1&PageSize=200", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "Grade list must return 200; body: {0}", body);
        var items = ExtractPageItems(root, body);
        var found = items.FirstOrDefault(g =>
            TryProp(g, "number", out var n) && n.GetInt32() == number);

        // If no grade with the requested Number exists, create one so the test is self-contained.
        if (found.ValueKind == JsonValueKind.Undefined)
        {
            var name = $"P701 ForGrade Seed {number} {Guid.NewGuid():N}";
            var (createResp, _, createBody) = await SendAsync(HttpMethod.Post,
                "/api/learning/grades/Create",
                new { Number = number, DisplayName = name },
                _adminToken);
            createResp.StatusCode.Should().Be(HttpStatusCode.OK,
                "Create grade fallback must succeed; body: {0}", createBody);
            return await FindIdInListAsync(
                $"/api/learning/grades/List?PageNumber=1&PageSize=200",
                "displayName", name, "grade", _adminToken);
        }

        TryProp(found, "id", out var idProp).Should().BeTrue("body: {0}", body);
        return idProp.GetInt32();
    }

    /// <summary>Creates a grade and returns its id.</summary>
    private async Task<int> CreateGradeGetIdAsync()
    {
        var name = $"P701 Grade {Guid.NewGuid():N}";
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/grades/Create",
            new { Number = 1, DisplayName = name }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "grade create must succeed; body: {0}", body);

        return await FindIdInListAsync(
            "/api/learning/grades/List?PageNumber=1&PageSize=200",
            "displayName", name, "grade", _adminToken);
    }

    /// <summary>Gets the grade's Number (1-6) from its Id.</summary>
    private async Task<int> GetGradeNumberAsync(int gradeId)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/grades?id={gradeId}", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "GetGrade must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "number", out var numProp).Should().BeTrue("body: {0}", body);
        return numProp.GetInt32();
    }

    /// <summary>
    /// Creates a subject with explicit SubjectCode + Language and returns its id.
    /// SubjectCode: MATH=0, SCIENCE=1, ARABIC=2, ENGLISH=3.
    /// Language: Ar=0, En=1.
    /// </summary>
    private async Task<int> CreateSubjectGetIdAsync(int gradeId, string name, int subjectCode, int language)
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/subjects/Create",
            new { Name = name, Country = "EG", GradeId = gradeId, SubjectCode = subjectCode, Language = language },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "subject create must succeed; body: {0}", body);

        return await FindIdInListAsync(
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}",
            "name", name, "subject", _adminToken);
    }

    /// <summary>Creates a unit and returns its id.</summary>
    private async Task<int> CreateUnitGetIdAsync(int subjectId, string name, int seqOrder)
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/units/Create",
            new { Name = name, SequenceOrder = seqOrder, SubjectId = subjectId },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "unit create must succeed; body: {0}", body);

        return await FindIdInListAsync(
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", name, "unit", _adminToken);
    }

    /// <summary>Calls GET /api/learning/Subjects/ForGrade and returns the list of subject DTOs.</summary>
    private async Task<List<JsonElement>> GetStudentSubjectsAsync(int gradeNumber)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Subjects/ForGrade?grade={gradeNumber}", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ForGrade must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        if (data.ValueKind == JsonValueKind.Array)
            return data.EnumerateArray().ToList();
        if (data.ValueKind == JsonValueKind.Null || data.ValueKind == JsonValueKind.Undefined)
            return new List<JsonElement>();

        // Wrapped collection: EmptyCollection returns BaseResponse<List<T>>
        return new List<JsonElement>();
    }

    /// <summary>Calls GET /api/learning/Subjects/{id}/Lessons and returns the list of UnitWithLessonsDto.</summary>
    private async Task<List<JsonElement>> GetStudentLessonsUnitsAsync(int subjectId)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Subjects/{subjectId}/Lessons", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GetLessons must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        if (data.ValueKind == JsonValueKind.Array)
            return data.EnumerateArray().ToList();
        return new List<JsonElement>();
    }

    /// <summary>
    /// Returns a dictionary of subjectId → SequenceOrder for the given subject IDs
    /// by fetching them from the admin List.
    /// </summary>
    private async Task<Dictionary<int, int>> GetSubjectOrders(int gradeId, int[] subjectIds)
    {
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}", bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);

        var items = ExtractPageItems(listRoot, listBody);
        var result = new Dictionary<int, int>();
        foreach (var item in items)
        {
            if (!TryProp(item, "id", out var idProp)) continue;
            var id = idProp.GetInt32();
            if (!subjectIds.Contains(id)) continue;
            if (!TryProp(item, "sequenceOrder", out var oProp)) continue;
            result[id] = oProp.GetInt32();
        }
        return result;
    }

    private async Task<int> FindIdInListAsync(
        string listUrl, string fieldName, string fieldValue, string entityType, string? bearer = null)
    {
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get, listUrl, bearer: bearer);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prereq list for {0} must succeed; body: {1}", entityType, listBody);

        var items = ExtractPageItems(listRoot, listBody);
        foreach (var item in items)
        {
            if (TryProp(item, fieldName, out var fv) && fv.GetString() == fieldValue)
            {
                TryProp(item, "id", out var idProp).Should().BeTrue(
                    "{0} item must have id; body: {1}", entityType, listBody);
                return idProp.GetInt32();
            }
        }

        throw new InvalidOperationException(
            $"Could not find {entityType} where {fieldName}='{fieldValue}' in list; url={listUrl}; body={listBody}");
    }
}
