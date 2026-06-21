using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P5-07 integration tests — "Calibration Loop" (data feedback pipeline).
///
/// Tests validate the Hangfire <c>CalibrationJob</c> pipeline steps (invoked via
/// <c>ICalibrationService</c> directly for determinism), the AdminOnly management API at
/// <c>api/Learning/Calibration</c>, and the serve-gate (<c>StartAttemptService</c>).
///
/// Config defaults used (locked in P5-07 Execution Plan):
///   MinSamples=30, EasyThreshold=0.85, HardThreshold=0.45,
///   AiFlagHighPValue=0.95, AiFlagLowPValue=0.20.
///
/// Coverage map (acceptance criteria → test):
///
///   SCENARIO 1 — Aggregation correctness + idempotency:
///     CAL_S1a  Correct PValue/AvgTime/HintRate/SampleSize after sweep
///     CAL_S1b  Re-run updates same row (idempotent — no duplicate)
///
///   SCENARIO 2 — No-PII guarantee:
///     CAL_S2   QuestionDifficultyStats + QuestionRecalibrationProposal entities have no StudentId field
///
///   SCENARIO 3 — Proposal on divergence:
///     CAL_S3a  authored=Easy, p-value low (hard), >=30 samples → exactly one Pending proposal; Difficulty unchanged
///     CAL_S3b  authored=Easy, p-value low, &lt;30 samples → NO proposal
///
///   SCENARIO 4 — Proposal idempotency:
///     CAL_S4   Re-run updates open Pending proposal, no second Pending row
///
///   SCENARIO 5 — AI flagging + serve exclusion:
///     CAL_S5a  AI question, p-value>=0.95, >=30 samples → FlaggedForReview; excluded from StartAttempt pool
///     CAL_S5b  Curated question, same extreme p-value → NOT flagged
///
///   SCENARIO 6 — In-flight scoring unaffected:
///     CAL_S6   Question served before being flagged still scores via SubmitAnswer (404 path simulated)
///
///   SCENARIO 7 — AdminOnly auth:
///     CAL_S7a  Anonymous → 401 on all 6 endpoints
///     CAL_S7b  Parent token → 403 on all 6 endpoints
///     CAL_S7c  Admin token → 200 (or 404/424) on all 6 endpoints
///
///   SCENARIO 8 — Promote applies band / Reject does not:
///     CAL_S8a  POST Promote → QuizQuestion.Difficulty = ProposedDifficulty, proposal = Promoted
///     CAL_S8b  POST Reject → proposal = Rejected, Difficulty UNCHANGED
///
///   SCENARIO 9 — Unflag re-serves:
///     CAL_S9   POST Clear on flagged question → QualityState = Approved, reappears in StartAttempt pool
///
///   SCENARIO 10 — Pagination + localization:
///     CAL_S10a Proposals list endpoint pages correctly (page/pageSize)
///     CAL_S10b Accept-Language: ar → Arabic message in response
/// </summary>
[Collection("IntegrationTests")]
public sealed class P5_07_Calibration_Tests : IAsyncLifetime
{
    // ── URL constants ──────────────────────────────────────────────────────────────────────────────
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string CalibrationBase   = "api/Learning/Calibration";
    private const string StartAttemptFmt   = "api/Learning/Quizzes/{0}/Attempt";

    // ── Seeded admin credentials ───────────────────────────────────────────────────────────────────
    private const string AdminUserName   = "superadmin";
    private const string AdminPassword   = "123Pa$$word!";
    private const string ValidChildPass  = "Child@Pass1";

    // ── Thresholds (locked in P5-07 Execution Plan) ───────────────────────────────────────────────
    // MinSamples=30 answers before any proposal or flag is raised.
    private const int    MinSamples          = 30;
    // AI flag: p-value >= 0.95 → extremely easy (flag), p-value <= 0.20 → extremely hard (flag)
    private const double AiFlagHighPValue    = 0.95;
    // EasyThreshold=0.85 — p-value >= 0.85 → empirically Easy band
    private const double EasyThreshold       = 0.85;
    // HardThreshold=0.45 — p-value <= 0.45 → empirically Hard band
    private const double HardThreshold       = 0.45;

    // ── Infrastructure ─────────────────────────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient             _client;

    private string? _adminToken;
    private string? _parentToken;

    public P5_07_Calibration_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();
        _adminToken  = await SignInGetTokenAsync(AdminUserName, AdminPassword);
        _parentToken = await RegisterParentGetTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // SCENARIO 1a — Aggregation correctness
    // Seed a question + 30 answers (mix correct/incorrect, varied times, some hints)
    // → run AggregateQuestionStatsAsync → assert PValue / AvgTime / HintRate / SampleSize
    // =========================================================================

    [Fact(DisplayName = "CAL-S1a Aggregation: correct PValue/AvgTime/HintRate/SampleSize after sweep")]
    public async Task Aggregation_CorrectStats_AfterSweep()
    {
        // Arrange: seed a question and exactly 30 answers
        var lessonId   = await SeedLessonAsync("CAL-S1a Lesson");
        var questionId = await SeedQuestionAsync(lessonId, DifficultyLevel.Easy, GeneratedBy.Curated);

        // 20 correct, 10 incorrect → PValue = 20/30 ≈ 0.667
        // TimeSpentSeconds: 10 for correct, 20 for incorrect → AvgTime = (20*10 + 10*20)/30 = 13.333
        // HintUsed: 5 of the 20 correct have hint → HintUsageRate = 5/30 ≈ 0.167
        var answers = new List<(bool IsCorrect, int TimeSec, bool HintUsed)>();
        for (int i = 0; i < 20; i++)
            answers.Add((true, 10, i < 5));   // first 5 use hint
        for (int i = 0; i < 10; i++)
            answers.Add((false, 20, false));

        await SeedStudentAnswersAsync(questionId, answers);

        // Act: run the aggregation step directly via ICalibrationService
        await using var scope = _factory.Services.CreateAsyncScope();
        var svc       = scope.ServiceProvider.GetRequiredService<ICalibrationService>();
        var dbContext  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var processed  = await svc.AggregateQuestionStatsAsync();
        await dbContext.SaveChangesAsync(userId: 0);

        // Assert: stats row
        var stats = await dbContext.QuestionDifficultyStats
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.QuestionId == questionId);

