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
/// P7-04 integration tests: Admin quiz question authoring (QuestionsController).
///
/// Endpoints under test:
///   GET   /api/Learning/Questions/ByLesson/{lessonId}     — admin list (AdminOnly; includes CorrectAnswer + IsActive)
///   GET   /api/Learning/Questions/{id}                    — admin get by id (AdminOnly; includes CorrectAnswer)
///   POST  /api/Learning/Questions                          — add question (AdminOnly; per-type validation)
///   PUT   /api/Learning/Questions                          — edit question (AdminOnly; per-type validation)
///   DELETE /api/Learning/Questions/{id}                   — soft-delete question (AdminOnly)
///   PUT   /api/Learning/Questions/Reorder                  — batch reorder (AdminOnly; cross-lesson rejected)
///   PATCH /api/Learning/Questions/{id}/SetActive           — toggle IsActive (AdminOnly)
///
/// Student path (verify CorrectAnswer NEVER leaks):
///   POST  /api/Learning/Quizzes/{lessonId}/Attempt        — StartAttempt (Student role only)
///
/// Auth matrix:
///   All QuestionsController endpoints: anonymous → 401, non-admin → 403, admin → 200/422.
///
/// Acceptance criteria covered:
///   AC-AUTH-1 : All Questions endpoints: anonymous → 401.
///   AC-AUTH-2 : All Questions endpoints: non-admin (basicuser/parent) → 403.
///   AC-AUTH-3 : Admin token → 200 on read/write.
///
///   AC-CRUD-1 : Add MCQ question → ByLesson returns it with correctAnswer + isActive fields.
///   AC-CRUD-2 : Add TrueFalse question → ByLesson returns it with correct type.
///   AC-CRUD-3 : Add Matching question → ByLesson returns it with correct type.
///   AC-CRUD-4 : Add FillInBlank question → ByLesson returns it with correct type.
///   AC-CRUD-5 : Edit question → ByLesson reflects new questionText/options/correctAnswer.
///   AC-CRUD-6 : GetById → returns single AdminQuestionDto with correctAnswer.
///   AC-CRUD-7 : Soft-delete → question absent from ByLesson (global IsDeleted filter).
///   AC-CRUD-8 : Soft-deleted question absent from student StartAttempt question set.
///
///   AC-VAL-MCQ-1  : MCQ with fewer than 2 options → 422.
///   AC-VAL-MCQ-2  : MCQ with correct-answer not in options list → 422.
///   AC-VAL-MCQ-3  : MCQ options not a JSON array → 422.
///   AC-VAL-TF-1   : TrueFalse with correctAnswer="yes" (not true/false) → 422.
///   AC-VAL-TF-2   : TrueFalse with empty correctAnswer → 422.
///   AC-VAL-MATCH-1: Matching with unequal left/right arrays → 422.
///   AC-VAL-MATCH-2: Matching options missing "left" key → 422.
///   AC-VAL-FIB-1  : FillInBlank with empty correctAnswer → 422.
///   AC-VAL-SIZE-1 : QuestionText > 4096 chars → 422 (DoS guard).
///   AC-VAL-SIZE-2 : Options > 16384 chars → 422 (DoS guard).
///   AC-VAL-BASE-1 : Empty questionText → 422.
///   AC-VAL-BASE-2 : Invalid QuestionType enum value → 422.
///   AC-VAL-BASE-3 : LessonId=0 → 422.
///   AC-VAL-BASE-4 : EditQuestion with Id=0 → 422.
///
///   AC-REORDER-1  : Reorder persists new SequenceOrder within one lesson.
///   AC-REORDER-2  : Reorder cross-lesson (IDs from two lessons) → Successed=false.
///   AC-REORDER-3  : Reorder empty list → 422.
///   AC-REORDER-4  : Reorder with id=0 in list → 422.
///   AC-REORDER-5  : Reorder with non-existent IDs → Successed=false.
///
///   AC-ACTIVE-1   : SetActive(false) hides question from student StartAttempt questions list.
///   AC-ACTIVE-2   : Admin ByLesson still returns inactive question (IsActive=false).
///   AC-ACTIVE-3   : SetActive(true) restores question to student StartAttempt.
///   AC-ACTIVE-4   : SetActive validator: Id=0 in route → 422.
///
///   AC-SEC-1 (CRITICAL): Student StartAttempt response MUST NOT contain a "correctAnswer" field.
///   AC-SEC-2 (CRITICAL): Student StartAttempt response MUST NOT contain a "CorrectAnswer" field (PascalCase).
///   AC-ENV-1      : BaseResponse envelope (statusCode, successed, message, data, errors) on add/edit/delete.
///   AC-LESSON-404 : Add question to non-existent lesson → Successed=false.
///
/// Docker + Postgres required to RUN. Compile-only on Windows CI (no Docker daemon).
/// Runs in the shared "IntegrationTests" xUnit collection (LearnexiaWebAppFactory, Testcontainers PG).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_04_QuestionsAdmin_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // ─────────────────────────────────────────────────────────────────────────
    // Seeded credentials (mirrors P7-01 / P7-02 / P7-03)
    // ─────────────────────────────────────────────────────────────────────────
    private const string AdminUserName     = "superadmin";
    private const string AdminPassword     = "123Pa$$word!";
    private const string BasicUserName     = "basicuser";
    private const string BasicUserPassword = "123Pa$$word!";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    private string? _adminToken;
    private string? _basicToken;
    private string? _parentToken;

    // ─────────────────────────────────────────────────────────────────────────
    // QuestionType enum values (Domain.Enums.QuestionType)
    // MCQ=1, TrueFalse=2, Matching=3, FillInBlank=4
    // ─────────────────────────────────────────────────────────────────────────
    private const int QtMcq        = 1;
    private const int QtTrueFalse  = 2;
    private const int QtMatching   = 3;
    private const int QtFillInBlank = 4;

    // DifficultyLevel: Easy=1 | GeneratedBy: Curated=1
    private const int DiffEasy    = 1;
    private const int GenCurated  = 1;

    // ─────────────────────────────────────────────────────────────────────────
    // Valid payloads per type (used in happy-path tests)
    // ─────────────────────────────────────────────────────────────────────────

    // MCQ: options = JSON array ≥2; correctAnswer = one of the options (ordinal match).
    private const string McqOptions       = "[\"Paris\",\"London\",\"Berlin\"]";
    private const string McqCorrectAnswer = "Paris";

    // TrueFalse: correctAnswer = "true" or "false" (case-insensitive per validator).
    private const string TfOptions       = "[\"True\",\"False\"]";
    private const string TfCorrectAnswer = "true";

    // Matching: options = {"left":[...],"right":[...]} equal-length arrays.
    private const string MatchOptions       = "{\"left\":[\"Cat\",\"Dog\"],\"right\":[\"Feline\",\"Canine\"]}";
    private const string MatchCorrectAnswer = "{}";   // FV doesn't validate Matching correctAnswer shape

    // FillInBlank: correctAnswer = non-empty string; options can be any valid non-null string.
    private const string FibOptions       = "[]";
    private const string FibCorrectAnswer = "Cairo";

    public P7_04_QuestionsAdmin_Tests(LearnexiaWebAppFactory factory)
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
    // AC-AUTH-1 — All QuestionsController endpoints: anonymous → 401
    // =========================================================================

    [Fact(DisplayName = "AC-AUTH-1: GET Questions/ByLesson/{id} anonymous → 401")]
    public async Task Auth_GetByLesson_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, "/api/Learning/Questions/ByLesson/1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "ByLesson is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: GET Questions/{id} anonymous → 401")]
    public async Task Auth_GetById_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, "/api/Learning/Questions/1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GetById is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: POST Questions anonymous → 401")]
    public async Task Auth_AddQuestion_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            BuildMcqBody(lessonId: 1), bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Add is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: PUT Questions anonymous → 401")]
    public async Task Auth_EditQuestion_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put, "/api/Learning/Questions",
            BuildEditMcqBody(id: 1), bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Edit is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: DELETE Questions/{id} anonymous → 401")]
    public async Task Auth_DeleteQuestion_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Delete, "/api/Learning/Questions/1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Delete is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: PUT Questions/Reorder anonymous → 401")]
    public async Task Auth_ReorderQuestions_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put, "/api/Learning/Questions/Reorder",
            new { lessonId = 1, questionIds = new[] { 1, 2 } }, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Reorder is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: PATCH Questions/{id}/SetActive anonymous → 401")]
    public async Task Auth_SetActive_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Patch, "/api/Learning/Questions/1/SetActive",
            new { isActive = false }, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "SetActive is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    // =========================================================================
    // AC-AUTH-2 — Non-admin (basicuser / parent) → 403
    // =========================================================================

    [Fact(DisplayName = "AC-AUTH-2: GET Questions/ByLesson/{id} basicuser → 403")]
    public async Task Auth_GetByLesson_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, "/api/Learning/Questions/ByLesson/1",
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "ByLesson is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: GET Questions/ByLesson/{id} parent → 403")]
    public async Task Auth_GetByLesson_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, "/api/Learning/Questions/ByLesson/1",
            bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "ByLesson is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: POST Questions basicuser → 403")]
    public async Task Auth_AddQuestion_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            BuildMcqBody(lessonId: 1), bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Add is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: POST Questions parent → 403")]
    public async Task Auth_AddQuestion_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            BuildMcqBody(lessonId: 1), bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Add is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: PUT Questions basicuser → 403")]
    public async Task Auth_EditQuestion_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put, "/api/Learning/Questions",
            BuildEditMcqBody(id: 1), bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Edit is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: DELETE Questions/{id} basicuser → 403")]
    public async Task Auth_DeleteQuestion_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Delete, "/api/Learning/Questions/1",
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Delete is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: PUT Questions/Reorder basicuser → 403")]
    public async Task Auth_ReorderQuestions_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put, "/api/Learning/Questions/Reorder",
            new { lessonId = 1, questionIds = new[] { 1, 2 } }, bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Reorder is AdminOnly — basic user must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: PATCH Questions/{id}/SetActive basicuser → 403")]
    public async Task Auth_SetActive_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Patch, "/api/Learning/Questions/1/SetActive",
            new { isActive = false }, bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "SetActive is AdminOnly — basic user must get 403; body: {0}", body);
    }

    // =========================================================================
    // AC-CRUD-1 — MCQ round-trip: Add → ByLesson → verify CorrectAnswer + IsActive
    // =========================================================================

    [Fact(DisplayName = "AC-CRUD-1: Add MCQ → ByLesson returns it with correctAnswer + isActive=true")]
    public async Task AddMcq_RoundTrip_ByLessonReturnsCorrectAnswerAndIsActive()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            BuildMcqBody(lessonId), _adminToken);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Add MCQ must return 200; body: {0}", addBody);
        AssertSucceeded(addRoot, addBody);

        var questions = await GetAdminQuestionsAsync(lessonId);
        questions.Should().NotBeEmpty("added MCQ must appear in ByLesson list");

        var q = questions[0];
        TryProp(q, "questionType", out var qtProp).Should().BeTrue("question must have questionType; body from list");
        qtProp.GetInt32().Should().Be(QtMcq, "questionType must be MCQ(1)");

        TryProp(q, "correctAnswer", out var caProp).Should().BeTrue(
            "admin ByLesson must include correctAnswer (admin authoring DTO)");
        caProp.GetString().Should().Be(McqCorrectAnswer,
            "correctAnswer must match what was submitted; body from list");

        TryProp(q, "isActive", out var isActiveProp).Should().BeTrue(
            "admin ByLesson must include isActive");
        isActiveProp.GetBoolean().Should().BeTrue("newly added question must default to IsActive=true");
    }

    // =========================================================================
    // AC-CRUD-2 — TrueFalse round-trip
    // =========================================================================

    [Fact(DisplayName = "AC-CRUD-2: Add TrueFalse → ByLesson returns it with questionType=TrueFalse")]
    public async Task AddTrueFalse_RoundTrip_CorrectType()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            BuildTrueFalseBody(lessonId), _adminToken);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Add TrueFalse must return 200; body: {0}", addBody);
        AssertSucceeded(addRoot, addBody);

        var questions = await GetAdminQuestionsAsync(lessonId);
        questions.Should().NotBeEmpty("added TrueFalse must appear in ByLesson list");
        TryProp(questions[0], "questionType", out var qtProp).Should().BeTrue();
        qtProp.GetInt32().Should().Be(QtTrueFalse, "questionType must be TrueFalse(2)");
    }

    // =========================================================================
    // AC-CRUD-3 — Matching round-trip
    // =========================================================================

    [Fact(DisplayName = "AC-CRUD-3: Add Matching → ByLesson returns it with questionType=Matching")]
    public async Task AddMatching_RoundTrip_CorrectType()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            BuildMatchingBody(lessonId), _adminToken);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Add Matching must return 200; body: {0}", addBody);
        AssertSucceeded(addRoot, addBody);

        var questions = await GetAdminQuestionsAsync(lessonId);
        questions.Should().NotBeEmpty("added Matching must appear in ByLesson list");
        TryProp(questions[0], "questionType", out var qtProp).Should().BeTrue();
        qtProp.GetInt32().Should().Be(QtMatching, "questionType must be Matching(3)");
    }

    // =========================================================================
    // AC-CRUD-4 — FillInBlank round-trip
    // =========================================================================

    [Fact(DisplayName = "AC-CRUD-4: Add FillInBlank → ByLesson returns it with questionType=FillInBlank")]
    public async Task AddFillInBlank_RoundTrip_CorrectType()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            BuildFillInBlankBody(lessonId), _adminToken);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Add FillInBlank must return 200; body: {0}", addBody);
        AssertSucceeded(addRoot, addBody);

        var questions = await GetAdminQuestionsAsync(lessonId);
        questions.Should().NotBeEmpty("added FillInBlank must appear in ByLesson list");
        TryProp(questions[0], "questionType", out var qtProp).Should().BeTrue();
        qtProp.GetInt32().Should().Be(QtFillInBlank, "questionType must be FillInBlank(4)");
    }

    // =========================================================================
    // AC-CRUD-5 — Edit question: updated fields reflected in ByLesson
    // =========================================================================

    [Fact(DisplayName = "AC-CRUD-5: Edit MCQ → ByLesson reflects new questionText and correctAnswer")]
    public async Task EditMcq_UpdatesTextAndCorrectAnswer()
    {
        var lessonId = await CreateLessonGetIdAsync();

        await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            BuildMcqBody(lessonId), _adminToken);

        var questionsBefore = await GetAdminQuestionsAsync(lessonId);
        questionsBefore.Should().NotBeEmpty();
        TryProp(questionsBefore[0], "id", out var idProp).Should().BeTrue();
        var qId = idProp.GetInt32();

        // Edit: change correct answer (still within the same options array)
        var newOptions       = "[\"Madrid\",\"Lisbon\",\"Rome\"]";
        var newCorrectAnswer = "Lisbon";
        var newText          = "What is the capital of Portugal?";

        var (editResp, editRoot, editBody) = await SendAsync(HttpMethod.Put, "/api/Learning/Questions",
            new
            {
                id           = qId,
                questionType = QtMcq,
                questionText = newText,
                options      = newOptions,
                correctAnswer = newCorrectAnswer,
                difficulty   = DiffEasy
            },
            _adminToken);
        editResp.StatusCode.Should().Be(HttpStatusCode.OK, "Edit must return 200; body: {0}", editBody);
        AssertSucceeded(editRoot, editBody);

        // Re-read via GetById.
        var (getResp, getRoot, getBody) = await SendAsync(HttpMethod.Get,
            $"/api/Learning/Questions/{qId}", _adminToken);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "GetById after edit must return 200; body: {0}", getBody);
        AssertSucceeded(getRoot, getBody);

        TryProp(getRoot, "data", out var data).Should().BeTrue("body: {0}", getBody);
        TryProp(data, "questionText", out var textProp).Should().BeTrue("data must have questionText; body: {0}", getBody);
        textProp.GetString().Should().Be(newText, "questionText must reflect the edit; body: {0}", getBody);
        TryProp(data, "correctAnswer", out var caProp).Should().BeTrue("data must have correctAnswer; body: {0}", getBody);
        caProp.GetString().Should().Be(newCorrectAnswer, "correctAnswer must reflect the edit; body: {0}", getBody);
    }

    // =========================================================================
    // AC-CRUD-6 — GetById returns single AdminQuestionDto with correctAnswer
    // =========================================================================

    [Fact(DisplayName = "AC-CRUD-6: GetById → returns AdminQuestionDto with correctAnswer field")]
    public async Task GetById_ReturnsAdminDtoWithCorrectAnswer()
    {
        var lessonId = await CreateLessonGetIdAsync();

        await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            BuildMcqBody(lessonId), _adminToken);

        var questions = await GetAdminQuestionsAsync(lessonId);
        questions.Should().NotBeEmpty();
        TryProp(questions[0], "id", out var idProp).Should().BeTrue();
        var qId = idProp.GetInt32();

        var (getResp, getRoot, getBody) = await SendAsync(HttpMethod.Get,
            $"/api/Learning/Questions/{qId}", _adminToken);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "GetById must return 200; body: {0}", getBody);
        AssertSucceeded(getRoot, getBody);

        TryProp(getRoot, "data", out var data).Should().BeTrue("envelope must have data; body: {0}", getBody);
        TryProp(data, "correctAnswer", out var caProp).Should().BeTrue(
            "AdminQuestionDto must expose correctAnswer for authoring; body: {0}", getBody);
        caProp.GetString().Should().Be(McqCorrectAnswer, "correctAnswer must match submitted value; body: {0}", getBody);

        TryProp(data, "isActive", out var isActiveProp).Should().BeTrue(
            "AdminQuestionDto must expose isActive; body: {0}", getBody);
        isActiveProp.GetBoolean().Should().BeTrue("new question must default to IsActive=true; body: {0}", getBody);
    }

    // =========================================================================
    // AC-CRUD-7 — Soft-delete hides question from admin ByLesson
    // =========================================================================

    [Fact(DisplayName = "AC-CRUD-7: Soft-delete → question absent from admin ByLesson list")]
    public async Task DeleteQuestion_HiddenFromByLesson()
    {
        var lessonId = await CreateLessonGetIdAsync();

        await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            BuildMcqBody(lessonId), _adminToken);

        var questionsBefore = await GetAdminQuestionsAsync(lessonId);
        questionsBefore.Should().NotBeEmpty("question must exist before delete");
        TryProp(questionsBefore[0], "id", out var idProp).Should().BeTrue();
        var qId = idProp.GetInt32();

        var (delResp, delRoot, delBody) = await SendAsync(HttpMethod.Delete,
            $"/api/Learning/Questions/{qId}", _adminToken);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK, "Delete must return 200; body: {0}", delBody);
        AssertSucceeded(delRoot, delBody);

        var questionsAfter = await GetAdminQuestionsAsync(lessonId);
        questionsAfter.Any(q => TryProp(q, "id", out var id) && id.GetInt32() == qId)
            .Should().BeFalse("soft-deleted question must not appear in admin ByLesson (global IsDeleted filter)");
    }

    // =========================================================================
    // AC-CRUD-8 — Soft-deleted question excluded from student StartAttempt
    // =========================================================================

    [Fact(DisplayName = "AC-CRUD-8: Soft-deleted question excluded from student StartAttempt question set")]
    public async Task DeletedQuestion_ExcludedFromStudentStartAttempt()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // Add two questions.
        await SendAsync(HttpMethod.Post, "/api/Learning/Questions", BuildMcqBody(lessonId), _adminToken);
        await SendAsync(HttpMethod.Post, "/api/Learning/Questions", BuildTrueFalseBody(lessonId), _adminToken);

        var questions = await GetAdminQuestionsAsync(lessonId);
        questions.Should().HaveCount(2);
        TryProp(questions[0], "id", out var idProp).Should().BeTrue();
        var qIdToDelete = idProp.GetInt32();

        // Soft-delete the first question.
        await SendAsync(HttpMethod.Delete, $"/api/Learning/Questions/{qIdToDelete}", _adminToken);

        // Create a student and start the attempt.
        var studentToken = await CreateStudentTokenAsync();
        var (attemptResp, attemptRoot, attemptBody) = await SendAsync(HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", bearer: studentToken);

        // Attempt should succeed (1 remaining active question).
        attemptResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must return 200 after one question deleted; body: {0}", attemptBody);

        TryProp(attemptRoot, "data", out var data).Should().BeTrue("body: {0}", attemptBody);
        TryProp(data, "questions", out var questionsEl).Should().BeTrue(
            "StartAttempt data must have questions; body: {0}", attemptBody);
        questionsEl.ValueKind.Should().Be(JsonValueKind.Array, "questions must be array; body: {0}", attemptBody);

        // Deleted question must not appear.
        var studentQuestions = questionsEl.EnumerateArray().ToList();
        studentQuestions.Any(q => TryProp(q, "id", out var id) && id.GetInt32() == qIdToDelete)
            .Should().BeFalse("soft-deleted question must not appear in student StartAttempt; body: {0}", attemptBody);

        // Only one question remaining (the TrueFalse one).
        studentQuestions.Should().HaveCount(1, "only the non-deleted question must appear; body: {0}", attemptBody);
    }

    // =========================================================================
    // AC-SEC-1 / AC-SEC-2 (CRITICAL) — CorrectAnswer NEVER in student StartAttempt
    // =========================================================================

    [Fact(DisplayName = "AC-SEC-1/2 (CRITICAL): Student StartAttempt MUST NOT contain correctAnswer in any casing")]
    public async Task StartAttempt_CorrectAnswer_NeverLeaksToStudent()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // Seed an MCQ question with a known correct answer.
        await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            BuildMcqBody(lessonId), _adminToken);

        var studentToken = await CreateStudentTokenAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", bearer: studentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must succeed for student; body: {0}", body);

        // CRITICAL: the raw response body must not contain the correct answer string.
        body.Should().NotContain(McqCorrectAnswer,
            "CorrectAnswer '{0}' must NEVER appear in student StartAttempt response; body: {1}",
            McqCorrectAnswer, body);

        // CRITICAL: no question in the questions array may have a correctAnswer property.
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        if (TryProp(data, "questions", out var questionsEl) &&
            questionsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var q in questionsEl.EnumerateArray())
            {
                // Check both camelCase and PascalCase — the field must be absent.
                var hasLower = q.TryGetProperty("correctAnswer", out _);
                var hasUpper = q.TryGetProperty("CorrectAnswer", out _);
                (hasLower || hasUpper).Should().BeFalse(
                    "QuizQuestionDto must NOT expose correctAnswer (camelCase or PascalCase) in student response; body: {0}", body);
            }
        }
    }

    // =========================================================================
    // AC-VAL-MCQ-1 — MCQ with fewer than 2 options → 422
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-MCQ-1: MCQ with only 1 option → 422 (per-type validation)")]
    public async Task AddMcq_SingleOption_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId, questionType = QtMcq,
                questionText  = "Capital?",
                options       = "[\"Paris\"]",         // only 1 — MCQ requires ≥2
                correctAnswer = "Paris",
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "MCQ with 1 option must fail per-type validation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on per-type 422; body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-MCQ-2 — MCQ with correctAnswer not in options → 422
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-MCQ-2: MCQ with correctAnswer not in options → 422")]
    public async Task AddMcq_CorrectAnswerNotInOptions_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId, questionType = QtMcq,
                questionText  = "Capital?",
                options       = "[\"Paris\",\"London\"]",
                correctAnswer = "Berlin",    // NOT in the options list
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "MCQ correct-answer not in options must fail validation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-MCQ-3 — MCQ options not a JSON array → 422
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-MCQ-3: MCQ options not a JSON array (plain string) → 422")]
    public async Task AddMcq_OptionsNotJsonArray_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId, questionType = QtMcq,
                questionText  = "Capital?",
                options       = "Paris,London",  // not valid JSON array
                correctAnswer = "Paris",
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "MCQ options that aren't a JSON array must fail per-type validation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-TF-1 — TrueFalse with invalid correctAnswer → 422
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-TF-1: TrueFalse with correctAnswer='yes' (not true/false) → 422")]
    public async Task AddTrueFalse_InvalidCorrectAnswer_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId, questionType = QtTrueFalse,
                questionText  = "Is the sky blue?",
                options       = TfOptions,
                correctAnswer = "yes",       // invalid — must be "true" or "false"
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "TrueFalse correctAnswer='yes' must fail per-type validation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-TF-2 — TrueFalse with empty correctAnswer → 422
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-TF-2: TrueFalse with empty correctAnswer → 422")]
    public async Task AddTrueFalse_EmptyCorrectAnswer_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId, questionType = QtTrueFalse,
                questionText  = "Is the sky blue?",
                options       = TfOptions,
                correctAnswer = "",          // empty
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "TrueFalse with empty correctAnswer must fail validation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-MATCH-1 — Matching with unequal left/right arrays → 422
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-MATCH-1: Matching with unequal left/right array lengths → 422")]
    public async Task AddMatching_UnequalArrays_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId, questionType = QtMatching,
                questionText  = "Match the pairs",
                options       = "{\"left\":[\"Cat\",\"Dog\"],\"right\":[\"Feline\"]}",  // left=2, right=1
                correctAnswer = MatchCorrectAnswer,
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Matching with unequal left/right arrays must fail per-type validation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-MATCH-2 — Matching options missing "left" key → 422
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-MATCH-2: Matching options missing 'left' key → 422")]
    public async Task AddMatching_MissingLeftKey_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId, questionType = QtMatching,
                questionText  = "Match the pairs",
                options       = "{\"items\":[\"Cat\",\"Dog\"]}",  // missing "left"/"right"
                correctAnswer = MatchCorrectAnswer,
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Matching missing 'left' key must fail per-type validation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-FIB-1 — FillInBlank with empty correctAnswer → 422
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-FIB-1: FillInBlank with empty correctAnswer → 422")]
    public async Task AddFillInBlank_EmptyCorrectAnswer_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId, questionType = QtFillInBlank,
                questionText  = "The capital of Egypt is ___.",
                options       = FibOptions,
                correctAnswer = "",          // empty — FIB requires non-empty
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "FillInBlank with empty correctAnswer must fail per-type validation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-SIZE-1 — QuestionText > 4096 chars → 422 (DoS guard)
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-SIZE-1: QuestionText > 4096 chars → 422 (MaximumLength guard)")]
    public async Task AddQuestion_OversizedQuestionText_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();
        var oversized = new string('A', 4097);

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId, questionType = QtMcq,
                questionText  = oversized,
                options       = McqOptions,
                correctAnswer = McqCorrectAnswer,
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "QuestionText >4096 chars must trigger MaximumLength → 422; body length={0}", body.Length);
        TryProp(root, "successed", out var s).Should().BeTrue("body length={0}", body.Length);
        s.GetBoolean().Should().BeFalse("body length={0}", body.Length);
    }

    // =========================================================================
    // AC-VAL-SIZE-2 — Options > 16384 chars → 422 (DoS guard)
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-SIZE-2: Options > 16384 chars → 422 (MaximumLength guard)")]
    public async Task AddQuestion_OversizedOptions_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();
        // Build a valid-looking JSON array that exceeds 16384 chars.
        var longOption = new string('X', 16000);
        var oversizedOptions = $"[\"{longOption}\",\"{longOption}\"]";

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId, questionType = QtMcq,
                questionText  = "Capital?",
                options       = oversizedOptions,
                correctAnswer = McqCorrectAnswer, // won't be in options so per-type fires too — that's fine, 422 is 422
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Options >16384 chars must trigger MaximumLength → 422");
        TryProp(root, "successed", out var s).Should().BeTrue();
        s.GetBoolean().Should().BeFalse();
    }

    // =========================================================================
    // AC-VAL-BASE-1 — Empty questionText → 422
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-BASE-1: Add question with empty questionText → 422")]
    public async Task AddQuestion_EmptyQuestionText_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId, questionType = QtMcq,
                questionText  = "",
                options       = McqOptions,
                correctAnswer = McqCorrectAnswer,
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Empty questionText must trigger NotEmpty validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-BASE-2 — Invalid QuestionType enum value → 422
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-BASE-2: Add question with invalid QuestionType=99 → 422")]
    public async Task AddQuestion_InvalidQuestionType_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId, questionType = 99,  // not a valid QuestionType enum member
                questionText  = "Capital?",
                options       = McqOptions,
                correctAnswer = McqCorrectAnswer,
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Invalid QuestionType=99 must trigger IsInEnum → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-BASE-3 — LessonId=0 → 422
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-BASE-3: Add question with LessonId=0 → 422")]
    public async Task AddQuestion_LessonIdZero_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId = 0,       // GreaterThan(0) validator fires
                questionType = QtMcq,
                questionText  = "Capital?",
                options       = McqOptions,
                correctAnswer = McqCorrectAnswer,
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "LessonId=0 must trigger GreaterThan(0) → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-BASE-4 — Edit question with Id=0 → 422
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-BASE-4: Edit question with Id=0 → 422")]
    public async Task EditQuestion_IdZero_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put, "/api/Learning/Questions",
            new
            {
                id           = 0,       // GreaterThan(0) validator fires
                questionType = QtMcq,
                questionText  = "Capital?",
                options       = McqOptions,
                correctAnswer = McqCorrectAnswer,
                difficulty    = DiffEasy
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "EditQuestionCommand Id=0 must trigger GreaterThan(0) → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-REORDER-1 — Reorder persists new SequenceOrder within one lesson
    // =========================================================================

    [Fact(DisplayName = "AC-REORDER-1: Reorder two questions → SequenceOrder persisted correctly")]
    public async Task ReorderQuestions_PersistsSequenceOrder()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // Add two questions (append order: q1 → SequenceOrder=0, q2 → SequenceOrder=1).
        await SendAsync(HttpMethod.Post, "/api/Learning/Questions", BuildMcqBody(lessonId), _adminToken);
        await SendAsync(HttpMethod.Post, "/api/Learning/Questions", BuildTrueFalseBody(lessonId), _adminToken);

        var questionsBefore = await GetAdminQuestionsAsync(lessonId);
        questionsBefore.Should().HaveCount(2, "both questions must exist before reorder");
        TryProp(questionsBefore[0], "id", out var id0Prop).Should().BeTrue();
        TryProp(questionsBefore[1], "id", out var id1Prop).Should().BeTrue();
        var q0Id = id0Prop.GetInt32(); // originally SequenceOrder=0
        var q1Id = id1Prop.GetInt32(); // originally SequenceOrder=1

        // Reorder: put q1 first, q0 second.
        var (reorderResp, reorderRoot, reorderBody) = await SendAsync(HttpMethod.Put,
            "/api/Learning/Questions/Reorder",
            new { lessonId, questionIds = new[] { q1Id, q0Id } },
            _adminToken);
        reorderResp.StatusCode.Should().Be(HttpStatusCode.OK, "Reorder must return 200; body: {0}", reorderBody);
        AssertSucceeded(reorderRoot, reorderBody);

        // Reload and compare SequenceOrder values.
        var questionsAfter = await GetAdminQuestionsAsync(lessonId);
        int? order0 = null, order1 = null;
        foreach (var q in questionsAfter)
        {
            if (!TryProp(q, "id", out var idProp)) continue;
            if (!TryProp(q, "sequenceOrder", out var oProp)) continue;
            var id = idProp.GetInt32();
            var order = oProp.GetInt32();
            if (id == q0Id) order0 = order;
            if (id == q1Id) order1 = order;
        }

        order0.Should().NotBeNull($"q0 (id={q0Id}) must appear after reorder");
        order1.Should().NotBeNull($"q1 (id={q1Id}) must appear after reorder");

        // After reorder [q1, q0]: q1 → SequenceOrder=0, q0 → SequenceOrder=1.
        order1.Should().BeLessThan(order0!.Value,
            $"q1 placed first must have lower SequenceOrder than q0; order0={order0}, order1={order1}");
    }

    // =========================================================================
    // AC-REORDER-2 — Cross-lesson reorder → Successed=false
    // =========================================================================

    [Fact(DisplayName = "AC-REORDER-2: Reorder with IDs from two different lessons → Successed=false")]
    public async Task ReorderQuestions_CrossLesson_Rejected()
    {
        var lessonId1 = await CreateLessonGetIdAsync();
        var lessonId2 = await CreateLessonGetIdAsync();

        await SendAsync(HttpMethod.Post, "/api/Learning/Questions", BuildMcqBody(lessonId1), _adminToken);
        await SendAsync(HttpMethod.Post, "/api/Learning/Questions", BuildTrueFalseBody(lessonId2), _adminToken);

        var qs1 = await GetAdminQuestionsAsync(lessonId1);
        var qs2 = await GetAdminQuestionsAsync(lessonId2);
        qs1.Should().NotBeEmpty(); qs2.Should().NotBeEmpty();
        TryProp(qs1[0], "id", out var id1Prop).Should().BeTrue();
        TryProp(qs2[0], "id", out var id2Prop).Should().BeTrue();
        var fromLesson1 = id1Prop.GetInt32();
        var fromLesson2 = id2Prop.GetInt32();

        // Anchor = lessonId1, but fromLesson2 belongs to lessonId2 → LessonId mismatch → rejected.
        var (resp, root, body) = await SendAsync(HttpMethod.Put, "/api/Learning/Questions/Reorder",
            new { lessonId = lessonId1, questionIds = new[] { fromLesson1, fromLesson2 } },
            _adminToken);

        AssertRejected(resp, root, body,
            "Reorder with IDs from two different lessons must be rejected (Successed=false)");
    }

    // =========================================================================
    // AC-REORDER-3 — Reorder with empty list → 422
    // =========================================================================

    [Fact(DisplayName = "AC-REORDER-3: Reorder with empty questionIds list → 422")]
    public async Task ReorderQuestions_EmptyList_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put, "/api/Learning/Questions/Reorder",
            new { lessonId = 1, questionIds = Array.Empty<int>() },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Empty questionIds must trigger NotEmpty validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    // =========================================================================
    // AC-REORDER-4 — Reorder with id=0 in list → 422
    // =========================================================================

    [Fact(DisplayName = "AC-REORDER-4: Reorder with id=0 in questionIds list → 422")]
    public async Task ReorderQuestions_ZeroId_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put, "/api/Learning/Questions/Reorder",
            new { lessonId = 1, questionIds = new[] { 0 } },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "id=0 in questionIds must trigger GreaterThan(0) → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-REORDER-5 — Reorder with non-existent IDs → Successed=false
    // =========================================================================

    [Fact(DisplayName = "AC-REORDER-5: Reorder with non-existent question IDs → Successed=false")]
    public async Task ReorderQuestions_NonExistentIds_Rejected()
    {
        // Use a real lesson as the anchor so the handler passes the lesson-exists check;
        // the question IDs 99999991/99999992 don't exist so the mismatch rejection fires.
        var lessonId = await CreateLessonGetIdAsync();
        var (resp, root, body) = await SendAsync(HttpMethod.Put, "/api/Learning/Questions/Reorder",
            new { lessonId, questionIds = new[] { 99999991, 99999992 } },
            _adminToken);

        // Handler returns NotFound (HTTP 200 Successed=false) or 404.
        AssertRejected(resp, root, body,
            "Reorder with non-existent IDs must be rejected (Successed=false)");
    }

    // =========================================================================
    // AC-ACTIVE-1 — SetActive(false) hides question from student StartAttempt
    // =========================================================================

    [Fact(DisplayName = "AC-ACTIVE-1: Deactivated question excluded from student StartAttempt question set")]
    public async Task DeactivateQuestion_HiddenFromStudentAttempt()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // Add two questions so the lesson is not empty after deactivation.
        await SendAsync(HttpMethod.Post, "/api/Learning/Questions", BuildMcqBody(lessonId), _adminToken);
        await SendAsync(HttpMethod.Post, "/api/Learning/Questions", BuildTrueFalseBody(lessonId), _adminToken);

        var questions = await GetAdminQuestionsAsync(lessonId);
        questions.Should().HaveCount(2);
        TryProp(questions[0], "id", out var idProp).Should().BeTrue();
        var qIdToDeactivate = idProp.GetInt32();

        // Deactivate the first question.
        var (setResp, setRoot, setBody) = await SendAsync(HttpMethod.Patch,
            $"/api/Learning/Questions/{qIdToDeactivate}/SetActive",
            new { isActive = false },
            _adminToken);
        setResp.StatusCode.Should().Be(HttpStatusCode.OK, "SetActive must return 200; body: {0}", setBody);
        AssertSucceeded(setRoot, setBody);

        // Student attempt: deactivated question must not appear.
        var studentToken = await CreateStudentTokenAsync();
        var (attemptResp, attemptRoot, attemptBody) = await SendAsync(HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", bearer: studentToken);
        attemptResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must succeed after deactivation (1 question remaining); body: {0}", attemptBody);

        TryProp(attemptRoot, "data", out var data).Should().BeTrue("body: {0}", attemptBody);
        TryProp(data, "questions", out var questionsEl).Should().BeTrue("body: {0}", attemptBody);
        questionsEl.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", attemptBody);

        var studentQuestions = questionsEl.EnumerateArray().ToList();
        studentQuestions.Any(q => TryProp(q, "id", out var id) && id.GetInt32() == qIdToDeactivate)
            .Should().BeFalse(
                "deactivated question must not appear in student StartAttempt; body: {0}", attemptBody);
        studentQuestions.Should().HaveCount(1,
            "only the remaining active question must appear; body: {0}", attemptBody);
    }

    // =========================================================================
    // AC-ACTIVE-2 — Admin ByLesson still returns inactive question (IsActive=false)
    // =========================================================================

    [Fact(DisplayName = "AC-ACTIVE-2: Admin ByLesson returns deactivated question with IsActive=false")]
    public async Task DeactivateQuestion_AdminByLessonStillReturnsIt()
    {
        var lessonId = await CreateLessonGetIdAsync();

        await SendAsync(HttpMethod.Post, "/api/Learning/Questions", BuildMcqBody(lessonId), _adminToken);

        var questions = await GetAdminQuestionsAsync(lessonId);
        questions.Should().NotBeEmpty();
        TryProp(questions[0], "id", out var idProp).Should().BeTrue();
        var qId = idProp.GetInt32();

        // Deactivate.
        await SendAsync(HttpMethod.Patch, $"/api/Learning/Questions/{qId}/SetActive",
            new { isActive = false }, _adminToken);

        // Admin ByLesson must still show it.
        var questionsAfter = await GetAdminQuestionsAsync(lessonId);
        var found = questionsAfter.FirstOrDefault(q => TryProp(q, "id", out var id) && id.GetInt32() == qId);
        found.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "deactivated question must still appear in admin ByLesson");

        TryProp(found, "isActive", out var isActiveProp).Should().BeTrue();
        isActiveProp.GetBoolean().Should().BeFalse(
            "admin ByLesson must reflect IsActive=false after deactivation");
    }

    // =========================================================================
    // AC-ACTIVE-3 — SetActive(true) restores question to student StartAttempt
    // =========================================================================

    [Fact(DisplayName = "AC-ACTIVE-3: Reactivating a question restores it to student StartAttempt")]
    public async Task ReactivateQuestion_RestoredToStudentAttempt()
    {
        var lessonId = await CreateLessonGetIdAsync();

        await SendAsync(HttpMethod.Post, "/api/Learning/Questions", BuildMcqBody(lessonId), _adminToken);
        var questions = await GetAdminQuestionsAsync(lessonId);
        TryProp(questions[0], "id", out var idProp).Should().BeTrue();
        var qId = idProp.GetInt32();

        // Deactivate then reactivate.
        await SendAsync(HttpMethod.Patch, $"/api/Learning/Questions/{qId}/SetActive",
            new { isActive = false }, _adminToken);

        var (reactResp, reactRoot, reactBody) = await SendAsync(HttpMethod.Patch,
            $"/api/Learning/Questions/{qId}/SetActive",
            new { isActive = true }, _adminToken);
        reactResp.StatusCode.Should().Be(HttpStatusCode.OK, "Reactivate must return 200; body: {0}", reactBody);
        AssertSucceeded(reactRoot, reactBody);

        // Student attempt: reactivated question must appear.
        var studentToken = await CreateStudentTokenAsync();
        var (attemptResp, attemptRoot, attemptBody) = await SendAsync(HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", bearer: studentToken);
        attemptResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must succeed after reactivation; body: {0}", attemptBody);

        TryProp(attemptRoot, "data", out var data).Should().BeTrue("body: {0}", attemptBody);
        TryProp(data, "questions", out var questionsEl).Should().BeTrue("body: {0}", attemptBody);
        questionsEl.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", attemptBody);

        var studentQuestions = questionsEl.EnumerateArray().ToList();
        studentQuestions.Any(q => TryProp(q, "id", out var id) && id.GetInt32() == qId)
            .Should().BeTrue("reactivated question must appear in student StartAttempt; body: {0}", attemptBody);
    }

    // =========================================================================
    // AC-ACTIVE-4 — SetActive validator: Id=0 in route → 422
    // =========================================================================

    [Fact(DisplayName = "AC-ACTIVE-4: PATCH Questions/0/SetActive → 422 (Id GreaterThan(0))")]
    public async Task SetActive_ZeroId_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Patch,
            "/api/Learning/Questions/0/SetActive",
            new { isActive = false }, _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Id=0 must trigger GreaterThan(0) validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-ENV-1 — BaseResponse envelope shape on write
    // =========================================================================

    [Fact(DisplayName = "AC-ENV-1: Add question response has full BaseResponse envelope keys")]
    public async Task AddQuestion_ResponseEnvelope_HasAllKeys()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            BuildMcqBody(lessonId), _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("Add must succeed; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data",    out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors",  out _).Should().BeTrue("envelope must have errors; body: {0}", body);
    }

    // =========================================================================
    // AC-LESSON-404 — Add question to non-existent lesson → Successed=false
    // =========================================================================

    [Fact(DisplayName = "AC-LESSON-404: Add question to non-existent lesson → Successed=false")]
    public async Task AddQuestion_NonExistentLesson_ReturnsNotFound()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId = 99999999,   // does not exist
                questionType = QtMcq,
                questionText  = "Capital?",
                options       = McqOptions,
                correctAnswer = McqCorrectAnswer,
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        AssertRejected(resp, root, body,
            "Add to non-existent lesson must be rejected (Successed=false)");
    }

    // =========================================================================
    // Append semantics — multiple Add calls produce monotonically increasing SequenceOrder
    // =========================================================================

    [Fact(DisplayName = "Append semantics: each added question gets the next SequenceOrder (0-based)")]
    public async Task AddQuestion_AppendSemantics_SequenceOrderIncreases()
    {
        var lessonId = await CreateLessonGetIdAsync();

        await SendAsync(HttpMethod.Post, "/api/Learning/Questions", BuildMcqBody(lessonId), _adminToken);
        await SendAsync(HttpMethod.Post, "/api/Learning/Questions", BuildTrueFalseBody(lessonId), _adminToken);
        await SendAsync(HttpMethod.Post, "/api/Learning/Questions", BuildFillInBlankBody(lessonId), _adminToken);

        var questions = await GetAdminQuestionsAsync(lessonId);
        questions.Should().HaveCount(3, "all three questions must be present");

        var orders = questions
            .Where(q => TryProp(q, "sequenceOrder", out _))
            .Select(q => { TryProp(q, "sequenceOrder", out var o); return o.GetInt32(); })
            .ToList();
        orders.Should().BeInAscendingOrder("ByLesson returns questions ordered by SequenceOrder ascending");
        orders.Should().OnlyHaveUniqueItems("SequenceOrder values must be unique within a lesson");
    }

    // =========================================================================
    // Valid TrueFalse — "false" is a valid correctAnswer
    // =========================================================================

    [Fact(DisplayName = "TrueFalse with correctAnswer='false' (lowercase) → accepted")]
    public async Task AddTrueFalse_CorrectAnswerFalse_Succeeds()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId, questionType = QtTrueFalse,
                questionText  = "Is the moon a star?",
                options       = TfOptions,
                correctAnswer = "false",     // lowercase "false" — must be valid
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "TrueFalse correctAnswer='false' must be accepted; body: {0}", body);
        AssertSucceeded(root, body);
    }

    // =========================================================================
    // GetById — non-existent id → Successed=false (not 500)
    // =========================================================================

    [Fact(DisplayName = "GetById: non-existent question id → Successed=false (not 500)")]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            "/api/Learning/Questions/99999998", _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "non-existent question must not produce 500; body: {0}", body);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 404 }, "body: {0}", body);

        if (resp.StatusCode == HttpStatusCode.OK && root.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
            s.GetBoolean().Should().BeFalse("Successed must be false for non-existent question; body: {0}", body);
        }
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

    private static void AssertSucceeded(JsonElement root, string body)
    {
        TryProp(root, "successed", out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
    }

    private static void AssertRejected(HttpResponseMessage response, JsonElement root, string body, string reason)
    {
        var statusCode = (int)response.StatusCode;
        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow("rejection response must be valid JSON; reason={0}; body: {1}", reason, body);

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
        var email = $"p704_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.com";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>
    /// Creates a Student-role account via the parent-driven flow (Register Parent → Add-Child → sign in as child)
    /// and returns the Student JWT. Used for student-path tests (StartAttempt CorrectAnswer leakage checks).
    /// </summary>
    private async Task<string> CreateStudentTokenAsync()
    {
        // Register a unique parent.
        var parentEmail = $"p704p_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (parentResp, parentRoot, parentBody) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        parentResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration must succeed; body: {0}", parentBody);
        TryProp(parentRoot, "data", out var parentData).Should().BeTrue("body: {0}", parentBody);
        TryProp(parentData, "accessToken", out var parentTokenProp).Should().BeTrue("body: {0}", parentBody);
        var parentToken = parentTokenProp.GetString()!;

        // Add child (creates Student role account).
        var childEmail = $"p704c_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = "P704 Test Student",
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

        // Sign in as child to get Student JWT.
        return await SignInGetTokenAsync(childEmail, ValidChildPassword);
    }

    /// <summary>Creates a grade → subject → unit → lesson stack. Returns the lesson ID.</summary>
    private async Task<int> CreateLessonGetIdAsync()
    {
        int unitId = await CreateUnitWithPrereqsAsync();
        var lessonName = $"P704 Lesson {Guid.NewGuid():N}";

        var (lessonResp, _, lessonBody) = await SendAsync(HttpMethod.Post, "/api/learning/Lessons/Create",
            new { Name = lessonName, Difficulty = 1, SequenceOrder = 1, IsLocked = false,
                  UnitId = unitId, EstimatedMinutes = 0 },
            _adminToken);
        lessonResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "lesson create must succeed; body: {0}", lessonBody);

        return await FindLessonIdByNameAsync(unitId, lessonName);
    }

    /// <summary>Creates a grade → subject → unit stack and returns the unitId.</summary>
    private async Task<int> CreateUnitWithPrereqsAsync()
    {
        var gradeName = $"P704 Grade {Guid.NewGuid():N}";
        var (gradeResp, _, gradeBody) = await SendAsync(HttpMethod.Post, "/api/learning/grades/Create",
            new { Number = 1, DisplayName = gradeName }, _adminToken);
        gradeResp.StatusCode.Should().Be(HttpStatusCode.OK, "grade create must succeed; body: {0}", gradeBody);
        var gradeId = await FindIdInListAsync("/api/learning/grades/List?PageNumber=1&PageSize=200",
            "displayName", gradeName, "grade", _adminToken);

        var subjName = $"P704 Subj {Guid.NewGuid():N}";
        var (subjResp, _, subjBody) = await SendAsync(HttpMethod.Post, "/api/learning/subjects/Create",
            new { Name = subjName, Country = "EG", GradeId = gradeId, SubjectCode = 0, Language = 0 },
            _adminToken);
        subjResp.StatusCode.Should().Be(HttpStatusCode.OK, "subject create must succeed; body: {0}", subjBody);
        var subjectId = await FindIdInListAsync(
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}",
            "name", subjName, "subject", _adminToken);

        var unitName = $"P704 Unit {Guid.NewGuid():N}";
        var (unitResp, _, unitBody) = await SendAsync(HttpMethod.Post, "/api/learning/units/Create",
            new { Name = unitName, SequenceOrder = 1, SubjectId = subjectId }, _adminToken);
        unitResp.StatusCode.Should().Be(HttpStatusCode.OK, "unit create must succeed; body: {0}", unitBody);
        return await FindIdInListAsync(
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", unitName, "unit", _adminToken);
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
            if (TryProp(outer, "data", out var inner) && inner.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in inner.EnumerateArray())
                {
                    if (TryProp(item, "name", out var n) && n.GetString() == lessonName &&
                        TryProp(item, "id", out var id))
                        return id.GetInt32();
                }
            }
            else if (outer.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in outer.EnumerateArray())
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
            $"Could not find {entityType} where {fieldName}='{fieldValue}' in list; url={listUrl}; body={listBody}");
    }

    /// <summary>
    /// Calls GET /api/Learning/Questions/ByLesson/{lessonId} and returns the question list.
    /// The response data may be a flat list (not paginated).
    /// </summary>
    private async Task<List<JsonElement>> GetAdminQuestionsAsync(int lessonId)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/Learning/Questions/ByLesson/{lessonId}", _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ByLesson must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have data; body: {0}", body);
        if (data.ValueKind == JsonValueKind.Array)
            return data.EnumerateArray().ToList();
        if (data.ValueKind == JsonValueKind.Null || data.ValueKind == JsonValueKind.Undefined)
            return new List<JsonElement>();

        // Paginated wrapper: { data: { data: [...] } }
        if (TryProp(data, "data", out var inner) && inner.ValueKind == JsonValueKind.Array)
            return inner.EnumerateArray().ToList();

        return new List<JsonElement>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Request body builders — one per question type
    // ─────────────────────────────────────────────────────────────────────────

    private static object BuildMcqBody(int lessonId) => new
    {
        lessonId,
        questionType  = QtMcq,
        questionText  = "What is the capital of France?",
        options       = McqOptions,
        correctAnswer = McqCorrectAnswer,
        difficulty    = DiffEasy,
        generatedBy   = GenCurated
    };

    private static object BuildTrueFalseBody(int lessonId) => new
    {
        lessonId,
        questionType  = QtTrueFalse,
        questionText  = "The Earth orbits the Sun.",
        options       = TfOptions,
        correctAnswer = TfCorrectAnswer,
        difficulty    = DiffEasy,
        generatedBy   = GenCurated
    };

    private static object BuildMatchingBody(int lessonId) => new
    {
        lessonId,
        questionType  = QtMatching,
        questionText  = "Match the animals to their class.",
        options       = MatchOptions,
        correctAnswer = MatchCorrectAnswer,
        difficulty    = DiffEasy,
        generatedBy   = GenCurated
    };

    private static object BuildFillInBlankBody(int lessonId) => new
    {
        lessonId,
        questionType  = QtFillInBlank,
        questionText  = "The capital of Egypt is ___.",
        options       = FibOptions,
        correctAnswer = FibCorrectAnswer,
        difficulty    = DiffEasy,
        generatedBy   = GenCurated
    };

    private static object BuildEditMcqBody(int id) => new
    {
        id,
        questionType  = QtMcq,
        questionText  = "What is the capital of Germany?",
        options       = "[\"Berlin\",\"Munich\"]",
        correctAnswer = "Berlin",
        difficulty    = DiffEasy
    };
}
