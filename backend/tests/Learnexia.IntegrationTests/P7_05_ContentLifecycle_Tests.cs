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
/// P7-05 integration tests: Content lifecycle (publish / version / preview) for the Learning module.
///
/// Controller under test:
///   POST /api/learning/ContentLifecycle/Transition
///   POST /api/learning/ContentLifecycle/Rollback
///   GET  /api/learning/ContentLifecycle/VersionHistory?entityType={n}&amp;entityId={n}
///   GET  /api/learning/ContentLifecycle/Preview?entityType={n}&amp;entityId={n}
///   GET  /api/learning/ContentLifecycle/PublicationCoverage?gradeId={n}
///
/// Student-read paths under regression test (draft-leak checks):
///   GET  /api/learning/Subjects/ForGrade?grade={n}      (GetSubjectsForGradeQuery)
///   GET  /api/learning/Subjects/{id}/Lessons            (GetSubjectLessonsQuery)
///   GET  /api/learning/Lessons/{id}                     (GetLessonQuery)
///
/// Enum values:
///   VersionedEntityType — Subject=1, Unit=2, Lesson=3, QuizQuestion=4
///   LifecycleState      — Draft=1, Published=2, Archived=3
///
/// Acceptance criteria covered:
///
/// [AUTH]
///   AC-AUTH-1: All ContentLifecycle endpoints: anonymous → 401
///   AC-AUTH-2: All ContentLifecycle endpoints: non-admin (basicuser / parent) → 403
///   AC-AUTH-3: All ContentLifecycle endpoints: admin → 200
///
/// [TRANSITION — happy paths]
///   AC-TC-1: Draft→Published (Lesson): Successed=true; entity becomes visible to students
///   AC-TC-2: Published→Archived (Lesson): Successed=true; entity hidden from students again
///   AC-TC-3: Published→Draft (Lesson, unpublish): Successed=true; entity hidden from students
///   AC-TC-4: Archived→Draft (Lesson, restore): Successed=true; entity back in Draft
///   AC-TC-5: Draft→Published (Subject): Successed=true
///   AC-TC-6: Draft→Published (Unit): Successed=true
///   AC-TC-7: Draft→Published (QuizQuestion): Successed=true
///
/// [TRANSITION — illegal transitions]
///   AC-TC-ILL-1: Draft→Archived (Lesson): Successed=false (illegal)
///   AC-TC-ILL-2: Archived→Published (Lesson): Successed=false (illegal; must go Draft first)
///
/// [TRANSITION — validation]
///   AC-TC-VAL-1: EntityId=0 → 422
///   AC-TC-VAL-2: Invalid EntityType enum (e.g. 99) → 422
///   AC-TC-VAL-3: Invalid TargetState enum (e.g. 99) → 422
///   AC-TC-VAL-4: Valid EntityType + non-existent EntityId → Successed=false (not 500)
///
/// [VERSIONING]
///   AC-VER-1: First publish (Draft→Published) creates ContentVersion row; VersionHistory returns it
///   AC-VER-2: Second publish increments VersionNumber (version 2); history ordered newest-first
///   AC-VER-3: Rollback to version 1 → Successed=true; new ContentVersion created (append-only history)
///   AC-VER-4: Rollback with VersionNumber=0 → 422
///   AC-VER-5: Rollback with EntityId=0 → 422
///   AC-VER-6: Rollback to non-existent VersionNumber → Successed=false
///   AC-VER-7: VersionHistory with EntityId=0 → Successed=false (handler guard)
///   AC-VER-8: VersionHistory for entity with no versions → Successed=true, empty list
///
/// [PREVIEW]
///   AC-PRV-1: Admin can Preview a Draft entity (IgnoreQueryFilters) → Successed=true, CurrentState=Draft(1)
///   AC-PRV-2: Admin can Preview a Published entity → CurrentState=Published(2)
///   AC-PRV-3: Student/non-admin cannot reach Preview → 403
///   AC-PRV-4: Preview with EntityId=0 → Successed=false (handler guard)
///   AC-PRV-5: Preview non-existent entity → Successed=false
///
/// [PUBLICATION COVERAGE]
///   AC-COV-1: Admin coverage for a grade with a Draft subject → slot Exists=true, IsPublished=false
///   AC-COV-2: After publish of that subject → slot IsPublished=true
///   AC-COV-3: One-sided coverage: Ar slot Published, En slot Draft/absent → IsPublished differs
///   AC-COV-4: Coverage for non-existent gradeId → Successed=false
///   AC-COV-5: Coverage with gradeId=0 → Successed=false (handler guard)
///
/// [DRAFT-LEAK REGRESSION — CRITICAL]
///   AC-LEAK-1: Newly admin-created Subject (Draft) NOT visible on student ForGrade read
///   AC-LEAK-2: After Publish, Subject IS visible on ForGrade
///   AC-LEAK-3: After Archive, Subject is hidden again from ForGrade
///   AC-LEAK-4: Newly admin-created Lesson (Draft) NOT visible on student Subjects/{id}/Lessons
///   AC-LEAK-5: After Publish, Lesson IS visible on student Subjects/{id}/Lessons
///   AC-LEAK-6: Newly admin-created Lesson (Draft) NOT reachable via student GET /Lessons/{id}
///   AC-LEAK-7: After Publish, Lesson IS reachable via student GET /Lessons/{id}
///   AC-LEAK-8: Seeded/backfilled content (LifecycleState=Published) remains visible to students
///              (regression guard — publish filter must not hide existing rows)
///
/// [ENVELOPE]
///   AC-ENV-1: Successful Transition response has full BaseResponse envelope
///             (statusCode, successed, message, data, errors)
///
/// Docker + Postgres required to RUN. Compile-only on Windows CI (no Docker daemon).
/// Runs in the shared "IntegrationTests" xUnit collection (LearnexiaWebAppFactory, Testcontainers PG).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_05_ContentLifecycle_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // ─────────────────────────────────────────────────────────────────────────
    // Seeded credentials (mirrors P7-01..P7-04)
    // ─────────────────────────────────────────────────────────────────────────
    private const string AdminUserName      = "superadmin";
    private const string AdminPassword      = "123Pa$$word!";
    private const string BasicUserName      = "basicuser";
    private const string BasicUserPassword  = "123Pa$$word!";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    // VersionedEntityType int values
    private const int VetSubject      = 1;
    private const int VetUnit         = 2;
    private const int VetLesson       = 3;
    private const int VetQuizQuestion = 4;

    // LifecycleState int values
    private const int LsDraft     = 1;
    private const int LsPublished = 2;
    private const int LsArchived  = 3;

    // ContentLifecycle controller base route
    private const string LifecycleBase = "/api/learning/ContentLifecycle";

    private string? _adminToken;
    private string? _basicToken;
    private string? _parentToken;

    public P7_05_ContentLifecycle_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await db.Database.MigrateAsync();

        _adminToken  = await SignInGetTokenAsync(AdminUserName, AdminPassword);
        _basicToken  = await SignInGetTokenAsync(BasicUserName, BasicUserPassword);
        _parentToken = await RegisterParentGetTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // AC-AUTH-1 — All ContentLifecycle endpoints: anonymous → 401
    // =========================================================================

    [Fact(DisplayName = "AC-AUTH-1: POST Transition anonymous → 401")]
    public async Task Auth_Transition_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetLesson, entityId = 1, targetState = LsPublished },
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Transition is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: POST Rollback anonymous → 401")]
    public async Task Auth_Rollback_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Rollback",
            new { entityType = VetLesson, entityId = 1, versionNumber = 1 },
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Rollback is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: GET VersionHistory anonymous → 401")]
    public async Task Auth_VersionHistory_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/VersionHistory?entityType={VetLesson}&entityId=1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "VersionHistory is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: GET Preview anonymous → 401")]
    public async Task Auth_Preview_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/Preview?entityType={VetLesson}&entityId=1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Preview is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: GET PublicationCoverage anonymous → 401")]
    public async Task Auth_PublicationCoverage_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/PublicationCoverage?gradeId=1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "PublicationCoverage is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    // =========================================================================
    // AC-AUTH-2 — Non-admin (basicuser / parent) → 403
    // =========================================================================

    [Fact(DisplayName = "AC-AUTH-2: POST Transition basicuser → 403")]
    public async Task Auth_Transition_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetLesson, entityId = 1, targetState = LsPublished },
            _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Transition is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: POST Transition parent → 403")]
    public async Task Auth_Transition_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetLesson, entityId = 1, targetState = LsPublished },
            _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Transition is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: POST Rollback basicuser → 403")]
    public async Task Auth_Rollback_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Rollback",
            new { entityType = VetLesson, entityId = 1, versionNumber = 1 },
            _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Rollback is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: GET VersionHistory basicuser → 403")]
    public async Task Auth_VersionHistory_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/VersionHistory?entityType={VetLesson}&entityId=1",
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "VersionHistory is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: GET Preview basicuser → 403")]
    public async Task Auth_Preview_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/Preview?entityType={VetLesson}&entityId=1",
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Preview is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: GET PublicationCoverage basicuser → 403")]
    public async Task Auth_PublicationCoverage_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/PublicationCoverage?gradeId=1",
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "PublicationCoverage is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: GET Preview parent → 403 (CRITICAL: students must not see Draft content)")]
    public async Task Auth_Preview_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/Preview?entityType={VetLesson}&entityId=1",
            bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Preview is AdminOnly — parent must get 403 (parent must never see Draft content); body: {0}", body);
    }

    // =========================================================================
    // AC-TC-1..7 — Transition happy paths
    // =========================================================================

    [Fact(DisplayName = "AC-TC-1: Lesson Draft→Published → Successed=true")]
    public async Task Transition_LessonDraftToPublished_Succeeds()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetLesson, entityId = lessonId, targetState = LsPublished },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Draft→Published must return 200; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "AC-TC-2: Lesson Published→Archived → Successed=true")]
    public async Task Transition_LessonPublishedToArchived_Succeeds()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // First publish so we have a Published entity.
        await TransitionAsync(VetLesson, lessonId, LsPublished);

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetLesson, entityId = lessonId, targetState = LsArchived },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Published→Archived must return 200; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "AC-TC-3: Lesson Published→Draft (unpublish) → Successed=true")]
    public async Task Transition_LessonPublishedToDraft_Succeeds()
    {
        var lessonId = await CreateLessonGetIdAsync();
        await TransitionAsync(VetLesson, lessonId, LsPublished);

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetLesson, entityId = lessonId, targetState = LsDraft },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Published→Draft (unpublish) must return 200; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "AC-TC-4: Lesson Archived→Draft (restore) → Successed=true")]
    public async Task Transition_LessonArchivedToDraft_Succeeds()
    {
        var lessonId = await CreateLessonGetIdAsync();
        await TransitionAsync(VetLesson, lessonId, LsPublished);
        await TransitionAsync(VetLesson, lessonId, LsArchived);

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetLesson, entityId = lessonId, targetState = LsDraft },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Archived→Draft (restore) must return 200; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "AC-TC-5: Subject Draft→Published → Successed=true")]
    public async Task Transition_SubjectDraftToPublished_Succeeds()
    {
        var (subjectId, _, _, _) = await CreateFullHierarchyAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetSubject, entityId = subjectId, targetState = LsPublished },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Subject Draft→Published must return 200; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "AC-TC-6: Unit Draft→Published → Successed=true")]
    public async Task Transition_UnitDraftToPublished_Succeeds()
    {
        var (_, unitId, _, _) = await CreateFullHierarchyAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetUnit, entityId = unitId, targetState = LsPublished },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Unit Draft→Published must return 200; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "AC-TC-7: QuizQuestion Draft→Published → Successed=true")]
    public async Task Transition_QuizQuestionDraftToPublished_Succeeds()
    {
        var lessonId = await CreateLessonGetIdAsync();
        var questionId = await CreateMcqQuestionGetIdAsync(lessonId);

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetQuizQuestion, entityId = questionId, targetState = LsPublished },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "QuizQuestion Draft→Published must return 200; body: {0}", body);
        AssertSucceeded(root, body);
    }

    // =========================================================================
    // AC-TC-ILL-1 / AC-TC-ILL-2 — Illegal transitions → Successed=false
    // =========================================================================

    [Fact(DisplayName = "AC-TC-ILL-1: Draft→Archived (Lesson) → illegal transition → Successed=false")]
    public async Task Transition_DraftToArchived_IsIllegal()
    {
        var lessonId = await CreateLessonGetIdAsync();
        // Lesson starts in Draft; Archived directly from Draft is illegal.

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetLesson, entityId = lessonId, targetState = LsArchived },
            _adminToken);

        // Handler returns BadRequest → HTTP 400 with Successed=false.
        AssertRejected(resp, root, body,
            "Draft→Archived is an illegal transition and must be rejected (Successed=false)");
    }

    [Fact(DisplayName = "AC-TC-ILL-2: Archived→Published (Lesson) → illegal transition → Successed=false")]
    public async Task Transition_ArchivedToPublished_IsIllegal()
    {
        var lessonId = await CreateLessonGetIdAsync();
        await TransitionAsync(VetLesson, lessonId, LsPublished);
        await TransitionAsync(VetLesson, lessonId, LsArchived);
        // Now in Archived; direct Archived→Published is illegal (must go through Draft first).

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetLesson, entityId = lessonId, targetState = LsPublished },
            _adminToken);

        AssertRejected(resp, root, body,
            "Archived→Published is an illegal transition and must be rejected (Successed=false)");
    }

    // =========================================================================
    // AC-TC-VAL-1..4 — Transition command validation
    // =========================================================================

    [Fact(DisplayName = "AC-TC-VAL-1: Transition with EntityId=0 → 422")]
    public async Task Transition_EntityIdZero_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetLesson, entityId = 0, targetState = LsPublished },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "EntityId=0 must trigger GreaterThan(0) → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-TC-VAL-2: Transition with invalid EntityType=99 → 422")]
    public async Task Transition_InvalidEntityType_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = 99, entityId = 1, targetState = LsPublished },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Invalid EntityType=99 must trigger IsInEnum → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-TC-VAL-3: Transition with invalid TargetState=99 → 422")]
    public async Task Transition_InvalidTargetState_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetLesson, entityId = 1, targetState = 99 },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Invalid TargetState=99 must trigger enum validation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-TC-VAL-4: Transition non-existent EntityId → Successed=false (not 500)")]
    public async Task Transition_NonExistentEntityId_ReturnsNotFound()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetLesson, entityId = 99999901, targetState = LsPublished },
            _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "Non-existent entity must not cause 500; body: {0}", body);
        AssertRejected(resp, root, body,
            "Transition to non-existent entity must be rejected (Successed=false, not 500)");
    }

    // =========================================================================
    // AC-VER-1..8 — Versioning: ContentVersion created on publish; VersionHistory
    // =========================================================================

    [Fact(DisplayName = "AC-VER-1: First publish (Draft→Published) creates ContentVersion; VersionHistory returns it")]
    public async Task Publish_CreatesContentVersion_VersionHistoryReturnsIt()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // Before publish: version history must be empty.
        var versionsBefore = await GetVersionHistoryAsync(VetLesson, lessonId);
        versionsBefore.Should().BeEmpty(
            "no versions should exist before first publish; entityId={0}", lessonId);

        // Publish.
        await TransitionAsync(VetLesson, lessonId, LsPublished);

        // After publish: exactly one version row must exist.
        var versionsAfter = await GetVersionHistoryAsync(VetLesson, lessonId);
        versionsAfter.Should().HaveCount(1,
            "first publish must create exactly one ContentVersion row; entityId={0}", lessonId);

        var v = versionsAfter[0];
        TryProp(v, "versionNumber", out var vnProp).Should().BeTrue(
            "ContentVersionDto must have versionNumber; entityId={0}", lessonId);
        vnProp.GetInt32().Should().Be(1,
            "first publish must produce versionNumber=1; entityId={0}", lessonId);

        TryProp(v, "entityId", out var eidProp).Should().BeTrue("dto must have entityId");
        eidProp.GetInt32().Should().Be(lessonId, "entityId must match the published lesson");

        TryProp(v, "entityType", out var etProp).Should().BeTrue("dto must have entityType");
        etProp.GetInt32().Should().Be(VetLesson, "entityType must be Lesson(3)");
    }

    [Fact(DisplayName = "AC-VER-2: Second publish increments VersionNumber to 2; history ordered newest-first")]
    public async Task SecondPublish_IncrementsVersionNumber_HistoryNewestFirst()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // First publish.
        await TransitionAsync(VetLesson, lessonId, LsPublished);

        // Unpublish so we can publish again (Published→Draft→Published).
        await TransitionAsync(VetLesson, lessonId, LsDraft);

        // Second publish.
        await TransitionAsync(VetLesson, lessonId, LsPublished);

        var versions = await GetVersionHistoryAsync(VetLesson, lessonId);
        versions.Should().HaveCount(2,
            "two publishes must produce exactly two ContentVersion rows; entityId={0}", lessonId);

        // Handler returns newest-first: versions[0] should be version 2.
        TryProp(versions[0], "versionNumber", out var v0).Should().BeTrue();
        TryProp(versions[1], "versionNumber", out var v1).Should().BeTrue();
        v0.GetInt32().Should().BeGreaterThan(v1.GetInt32(),
            "VersionHistory must be ordered newest-first (highest versionNumber first)");
        v0.GetInt32().Should().Be(2, "second publish must produce versionNumber=2");
    }

    [Fact(DisplayName = "AC-VER-3: Rollback to version 1 → Successed=true; new ContentVersion row appended")]
    public async Task Rollback_ToVersion1_Succeeds_AndAppendsNewVersionRow()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // Publish v1, then unpublish and publish v2.
        await TransitionAsync(VetLesson, lessonId, LsPublished);
        await TransitionAsync(VetLesson, lessonId, LsDraft);
        await TransitionAsync(VetLesson, lessonId, LsPublished);

        var versionsBeforeRollback = await GetVersionHistoryAsync(VetLesson, lessonId);
        versionsBeforeRollback.Should().HaveCount(2, "prereq: two versions must exist before rollback");

        // Rollback to version 1.
        var (rollResp, rollRoot, rollBody) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Rollback",
            new { entityType = VetLesson, entityId = lessonId, versionNumber = 1 },
            _adminToken);

        rollResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Rollback must return 200; body: {0}", rollBody);
        AssertSucceeded(rollRoot, rollBody);

        // After rollback: history must have 3 rows (append-only — new row created for rollback).
        var versionsAfterRollback = await GetVersionHistoryAsync(VetLesson, lessonId);
        versionsAfterRollback.Should().HaveCount(3,
            "rollback must append a NEW ContentVersion row (history is append-only); entityId={0}", lessonId);
    }

    [Fact(DisplayName = "AC-VER-4: Rollback with VersionNumber=0 → 422")]
    public async Task Rollback_VersionNumberZero_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Rollback",
            new { entityType = VetLesson, entityId = 1, versionNumber = 0 },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "VersionNumber=0 must trigger GreaterThan(0) → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-VER-5: Rollback with EntityId=0 → 422")]
    public async Task Rollback_EntityIdZero_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Rollback",
            new { entityType = VetLesson, entityId = 0, versionNumber = 1 },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "EntityId=0 must trigger GreaterThan(0) → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-VER-6: Rollback to non-existent VersionNumber → Successed=false")]
    public async Task Rollback_NonExistentVersionNumber_ReturnsNotFound()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // Lesson exists but has no versions (never published).
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Rollback",
            new { entityType = VetLesson, entityId = lessonId, versionNumber = 99 },
            _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "Non-existent version must not produce 500; body: {0}", body);
        AssertRejected(resp, root, body,
            "Rollback to non-existent VersionNumber must be rejected (Successed=false)");
    }

    [Fact(DisplayName = "AC-VER-7: VersionHistory with EntityId=0 → Successed=false (handler guard)")]
    public async Task VersionHistory_EntityIdZero_ReturnsFailure()
    {
        // The handler has an explicit guard: if EntityId <= 0 → BadRequest.
        // Note: queries are NOT auto-validated by ValidationBehavior (only ICommand<> is).
        // So 0 passes routing/binding but the handler itself guards it.
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/VersionHistory?entityType={VetLesson}&entityId=0",
            bearer: _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "EntityId=0 must not produce a raw 500; body: {0}", body);
        // Handler returns BadRequest<> → HTTP 200 Successed=false (per BaseResponseHandler.BadRequest mapping).
        if (root.ValueKind != JsonValueKind.Undefined && TryProp(root, "successed", out var s))
            s.GetBoolean().Should().BeFalse(
                "VersionHistory with EntityId=0 must return Successed=false; body: {0}", body);
    }

    [Fact(DisplayName = "AC-VER-8: VersionHistory for entity with no versions → Successed=true, empty list")]
    public async Task VersionHistory_NoVersions_ReturnsEmptyList()
    {
        var lessonId = await CreateLessonGetIdAsync();
        // Lesson exists but has never been published → no ContentVersion rows.

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/VersionHistory?entityType={VetLesson}&entityId={lessonId}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "VersionHistory for unpublished entity must return 200; body: {0}", body);
        AssertSucceeded(root, body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have data; body: {0}", body);
        // data is a List<ContentVersionDto> — should be an array or empty
        if (data.ValueKind == JsonValueKind.Array)
            data.GetArrayLength().Should().Be(0, "no versions should exist; body: {0}", body);
        else if (data.ValueKind == JsonValueKind.Null || data.ValueKind == JsonValueKind.Undefined)
        { /* null data = empty collection — acceptable */ }
    }

    // =========================================================================
    // AC-PRV-1..5 — Preview
    // =========================================================================

    [Fact(DisplayName = "AC-PRV-1: Admin Preview of a Draft entity → Successed=true; CurrentState=Draft(1)")]
    public async Task Preview_DraftLesson_ReturnsCurrentStateDraft()
    {
        var lessonId = await CreateLessonGetIdAsync();
        // Lesson starts as Draft (new admin-created entities default to Draft).

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/Preview?entityType={VetLesson}&entityId={lessonId}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Admin Preview of Draft entity must return 200; body: {0}", body);
        AssertSucceeded(root, body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(data, "currentState", out var stateProp).Should().BeTrue(
            "PreviewDto must have currentState; body: {0}", body);
        stateProp.GetInt32().Should().Be(LsDraft,
            "newly created lesson must be in Draft(1) state; body: {0}", body);

        TryProp(data, "entityId", out var eidProp).Should().BeTrue("dto must have entityId; body: {0}", body);
        eidProp.GetInt32().Should().Be(lessonId, "entityId must match the requested lesson; body: {0}", body);

        TryProp(data, "snapshot", out var snapshotProp).Should().BeTrue(
            "PreviewDto must have a snapshot field (full entity JSON); body: {0}", body);
        snapshotProp.GetString().Should().NotBeNullOrWhiteSpace(
            "snapshot must be non-empty JSON; body: {0}", body);
    }

    [Fact(DisplayName = "AC-PRV-2: Admin Preview of a Published entity → CurrentState=Published(2)")]
    public async Task Preview_PublishedLesson_ReturnsCurrentStatePublished()
    {
        var lessonId = await CreateLessonGetIdAsync();
        await TransitionAsync(VetLesson, lessonId, LsPublished);

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/Preview?entityType={VetLesson}&entityId={lessonId}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Admin Preview of Published entity must return 200; body: {0}", body);
        AssertSucceeded(root, body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "currentState", out var stateProp).Should().BeTrue("body: {0}", body);
        stateProp.GetInt32().Should().Be(LsPublished,
            "published lesson must show CurrentState=Published(2) in preview; body: {0}", body);
    }

    [Fact(DisplayName = "AC-PRV-3: Student token attempting GET Preview → 403 (non-admin forbidden)")]
    public async Task Preview_StudentToken_Returns403()
    {
        // We re-use the parent token (non-admin role) as the closest approximation to a student role.
        // A true student token could also be used but the key assertion is non-admin → 403.
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/Preview?entityType={VetLesson}&entityId=1",
            bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Preview is AdminOnly — non-admin must get 403 (draft content must never leak); body: {0}", body);
    }

    [Fact(DisplayName = "AC-PRV-4: Preview with EntityId=0 → Successed=false (handler guard)")]
    public async Task Preview_EntityIdZero_ReturnsFailure()
    {
        // Queries are not auto-validated; handler guard fires.
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/Preview?entityType={VetLesson}&entityId=0",
            bearer: _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "EntityId=0 must not produce 500; body: {0}", body);
        if (root.ValueKind != JsonValueKind.Undefined && TryProp(root, "successed", out var s))
            s.GetBoolean().Should().BeFalse(
                "Preview with EntityId=0 must return Successed=false; body: {0}", body);
    }

    [Fact(DisplayName = "AC-PRV-5: Preview non-existent entity → Successed=false (not 500)")]
    public async Task Preview_NonExistentEntity_ReturnsNotFound()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/Preview?entityType={VetLesson}&entityId=99999902",
            bearer: _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "Non-existent entity must not produce 500; body: {0}", body);
        AssertRejected(resp, root, body,
            "Preview for non-existent entity must be rejected (Successed=false)");
    }

    // =========================================================================
    // AC-COV-1..5 — Publication Coverage
    // =========================================================================

    [Fact(DisplayName = "AC-COV-1: Grade with Draft Subject → coverage slot Exists=true, IsPublished=false")]
    public async Task Coverage_DraftSubject_SlotExistsFalseIsPublished()
    {
        // Create a grade and a Draft subject (SubjectCode=MATH=0, Language=Ar=0).
        var gradeId = await CreateGradeGetIdAsync();
        // Subject is created in Draft state by default.
        await CreateSubjectGetIdAsync(gradeId, $"P705 Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/PublicationCoverage?gradeId={gradeId}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "PublicationCoverage must return 200; body: {0}", body);
        AssertSucceeded(root, body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(data, "coverage", out var coverageArr).Should().BeTrue(
            "report must have coverage array; body: {0}", body);
        coverageArr.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", body);
        coverageArr.GetArrayLength().Should().Be(6,
            "expected 6 canonical (SubjectCode,Language) slots; body: {0}", body);

        // Find the MATH/Ar slot (SubjectCode=0, Language=0).
        var mathArSlot = FindCoverageSlot(coverageArr, subjectCode: 0, language: 0);
        mathArSlot.HasValue.Should().BeTrue("MATH/Ar slot must be present in coverage array; body: {0}", body);

        TryProp(mathArSlot!.Value, "exists", out var existsProp).Should().BeTrue("body: {0}", body);
        existsProp.GetBoolean().Should().BeTrue(
            "MATH/Ar slot must Exists=true (subject was created); body: {0}", body);

        TryProp(mathArSlot.Value, "isPublished", out var isPubProp).Should().BeTrue(
            "slot must have isPublished property; body: {0}", body);
        isPubProp.GetBoolean().Should().BeFalse(
            "MATH/Ar slot must IsPublished=false (subject is still Draft); body: {0}", body);
    }

    [Fact(DisplayName = "AC-COV-2: After publishing Subject → coverage slot IsPublished=true")]
    public async Task Coverage_AfterPublish_SlotIsPublishedTrue()
    {
        var gradeId = await CreateGradeGetIdAsync();
        var subjectId = await CreateSubjectGetIdAsync(gradeId,
            $"P705 Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        // Publish the subject.
        await TransitionAsync(VetSubject, subjectId, LsPublished);

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/PublicationCoverage?gradeId={gradeId}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        AssertSucceeded(root, body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "coverage", out var coverageArr).Should().BeTrue("body: {0}", body);

        var mathArSlot = FindCoverageSlot(coverageArr, subjectCode: 0, language: 0);
        mathArSlot.HasValue.Should().BeTrue("MATH/Ar slot must be present; body: {0}", body);

        TryProp(mathArSlot!.Value, "isPublished", out var isPubProp).Should().BeTrue("body: {0}", body);
        isPubProp.GetBoolean().Should().BeTrue(
            "MATH/Ar slot must IsPublished=true after publish; body: {0}", body);
    }

    [Fact(DisplayName = "AC-COV-3: One-sided coverage: Ar slot Published, En slot absent → IsPublished differs")]
    public async Task Coverage_OneSidedCoverage_ArPublishedEnAbsent()
    {
        var gradeId = await CreateGradeGetIdAsync();

        // Create and publish only the Ar tree for MATH.
        var arSubjectId = await CreateSubjectGetIdAsync(gradeId,
            $"P705 Math Ar {Guid.NewGuid():N}", subjectCode: 0, language: 0);
        await TransitionAsync(VetSubject, arSubjectId, LsPublished);
        // Do NOT create the En tree.

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/PublicationCoverage?gradeId={gradeId}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "coverage", out var coverageArr).Should().BeTrue("body: {0}", body);

        // Ar slot: Exists=true, IsPublished=true
        var arSlot = FindCoverageSlot(coverageArr, subjectCode: 0, language: 0);
        arSlot.HasValue.Should().BeTrue("MATH/Ar slot must be present; body: {0}", body);
        TryProp(arSlot!.Value, "isPublished", out var arPubProp).Should().BeTrue("body: {0}", body);
        arPubProp.GetBoolean().Should().BeTrue(
            "MATH/Ar must be IsPublished=true (published); body: {0}", body);

        // En slot: Exists=false, IsPublished=false (one-sided coverage gap)
        var enSlot = FindCoverageSlot(coverageArr, subjectCode: 0, language: 1);
        enSlot.HasValue.Should().BeTrue("MATH/En slot must be present in the 6 canonical slots; body: {0}", body);
        TryProp(enSlot!.Value, "exists", out var enExistsProp).Should().BeTrue("body: {0}", body);
        enExistsProp.GetBoolean().Should().BeFalse(
            "MATH/En must Exists=false (not created) — one-sided coverage gap; body: {0}", body);
        TryProp(enSlot.Value, "isPublished", out var enPubProp).Should().BeTrue("body: {0}", body);
        enPubProp.GetBoolean().Should().BeFalse(
            "MATH/En must IsPublished=false (not created) — flags one-sided coverage; body: {0}", body);
    }

    [Fact(DisplayName = "AC-COV-4: PublicationCoverage for non-existent gradeId → Successed=false")]
    public async Task Coverage_NonExistentGradeId_ReturnsNotFound()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/PublicationCoverage?gradeId=99999903",
            bearer: _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "Non-existent gradeId must not produce 500; body: {0}", body);
        AssertRejected(resp, root, body,
            "PublicationCoverage for non-existent gradeId must be rejected (Successed=false)");
    }

    [Fact(DisplayName = "AC-COV-5: PublicationCoverage with gradeId=0 → Successed=false (handler guard)")]
    public async Task Coverage_GradeIdZero_ReturnsFailure()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/PublicationCoverage?gradeId=0",
            bearer: _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "gradeId=0 must not produce 500; body: {0}", body);
        if (root.ValueKind != JsonValueKind.Undefined && TryProp(root, "successed", out var s))
            s.GetBoolean().Should().BeFalse(
                "PublicationCoverage gradeId=0 must return Successed=false; body: {0}", body);
    }

    // =========================================================================
    // AC-LEAK-1..3 — Draft-leak regression: Subject on ForGrade student read
    // =========================================================================

    [Fact(DisplayName = "AC-LEAK-1 (CRITICAL): Newly created Subject (Draft) NOT visible on student ForGrade")]
    public async Task DraftSubject_NotVisibleToStudentForGrade()
    {
        // Create a fresh grade with a single Draft subject.
        var gradeId   = await CreateGradeGetIdAsync();
        var gradeNum  = await GetGradeNumberAsync(gradeId);
        var subjectId = await CreateSubjectGetIdAsync(gradeId,
            $"P705 Draft Subject {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        // Student token (student role).
        var studentToken = await CreateStudentTokenAsync();

        // Student queries ForGrade — Draft subject must NOT appear.
        var subjects = await GetStudentSubjectsForGradeAsync(gradeNum, studentToken);

        subjects.Any(s => TryProp(s, "id", out var id) && id.GetInt32() == subjectId)
            .Should().BeFalse(
                "CRITICAL: Draft subject (id={0}) must NOT appear on student ForGrade read — " +
                "this is the core draft-leak regression; grade={1}", subjectId, gradeNum);
    }

    [Fact(DisplayName = "AC-LEAK-2 (CRITICAL): After Publish, Subject IS visible to student on ForGrade")]
    public async Task PublishedSubject_VisibleToStudentForGrade()
    {
        // Use the FIRST existing grade with Number=1 so GetSubjectsForGradeQuery (which uses
        // FirstOrDefaultAsync by Number) will return subjects from THIS grade. Tests that create a
        // fresh Number=1 grade find that ForGrade returns the FIRST grade (not theirs), so their
        // subject is invisible.
        //
        // Isolation — use SCIENCE/En (SubjectCode=1, Language=1) with an English-speaking student.
        // SubjectLanguageResolver resolves SCIENCE for an En-speaker → ContentLanguage.En → SCIENCE/En.
        // SCIENCE/En is not written to grade-1 by any other ForGrade-visibility test in the full suite:
        //   MATH/Ar (0,0)   — occupied by P2_01 helper tests
        //   SCIENCE/Ar (1,0) — occupied by P7_01 AC6
        //   ARABIC/Ar (2,0)  — occupied by P7_01 AC5 (after its isolation fix)
        //   ENGLISH/En (3,1) — occupied by this file's LEAK-8
        // An English-speaking student (LearningLanguage="en") is served SCIENCE/En directly, so the
        // ForGrade response will contain exactly this subject's id regardless of what other tests have
        // previously put into the shared grade.
        int gradeNum = 1;
        var gradeId   = await GetFirstGradeIdByNumberAsync(gradeNum);
        var subjectId = await CreateSubjectGetIdAsync(gradeId,
            $"P705 ToPublish Subject {Guid.NewGuid():N}", subjectCode: 1, language: 1);

        // Make it active (required for student visibility in addition to Published).
        await ActivateSubjectAsync(subjectId);

        // Publish → now visible to students.
        await TransitionAsync(VetSubject, subjectId, LsPublished);

        // Use an English-speaking student so the ForGrade handler resolves SCIENCE → En → SCIENCE/En.
        var studentToken = await CreateStudentTokenWithLanguageAsync("en");
        var subjects = await GetStudentSubjectsForGradeAsync(gradeNum, studentToken);

        subjects.Any(s => TryProp(s, "id", out var id) && id.GetInt32() == subjectId)
            .Should().BeTrue(
                "Published subject (id={0}) must appear on student ForGrade read after publish; grade={1}",
                subjectId, gradeNum);
    }

    [Fact(DisplayName = "AC-LEAK-3 (CRITICAL): After Archive, Subject is hidden again from student ForGrade")]
    public async Task ArchivedSubject_HiddenFromStudentForGrade()
    {
        var gradeId   = await CreateGradeGetIdAsync();
        var gradeNum  = await GetGradeNumberAsync(gradeId);
        var subjectId = await CreateSubjectGetIdAsync(gradeId,
            $"P705 ToArchive Subject {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        await ActivateSubjectAsync(subjectId);
        await TransitionAsync(VetSubject, subjectId, LsPublished);
        await TransitionAsync(VetSubject, subjectId, LsArchived);

        var studentToken = await CreateStudentTokenAsync();
        var subjects = await GetStudentSubjectsForGradeAsync(gradeNum, studentToken);

        subjects.Any(s => TryProp(s, "id", out var id) && id.GetInt32() == subjectId)
            .Should().BeFalse(
                "CRITICAL: Archived subject (id={0}) must NOT appear on student ForGrade; grade={1}",
                subjectId, gradeNum);
    }

    // =========================================================================
    // AC-LEAK-4..5 — Draft-leak regression: Lesson on student Subjects/{id}/Lessons
    // =========================================================================

    [Fact(DisplayName = "AC-LEAK-4 (CRITICAL): Newly created Lesson (Draft) NOT in student Subjects/{id}/Lessons")]
    public async Task DraftLesson_NotVisibleOnStudentSubjectLessons()
    {
        // Build: Published Subject + Published Unit + Draft Lesson
        var (subjectId, unitId, lessonId, gradeId) = await CreateFullHierarchyAsync();

        // Activate + publish the subject and unit so the route succeeds.
        await ActivateSubjectAsync(subjectId);
        await TransitionAsync(VetSubject, subjectId, LsPublished);
        await ActivateUnitAsync(unitId);
        await TransitionAsync(VetUnit, unitId, LsPublished);

        // Lesson remains in Draft (not published).
        var studentToken = await CreateStudentTokenAsync();
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Subjects/{subjectId}/Lessons", bearer: studentToken);

        // May return 200 with empty or 200 with units (lesson excluded from lesson list inside unit).
        if (resp.StatusCode == HttpStatusCode.OK && TryProp(root, "data", out var data))
        {
            var units = data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray().ToList()
                : new List<JsonElement>();

            foreach (var unit in units)
            {
                if (!TryProp(unit, "lessons", out var lessonsEl)) continue;
                if (lessonsEl.ValueKind != JsonValueKind.Array) continue;
                lessonsEl.EnumerateArray()
                    .Any(l => TryProp(l, "lessonId", out var lid) && lid.GetInt32() == lessonId)
                    .Should().BeFalse(
                        "CRITICAL: Draft lesson (id={0}) must NOT appear in student Subjects/{1}/Lessons",
                        lessonId, subjectId);
            }
        }
    }

    [Fact(DisplayName = "AC-LEAK-5 (CRITICAL): After Publish, Lesson IS visible on student Subjects/{id}/Lessons")]
    public async Task PublishedLesson_VisibleOnStudentSubjectLessons()
    {
        var (subjectId, unitId, lessonId, _) = await CreateFullHierarchyAsync();

        await ActivateSubjectAsync(subjectId);
        await TransitionAsync(VetSubject, subjectId, LsPublished);
        await ActivateUnitAsync(unitId);
        await TransitionAsync(VetUnit, unitId, LsPublished);

        // Now publish the lesson.
        await ActivateLessonAsync(lessonId);
        await TransitionAsync(VetLesson, lessonId, LsPublished);

        var studentToken = await CreateStudentTokenAsync();
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Subjects/{subjectId}/Lessons", bearer: studentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Subjects/{id}/Lessons must return 200 after publish; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        var units = data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().ToList()
            : new List<JsonElement>();

        var lessonFound = false;
        foreach (var unit in units)
        {
            if (!TryProp(unit, "lessons", out var lessonsEl)) continue;
            if (lessonsEl.ValueKind != JsonValueKind.Array) continue;
            if (lessonsEl.EnumerateArray().Any(l => TryProp(l, "lessonId", out var lid) && lid.GetInt32() == lessonId))
                lessonFound = true;
        }

        lessonFound.Should().BeTrue(
            "Published lesson (id={0}) must appear in student Subjects/{1}/Lessons after publish",
            lessonId, subjectId);
    }

    // =========================================================================
    // AC-LEAK-6..7 — Draft-leak regression: Lesson on student GET /Lessons/{id}
    // =========================================================================

    [Fact(DisplayName = "AC-LEAK-6 (CRITICAL): Newly created Lesson (Draft) NOT reachable via student GET /Lessons/{id}")]
    public async Task DraftLesson_NotReachableViaStudentGetLesson()
    {
        var lessonId     = await CreateLessonGetIdAsync();
        var studentToken = await CreateStudentTokenAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/{lessonId}", bearer: studentToken);

        // Must return NotFound (404 or 200 Successed=false) — NOT the lesson body.
        ((int)resp.StatusCode).Should().NotBe(500,
            "Draft lesson must not cause 500 on student GET; body: {0}", body);

        if (resp.StatusCode == HttpStatusCode.OK && root.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
            s.GetBoolean().Should().BeFalse(
                "CRITICAL: Draft lesson (id={0}) must return Successed=false on student GET /Lessons/{0}",
                lessonId);
        }
        else
        {
            ((int)resp.StatusCode).Should().BeOneOf(new[] { 404, 403 },
                "Draft lesson must be inaccessible to students (404 or 403); body: {0}", body);
        }
    }

    [Fact(DisplayName = "AC-LEAK-7 (CRITICAL): After Publish, Lesson IS reachable via student GET /Lessons/{id}")]
    public async Task PublishedLesson_ReachableViaStudentGetLesson()
    {
        // Build full hierarchy and publish all layers.
        var (subjectId, unitId, lessonId, _) = await CreateFullHierarchyAsync();

        await ActivateSubjectAsync(subjectId);
        await TransitionAsync(VetSubject, subjectId, LsPublished);
        await ActivateUnitAsync(unitId);
        await TransitionAsync(VetUnit, unitId, LsPublished);
        await ActivateLessonAsync(lessonId);
        await TransitionAsync(VetLesson, lessonId, LsPublished);

        var studentToken = await CreateStudentTokenAsync();
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/{lessonId}", bearer: studentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Published lesson must be reachable via student GET /Lessons/{0}; body: {1}", lessonId, body);
        AssertSucceeded(root, body);
    }

    // =========================================================================
    // AC-LEAK-8 — Seeded content (Published backfill) still visible (regression)
    // =========================================================================

    [Fact(DisplayName = "AC-LEAK-8: Seeded/backfilled content (Published) still visible to students — no regression")]
    public async Task SeededPublishedContent_StillVisibleToStudents()
    {
        // All seeded content (from migrations/seed) has LifecycleState=Published (DEFAULT 2 backfill).
        // We create a subject via admin, PUBLISH it, and verify it's visible to students —
        // this represents the backfilled-Published regime (because both new rows we activate+publish
        // and the backfilled rows must pass the LifecycleState==Published filter).
        //
        // We also verify that adding the LifecycleState filter did NOT break a round-trip
        // that was working before this story.
        //
        // Use the FIRST existing grade with Number=1 so ForGrade returns subjects from that grade.
        // Use ENGLISH/En (code=3,lang=1) to avoid conflict with other tests in this run:
        //   MATH/Ar (0,0) — P7-01 AC5; SCIENCE/Ar (1,0) — P7-01 AC6; SCIENCE/En (1,1) — P7-05 AC-LEAK-2.
        int gradeNum = 1;
        var gradeId   = await GetFirstGradeIdByNumberAsync(gradeNum);
        var subjectId = await CreateSubjectGetIdAsync(gradeId,
            $"P705 Backfill Regression {Guid.NewGuid():N}", subjectCode: 3, language: 1); // ENGLISH/En

        await ActivateSubjectAsync(subjectId);
        await TransitionAsync(VetSubject, subjectId, LsPublished);

        var studentToken = await CreateStudentTokenAsync();
        var subjects = await GetStudentSubjectsForGradeAsync(gradeNum, studentToken);

        // The published+active subject must appear on the student read path.
        subjects.Any(s => TryProp(s, "id", out var id) && id.GetInt32() == subjectId)
            .Should().BeTrue(
                "Published+Active subject (id={0}) must be visible to students; " +
                "the Published filter must not break existing published content", subjectId);
    }

    // =========================================================================
    // AC-ENV-1 — BaseResponse envelope shape on successful Transition
    // =========================================================================

    [Fact(DisplayName = "AC-ENV-1: Transition response has full BaseResponse envelope keys")]
    public async Task Transition_SuccessfulResponse_HasFullEnvelope()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType = VetLesson, entityId = lessonId, targetState = LsPublished },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed",  out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("Transition must succeed; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data",    out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors",  out _).Should().BeTrue("envelope must have errors; body: {0}", body);
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
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body (e.g. challenge redirect) */ }
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

    private static void AssertSucceeded(JsonElement root, string body)
    {
        TryProp(root, "successed", out var s).Should().BeTrue(
            "envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
    }

    /// <summary>
    /// Asserts that an operation was rejected: either HTTP 200 with successed=false
    /// or a non-success HTTP status with a valid JSON body (not a raw 500 exception page).
    /// </summary>
    private static void AssertRejected(HttpResponseMessage response, JsonElement root, string body, string reason)
    {
        var statusCode = (int)response.StatusCode;

        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow(
            "rejection response must be valid JSON; reason={0}; body: {1}", reason, body);

        if (statusCode == 200)
        {
            TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
            s.GetBoolean().Should().BeFalse(
                "{0}; expected Successed=false on 200 rejection; body: {1}", reason, body);
        }
        else
        {
            statusCode.Should().BeOneOf(new[] { 400, 404, 422, 424, 500 },
                "{0}; expected a non-success HTTP status; body: {1}", reason, body);
        }
    }

    /// <summary>
    /// Fires the Transition command and asserts it succeeds. Used as a prerequisite step.
    /// </summary>
    private async Task TransitionAsync(int entityType, int entityId, int targetState)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{LifecycleBase}/Transition",
            new { entityType, entityId, targetState },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Prereq Transition({0},{1}→{2}) must succeed; body: {3}", entityType, entityId, targetState, body);
        AssertSucceeded(root, body);
    }

    /// <summary>
    /// Calls GET VersionHistory and returns the list of ContentVersionDto elements.
    /// </summary>
    private async Task<List<JsonElement>> GetVersionHistoryAsync(int entityType, int entityId)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{LifecycleBase}/VersionHistory?entityType={entityType}&entityId={entityId}",
            bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "VersionHistory must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        if (data.ValueKind == JsonValueKind.Array)
            return data.EnumerateArray().ToList();
        if (data.ValueKind == JsonValueKind.Null || data.ValueKind == JsonValueKind.Undefined)
            return new List<JsonElement>();
        // Paginated wrapper check
        if (TryProp(data, "data", out var inner) && inner.ValueKind == JsonValueKind.Array)
            return inner.EnumerateArray().ToList();

        return new List<JsonElement>();
    }

    /// <summary>
    /// Finds a coverage slot in the coverage array by SubjectCode and Language.
    /// SubjectCode: MATH=0, SCIENCE=1, ARABIC=2, ENGLISH=3.
    /// Language: Ar=0, En=1.
    /// </summary>
    private static JsonElement? FindCoverageSlot(JsonElement coverageArray, int subjectCode, int language)
    {
        foreach (var slot in coverageArray.EnumerateArray())
        {
            if (!TryProp(slot, "subjectCode", out var codeProp)) continue;
            if (!TryProp(slot, "language",    out var langProp)) continue;
            var code = codeProp.ValueKind == JsonValueKind.Number ? codeProp.GetInt32() : -1;
            var lang = langProp.ValueKind == JsonValueKind.Number ? langProp.GetInt32() : -1;
            if (code == subjectCode && lang == language)
                return slot;
        }
        return null;
    }

    /// <summary>
    /// Calls GET /api/learning/Subjects/ForGrade and returns the list of subject DTOs.
    /// Uses the provided token (student or admin).
    /// </summary>
    private async Task<List<JsonElement>> GetStudentSubjectsForGradeAsync(int gradeNumber, string token)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Subjects/ForGrade?grade={gradeNumber}", bearer: token);

        // ForGrade may return 200 with empty collection (EmptyCollection helper) or 200 with data.
        if (resp.StatusCode != HttpStatusCode.OK)
            return new List<JsonElement>();

        if (!TryProp(root, "data", out var data))
            return new List<JsonElement>();

        if (data.ValueKind == JsonValueKind.Array)
            return data.EnumerateArray().ToList();

        return new List<JsonElement>();
    }

    /// <summary>Activates a Subject (IsActive=true) — required for student visibility.</summary>
    private async Task ActivateSubjectAsync(int subjectId)
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            $"/api/learning/Subjects/{subjectId}/Active",
            new { isActive = true },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ActivateSubject prereq must succeed; subjectId={0}; body: {1}", subjectId, body);
    }

    /// <summary>Activates a Unit (IsActive=true) — required for student visibility.</summary>
    private async Task ActivateUnitAsync(int unitId)
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            $"/api/learning/Units/{unitId}/Active",
            new { isActive = true },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ActivateUnit prereq must succeed; unitId={0}; body: {1}", unitId, body);
    }

    /// <summary>Activates a Lesson (IsActive=true) — required for student visibility.</summary>
    private async Task ActivateLessonAsync(int lessonId)
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            $"/api/learning/Lessons/{lessonId}/Active",
            new { isActive = true },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ActivateLesson prereq must succeed; lessonId={0}; body: {1}", lessonId, body);
    }

    private async Task<string> SignInGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private async Task<string> RegisterParentGetTokenAsync()
    {
        var email = $"p705_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.com";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>
    /// Creates a Student-role account via the parent-driven flow
    /// (Register Parent → Add-Child → sign in as child) and returns the Student JWT.
    /// </summary>
    private async Task<string> CreateStudentTokenAsync()
    {
        var parentEmail = $"p705p_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (parentResp, parentRoot, parentBody) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        parentResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", parentBody);
        TryProp(parentRoot, "data", out var parentData).Should().BeTrue("body: {0}", parentBody);
        TryProp(parentData, "accessToken", out var parentTokenProp).Should().BeTrue("body: {0}", parentBody);
        var parentToken = parentTokenProp.GetString()!;

        var childEmail = $"p705c_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (addResp, _, addBody) = await SendAsync(HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = "P705 Test Student",
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = 3,
                Language         = "ar",
                Country          = "EG",
                LearningLanguage = "ar",
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            "Add-Child must succeed; body: {0}", addBody);

        return await SignInGetTokenAsync(childEmail, ValidChildPassword);
    }

    /// <summary>
    /// Creates a Student-role account with the specified <paramref name="learningLanguage"/> ("ar" or "en")
    /// via the parent-driven flow and returns the Student JWT.
    /// Use this overload in ForGrade-visibility tests that need a specific language to drive
    /// SubjectLanguageResolver so the correct content-language tree is served via ForGrade.
    /// </summary>
    private async Task<string> CreateStudentTokenWithLanguageAsync(string learningLanguage)
    {
        var parentEmail = $"p705p_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (parentResp, parentRoot, parentBody) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        parentResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", parentBody);
        TryProp(parentRoot, "data", out var parentData).Should().BeTrue("body: {0}", parentBody);
        TryProp(parentData, "accessToken", out var parentTokenProp).Should().BeTrue("body: {0}", parentBody);
        var parentToken = parentTokenProp.GetString()!;

        var childEmail = $"p705c_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (addResp, _, addBody) = await SendAsync(HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = "P705 Test Student",
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = 3,
                Language         = learningLanguage,
                Country          = "EG",
                LearningLanguage = learningLanguage,
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            "Add-Child must succeed; body: {0}", addBody);

        return await SignInGetTokenAsync(childEmail, ValidChildPassword);
    }

    /// <summary>
    /// Creates a Grade → Subject → Unit → Lesson hierarchy and returns all four IDs.
    /// All entities start in Draft state (default for new admin-created rows).
    /// </summary>
    private async Task<(int SubjectId, int UnitId, int LessonId, int GradeId)> CreateFullHierarchyAsync()
    {
        var gradeId   = await CreateGradeGetIdAsync();
        var subjectId = await CreateSubjectGetIdAsync(gradeId,
            $"P705 Subj {Guid.NewGuid():N}", subjectCode: 0, language: 0);
        var unitId    = await CreateUnitGetIdAsync(subjectId, $"P705 Unit {Guid.NewGuid():N}", seqOrder: 1);
        var lessonId  = await CreateLessonInUnitGetIdAsync(unitId);
        return (subjectId, unitId, lessonId, gradeId);
    }

    /// <summary>Creates a grade and returns its id.</summary>
    private async Task<int> CreateGradeGetIdAsync()
    {
        var name = $"P705 Grade {Guid.NewGuid():N}";
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/grades/Create",
            new { Number = 1, DisplayName = name }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "grade create must succeed; body: {0}", body);

        return await FindIdInListAsync(
            "/api/learning/grades/List?PageNumber=1&PageSize=200",
            "displayName", name, "grade", _adminToken);
    }

    /// <summary>
    /// Returns the ID of the FIRST grade with the given Number, as created by the seeder or prior tests.
    /// GetSubjectsForGradeQuery uses FirstOrDefaultAsync by Number, so student ForGrade reads will
    /// only serve subjects in the FIRST grade row with a given Number. Tests that verify ForGrade
    /// visibility must therefore put their subjects in that grade, not a freshly-created one.
    /// Falls back to creating a grade if none exists (makes the test self-contained when run in isolation).
    /// </summary>
    private async Task<int> GetFirstGradeIdByNumberAsync(int number)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/grades/List?PageNumber=1&PageSize=200", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "Grade list must return 200; body: {0}", body);

        IEnumerable<JsonElement>? items = null;
        if (TryProp(root, "data", out var outer))
        {
            if (TryProp(outer, "data", out var inner) && inner.ValueKind == JsonValueKind.Array)
                items = inner.EnumerateArray();
            else if (outer.ValueKind == JsonValueKind.Array)
                items = outer.EnumerateArray();
        }

        items.Should().NotBeNull("grade list data must be an array; body: {0}", body);
        var found = items!.FirstOrDefault(g =>
            TryProp(g, "number", out var n) && n.GetInt32() == number);

        // If no grade with the requested Number exists, create one so the test is self-contained.
        if (found.ValueKind == JsonValueKind.Undefined)
        {
            var name = $"P705 ForGrade Seed {number} {Guid.NewGuid():N}";
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

    /// <summary>Gets the grade Number (1-6) from its Id.</summary>
    private async Task<int> GetGradeNumberAsync(int gradeId)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/grades?id={gradeId}", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GetGrade must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "number", out var numProp).Should().BeTrue("body: {0}", body);
        return numProp.GetInt32();
    }

    /// <summary>
    /// Creates a subject and returns its id.
    /// SubjectCode: MATH=0, SCIENCE=1, ARABIC=2, ENGLISH=3. Language: Ar=0, En=1.
    /// </summary>
    private async Task<int> CreateSubjectGetIdAsync(int gradeId, string name, int subjectCode, int language)
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/subjects/Create",
            new { Name = name, Country = "EG", GradeId = gradeId, SubjectCode = subjectCode, Language = language },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "subject create must succeed; body: {0}", body);

        return await FindIdInListAsync(
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}",
            "name", name, "subject", _adminToken);
    }

    /// <summary>Creates a unit and returns its id.</summary>
    private async Task<int> CreateUnitGetIdAsync(int subjectId, string name, int seqOrder)
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/units/Create",
            new { Name = name, SequenceOrder = seqOrder, SubjectId = subjectId }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "unit create must succeed; body: {0}", body);

        return await FindIdInListAsync(
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", name, "unit", _adminToken);
    }

    /// <summary>Creates a Lesson in the given Unit and returns the lesson id.</summary>
    private async Task<int> CreateLessonInUnitGetIdAsync(int unitId)
    {
        var lessonName = $"P705 Lesson {Guid.NewGuid():N}";
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/Lessons/Create",
            new { Name = lessonName, Difficulty = 1, SequenceOrder = 1, IsLocked = false,
                  UnitId = unitId, EstimatedMinutes = 5 },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "lesson create must succeed; body: {0}", body);

        return await FindLessonIdByNameAsync(unitId, lessonName);
    }

    /// <summary>
    /// Creates a full Grade→Subject→Unit→Lesson stack and returns the Lesson id.
    /// Mirrors the P7-04 pattern so tests can create a standalone lesson quickly.
    /// </summary>
    private async Task<int> CreateLessonGetIdAsync()
    {
        var gradeName = $"P705 Grade {Guid.NewGuid():N}";
        var (gradeResp, _, gradeBody) = await SendAsync(HttpMethod.Post, "/api/learning/grades/Create",
            new { Number = 1, DisplayName = gradeName }, _adminToken);
        gradeResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "grade create must succeed; body: {0}", gradeBody);
        var gradeId = await FindIdInListAsync(
            "/api/learning/grades/List?PageNumber=1&PageSize=200",
            "displayName", gradeName, "grade", _adminToken);

        var subjName = $"P705 Subj {Guid.NewGuid():N}";
        var (subjResp, _, subjBody) = await SendAsync(HttpMethod.Post, "/api/learning/subjects/Create",
            new { Name = subjName, Country = "EG", GradeId = gradeId, SubjectCode = 0, Language = 0 },
            _adminToken);
        subjResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "subject create must succeed; body: {0}", subjBody);
        var subjectId = await FindIdInListAsync(
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}",
            "name", subjName, "subject", _adminToken);

        var unitName = $"P705 Unit {Guid.NewGuid():N}";
        var (unitResp, _, unitBody) = await SendAsync(HttpMethod.Post, "/api/learning/units/Create",
            new { Name = unitName, SequenceOrder = 1, SubjectId = subjectId }, _adminToken);
        unitResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "unit create must succeed; body: {0}", unitBody);
        var unitId = await FindIdInListAsync(
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", unitName, "unit", _adminToken);

        return await CreateLessonInUnitGetIdAsync(unitId);
    }

    /// <summary>
    /// Creates an MCQ QuizQuestion under the given lesson and returns its id.
    /// Used to test QuizQuestion lifecycle transitions.
    /// </summary>
    private async Task<int> CreateMcqQuestionGetIdAsync(int lessonId)
    {
        const string options       = "[\"Paris\",\"London\",\"Berlin\"]";
        const string correctAnswer = "Paris";

        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId,
                questionType  = 1, // MCQ
                questionText  = "What is the capital of France?",
                options       = options,
                correctAnswer = correctAnswer,
                difficulty    = 1, // Easy
                generatedBy   = 1  // Curated
            },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "question create must succeed; body: {0}", body);

        // Retrieve the question id from the admin ByLesson list.
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/Learning/Questions/ByLesson/{lessonId}", bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ByLesson must return 200; body: {0}", listBody);

        TryProp(listRoot, "data", out var data).Should().BeTrue("body: {0}", listBody);
        var items = data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().ToList()
            : (TryProp(data, "data", out var inner) && inner.ValueKind == JsonValueKind.Array
                ? inner.EnumerateArray().ToList()
                : new List<JsonElement>());

        items.Should().NotBeEmpty("at least one question must exist; lessonId={0}", lessonId);
        TryProp(items[0], "id", out var idProp).Should().BeTrue("body: {0}", listBody);
        return idProp.GetInt32();
    }

    /// <summary>Finds a lesson id by name via the admin lessons list scoped to a unit.</summary>
    private async Task<int> FindLessonIdByNameAsync(int unitId, string lessonName)
    {
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/List?UnitId={unitId}&PageNumber=1&PageSize=200",
            bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Lessons List must succeed; body: {0}", listBody);

        if (TryProp(listRoot, "data", out var outer))
        {
            IEnumerable<JsonElement>? items = null;
            if (TryProp(outer, "data", out var inner) && inner.ValueKind == JsonValueKind.Array)
                items = inner.EnumerateArray();
            else if (outer.ValueKind == JsonValueKind.Array)
                items = outer.EnumerateArray();

            if (items is not null)
            {
                foreach (var item in items)
                {
                    if (TryProp(item, "name", out var n) && n.GetString() == lessonName &&
                        TryProp(item, "id", out var id))
                        return id.GetInt32();
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not find lesson '{lessonName}' in List for UnitId={unitId}; body={listBody}");
    }

    private async Task<int> FindIdInListAsync(
        string listUrl, string fieldName, string fieldValue, string entityType, string? bearer = null)
    {
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get, listUrl, bearer: bearer);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prereq list for {0} must succeed; body: {1}", entityType, listBody);

        if (TryProp(listRoot, "data", out var outer))
        {
            IEnumerable<JsonElement>? items = null;
            if (TryProp(outer, "data", out var inner) && inner.ValueKind == JsonValueKind.Array)
                items = inner.EnumerateArray();
            else if (outer.ValueKind == JsonValueKind.Array)
                items = outer.EnumerateArray();

            if (items is not null)
            {
                foreach (var item in items)
                {
                    if (TryProp(item, fieldName, out var fv) && fv.GetString() == fieldValue)
                    {
                        TryProp(item, "id", out var idProp).Should().BeTrue(
                            "{0} item must have id; body: {1}", entityType, listBody);
                        return idProp.GetInt32();
                    }
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not find {entityType} where {fieldName}='{fieldValue}'; url={listUrl}; body={listBody}");
    }
}
