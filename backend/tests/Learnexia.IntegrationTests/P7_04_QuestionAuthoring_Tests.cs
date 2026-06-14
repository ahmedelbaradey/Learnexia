using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P7-04 CorrectAnswer fix — regression and round-trip integration tests.
///
/// The fix JSON-encodes the scalar CorrectAnswer on write (Add/Edit handlers) to match the seeder
/// and AnswerComparator.NormalizeJsonScalar decode. Before the fix every Add/Edit of an MCQ,
/// TrueFalse, or FillInBlank question returned HTTP 500 because storing a raw string like
/// "Paris" into a jsonb column fails Postgres check 22P02 (not valid JSON).
///
/// Groups covered:
///
/// Bucket A — no-500 regression: the exact question-type inputs that returned 500 before the fix
///   must now succeed (HTTP 200, Successed=true).
///   BE-TC-A1  AddMcq_NoLonger500
///   BE-TC-A2  AddTrueFalse_NoLonger500
///   BE-TC-A3  AddFillInBlank_NoLonger500
///   BE-TC-A4  AddMatching_NoLonger500 (Matching was not broken, but must still pass)
///   BE-TC-A5  EditMcq_NoLonger500
///   BE-TC-A6  EditTrueFalse_NoLonger500
///   BE-TC-A7  EditFillInBlank_NoLonger500
///   BE-TC-A8  EditMatching_NoLonger500
///   BE-TC-A9  Add_McqValidation_1Option_422     (regression: 422 not 500 when validation fires)
///   BE-TC-A10 Add_McqValidation_WrongAnswer_422
///   BE-TC-A11 Add_TfValidation_BadAnswer_422
///   BE-TC-A12 Add_FibValidation_EmptyAnswer_422
///   BE-TC-A13 Add_MatchValidation_UnequalArrays_422
///   BE-TC-A14 Add_LessonNotFound_NotSucceeded    (non-existent lesson → Successed=false, not 500)
///
/// Bucket G — grading round-trip: author via admin API → grade via student path → CORRECT.
///   BE-TC-G1  Mcq_GradingRoundTrip_Correct
///   BE-TC-G2  TrueFalse_GradingRoundTrip_Correct
///   BE-TC-G3  FillInBlank_GradingRoundTrip_Correct
///   BE-TC-G4  TrueFalseFalse_GradingRoundTrip_Correct  (correctAnswer="false")
///
/// Bucket E — edit round-trip: author → GET → resubmit via PUT → grade → still CORRECT.
/// Also asserts what the admin GET returns for correctAnswer (raw scalar or JSON-encoded form)
/// and whether that creates a symmetric read/write contract with the Edit handler.
///   BE-TC-E1  Mcq_EditRoundTrip_GetReturnsScalar_Or_DetectsDoubleEncode
///   BE-TC-E2  TrueFalse_EditRoundTrip_GradeStillCorrect
///   BE-TC-E3  FillInBlank_EditRoundTrip_GradeStillCorrect
///
/// Prerequisites: Docker + Testcontainers Postgres (pgvector/pgvector:pg16).
/// Run: dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P7_04"
/// Note: this filter also picks up the existing P7_04_QuestionsAdmin_Tests suite.
/// To run only this file: --filter "FullyQualifiedName~P7_04_QuestionAuthoring"
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_04_QuestionAuthoring_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // Credentials (mirror P7-04 + P7-01/P7-02/P7-03 seeded accounts)
    // -------------------------------------------------------------------------
    private const string AdminUserName     = "superadmin";
    private const string AdminPassword     = "123Pa$$word!";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    // -------------------------------------------------------------------------
    // QuestionType enum values: MCQ=1, TrueFalse=2, Matching=3, FillInBlank=4
    // DifficultyLevel enum:     Easy=1
    // GeneratedBy enum:         Curated=1
    // -------------------------------------------------------------------------
    private const int QtMcq         = 1;
    private const int QtTrueFalse   = 2;
    private const int QtMatching    = 3;
    private const int QtFillInBlank = 4;
    private const int DiffEasy      = 1;
    private const int GenCurated    = 1;

    // -------------------------------------------------------------------------
    // Canonical "authored" values — what an admin types into the authoring UI
    // -------------------------------------------------------------------------
    private const string McqOptions        = "[\"Paris\",\"London\",\"Berlin\"]";
    private const string McqCorrectAnswer  = "Paris";

    private const string TfOptions         = "[\"True\",\"False\"]";
    private const string TfCorrectAnswerT  = "true";
    private const string TfCorrectAnswerF  = "false";

    private const string MatchOptions      = "{\"left\":[\"Cat\",\"Dog\"],\"right\":[\"Feline\",\"Canine\"]}";
    private const string MatchCorrectAnswer = "{}";  // validator does not inspect Matching correctAnswer shape

    private const string FibOptions        = "[]";
    private const string FibCorrectAnswer  = "Cairo";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;
    private string? _adminToken;

    public P7_04_QuestionAuthoring_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        // Apply Learning migrations explicitly (idempotent).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await db.Database.MigrateAsync();

        _adminToken = await SignInGetTokenAsync(AdminUserName, AdminPassword);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Bucket A — No-500 regression
    // The bug: storing raw "Paris" into a jsonb column → Postgres 22P02 → 500.
    // The fix: JsonSerializer.Serialize("Paris") → "\"Paris\"" (valid JSON string literal).
    // =========================================================================

    [Fact(DisplayName = "BE-TC-A1: Add MCQ question → HTTP 200 (NOT 500) — CorrectAnswer jsonb fix")]
    public async Task AddMcq_NoLonger500()
    {
        var lessonId = await CreateLessonAsync();
        var (resp, root, body) = await AddQuestionAsync(lessonId, QtMcq, McqOptions, McqCorrectAnswer);

        ((int)resp.StatusCode).Should().NotBe(500,
            "P7-04 fix: adding an MCQ must no longer return 500; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Add MCQ should return 200 OK; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "BE-TC-A2: Add TrueFalse question → HTTP 200 (NOT 500) — CorrectAnswer jsonb fix")]
    public async Task AddTrueFalse_NoLonger500()
    {
        var lessonId = await CreateLessonAsync();
        var (resp, root, body) = await AddQuestionAsync(lessonId, QtTrueFalse, TfOptions, TfCorrectAnswerT);

        ((int)resp.StatusCode).Should().NotBe(500,
            "P7-04 fix: adding a TrueFalse must no longer return 500; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Add TrueFalse should return 200 OK; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "BE-TC-A3: Add FillInBlank question → HTTP 200 (NOT 500) — CorrectAnswer jsonb fix")]
    public async Task AddFillInBlank_NoLonger500()
    {
        var lessonId = await CreateLessonAsync();
        var (resp, root, body) = await AddQuestionAsync(lessonId, QtFillInBlank, FibOptions, FibCorrectAnswer);

        ((int)resp.StatusCode).Should().NotBe(500,
            "P7-04 fix: adding a FillInBlank must no longer return 500; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Add FillInBlank should return 200 OK; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "BE-TC-A4: Add Matching question → HTTP 200 — Matching correctAnswer stored as-is")]
    public async Task AddMatching_NoLonger500()
    {
        var lessonId = await CreateLessonAsync();
        var (resp, root, body) = await AddQuestionAsync(lessonId, QtMatching, MatchOptions, MatchCorrectAnswer);

        ((int)resp.StatusCode).Should().NotBe(500,
            "Add Matching must not return 500; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Add Matching should return 200 OK; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "BE-TC-A5: Edit MCQ question → HTTP 200 (NOT 500) — CorrectAnswer jsonb fix")]
    public async Task EditMcq_NoLonger500()
    {
        var lessonId = await CreateLessonAsync();
        await AddQuestionAsync(lessonId, QtMcq, McqOptions, McqCorrectAnswer);
        var qId = await GetFirstQuestionIdAsync(lessonId);

        var (resp, root, body) = await EditQuestionAsync(qId, QtMcq,
            "[\"Rome\",\"Madrid\",\"Lisbon\"]", "Madrid");

        ((int)resp.StatusCode).Should().NotBe(500,
            "P7-04 fix: editing an MCQ must no longer return 500; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Edit MCQ should return 200 OK; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "BE-TC-A6: Edit TrueFalse question → HTTP 200 (NOT 500) — CorrectAnswer jsonb fix")]
    public async Task EditTrueFalse_NoLonger500()
    {
        var lessonId = await CreateLessonAsync();
        await AddQuestionAsync(lessonId, QtTrueFalse, TfOptions, TfCorrectAnswerT);
        var qId = await GetFirstQuestionIdAsync(lessonId);

        var (resp, root, body) = await EditQuestionAsync(qId, QtTrueFalse, TfOptions, TfCorrectAnswerF);

        ((int)resp.StatusCode).Should().NotBe(500,
            "P7-04 fix: editing a TrueFalse must no longer return 500; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Edit TrueFalse should return 200 OK; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "BE-TC-A7: Edit FillInBlank question → HTTP 200 (NOT 500) — CorrectAnswer jsonb fix")]
    public async Task EditFillInBlank_NoLonger500()
    {
        var lessonId = await CreateLessonAsync();
        await AddQuestionAsync(lessonId, QtFillInBlank, FibOptions, FibCorrectAnswer);
        var qId = await GetFirstQuestionIdAsync(lessonId);

        var (resp, root, body) = await EditQuestionAsync(qId, QtFillInBlank, FibOptions, "Alexandria");

        ((int)resp.StatusCode).Should().NotBe(500,
            "P7-04 fix: editing a FillInBlank must no longer return 500; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Edit FillInBlank should return 200 OK; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "BE-TC-A8: Edit Matching question → HTTP 200 — Matching stored as-is")]
    public async Task EditMatching_NoLonger500()
    {
        var lessonId = await CreateLessonAsync();
        await AddQuestionAsync(lessonId, QtMatching, MatchOptions, MatchCorrectAnswer);
        var qId = await GetFirstQuestionIdAsync(lessonId);

        var newOpts = "{\"left\":[\"Sun\",\"Moon\"],\"right\":[\"Star\",\"Satellite\"]}";
        var (resp, root, body) = await EditQuestionAsync(qId, QtMatching, newOpts, MatchCorrectAnswer);

        ((int)resp.StatusCode).Should().NotBe(500,
            "Edit Matching must not return 500; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Edit Matching should return 200 OK; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "BE-TC-A9: Add MCQ with 1 option → 422, Successed=false (not 500)")]
    public async Task Add_McqValidation_1Option_Returns422()
    {
        var lessonId = await CreateLessonAsync();
        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId,
                questionType  = QtMcq,
                questionText  = "Capital?",
                options       = "[\"Paris\"]",          // only 1 — MCQ needs ≥2
                correctAnswer = "Paris",
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "MCQ with 1 option must not 500 — validator fires first; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "MCQ with 1 option must return 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("Successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-A10: Add MCQ with correctAnswer not in options → 422, Successed=false (not 500)")]
    public async Task Add_McqValidation_WrongAnswer_Returns422()
    {
        var lessonId = await CreateLessonAsync();
        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId,
                questionType  = QtMcq,
                questionText  = "Capital?",
                options       = "[\"Paris\",\"London\"]",
                correctAnswer = "Berlin",      // not in options
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "MCQ answer not in options must not 500 — validator fires first; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "MCQ answer not in options must return 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-A11: Add TrueFalse with correctAnswer='yes' → 422, Successed=false (not 500)")]
    public async Task Add_TfValidation_BadAnswer_Returns422()
    {
        var lessonId = await CreateLessonAsync();
        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId,
                questionType  = QtTrueFalse,
                questionText  = "Is sky blue?",
                options       = TfOptions,
                correctAnswer = "yes",         // invalid — must be true/false
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "TrueFalse with 'yes' must not 500 — validator fires first; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "TrueFalse with 'yes' must return 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-A12: Add FillInBlank with empty correctAnswer → 422, Successed=false (not 500)")]
    public async Task Add_FibValidation_EmptyAnswer_Returns422()
    {
        var lessonId = await CreateLessonAsync();
        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId,
                questionType  = QtFillInBlank,
                questionText  = "The capital of Egypt is ___.",
                options       = FibOptions,
                correctAnswer = "",            // empty — FIB needs non-empty
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "FIB with empty answer must not 500 — validator fires first; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "FIB with empty correctAnswer must return 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-A13: Add Matching with unequal left/right arrays → 422 (not 500)")]
    public async Task Add_MatchValidation_UnequalArrays_Returns422()
    {
        var lessonId = await CreateLessonAsync();
        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId,
                questionType  = QtMatching,
                questionText  = "Match the pairs",
                options       = "{\"left\":[\"Cat\",\"Dog\"],\"right\":[\"Feline\"]}",  // 2 vs 1
                correctAnswer = MatchCorrectAnswer,
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "Matching with unequal arrays must not 500 — validator fires first; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Matching with unequal left/right arrays must return 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-A14: Add question to non-existent lesson → Successed=false (not 500)")]
    public async Task Add_LessonNotFound_NotSucceeded()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId      = 999_999_001,   // does not exist
                questionType  = QtMcq,
                questionText  = "Capital?",
                options       = McqOptions,
                correctAnswer = McqCorrectAnswer,
                difficulty    = DiffEasy,
                generatedBy   = GenCurated
            },
            _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "Non-existent lesson must not 500; body: {0}", body);

        // Handler returns NotFound (HTTP 200 Successed=false per BaseResponseHandler convention) or 404.
        var sc = (int)resp.StatusCode;
        sc.Should().BeOneOf(new[] { 200, 404 },
            "Non-existent lesson must return 200/NotFound or 404, not 500; body: {0}", body);

        if (resp.StatusCode == HttpStatusCode.OK)
        {
            TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
            s.GetBoolean().Should().BeFalse("Successed=false for non-existent lesson; body: {0}", body);
        }
    }

    // =========================================================================
    // Bucket G — Grading round-trip
    // Admin authors a question via the API → student submits the correct answer → grades CORRECT.
    // This proves the encoding stored by AddQuestionCommandHandler is the exact shape that
    // AnswerComparator.NormalizeJsonScalar can decode to match a student's raw payload.
    // =========================================================================

    [Fact(DisplayName = "BE-TC-G1: MCQ authored via admin API → student submits correct answer → isCorrect=true")]
    public async Task Mcq_GradingRoundTrip_Correct()
    {
        // 1. Author the question via the admin API.
        var lessonId = await CreateLessonAsync();
        var (addResp, _, addBody) = await AddQuestionAsync(lessonId, QtMcq, McqOptions, McqCorrectAnswer);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Prerequisite: MCQ add must succeed; body: {0}", addBody);

        var qId = await GetFirstQuestionIdAsync(lessonId);

        // 2. Create a student and start an attempt.
        var studentToken = await CreateStudentTokenAsync();
        var attemptId = await StartAttemptAsync(lessonId, studentToken);

        // 3. Submit the correct answer (raw scalar, exactly what the student types).
        // AnswerComparator.NormalizeJsonScalar unwraps the JSON-encoded stored value ("\"Paris\"")
        // and compares it to the raw submitted value "Paris" — must grade CORRECT.
        var (submitResp, submitRoot, submitBody) = await SubmitAnswerAsync(
            attemptId, qId, McqCorrectAnswer, studentToken);
        submitResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SubmitAnswer must return 200; body: {0}", submitBody);

        TryProp(submitRoot, "data", out var data).Should().BeTrue("body: {0}", submitBody);
        TryProp(data, "isCorrect", out var isCorrectProp).Should().BeTrue(
            "SubmitAnswer data must have isCorrect; body: {0}", submitBody);
        isCorrectProp.GetBoolean().Should().BeTrue(
            "MCQ authored via admin API must grade CORRECT when the student submits the right answer; " +
            "this proves AddQuestionCommandHandler encodes in the format AnswerComparator can decode. " +
            "body: {0}", submitBody);
    }

    [Fact(DisplayName = "BE-TC-G2: TrueFalse (true) authored via admin API → student submits 'true' → isCorrect=true")]
    public async Task TrueFalse_GradingRoundTrip_Correct()
    {
        var lessonId = await CreateLessonAsync();
        var (addResp, _, addBody) = await AddQuestionAsync(lessonId, QtTrueFalse, TfOptions, TfCorrectAnswerT);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Prerequisite: TrueFalse add must succeed; body: {0}", addBody);

        var qId = await GetFirstQuestionIdAsync(lessonId);
        var studentToken = await CreateStudentTokenAsync();
        var attemptId = await StartAttemptAsync(lessonId, studentToken);

        var (submitResp, submitRoot, submitBody) = await SubmitAnswerAsync(
            attemptId, qId, "true", studentToken);
        submitResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SubmitAnswer must return 200; body: {0}", submitBody);

        TryProp(submitRoot, "data", out var data).Should().BeTrue("body: {0}", submitBody);
        TryProp(data, "isCorrect", out var isCorrectProp).Should().BeTrue(
            "SubmitAnswer data must have isCorrect; body: {0}", submitBody);
        isCorrectProp.GetBoolean().Should().BeTrue(
            "TrueFalse(true) authored via admin API must grade CORRECT when student submits 'true'; " +
            "body: {0}", submitBody);
    }

    [Fact(DisplayName = "BE-TC-G3: FillInBlank authored via admin API → student submits correct value → isCorrect=true")]
    public async Task FillInBlank_GradingRoundTrip_Correct()
    {
        var lessonId = await CreateLessonAsync();
        var (addResp, _, addBody) = await AddQuestionAsync(lessonId, QtFillInBlank, FibOptions, FibCorrectAnswer);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Prerequisite: FillInBlank add must succeed; body: {0}", addBody);

        var qId = await GetFirstQuestionIdAsync(lessonId);
        var studentToken = await CreateStudentTokenAsync();
        var attemptId = await StartAttemptAsync(lessonId, studentToken);

        var (submitResp, submitRoot, submitBody) = await SubmitAnswerAsync(
            attemptId, qId, FibCorrectAnswer, studentToken);
        submitResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SubmitAnswer must return 200; body: {0}", submitBody);

        TryProp(submitRoot, "data", out var data).Should().BeTrue("body: {0}", submitBody);
        TryProp(data, "isCorrect", out var isCorrectProp).Should().BeTrue(
            "SubmitAnswer data must have isCorrect; body: {0}", submitBody);
        isCorrectProp.GetBoolean().Should().BeTrue(
            "FillInBlank authored via admin API must grade CORRECT when student submits the right value; " +
            "body: {0}", submitBody);
    }

    [Fact(DisplayName = "BE-TC-G4: TrueFalse (false) authored via admin API → student submits 'false' → isCorrect=true")]
    public async Task TrueFalseFalse_GradingRoundTrip_Correct()
    {
        var lessonId = await CreateLessonAsync();
        var (addResp, _, addBody) = await AddQuestionAsync(lessonId, QtTrueFalse, TfOptions, TfCorrectAnswerF);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Prerequisite: TrueFalse(false) add must succeed; body: {0}", addBody);

        var qId = await GetFirstQuestionIdAsync(lessonId);
        var studentToken = await CreateStudentTokenAsync();
        var attemptId = await StartAttemptAsync(lessonId, studentToken);

        var (submitResp, submitRoot, submitBody) = await SubmitAnswerAsync(
            attemptId, qId, "false", studentToken);
        submitResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SubmitAnswer must return 200; body: {0}", submitBody);

        TryProp(submitRoot, "data", out var data).Should().BeTrue("body: {0}", submitBody);
        TryProp(data, "isCorrect", out var isCorrectProp).Should().BeTrue(
            "SubmitAnswer data must have isCorrect; body: {0}", submitBody);
        isCorrectProp.GetBoolean().Should().BeTrue(
            "TrueFalse(false) authored via admin API must grade CORRECT when student submits 'false'; " +
            "body: {0}", submitBody);
    }

    // =========================================================================
    // Bucket E — Edit round-trip (double-encode detection)
    //
    // Flow: Add → GET (admin read) → inspect correctAnswer shape → PUT (resubmit) → grade.
    //
    // The double-encoding risk:
    //   1. Add stores JsonSerializer.Serialize("Paris") = "\"Paris\"" in the DB (correct).
    //   2. GetById maps the entity.CorrectAnswer directly to AdminQuestionDto.CorrectAnswer.
    //      The entity stores "\"Paris\"" so the GET response returns "\"Paris\"" (JSON-encoded form).
    //   3. An admin opens the question in the UI, sees this value, and submits it BACK verbatim
    //      via PUT/Edit. The Edit handler calls JsonSerializer.Serialize("\"Paris\"") again,
    //      producing "\"\\\"Paris\\\"\"" in the DB — double-encoded.
    //   4. AnswerComparator.NormalizeJsonScalar unwraps one JSON layer: "\"\\\"Paris\\\"\""
    //      → "\"Paris\"" (still quoted). This does NOT match the student's raw "Paris" → wrong grade.
    //
    // This test detects the defect by:
    //   a) Recording what correctAnswer the admin GET returns.
    //   b) Simulating an admin "save unchanged" by submitting that GET value back to the Edit endpoint.
    //   c) Grading the question — if the grade is INCORRECT, that proves double-encoding occurred.
    //
    // If the GET returns the RAW scalar (e.g. "Paris" without outer quotes), the contract is
    // symmetric and edit round-trips will be safe. This test then asserts the grade is CORRECT.
    // =========================================================================

    [Fact(DisplayName = "BE-TC-E1: MCQ Edit round-trip — GET correctAnswer shape determines if double-encode defect exists")]
    public async Task Mcq_EditRoundTrip_GetReturnsScalar_Or_DetectsDoubleEncode()
    {
        // Step 1: Author the question.
        var lessonId = await CreateLessonAsync();
        var (addResp, _, addBody) = await AddQuestionAsync(lessonId, QtMcq, McqOptions, McqCorrectAnswer);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Prerequisite: MCQ add must succeed; body: {0}", addBody);
        var qId = await GetFirstQuestionIdAsync(lessonId);

        // Step 2: Read via admin GET — capture the correctAnswer shape the UI would see.
        var (getResp, getRoot, getBody) = await SendAsync(HttpMethod.Get,
            $"/api/Learning/Questions/{qId}", bearer: _adminToken);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Admin GetById must return 200; body: {0}", getBody);

        TryProp(getRoot, "data", out var getData).Should().BeTrue(
            "GetById envelope must have data; body: {0}", getBody);
        TryProp(getData, "correctAnswer", out var caFromGet).Should().BeTrue(
            "GetById data must have correctAnswer; body: {0}", getBody);
        var caValueFromGet = caFromGet.GetString()!;

        // Diagnostic: record what GET returned so the report is explicit.
        // A symmetric API returns the raw scalar ("Paris").
        // An asymmetric API returns the JSON-encoded form ("\"Paris\"").
        var isGetSymmetric = string.Equals(caValueFromGet, McqCorrectAnswer, StringComparison.OrdinalIgnoreCase);

        // Step 3: Simulate "admin opens question and saves without changing" —
        // resubmit the exact correctAnswer string that GET returned.
        var (editResp, _, editBody) = await EditQuestionAsync(qId, QtMcq, McqOptions, caValueFromGet);
        editResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Edit must return 200 (resubmitting what GET returned); body: {0}", editBody);

        // Step 4: Grade — create a fresh student attempt and submit the canonical answer.
        var studentToken = await CreateStudentTokenAsync();
        var attemptId = await StartAttemptAsync(lessonId, studentToken);

        var (submitResp, submitRoot, submitBody) = await SubmitAnswerAsync(
            attemptId, qId, McqCorrectAnswer, studentToken);
        submitResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SubmitAnswer must return 200; body: {0}", submitBody);

        TryProp(submitRoot, "data", out var submitData).Should().BeTrue("body: {0}", submitBody);
        TryProp(submitData, "isCorrect", out var isCorrectProp).Should().BeTrue(
            "SubmitAnswer data must have isCorrect; body: {0}", submitBody);
        var gradeAfterEdit = isCorrectProp.GetBoolean();

        // Step 5: Assert.
        // If GET returned the raw scalar ("Paris"), the contract is symmetric and
        // the edit round-trip should grade CORRECT.
        // If GET returned the encoded form ("\"Paris\""), the Edit handler re-encoded it
        // ("\"\\\"Paris\\\"\"") and grading will be WRONG — that is a defect.
        if (isGetSymmetric)
        {
            // Symmetric contract — grade must be CORRECT.
            gradeAfterEdit.Should().BeTrue(
                "Admin GET returns raw scalar ('{0}') — Edit is symmetric, so grading after " +
                "a no-change edit must still be CORRECT. body: {1}",
                caValueFromGet, submitBody);
        }
        else
        {
            // DEFECT DETECTED: GetById returned '{caValueFromGet}' (JSON-encoded, not the raw scalar
            // '{McqCorrectAnswer}'). An admin who opens this question and saves it without changes
            // would cause the Edit handler to double-encode, corrupting the stored answer.
            // This breaks grading for all questions edited via the admin UI.
            //
            // Severity: HIGH — every question saved through the admin edit flow will silently
            // produce wrong grades for students. The only workaround is to never use the Edit
            // endpoint after a question is authored, which is not a real-world option.
            //
            // Root cause: AdminQuestionDto.CorrectAnswer exposes the raw DB value ("\"Paris\"")
            // rather than the decoded scalar ("Paris"). The Edit handler then re-encodes it.
            //
            // Fix required (in feature code — do NOT fix here per agent rules):
            //   Option A (recommended): Decode the CorrectAnswer in the AdminQuestionDto mapping
            //     (AutoMapper profile or the query handler) before returning it to the client.
            //     This makes GET return "Paris" and Edit receive "Paris" → single-encode → correct.
            //   Option B: The Edit handler detects that the incoming value is already JSON-encoded
            //     (starts+ends with quotes after trimming) and stores it as-is instead of re-encoding.
            //     Risk: ambiguous when the admin intentionally changes the answer.
            //
            // Failing assertion to surface this in the test run:
            gradeAfterEdit.Should().BeTrue(
                "DEFECT (P7-04 read-path decode missing): Admin GET returned correctAnswer='{0}' " +
                "(JSON-encoded form) instead of the raw scalar '{1}'. When this encoded value is " +
                "submitted back to the Edit endpoint unchanged, EditQuestionCommandHandler calls " +
                "JsonSerializer.Serialize on it again, producing a double-encoded value in the DB. " +
                "AnswerComparator.NormalizeJsonScalar only unwraps one layer, so grading fails. " +
                "Fix: decode CorrectAnswer before returning it from GetById/ByLesson so the admin " +
                "read/write contract is symmetric. body: {2}",
                caValueFromGet, McqCorrectAnswer, submitBody);
        }
    }

    [Fact(DisplayName = "BE-TC-E2: TrueFalse Edit round-trip — grade still CORRECT after GET → resubmit to Edit")]
    public async Task TrueFalse_EditRoundTrip_GradeStillCorrect()
    {
        var lessonId = await CreateLessonAsync();
        var (addResp, _, addBody) = await AddQuestionAsync(lessonId, QtTrueFalse, TfOptions, TfCorrectAnswerT);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Prerequisite: TrueFalse add must succeed; body: {0}", addBody);
        var qId = await GetFirstQuestionIdAsync(lessonId);

        // GET — capture correctAnswer as the UI would see it.
        var (getResp, getRoot, getBody) = await SendAsync(HttpMethod.Get,
            $"/api/Learning/Questions/{qId}", bearer: _adminToken);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", getBody);
        TryProp(getRoot, "data", out var getData).Should().BeTrue("body: {0}", getBody);
        TryProp(getData, "correctAnswer", out var caFromGet).Should().BeTrue("body: {0}", getBody);
        var caValueFromGet = caFromGet.GetString()!;

        // Resubmit what GET returned to Edit.
        var (editResp, _, editBody) = await EditQuestionAsync(qId, QtTrueFalse, TfOptions, caValueFromGet);
        editResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Edit (resubmitting GET value) must return 200; body: {0}", editBody);

        // Grade.
        var studentToken = await CreateStudentTokenAsync();
        var attemptId = await StartAttemptAsync(lessonId, studentToken);
        var (submitResp, submitRoot, submitBody) = await SubmitAnswerAsync(
            attemptId, qId, TfCorrectAnswerT, studentToken);
        submitResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", submitBody);
        TryProp(submitRoot, "data", out var submitData).Should().BeTrue("body: {0}", submitBody);
        TryProp(submitData, "isCorrect", out var isCorrectProp).Should().BeTrue("body: {0}", submitBody);

        isCorrectProp.GetBoolean().Should().BeTrue(
            "TrueFalse grade must still be CORRECT after a GET → resubmit edit cycle. " +
            "GET returned correctAnswer='{0}'. If this fails, double-encoding occurred. body: {1}",
            caValueFromGet, submitBody);
    }

    [Fact(DisplayName = "BE-TC-E3: FillInBlank Edit round-trip — grade still CORRECT after GET → resubmit to Edit")]
    public async Task FillInBlank_EditRoundTrip_GradeStillCorrect()
    {
        var lessonId = await CreateLessonAsync();
        var (addResp, _, addBody) = await AddQuestionAsync(lessonId, QtFillInBlank, FibOptions, FibCorrectAnswer);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Prerequisite: FillInBlank add must succeed; body: {0}", addBody);
        var qId = await GetFirstQuestionIdAsync(lessonId);

        // GET — capture correctAnswer as the UI would see it.
        var (getResp, getRoot, getBody) = await SendAsync(HttpMethod.Get,
            $"/api/Learning/Questions/{qId}", bearer: _adminToken);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", getBody);
        TryProp(getRoot, "data", out var getData).Should().BeTrue("body: {0}", getBody);
        TryProp(getData, "correctAnswer", out var caFromGet).Should().BeTrue("body: {0}", getBody);
        var caValueFromGet = caFromGet.GetString()!;

        // Resubmit what GET returned to Edit.
        var (editResp, _, editBody) = await EditQuestionAsync(qId, QtFillInBlank, FibOptions, caValueFromGet);
        editResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Edit (resubmitting GET value) must return 200; body: {0}", editBody);

        // Grade.
        var studentToken = await CreateStudentTokenAsync();
        var attemptId = await StartAttemptAsync(lessonId, studentToken);
        var (submitResp, submitRoot, submitBody) = await SubmitAnswerAsync(
            attemptId, qId, FibCorrectAnswer, studentToken);
        submitResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", submitBody);
        TryProp(submitRoot, "data", out var submitData).Should().BeTrue("body: {0}", submitBody);
        TryProp(submitData, "isCorrect", out var isCorrectProp).Should().BeTrue("body: {0}", submitBody);

        isCorrectProp.GetBoolean().Should().BeTrue(
            "FillInBlank grade must still be CORRECT after a GET → resubmit edit cycle. " +
            "GET returned correctAnswer='{0}'. If this fails, double-encoding occurred. body: {1}",
            caValueFromGet, submitBody);
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
        TryProp(root, "successed", out var s).Should().BeTrue(
            "envelope must have 'successed' field; body: {0}", body);
        s.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
    }

    private async Task<string> SignInGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Sign-in prerequisite must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private async Task<string> CreateStudentTokenAsync()
    {
        // Register parent.
        var parentEmail = $"p704auth_parent_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (parentResp, parentRoot, parentBody) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        parentResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Parent registration prerequisite must succeed; body: {0}", parentBody);
        TryProp(parentRoot, "data", out var parentData).Should().BeTrue("body: {0}", parentBody);
        TryProp(parentData, "accessToken", out var parentTokenProp).Should().BeTrue("body: {0}", parentBody);
        var parentToken = parentTokenProp.GetString()!;

        // Add child.
        var childEmail = $"p704auth_child_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (addResp, _, addBody) = await SendAsync(HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = "P704 Authoring Test Student",
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = 3,
                Language         = "ar",
                Country          = "EG",
                LearningLanguage = "ar",
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            "Add-Child prerequisite must succeed; body: {0}", addBody);

        // Sign in as child.
        return await SignInGetTokenAsync(childEmail, ValidChildPassword);
    }

    /// <summary>
    /// Creates a Published+Active lesson via the admin API (Grade → Subject → Unit → Lesson).
    /// Returns the lesson ID. Uses the DB layer directly for speed (no API noise in prerequisites).
    /// </summary>
    private async Task<int> CreateLessonAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        var grade = new Grade
        {
            Number      = Random.Shared.Next(100, 999),
            DisplayName = $"P704_Grade_{Guid.NewGuid():N}",
            CreatedAt   = now,
            CreatedBy   = 0
        };
        await db.Grades.AddAsync(grade);
        await db.SaveChangesAsync();

        var subject = new Subject
        {
            Name      = $"P704_Subj_{Guid.NewGuid():N}",
            GradeId   = grade.Id,
            CreatedAt = now,
            CreatedBy = 0
        };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var unit = new Learnexia.Modules.Learning.Domain.Entities.Unit
        {
            Name          = $"P704_Unit_{Guid.NewGuid():N}",
            SequenceOrder = 1,
            SubjectId     = subject.Id,
            CreatedAt     = now,
            CreatedBy     = 0
        };
        await db.Units.AddAsync(unit);
        await db.SaveChangesAsync();

        var lesson = new Lesson
        {
            Name           = $"P704_Lesson_{Guid.NewGuid():N}",
            Difficulty     = DifficultyLevel.Easy,
            SequenceOrder  = 1,
            IsLocked       = false,
            UnitId         = unit.Id,
            IsActive       = true,
            // Must be Published so StartAttempt passes the lifecycle guard.
            LifecycleState = LifecycleState.Published,
            CreatedAt      = now,
            CreatedBy      = 0
        };
        await db.Lessons.AddAsync(lesson);
        await db.SaveChangesAsync();

        return lesson.Id;
    }

    private Task<(HttpResponseMessage, JsonElement, string)> AddQuestionAsync(
        int lessonId, int questionType, string options, string correctAnswer,
        string questionText = "Test question?")
        => SendAsync(HttpMethod.Post, "/api/Learning/Questions",
            new
            {
                lessonId,
                questionType,
                questionText,
                options,
                correctAnswer,
                difficulty  = DiffEasy,
                generatedBy = GenCurated
            },
            _adminToken);

    private Task<(HttpResponseMessage, JsonElement, string)> EditQuestionAsync(
        int id, int questionType, string options, string correctAnswer,
        string questionText = "Updated test question?")
        => SendAsync(HttpMethod.Put, "/api/Learning/Questions",
            new
            {
                id,
                questionType,
                questionText,
                options,
                correctAnswer,
                difficulty = DiffEasy
            },
            _adminToken);

    /// <summary>
    /// Reads the admin ByLesson list and returns the id of the first question.
    /// Fails the test if no question is found.
    /// </summary>
    private async Task<int> GetFirstQuestionIdAsync(int lessonId)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/Learning/Questions/ByLesson/{lessonId}", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ByLesson prereq must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        IEnumerable<JsonElement>? items = null;
        if (data.ValueKind == JsonValueKind.Array)
            items = data.EnumerateArray();
        else if (TryProp(data, "data", out var inner) && inner.ValueKind == JsonValueKind.Array)
            items = inner.EnumerateArray();

        items.Should().NotBeNull("ByLesson data must contain an array; body: {0}", body);
        var list = items!.ToList();
        list.Should().NotBeEmpty("At least one question must exist for lesson {0}; body: {1}", lessonId, body);

        TryProp(list[0], "id", out var idProp).Should().BeTrue("Question must have id; body: {0}", body);
        return idProp.GetInt32();
    }

    /// <summary>
    /// Starts a student quiz attempt. Returns the attemptId.
    /// </summary>
    private async Task<int> StartAttemptAsync(int lessonId, string studentToken)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", bearer: studentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must return 200; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "attemptId", out var idProp).Should().BeTrue(
            "StartAttempt data must have attemptId; body: {0}", body);
        return idProp.GetInt32();
    }

    /// <summary>
    /// Submits a student answer. Returns the raw response tuple.
    /// </summary>
    private Task<(HttpResponseMessage, JsonElement, string)> SubmitAnswerAsync(
        int attemptId, int questionId, string answerPayload, string studentToken)
        => SendAsync(HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Answers",
            new
            {
                QuestionId       = questionId,
                AnswerPayload    = answerPayload,
                TimeSpentSeconds = 5,
                HintUsed         = false
            },
            studentToken);
}