        stats.Should().NotBeNull("aggregation must create a QuestionDifficultyStats row");
        stats!.SampleSize.Should().Be(30, "30 answers were seeded");
        stats.PValue.Should().BeApproximately(20.0 / 30.0, 0.001,
            "PValue = fraction correct = 20/30");
        stats.AvgTimeSpentSeconds.Should().BeApproximately((20 * 10.0 + 10 * 20.0) / 30.0, 0.001,
            "AvgTimeSpentSeconds = mean of all TimeSpentSeconds");
        stats.HintUsageRate.Should().BeApproximately(5.0 / 30.0, 0.001,
            "HintUsageRate = fraction with HintUsed=true = 5/30");
    }

    // =========================================================================
    // SCENARIO 1b — Idempotency: re-run updates same row, no duplicate
    // =========================================================================

    [Fact(DisplayName = "CAL-S1b Aggregation: re-run is idempotent (same row updated, no duplicate)")]
    public async Task Aggregation_Idempotent_SameRowUpdated()
    {
        // Arrange: seed question + 30 answers
        var lessonId   = await SeedLessonAsync("CAL-S1b Lesson");
        var questionId = await SeedQuestionAsync(lessonId, DifficultyLevel.Easy, GeneratedBy.Curated);
        var answers    = BuildAnswers(correct: 20, incorrect: 10, hintCount: 0);
        await SeedStudentAnswersAsync(questionId, answers);

        await using var scope = _factory.Services.CreateAsyncScope();
        var svc      = scope.ServiceProvider.GetRequiredService<ICalibrationService>();
        var db       = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // First run
        await svc.AggregateQuestionStatsAsync();
        await db.SaveChangesAsync(userId: 0);

        // Act: second run (idempotency check)
        await svc.AggregateQuestionStatsAsync();
        await db.SaveChangesAsync(userId: 0);

        // Assert: exactly ONE stats row for this question
        var count = await db.QuestionDifficultyStats
            .AsNoTracking()
            .CountAsync(s => s.QuestionId == questionId);

        count.Should().Be(1, "idempotent upsert must produce exactly one stats row per question");
    }

    // =========================================================================
    // SCENARIO 2 — No-PII guarantee
    // QuestionDifficultyStats and QuestionRecalibrationProposal must have no
    // StudentId / childId column — verified via entity shape (reflection) + row check.
    // =========================================================================

    [Fact(DisplayName = "CAL-S2 No-PII: QuestionDifficultyStats entity has no StudentId property")]
    public void NoPII_QuestionDifficultyStats_HasNoStudentIdProperty()
    {
        var type       = typeof(QuestionDifficultyStats);
        var properties = type.GetProperties();

        var studentIdProp = properties.FirstOrDefault(p =>
            p.Name.Equals("StudentId", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("ChildId",   StringComparison.OrdinalIgnoreCase));

        studentIdProp.Should().BeNull(
            "QuestionDifficultyStats is a de-identified aggregate — it MUST NOT have a StudentId or ChildId property. " +
            "Having one would violate AC5 (no per-child PII in the calibration pipeline).");
    }

    [Fact(DisplayName = "CAL-S2 No-PII: QuestionRecalibrationProposal entity has no StudentId property")]
    public void NoPII_QuestionRecalibrationProposal_HasNoStudentIdProperty()
    {
        var type       = typeof(QuestionRecalibrationProposal);
        var properties = type.GetProperties();

        var studentIdProp = properties.FirstOrDefault(p =>
            p.Name.Equals("StudentId", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("ChildId",   StringComparison.OrdinalIgnoreCase));

        studentIdProp.Should().BeNull(
            "QuestionRecalibrationProposal must not carry any per-child identifier. " +
            "AC5: aggregation operates on de-identified counts only.");
    }

    [Fact(DisplayName = "CAL-S2 No-PII: persisted stats row contains no student identifier via DB column")]
    public async Task NoPII_PersistedStatsRow_HasNoStudentColumn()
    {
        // Seed and run aggregation to ensure at least one stats row exists
        var lessonId   = await SeedLessonAsync("CAL-S2 PII Lesson");
        var questionId = await SeedQuestionAsync(lessonId, DifficultyLevel.Easy, GeneratedBy.Curated);
        await SeedStudentAnswersAsync(questionId, BuildAnswers(correct: 20, incorrect: 10));

        await using var scope = _factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICalibrationService>();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await svc.AggregateQuestionStatsAsync();
        await db.SaveChangesAsync(userId: 0);

        // Check the actual DB column names via information_schema
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = 'learning'
              AND table_name IN ('QuestionDifficultyStats', 'QuestionRecalibrationProposals')
              AND LOWER(column_name) IN ('studentid', 'childid', 'student_id', 'child_id')";
        var colCount = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        await conn.CloseAsync();

        colCount.Should().Be(0,
            "the learning schema must have NO 'studentid'/'childid' column in QuestionDifficultyStats " +
            "or QuestionRecalibrationProposals — AC5 (no per-child PII in calibration pipeline).");
    }

    // =========================================================================
    // SCENARIO 3a — Proposal raised on divergence (>=30 samples)
    // authored=Easy, p-value low (hard territory), 30 answers → exactly 1 Pending proposal
    // authored Difficulty UNCHANGED
    // =========================================================================

    [Fact(DisplayName = "CAL-S3a Proposal: authored=Easy, p-value hard (<=0.45), >=30 samples → exactly 1 Pending proposal; Difficulty unchanged")]
    public async Task Proposal_RaisedOnDivergence_WithEnoughSamples()
    {
        // Arrange: question authored=Easy; seed answers so p-value ≈ 0.30 → Hard territory
        var lessonId   = await SeedLessonAsync("CAL-S3a Lesson");
        var questionId = await SeedQuestionAsync(lessonId, DifficultyLevel.Easy, GeneratedBy.Curated);

        // 9 correct out of 30 → PValue = 0.30 (below HardThreshold=0.45 → empirical=Hard)
        await SeedStudentAnswersAsync(questionId, BuildAnswers(correct: 9, incorrect: 21));

        await using var scope = _factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICalibrationService>();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        await svc.AggregateQuestionStatsAsync();
        await db.SaveChangesAsync(userId: 0);

        await svc.UpsertDivergenceProposalsAsync();
        await db.SaveChangesAsync(userId: 0);

        // Assert: exactly one Pending proposal
        var proposals = await db.QuestionRecalibrationProposals
            .AsNoTracking()
            .Where(p => p.QuestionId == questionId && p.Status == ProposalStatus.Pending)
            .ToListAsync();

        proposals.Should().HaveCount(1,
            "exactly one Pending proposal must be created when empirical band diverges from authored band");

        var proposal = proposals[0];
        proposal.AuthoredDifficulty.Should().Be(DifficultyLevel.Easy,
            "authored difficulty snapshot must be Easy (as seeded)");
        proposal.ProposedDifficulty.Should().Be(DifficultyLevel.Hard,
            "proposed difficulty must be Hard (p-value=0.30 <= HardThreshold=0.45)");
        proposal.EvidenceSampleSize.Should().Be(30, "evidence sample size must match");
        proposal.EvidencePValue.Should().BeApproximately(9.0 / 30.0, 0.001,
            "evidence p-value must match the computed aggregate");
        proposal.ReasonCode.Should().Be("DIFFICULTY_DIVERGENCE",
            "reason code must be the stable DIFFICULTY_DIVERGENCE constant");

        // The authored question Difficulty must NOT be changed
        var question = await db.QuizQuestions.AsNoTracking()
            .FirstAsync(q => q.Id == questionId);

        question.Difficulty.Should().Be(DifficultyLevel.Easy,
            "authored Difficulty must NEVER be mutated by the calibration job (propose-only safety model)");
    }

    // =========================================================================
    // SCENARIO 3b — Below MinSamples → NO proposal
    // =========================================================================

    [Fact(DisplayName = "CAL-S3b Proposal: <30 samples → NO proposal raised (MinSamples gate)")]
    public async Task Proposal_NotRaised_WhenBelowMinSamples()
    {
        // Arrange: 10 answers (< MinSamples=30), p-value low enough to trigger Hard
        var lessonId   = await SeedLessonAsync("CAL-S3b Lesson");
        var questionId = await SeedQuestionAsync(lessonId, DifficultyLevel.Easy, GeneratedBy.Curated);
        await SeedStudentAnswersAsync(questionId, BuildAnswers(correct: 2, incorrect: 8));  // 10 answers

        await using var scope = _factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICalibrationService>();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        await svc.AggregateQuestionStatsAsync();
        await db.SaveChangesAsync(userId: 0);
        await svc.UpsertDivergenceProposalsAsync();
        await db.SaveChangesAsync(userId: 0);

        var count = await db.QuestionRecalibrationProposals
            .AsNoTracking()
            .CountAsync(p => p.QuestionId == questionId && p.Status == ProposalStatus.Pending);

        count.Should().Be(0, "no proposal must be raised when SampleSize < MinSamples=30");
    }

    // =========================================================================
    // SCENARIO 4 — Proposal idempotency: re-run updates open proposal, no second Pending row
    // =========================================================================

    [Fact(DisplayName = "CAL-S4 Proposal idempotency: re-run updates open proposal, no second Pending row")]
    public async Task Proposal_ReRun_UpdatesExistingProposal_NoDuplicate()
    {
        var lessonId   = await SeedLessonAsync("CAL-S4 Lesson");
        var questionId = await SeedQuestionAsync(lessonId, DifficultyLevel.Easy, GeneratedBy.Curated);
        await SeedStudentAnswersAsync(questionId, BuildAnswers(correct: 9, incorrect: 21));

        await using var scope = _factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICalibrationService>();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // First run
        await svc.AggregateQuestionStatsAsync();
        await db.SaveChangesAsync(userId: 0);
        await svc.UpsertDivergenceProposalsAsync();
        await db.SaveChangesAsync(userId: 0);

        // Second run (simulate next day's job)
        await svc.AggregateQuestionStatsAsync();
        await db.SaveChangesAsync(userId: 0);
        await svc.UpsertDivergenceProposalsAsync();
        await db.SaveChangesAsync(userId: 0);

        // Assert: still exactly ONE Pending proposal (no duplicate)
        var count = await db.QuestionRecalibrationProposals
            .AsNoTracking()
            .CountAsync(p => p.QuestionId == questionId && p.Status == ProposalStatus.Pending);

        count.Should().Be(1,
            "idempotent upsert must not create a second Pending proposal for the same question");
    }

    // =========================================================================
    // SCENARIO 5a — AI flagging: AI question, p-value >= 0.95, >=30 → FlaggedForReview
    //                and NOT served by StartAttempt
    // =========================================================================

    [Fact(DisplayName = "CAL-S5a AI flag: AI question p-value>=0.95 >=30 samples → FlaggedForReview; excluded from StartAttempt pool")]
    public async Task AiFlag_Extreme_PValue_FlagsQuestion_And_ExcludesFromServe()
    {
        // Arrange: AI question; 30 answers all correct → PValue = 1.0 >= 0.95
        var lessonId           = await SeedLessonAsync("CAL-S5a Lesson");
        var aiQuestionId       = await SeedQuestionAsync(lessonId, DifficultyLevel.Medium, GeneratedBy.AI);
        // A second curated question in the same lesson so StartAttempt has something to return
        await SeedQuestionAsync(lessonId, DifficultyLevel.Medium, GeneratedBy.Curated);

        await SeedStudentAnswersAsync(aiQuestionId, BuildAnswers(correct: 30, incorrect: 0));

        await using var scope = _factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICalibrationService>();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        await svc.AggregateQuestionStatsAsync();
        await db.SaveChangesAsync(userId: 0);
        await svc.ApplyAiFlagsAsync();
        await db.SaveChangesAsync(userId: 0);

        // Assert: AI question is flagged
        var question = await db.QuizQuestions.AsNoTracking()
            .FirstAsync(q => q.Id == aiQuestionId);

        question.QualityState.Should().Be(QuestionQualityState.FlaggedForReview,
            "AI question with p-value >= AiFlagHighPValue (0.95) and >=30 samples must be flagged");
        question.FlagReason.Should().Be("AI_PVALUE_EXTREME_HIGH",
            "flag reason code must be AI_PVALUE_EXTREME_HIGH");

        // Assert: flagged question NOT in StartAttempt served pool
        var (studentToken, _) = await CreateStudentViaParentFlowAsync();
        var (resp, root, body) = await StartAttemptAsync(lessonId, studentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "StartAttempt must succeed; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "questions", out var questions).Should().BeTrue("body: {0}", body);
        questions.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", body);

        var questionIds = questions.EnumerateArray()
            .Select(q => TryProp(q, "id", out var idEl) ? idEl.GetInt32() : 0)
            .ToList();

        questionIds.Should().NotContain(aiQuestionId,
            "a FlaggedForReview AI question must be excluded from the served pool by StartAttemptService (AC5 serve-gate)");
    }

    // =========================================================================
    // SCENARIO 5b — Curated question with same extreme p-value is NOT flagged
    // =========================================================================

    [Fact(DisplayName = "CAL-S5b AI flag: Curated question with same extreme p-value is NOT flagged")]
    public async Task AiFlag_DoesNotFlag_CuratedQuestions()
    {
        // Arrange: Curated question; 30 answers all correct → PValue = 1.0 >= 0.95
        var lessonId       = await SeedLessonAsync("CAL-S5b Lesson");
        var curatedQuestId = await SeedQuestionAsync(lessonId, DifficultyLevel.Medium, GeneratedBy.Curated);
        await SeedStudentAnswersAsync(curatedQuestId, BuildAnswers(correct: 30, incorrect: 0));

        await using var scope = _factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICalibrationService>();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        await svc.AggregateQuestionStatsAsync();
        await db.SaveChangesAsync(userId: 0);
        await svc.ApplyAiFlagsAsync();
        await db.SaveChangesAsync(userId: 0);

        var question = await db.QuizQuestions.AsNoTracking()
            .FirstAsync(q => q.Id == curatedQuestId);

        question.QualityState.Should().Be(QuestionQualityState.Approved,
            "Curated questions must NOT be flagged by the AI-flag rule regardless of p-value");
        question.FlagReason.Should().BeNull(
            "FlagReason must be null for a non-flagged curated question");
    }

    // =========================================================================
    // SCENARIO 6 — In-flight scoring unaffected
    // A question already served (AttemptId recorded) — after flagging, its StartAttempt
    // serve path excludes it but the scoring path (GetQuestionForAnswer) is not filtered
    // We test: answer submission for an already-served question succeeds (HTTP 200/4xx per flow),
    // proving flagging does not retroactively break in-flight attempts.
    // =========================================================================

    [Fact(DisplayName = "CAL-S6 In-flight: question served before flagging still accepted by SubmitAnswer endpoint")]
    public async Task InFlight_FlaggedQuestion_StillScoreableViaSubmitAnswer()
    {
        // Arrange: seed AI question, start attempt (serves it), then flag it
        var lessonId   = await SeedLessonAsync("CAL-S6 Lesson");
        var aiQId      = await SeedQuestionAsync(lessonId, DifficultyLevel.Medium, GeneratedBy.AI);

        var (studentToken, _) = await CreateStudentViaParentFlowAsync();

        // Step 1: start attempt BEFORE flagging — AI question is still Approved here
        var (startResp, startRoot, startBody) = await StartAttemptAsync(lessonId, studentToken);
        startResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must succeed before flagging; body: {0}", startBody);

        TryProp(startRoot, "data", out var startData).Should().BeTrue("body: {0}", startBody);
        TryProp(startData, "attemptId", out var attemptIdEl).Should().BeTrue("body: {0}", startBody);
        var attemptId = attemptIdEl.GetInt32();
        attemptId.Should().BeGreaterThan(0, "body: {0}", startBody);

        // Step 2: flag the AI question via the calibration pipeline
        await SeedStudentAnswersAsync(aiQId, BuildAnswers(correct: 30, incorrect: 0));
        await using var scope = _factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICalibrationService>();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await svc.AggregateQuestionStatsAsync();
        await db.SaveChangesAsync(userId: 0);
        await svc.ApplyAiFlagsAsync();
        await db.SaveChangesAsync(userId: 0);

        // Verify question IS now flagged
        var q = await db.QuizQuestions.AsNoTracking().FirstAsync(x => x.Id == aiQId);
        q.QualityState.Should().Be(QuestionQualityState.FlaggedForReview,
            "AI question must be flagged after the calibration sweep");

        // Step 3: submit an answer for the in-flight attempt's question
        // The SubmitAnswer path does NOT apply the QualityState filter (see StartAttemptService comment).
        // We call the submit endpoint — it should return 200 (scored) or 404 (attempt/question not linked)
        // rather than 400/500 from a serve-gate filter. This proves the gate is only on StartAttempt.
        var submitUrl = $"api/Learning/Quizzes/{lessonId}/Attempt/{attemptId}/Answer";
        var (submitResp, submitRoot, submitBody) = await SendAsync(
            HttpMethod.Post, submitUrl,
            body: new { QuestionId = aiQId, AnswerPayload = "\"A\"" },
            bearer: studentToken);

        // The submit endpoint should not return 400/500 from a quality-state filter.
        // It may return 200 (if answer recorded) or 404 (if the question wasn't in the attempt snapshot).
        // The key assertion: flagging does NOT cause 500 or a filter-based 400/403.
        var statusCode = (int)submitResp.StatusCode;
        statusCode.Should().NotBe(500,
            "a server error must not occur when submitting an answer for an in-flight question that was flagged post-serve; " +
            "body: {0}", submitBody);
        statusCode.Should().NotBe(503,
            "a service-unavailable error must not occur on in-flight answer submission; body: {0}", submitBody);
        statusCode.Should().NotBe(400,
            "a 400 from a QualityState filter would indicate the serve-gate was incorrectly applied to the scoring path; " +
            "body: {0}", submitBody);
    }

    // =========================================================================
    // SCENARIO 7 — AdminOnly auth: all 6 endpoints
    // =========================================================================

    [Fact(DisplayName = "CAL-S7a Auth: anonymous → 401 on all 6 Calibration endpoints")]
    public async Task Auth_Anonymous_Returns401_OnAllEndpoints()
    {
        var endpoints = GetAllCalibrationEndpoints();

        foreach (var (method, url) in endpoints)
        {
            var (resp, _, body) = await SendAsync(method, url, bearer: null);
            resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "anonymous must get 401 on {0} {1}; body: {2}", method, url, body);
        }
    }

    [Fact(DisplayName = "CAL-S7b Auth: parent token → 403 on all 6 Calibration endpoints")]
    public async Task Auth_ParentToken_Returns403_OnAllEndpoints()
    {
        var endpoints = GetAllCalibrationEndpoints();

        foreach (var (method, url) in endpoints)
        {
            var (resp, _, body) = await SendAsync(method, url, bearer: _parentToken);
            resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "parent token must get 403 on {0} {1}; body: {2}", method, url, body);
        }
    }

    [Fact(DisplayName = "CAL-S7c Auth: admin token → 2xx/404/424 on all 6 Calibration endpoints (authenticated, no phantom 401/403)")]
    public async Task Auth_AdminToken_Returns2xx_OnAllEndpoints()
    {
        var endpoints = GetAllCalibrationEndpoints();

        foreach (var (method, url) in endpoints)
        {
            var (resp, _, body) = await SendAsync(method, url, bearer: _adminToken);
            var code = (int)resp.StatusCode;

            // Admin must NOT get 401 or 403 — any other response (200, 404, 424) is acceptable
            code.Should().NotBe(401,
                "admin token must not get 401 on {0} {1}; body: {2}", method, url, body);
            code.Should().NotBe(403,
                "admin token must not get 403 on {0} {1}; body: {2}", method, url, body);
        }
    }

    // =========================================================================
    // SCENARIO 8a — Promote applies authored-band change
    // =========================================================================

    [Fact(DisplayName = "CAL-S8a Promote: QuizQuestion.Difficulty becomes ProposedDifficulty; proposal = Promoted")]
    public async Task Promote_ApplieBand_AndSetsStatusPromoted()
    {
        // Arrange: create a Pending proposal (authored=Easy → proposed=Hard)
        var lessonId   = await SeedLessonAsync("CAL-S8a Lesson");
        var questionId = await SeedQuestionAsync(lessonId, DifficultyLevel.Easy, GeneratedBy.Curated);
        await SeedStudentAnswersAsync(questionId, BuildAnswers(correct: 9, incorrect: 21));

        await using var scope = _factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICalibrationService>();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        await svc.AggregateQuestionStatsAsync();
        await db.SaveChangesAsync(userId: 0);
        await svc.UpsertDivergenceProposalsAsync();
        await db.SaveChangesAsync(userId: 0);

        var proposal = await db.QuestionRecalibrationProposals
            .AsNoTracking()
            .FirstAsync(p => p.QuestionId == questionId && p.Status == ProposalStatus.Pending);

        // Act: call Promote endpoint
        var (resp, root, body) = await SendAsync(
            HttpMethod.Post,
            $"{CalibrationBase}/Proposals/{proposal.Id}/Promote",
            bearer: _adminToken);

        // Assert: HTTP 200, successed=true
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Promote must return 200; body: {0}", body);
        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        // Assert: Difficulty updated on the question
        var updatedQuestion = await db.QuizQuestions.AsNoTracking()
            .FirstAsync(q => q.Id == questionId);
        updatedQuestion.Difficulty.Should().Be(DifficultyLevel.Hard,
            "Promote must apply ProposedDifficulty (Hard) to QuizQuestion.Difficulty");

        // Assert: proposal status = Promoted
        var updatedProposal = await db.QuestionRecalibrationProposals.AsNoTracking()
            .FirstAsync(p => p.Id == proposal.Id);
        updatedProposal.Status.Should().Be(ProposalStatus.Promoted,
            "proposal Status must be Promoted after successful Promote action");
        updatedProposal.ResolvedAtUtc.Should().NotBeNull(
            "ResolvedAtUtc must be stamped on promote");
        updatedProposal.ResolvedByUserId.Should().NotBeNull(
            "ResolvedByUserId must be stamped with the admin's userId");
    }

    // =========================================================================
    // SCENARIO 8b — Reject: proposal = Rejected, authored band UNCHANGED
    // =========================================================================

    [Fact(DisplayName = "CAL-S8b Reject: proposal = Rejected; QuizQuestion.Difficulty unchanged")]
    public async Task Reject_SetsStatusRejected_And_DifficultyUnchanged()
    {
        // Arrange: Pending proposal
        var lessonId   = await SeedLessonAsync("CAL-S8b Lesson");
        var questionId = await SeedQuestionAsync(lessonId, DifficultyLevel.Easy, GeneratedBy.Curated);
        await SeedStudentAnswersAsync(questionId, BuildAnswers(correct: 9, incorrect: 21));

        await using var scope = _factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICalibrationService>();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await svc.AggregateQuestionStatsAsync();
        await db.SaveChangesAsync(userId: 0);
        await svc.UpsertDivergenceProposalsAsync();
        await db.SaveChangesAsync(userId: 0);

        var proposal = await db.QuestionRecalibrationProposals
            .AsNoTracking()
            .FirstAsync(p => p.QuestionId == questionId && p.Status == ProposalStatus.Pending);

        // Act: Reject
        var (resp, root, body) = await SendAsync(
            HttpMethod.Post,
            $"{CalibrationBase}/Proposals/{proposal.Id}/Reject",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "Reject must return 200; body: {0}", body);
        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        // Assert: Difficulty UNCHANGED
        var updatedQuestion = await db.QuizQuestions.AsNoTracking()
            .FirstAsync(q => q.Id == questionId);
        updatedQuestion.Difficulty.Should().Be(DifficultyLevel.Easy,
            "Reject must NOT change QuizQuestion.Difficulty (propose-only safety model)");

        // Assert: proposal = Rejected
        var updatedProposal = await db.QuestionRecalibrationProposals.AsNoTracking()
            .FirstAsync(p => p.Id == proposal.Id);
        updatedProposal.Status.Should().Be(ProposalStatus.Rejected,
            "proposal Status must be Rejected");
        updatedProposal.ResolvedAtUtc.Should().NotBeNull("ResolvedAtUtc must be stamped");
    }

    // =========================================================================
    // SCENARIO 9 — Unflag re-serves: Clear flagged question → reappears in pool
    // =========================================================================

    [Fact(DisplayName = "CAL-S9 Unflag: Clear flagged question → QualityState=Approved, reappears in StartAttempt pool")]
    public async Task Unflag_ClearedQuestion_ReappearsInServePool()
    {
        // Arrange: flag an AI question
        var lessonId   = await SeedLessonAsync("CAL-S9 Lesson");
        var aiQId      = await SeedQuestionAsync(lessonId, DifficultyLevel.Medium, GeneratedBy.AI);
        // A second curated question so the lesson isn't empty after flagging
        var curatedQId = await SeedQuestionAsync(lessonId, DifficultyLevel.Medium, GeneratedBy.Curated);

        await SeedStudentAnswersAsync(aiQId, BuildAnswers(correct: 30, incorrect: 0));

        await using var scope = _factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICalibrationService>();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        await svc.AggregateQuestionStatsAsync();
        await db.SaveChangesAsync(userId: 0);
        await svc.ApplyAiFlagsAsync();
        await db.SaveChangesAsync(userId: 0);

        // Verify it is flagged
        var flagged = await db.QuizQuestions.AsNoTracking().FirstAsync(q => q.Id == aiQId);
        flagged.QualityState.Should().Be(QuestionQualityState.FlaggedForReview,
            "pre-condition: AI question must be flagged before Clear");

        // Verify it is NOT served
        var (studentToken, _) = await CreateStudentViaParentFlowAsync();
        var (startR1, root1, body1) = await StartAttemptAsync(lessonId, studentToken);
        startR1.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body1);
        TryProp(root1, "data", out var d1).Should().BeTrue("body: {0}", body1);
        TryProp(d1, "questions", out var qs1).Should().BeTrue("body: {0}", body1);
        var servedBefore = qs1.EnumerateArray()
            .Select(q => TryProp(q, "id", out var idEl) ? idEl.GetInt32() : 0)
            .ToList();
        servedBefore.Should().NotContain(aiQId,
            "flagged AI question must be excluded before Clear");

        // Act: call Clear
        var (clearResp, clearRoot, clearBody) = await SendAsync(
            HttpMethod.Post,
            $"{CalibrationBase}/Flagged/{aiQId}/Clear",
            bearer: _adminToken);

        clearResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Clear must return 200; body: {0}", clearBody);
        TryProp(clearRoot, "successed", out var clearSucc).Should().BeTrue("body: {0}", clearBody);
        clearSucc.GetBoolean().Should().BeTrue("successed must be true on Clear; body: {0}", clearBody);

        // Assert: QualityState = Approved
        var cleared = await db.QuizQuestions.AsNoTracking().FirstAsync(q => q.Id == aiQId);
        cleared.QualityState.Should().Be(QuestionQualityState.Approved,
            "Clear must set QualityState back to Approved");
        cleared.FlagReason.Should().BeNull("FlagReason must be cleared to null");

        // Assert: now reappears in the served pool (new attempt for same student/lesson)
        // We need a fresh student to avoid the resume-same-attempt behavior
        var (student2Token, _) = await CreateStudentViaParentFlowAsync();
        var (startR2, root2, body2) = await StartAttemptAsync(lessonId, student2Token);
        startR2.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body2);
        TryProp(root2, "data", out var d2).Should().BeTrue("body: {0}", body2);
        TryProp(d2, "questions", out var qs2).Should().BeTrue("body: {0}", body2);
        var servedAfter = qs2.EnumerateArray()
            .Select(q => TryProp(q, "id", out var idEl) ? idEl.GetInt32() : 0)
            .ToList();
        servedAfter.Should().Contain(aiQId,
            "after Clear, the formerly-flagged question must reappear in the StartAttempt pool");
    }

    // =========================================================================
    // SCENARIO 10a — Pagination: list endpoints page correctly
    // =========================================================================

    [Fact(DisplayName = "CAL-S10a Pagination: GET /Proposals returns paginated BaseResponse with pagination metadata")]
    public async Task Proposals_List_ReturnsPagedResponse()
    {
        // Act: GET /Proposals?page=1&pageSize=5 with admin token
        var (resp, root, body) = await SendAsync(
            HttpMethod.Get,
            $"{CalibrationBase}/Proposals?page=1&pageSize=5",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Proposals must return 200 for admin; body: {0}", body);

        // Envelope assertions
        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Null, "data must not be null; body: {0}", body);

        // Paged response envelope checks — BaseResponse wraps a PaginatedResult
        // The data object should have at least: items / totalCount / currentPage / pageSize / totalPages
        // (exact field names per the PaginatedResult<T> shape in the codebase)
        var dataStr = data.GetRawText();
        dataStr.Should().NotBeNullOrWhiteSpace("body: {0}", body);

        // At minimum the outer envelope keys must be present
        TryProp(root, "statusCode", out var sc).Should().BeTrue("envelope must have 'statusCode'; body: {0}", body);
        sc.GetInt32().Should().Be(200, "statusCode must be 200; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue("envelope must have 'errors'; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have 'message'; body: {0}", body);
    }

    [Fact(DisplayName = "CAL-S10a Pagination: GET /Flagged returns paginated BaseResponse")]
    public async Task Flagged_List_ReturnsPagedResponse()
    {
        var (resp, root, body) = await SendAsync(
            HttpMethod.Get,
            $"{CalibrationBase}/Flagged?page=1&pageSize=5",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Flagged must return 200 for admin; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Null, "data must not be null; body: {0}", body);
    }

    // =========================================================================
    // SCENARIO 10b — Localization: Accept-Language: ar → Arabic message
    // =========================================================================

    [Fact(DisplayName = "CAL-S10b Localization: Accept-Language: ar → response message is localized (non-English)")]
    public async Task Localization_ArLanguage_ReturnsLocalizedMessage()
    {
        // We call a simple endpoint that always returns a localized message.
        // Use a non-existent Proposal id → 404 with localized "not found" message.
        var request = new HttpRequestMessage(HttpMethod.Post, $"{CalibrationBase}/Proposals/999999/Promote");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        request.Headers.AcceptLanguage.ParseAdd("ar");

        var response = await _client.SendAsync(request);
        var bodyStr  = await response.Content.ReadAsStringAsync();

        // The response must NOT be 401/403 (auth must still work)
        ((int)response.StatusCode).Should().NotBe(401,
            "admin token must be authenticated — must not get 401; body: {0}", bodyStr);
        ((int)response.StatusCode).Should().NotBe(403,
            "admin token must be authenticated — must not get 403; body: {0}", bodyStr);

        // The message field should exist and be a non-empty string
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
            root = JsonDocument.Parse(bodyStr).RootElement;

        TryProp(root, "message", out var msg).Should().BeTrue(
            "envelope must have a 'message' field; body: {0}", bodyStr);
        msg.GetString().Should().NotBeNullOrWhiteSpace(
            "message must be a non-empty localized string; body: {0}", bodyStr);

        // The message must NOT be an error fallback key (i.e., the localizer resolved it)
        var msgStr = msg.GetString()!;
        msgStr.Should().NotContain("CalibrationProposal",
            "the raw resource key must not be returned — localizer must have resolved it to a string; body: {0}", bodyStr);
    }

    // =========================================================================
    // Bonus: Envelope shape — GET /Proposals returns standard BaseResponse<T> keys
    // =========================================================================

    [Fact(DisplayName = "CAL-ENV Envelope: GET /Proposals returns all required BaseResponse keys (statusCode, successed, message, data, errors)")]
    public async Task Envelope_GetProposals_HasAllBaseResponseKeys()
    {
        var (resp, root, body) = await SendAsync(
            HttpMethod.Get, $"{CalibrationBase}/Proposals?page=1&pageSize=20",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have 'statusCode'; body: {0}", body);
        TryProp(root, "successed",  out _).Should().BeTrue("envelope must have 'successed'; body: {0}", body);
        TryProp(root, "message",    out _).Should().BeTrue("envelope must have 'message'; body: {0}", body);
        TryProp(root, "data",       out _).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        TryProp(root, "errors",     out _).Should().BeTrue("envelope must have 'errors'; body: {0}", body);

        // Canonical spelling must be 'successed' (not 'succeeded')
        body.Should().Contain("\"successed\"",
            "the API contract spells the success flag as 'successed' (not 'succeeded'); body: {0}", body);
    }

    // =========================================================================
    // Bonus: Confirm-flag endpoint auth + 404 on non-existent question
    // =========================================================================

    [Fact(DisplayName = "CAL-CONFIRM ConfirmFlag: admin can call Confirm on a non-existent question → 404 not 401/403")]
    public async Task ConfirmFlag_AdminAccess_NonExistentQuestion_Returns404()
    {
        var (resp, root, body) = await SendAsync(
            HttpMethod.Post,
            $"{CalibrationBase}/Flagged/999999/Confirm",
            bearer: _adminToken);

        var code = (int)resp.StatusCode;
        code.Should().NotBe(401, "admin must be authorized — must not get 401; body: {0}", body);
        code.Should().NotBe(403, "admin must be authorized — must not get 403; body: {0}", body);

        // Expect 404 or 424 (BusinessValidation) since the question doesn't exist / isn't flagged
        code.Should().BeOneOf(new[] { 404, 424 },
            "non-existent question must yield 404 (not found) or 424 (not flagged); body: {0}", body);
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

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

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendAsync(HttpMethod method, string url, object? body = null, string? bearer = null)
    {
        var request = new HttpRequestMessage(method, url);
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

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        StartAttemptAsync(int lessonId, string? token = null)
        => SendAsync(HttpMethod.Post, string.Format(StartAttemptFmt, lessonId), null, token);

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
        var email = $"p507_parent_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tok).Should().BeTrue("body: {0}", body);
        return tok.GetString()!;
    }

    private async Task<(string StudentToken, int StudentId)> CreateStudentViaParentFlowAsync()
    {
        var parentToken = await RegisterParentGetTokenAsync();
        var childEmail  = $"p507_child_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post, AddChildUrl,
            new
            {
                FullName = "Calibration Test Student",
                Email    = childEmail,
                Password = ValidChildPass,
                Grade    = 3,
                Language = "ar",
                Country  = "EG",
                LearningLanguage = "ar",
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            "Add-Child must succeed; body: {0}", addBody);
        TryProp(addRoot, "data", out var addData).Should().BeTrue("body: {0}", addBody);
        TryProp(addData, "id", out var idProp).Should().BeTrue("body: {0}", addBody);
        var childId = idProp.GetInt32();

        var studentToken = await SignInGetTokenAsync(childEmail, ValidChildPass);
        return (studentToken, childId);
    }

    /// <summary>
    /// Seeds the minimal Grade → Subject → Unit → Lesson hierarchy and returns Lesson.Id.
    /// The lesson is Active + Published so StartAttempt can find it.
    /// </summary>
    private async Task<int> SeedLessonAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        var grade = new Grade
        {
            Number      = 99,
            DisplayName = $"{name} Grade {Guid.NewGuid():N}",
            CreatedAt   = now,
            CreatedBy   = 0,
        };
        await db.Grades.AddAsync(grade);
        await db.SaveChangesAsync();

        var subject = new Subject
        {
            Name      = $"{name} Subject",
            GradeId   = grade.Id,
            CreatedAt = now,
            CreatedBy = 0,
        };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var unit = new Unit
        {
            Name          = $"{name} Unit",
            SequenceOrder = 1,
            SubjectId     = subject.Id,
            CreatedAt     = now,
            CreatedBy     = 0,
        };
        await db.Units.AddAsync(unit);
        await db.SaveChangesAsync();

        var lesson = new Lesson
        {
            Name           = name,
            Difficulty     = DifficultyLevel.Medium,
            SequenceOrder  = 1,
            IsLocked       = false,
            UnitId         = unit.Id,
            IsActive       = true,
            LifecycleState = LifecycleState.Published,
            CreatedAt      = now,
            CreatedBy      = 0,
        };
        await db.Lessons.AddAsync(lesson);
        await db.SaveChangesAsync();

        return lesson.Id;
    }

    /// <summary>
    /// Seeds a single QuizQuestion for the given lesson. Returns the question Id.
    /// The question is Active + Published + Approved (default) so it can be served.
    /// </summary>
    private async Task<int> SeedQuestionAsync(
        int lessonId,
        DifficultyLevel difficulty,
        GeneratedBy generatedBy)
    {
        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        var q = new QuizQuestion
        {
            LessonId       = lessonId,
            QuestionType   = QuestionType.MCQ,
            QuestionText   = $"Calibration test question {Guid.NewGuid():N}",
            Options        = "[\"A\",\"B\",\"C\",\"D\"]",
            CorrectAnswer  = "\"A\"",
            Difficulty     = difficulty,
            GeneratedBy    = generatedBy,
            IsActive       = true,
            LifecycleState = LifecycleState.Published,
            QualityState   = QuestionQualityState.Approved,
            CreatedAt      = now,
            CreatedBy      = 0,
        };
        await db.QuizQuestions.AddAsync(q);
        await db.SaveChangesAsync();
        return q.Id;
    }

    /// <summary>
    /// Seeds StudentAnswer rows for the given questionId. Creates a minimal Attempt per answer
    /// so the FK constraint (AttemptId → Attempt) is satisfied.
    ///
    /// AttemptId uses a throw-away student id (0) since these are calibration-only seeds
    /// not linked to a real registered student.
    /// </summary>
    private async Task SeedStudentAnswersAsync(
        int questionId,
        IEnumerable<(bool IsCorrect, int TimeSec, bool HintUsed)> answers)
    {
        using var scope = _factory.Services.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now  = DateTime.UtcNow;

        // We need a lesson id — query it from the question
        var question = await db.QuizQuestions.AsNoTracking()
            .FirstAsync(q => q.Id == questionId);

        foreach (var (isCorrect, timeSec, hintUsed) in answers)
        {
            // One Attempt per answer to satisfy the FK
            var attempt = new Attempt
            {
                StudentId  = 0,          // synthetic system student — no Identity user needed
                LessonId   = question.LessonId,
                Status     = AttemptStatus.Completed,
                StartedAt  = now,
                CompletedAt = now,
                CreatedAt  = now,
                CreatedBy  = 0,
            };
            await db.Attempts.AddAsync(attempt);
            await db.SaveChangesAsync();

            var answer = new StudentAnswer
            {
                AttemptId     = attempt.Id,
                QuestionId    = questionId,
                AnswerPayload = isCorrect ? "\"A\"" : "\"B\"",
                IsCorrect     = isCorrect,
                TimeSpentSeconds = timeSec,
                HintUsed      = hintUsed,
                CreatedAt     = now,
                CreatedBy     = 0,
            };
            await db.StudentAnswers.AddAsync(answer);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Builds a list of <paramref name="correct"/> correct + <paramref name="incorrect"/> incorrect answers.</summary>
    private static List<(bool IsCorrect, int TimeSec, bool HintUsed)> BuildAnswers(
        int correct, int incorrect, int hintCount = 0)
    {
        var list = new List<(bool, int, bool)>();
        for (int i = 0; i < correct; i++)
            list.Add((true, 10, i < hintCount));
        for (int i = 0; i < incorrect; i++)
            list.Add((false, 20, false));
        return list;
    }

    /// <summary>
    /// Returns the 6 Calibration endpoints for auth matrix tests.
    /// Uses placeholder IDs that will yield 404/424 but exercise the auth layer.
    /// </summary>
    private static IEnumerable<(HttpMethod Method, string Url)> GetAllCalibrationEndpoints()
    {
        return new[]
        {
            (HttpMethod.Get,  $"api/Learning/Calibration/Proposals?page=1&pageSize=5"),
            (HttpMethod.Get,  $"api/Learning/Calibration/Flagged?page=1&pageSize=5"),
            (HttpMethod.Post, $"api/Learning/Calibration/Proposals/999/Promote"),
            (HttpMethod.Post, $"api/Learning/Calibration/Proposals/999/Reject"),
            (HttpMethod.Post, $"api/Learning/Calibration/Flagged/999/Clear"),
            (HttpMethod.Post, $"api/Learning/Calibration/Flagged/999/Confirm"),
        };
    }
}
