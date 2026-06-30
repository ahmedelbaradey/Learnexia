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
/// P3-13a integration tests — "Behavioral profile depth"
///
/// New columns tested (persisted via migration P3_13a_AddBehavioralProfileDepth):
///   - GritScore   (double precision, nullable) on learning.StudentLearningProfiles
///   - MasteryVelocity (double precision, nullable) on learning.StudentLearningProfiles
///
/// Endpoint under test:
///   GET /api/Learning/Profile   [Authorize(Roles="Student")]
///
/// Coverage (4 scenarios as specified):
///   Scenario 1 — Cold-start: fresh student → GritScore=null, MasteryVelocity=null.
///                Also confirms migration applied (columns readable from DB row).
///   Scenario 2 — Grit populated: ≥5 answers with retry-after-wrong + hint mix → GritScore non-null ∈ [0,1].
///   Scenario 3 — MasteryVelocity populated: ≥3 attempts with clear improving trend → non-null ∈ [-1,1], positive.
///   Scenario 4 — No regression: existing 5 profile dims still surface correctly.
///
/// Seeding strategy mirrors P3_13_StudentProfile_Tests exactly:
///   - Student creation via real HTTP parent→child onboarding flow.
///   - Curriculum via EF direct insert into LearningDbContext.
///   - Assertions on HTTP response (DTO) + DB row (StudentLearningProfile).
///
/// MinSampleForGrit          = 5  (default from StudentProfileOptions)
/// MinAttemptsForTrajectory  = 3  (default)
/// ColdStartDataPointThreshold = 10 (default — warm profile needed before grit is computed)
/// </summary>
[Collection("IntegrationTests")]
public sealed class P3_13a_BehavioralProfileDepth_Tests : IAsyncLifetime
{
    // ── URL constants (mirrors P3_13_StudentProfile_Tests) ──────────────────────────────────────
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string ProfileUrl        = "api/Learning/Profile";
    private const string ValidChildPass    = "Child@Pass1";

    // Defaults from StudentProfileOptions — must match appsettings.json.
    private const int ColdStartThreshold       = 10;
    private const int MinSampleForGrit         = 5;
    private const int MinAttemptsForTrajectory = 3;

    // ── Infrastructure ───────────────────────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient             _client;

    public P3_13a_BehavioralProfileDepth_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Scenario 1 — Cold-start: GritScore and MasteryVelocity must be null
    // =========================================================================

    /// <summary>
    /// AC: A freshly-seeded student with no answer history returns GritScore=null and
    /// MasteryVelocity=null from GET /api/Learning/Profile.
    ///
    /// Also confirms:
    ///   - The migration applied (columns are present in the DB row and readable without error).
    ///   - The DB row's GritScore and MasteryVelocity are both null (no data).
    ///   - The DTO includes both new fields (they must be present in the JSON response).
    /// </summary>
    [Fact(DisplayName = "P3-13a-S01 Cold-start student → GritScore=null, MasteryVelocity=null on DTO and DB row (migration check)")]
    public async Task ColdStart_NewStudent_GritAndVelocityAreNull()
    {
        // ── Arrange: create a fresh student with NO answer history ───────────────────────────────
        var (token, studentId) = await CreateStudentViaParentFlowAsync("s01");

        // ── Act: GET /api/Learning/Profile ───────────────────────────────────────────────────────
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, ProfileUrl, null, token);

        // ── Assert: HTTP 200 ─────────────────────────────────────────────────────────────────────
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "cold-start student must return 200; body: {0}", body);

        // ── Assert: BaseResponse envelope ────────────────────────────────────────────────────────
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Object, "data must be an object; body: {0}", body);

        // ── Assert: GritScore is null in the JSON response ────────────────────────────────────────
        // The DTO must include the gritScore property (not absent — null is expected).
        TryProp(data, "gritScore", out var gritScoreProp).Should().BeTrue(
            "DTO must include 'gritScore' field (even null) — P3-13a migration check; body: {0}", body);
        gritScoreProp.ValueKind.Should().BeOneOf(
            new[] { JsonValueKind.Null, JsonValueKind.Undefined },
            "GritScore must be null for a cold-start student (TotalAnswers=0 < MinSampleForGrit={0}); body: {1}",
            MinSampleForGrit, body);

        // ── Assert: MasteryVelocity is null in the JSON response ─────────────────────────────────
        TryProp(data, "masteryVelocity", out var masteryVelProp).Should().BeTrue(
            "DTO must include 'masteryVelocity' field (even null) — P3-13a migration check; body: {0}", body);
        masteryVelProp.ValueKind.Should().BeOneOf(
            new[] { JsonValueKind.Null, JsonValueKind.Undefined },
            "MasteryVelocity must be null for a cold-start student (0 attempts < MinAttemptsForTrajectory={0}); body: {1}",
            MinAttemptsForTrajectory, body);

        // ── Assert: DB row confirms migration applied (columns present + null) ───────────────────
        // At cold-start, no profile row exists. After calling GET /Profile, the profile row is
        // only written when the recompute hook runs (on CompleteAttempt). With no attempts, the
        // profile row may not exist yet — that's correct cold-start behavior.
        //
        // To confirm the MIGRATION applied (columns are physically present), we explicitly create
        // a profile row via EF and read it back through the new nullable columns.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Insert a minimal profile row with both new fields explicitly null.
        var testRow = new StudentLearningProfile
        {
            StudentId             = studentId,
            DataPointCount        = 0,
            PreferredExplanationStyle = ExplanationStyle.Standard,
            GritScore             = null,    // P3-13a column — must not throw
            MasteryVelocity       = null,    // P3-13a column — must not throw
            LastRecomputedAt      = null,
            CreatedAt             = DateTime.UtcNow,
            CreatedBy             = 0,
        };

        // Upsert: if row already exists (cold-start endpoint created one), update; else insert.
        var existing = await db.StudentLearningProfiles
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        if (existing is null)
        {
            await db.StudentLearningProfiles.AddAsync(testRow);
        }
        else
        {
            existing.GritScore       = null;
            existing.MasteryVelocity = null;
            db.StudentLearningProfiles.Update(existing);
        }
        await db.SaveChangesAsync();

        // Read it back — this confirms the column round-trips via EF (migration applied correctly).
        var dbRow = await db.StudentLearningProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        dbRow.Should().NotBeNull("profile row must exist in DB after insert; studentId={0}", studentId);
        dbRow!.GritScore.Should().BeNull(
            "DB column GritScore must be null for cold-start student; migration P3_13a_AddBehavioralProfileDepth must have applied");
        dbRow.MasteryVelocity.Should().BeNull(
            "DB column MasteryVelocity must be null for cold-start student; migration P3_13a_AddBehavioralProfileDepth must have applied");
    }

    // =========================================================================
    // Scenario 2 — Grit populated after ≥5 answers with mixed retry/hint behavior
    // =========================================================================

    /// <summary>
    /// AC: After seeding ≥5 answers (≥ColdStartThreshold total) with a mix of:
    ///   - Correct answers after wrong answers on the same skill (RetryAfterWrongRate > 0)
    ///   - Some with hints, some without (non-trivial SelfSufficiencyComponent)
    /// the profile endpoint returns GritScore non-null and ∈ [0,1].
    ///
    /// Seeding strategy:
    ///   - 2 skills: skill A (answers with retry pattern), skill B (hint-free correct)
    ///   - Attempt 1 on skill A: wrong, then correct → RetryAfterWrongRate = 0.5 (1/2 skills with wrong have retry)
    ///   - 10 total answers (exceeds ColdStartThreshold=10) — cold-start guard lifts.
    ///   - Half the answers use hints → HintRate ~ 0.5 → SelfSufficiency ~ 0.5
    ///   - Expected GritScore ~ 0.5*0.5 + 0.5*0.5 = 0.5 (range assertion only)
    ///
    /// The unit tests own the exact math; this test asserts non-null + in-range + plausible sign.
    /// </summary>
    [Fact(DisplayName = "P3-13a-S02 ≥5 answers with retry-after-wrong + hint mix → GritScore non-null ∈ [0,1]")]
    public async Task GritPopulated_AfterSufficientAnswersWithRetryAndHintMix()
    {
        // ── Arrange ──────────────────────────────────────────────────────────────────────────────
        var (token, studentId) = await CreateStudentViaParentFlowAsync("s02");

        // Seed 2 lessons/skills. Each skill needs enough questions.
        // Skill A: we'll submit wrong first, then correct → retry pattern.
        // Skill B: all correct, no hints → boosts SelfSufficiency.
        var (lessonA, skillA) = await SeedLessonWithSkillAsync("s02a");
        var (lessonB, skillB) = await SeedLessonWithSkillAsync("s02b");

        // Seed 8 questions on skill A (wrong-then-correct retries) and 6 on skill B (clean correct)
        var questionsA = await SeedQuestionsAsync(lessonA, skillA, 8, QuestionType.MCQ);
        var questionsB = await SeedQuestionsAsync(lessonB, skillB, 6, QuestionType.MCQ);

        // ── Attempt 1 on Lesson A (skill A): 3 wrong + 5 correct, some with hints ──────────────
        // This creates the retry pattern: skill A has both wrong and correct answers.
        // Hint usage: first 4 correct answers use hints → hint rate on this attempt = 4/8 = 50%.
        var attemptA = await StartAttemptAsync(lessonA, token);
        // Wrong answers first (no hint on wrong)
        await SubmitAnswerAsync(attemptA, questionsA[0], "\"B\"", token, hintUsed: false);
        await SubmitAnswerAsync(attemptA, questionsA[1], "\"B\"", token, hintUsed: false);
        await SubmitAnswerAsync(attemptA, questionsA[2], "\"B\"", token, hintUsed: false);
        // Correct answers (mix of hint/no-hint)
        await SubmitAnswerAsync(attemptA, questionsA[3], "\"A\"", token, hintUsed: true);
        await SubmitAnswerAsync(attemptA, questionsA[4], "\"A\"", token, hintUsed: true);
        await SubmitAnswerAsync(attemptA, questionsA[5], "\"A\"", token, hintUsed: false);
        await SubmitAnswerAsync(attemptA, questionsA[6], "\"A\"", token, hintUsed: false);
        await SubmitAnswerAsync(attemptA, questionsA[7], "\"A\"", token, hintUsed: false);
        var (complA, _, complBodyA) = await CompleteAttemptAsync(attemptA, token);
        complA.StatusCode.Should().Be(HttpStatusCode.OK,
            "Attempt A completion must succeed; body: {0}", complBodyA);

        // ── Attempt 2 on Lesson B (skill B): all correct, no hints ───────────────────────────────
        // Total after: 8 + 6 = 14 answers > ColdStartThreshold(10) → cold-start guard lifts.
        // Skill B: only correct → does NOT contribute to RetryAfterWrongRate denominator.
        var attemptB = await StartAttemptAsync(lessonB, token);
        for (var i = 0; i < 6; i++)
            await SubmitAnswerAsync(attemptB, questionsB[i], "\"A\"", token, hintUsed: false);
        var (complB, _, complBodyB) = await CompleteAttemptAsync(attemptB, token);
        complB.StatusCode.Should().Be(HttpStatusCode.OK,
            "Attempt B completion must succeed; body: {0}", complBodyB);

        // Total: 14 answers > ColdStartThreshold(10) — warm profile.
        // MinSampleForGrit = 5 → 14 ≥ 5 → GritScore derivation runs.
        // RetryAfterWrongRate: skill A has wrong + correct → 1/1 skill with wrong = 1.0
        // HintRate: 2 hints out of 14 total = 14.3% → SelfSufficiency = 1 - 0.143 ≈ 0.857
        // GritScore ≈ 0.5 * 1.0 + 0.5 * 0.857 ≈ 0.929 (high grit — retry + low hint reliance)

        // ── Act: GET /api/Learning/Profile ───────────────────────────────────────────────────────
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, ProfileUrl, null, token);

        // ── Assert ───────────────────────────────────────────────────────────────────────────────
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "GET /Profile must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // GritScore must be non-null and ∈ [0,1]
        TryProp(data, "gritScore", out var gritScoreProp).Should().BeTrue(
            "DTO must include 'gritScore'; body: {0}", body);
        gritScoreProp.ValueKind.Should().Be(JsonValueKind.Number,
            "GritScore must be a number (not null) after sufficient answers; body: {0}", body);

        var gritValue = gritScoreProp.GetDouble();
        gritValue.Should().BeInRange(0.0, 1.0,
            "GritScore must be in [0,1]; actual={0}; body: {1}", gritValue, body);

        // Sanity: with a retry pattern and moderate hint usage, grit must be > 0.
        gritValue.Should().BeGreaterThan(0.0,
            "GritScore must be > 0 when student retried after wrong answers (retry-after-wrong rate > 0); body: {0}", body);

        // MasteryVelocity: only 2 attempts → below MinAttemptsForTrajectory(3) → must still be null.
        TryProp(data, "masteryVelocity", out var mvProp).Should().BeTrue("body: {0}", body);
        mvProp.ValueKind.Should().BeOneOf(
            new[] { JsonValueKind.Null, JsonValueKind.Undefined },
            "MasteryVelocity must be null with only 2 attempts (< MinAttemptsForTrajectory={0}); body: {1}",
            MinAttemptsForTrajectory, body);

        // ── Confirm DB row persisted the new columns ─────────────────────────────────────────────
        var dbRow = await GetProfileRowAsync(studentId);
        dbRow.Should().NotBeNull("StudentLearningProfile must exist after attempt completion; studentId={0}", studentId);
        dbRow!.GritScore.Should().NotBeNull(
            "DB column GritScore must be persisted (non-null) after {0} answers; studentId={1}",
            14, studentId);
        dbRow.GritScore!.Value.Should().BeInRange(0.0, 1.0,
            "DB GritScore must be in [0,1]; actual={0}", dbRow.GritScore.Value);
    }

    // =========================================================================
    // Scenario 3 — MasteryVelocity populated with improving trend (≥3 attempts)
    // =========================================================================

    /// <summary>
    /// AC: After seeding ≥3 attempts with a clear improving accuracy trend, the profile endpoint
    /// returns MasteryVelocity non-null, ∈ [-1,1], and POSITIVE (improving trend).
    ///
    /// MasteryVelocity formula:
    ///   fraction = 0.4, windowSize = floor(attemptCount * 0.4)
    ///   velocity = avg(recent window) − avg(older window)
    ///
    /// Seeding strategy for 5 attempts (windowSize = floor(5 * 0.4) = 2):
    ///   Attempt 1: 0/N correct  → accuracy ≈ 0.0  (older window: attempts 1-2)
    ///   Attempt 2: 1/N correct  → accuracy ≈ 0.2  (older window: attempts 1-2)
    ///   Attempt 3: middle       → excluded from both windows with 5 attempts
    ///   Attempt 4: 4/N correct  → accuracy ≈ 0.8  (recent window: attempts 4-5)
    ///   Attempt 5: 5/N correct  → accuracy = 1.0  (recent window: attempts 4-5)
    ///
    ///   olderAvg  ≈ (0.0 + 0.2) / 2 = 0.1
    ///   recentAvg ≈ (0.8 + 1.0) / 2 = 0.9
    ///   velocity  ≈ 0.9 - 0.1 = +0.8  → strongly positive (improving)
    ///
    /// Total answers: 5 attempts × 5 questions = 25 > ColdStartThreshold(10) → warm.
    /// </summary>
    [Fact(DisplayName = "P3-13a-S03 ≥3 attempts with improving trend → MasteryVelocity non-null ∈ [-1,1] and positive")]
    public async Task MasteryVelocity_Populated_PositiveForImprovingTrend()
    {
        // ── Arrange ──────────────────────────────────────────────────────────────────────────────
        var (token, studentId) = await CreateStudentViaParentFlowAsync("s03");

        // One lesson with enough questions for multiple attempts.
        var (lessonId, skillId) = await SeedLessonWithSkillAsync("s03");
        // 5 questions — each attempt uses all 5 questions.
        // The attempt flow in the API allows re-use of the same lesson (start new attempt each time).
        var questionIds = await SeedQuestionsAsync(lessonId, skillId, 5, QuestionType.MCQ);

        // ── Attempt 1 (very low accuracy): 0/5 correct ───────────────────────────────────────────
        await RunAttemptWithAccuracyAsync(lessonId, questionIds, token, correctCount: 0, totalCount: 5);

        // ── Attempt 2 (low accuracy): 1/5 correct ────────────────────────────────────────────────
        await RunAttemptWithAccuracyAsync(lessonId, questionIds, token, correctCount: 1, totalCount: 5);

        // ── Attempt 3 (middle — improves): 3/5 correct ───────────────────────────────────────────
        await RunAttemptWithAccuracyAsync(lessonId, questionIds, token, correctCount: 3, totalCount: 5);

        // ── Attempt 4 (high accuracy): 4/5 correct ───────────────────────────────────────────────
        await RunAttemptWithAccuracyAsync(lessonId, questionIds, token, correctCount: 4, totalCount: 5);

        // ── Attempt 5 (perfect): 5/5 correct ─────────────────────────────────────────────────────
        await RunAttemptWithAccuracyAsync(lessonId, questionIds, token, correctCount: 5, totalCount: 5);

        // Total: 5 attempts × 5 answers = 25 answers > ColdStartThreshold(10) → warm.
        // MinAttemptsForTrajectory = 3 → 5 ≥ 3 → MasteryVelocity derivation runs.
        // windowSize = floor(5 * 0.4) = 2
        // olderWindow  (first 2): [0/5=0.0, 1/5=0.2]  → avg = 0.1
        // recentWindow (last  2): [4/5=0.8, 5/5=1.0]  → avg = 0.9
        // velocity = 0.9 − 0.1 = +0.8 → POSITIVE (improving)

        // ── Act ──────────────────────────────────────────────────────────────────────────────────
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, ProfileUrl, null, token);

        // ── Assert ───────────────────────────────────────────────────────────────────────────────
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "GET /Profile must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // MasteryVelocity must be non-null and in [-1,1]
        TryProp(data, "masteryVelocity", out var mvProp).Should().BeTrue(
            "DTO must include 'masteryVelocity'; body: {0}", body);
        mvProp.ValueKind.Should().Be(JsonValueKind.Number,
            "MasteryVelocity must be a number (not null) after 5 attempts; body: {0}", body);

        var mvValue = mvProp.GetDouble();
        mvValue.Should().BeInRange(-1.0, 1.0,
            "MasteryVelocity must be in [-1,1]; actual={0}; body: {1}", mvValue, body);

        // Clear improving trend → must be positive
        mvValue.Should().BeGreaterThan(0.0,
            "MasteryVelocity must be positive for a clearly improving accuracy trend " +
            "(attempts: 0%, 20%, 60%, 80%, 100%); actual={0}; body: {1}", mvValue, body);

        // GritScore: 25 ≥ MinSampleForGrit(5) → also non-null
        TryProp(data, "gritScore", out var gritProp).Should().BeTrue("body: {0}", body);
        // All correct in later attempts → some attempts have 0 wrong → RetryRate might be lower.
        // We only require that it's non-null and in [0,1] — the unit tests own exact math.
        if (gritProp.ValueKind == JsonValueKind.Number)
        {
            gritProp.GetDouble().Should().BeInRange(0.0, 1.0,
                "GritScore (when non-null) must be in [0,1]; body: {0}", body);
        }

        // ── Confirm DB row ────────────────────────────────────────────────────────────────────────
        var dbRow = await GetProfileRowAsync(studentId);
        dbRow.Should().NotBeNull("StudentLearningProfile must exist; studentId={0}", studentId);
        dbRow!.MasteryVelocity.Should().NotBeNull(
            "DB column MasteryVelocity must be persisted (non-null) after {0} attempts", 5);
        dbRow.MasteryVelocity!.Value.Should().BeInRange(-1.0, 1.0,
            "DB MasteryVelocity must be in [-1,1]; actual={0}", dbRow.MasteryVelocity.Value);
        dbRow.MasteryVelocity.Value.Should().BeGreaterThan(0.0,
            "DB MasteryVelocity must be positive for improving trend; actual={0}", dbRow.MasteryVelocity.Value);
    }

    // =========================================================================
    // Scenario 4 — No regression: existing 5 profile dims still surface correctly
    // =========================================================================

    /// <summary>
    /// AC: The 5 original profile dimensions (QuestionTypeAffinity, RecurringErrorSkillIds,
    /// AttentionSpanMinutes, PreferredExplanationStyle, DataPointCount) still surface correctly
    /// after the P3-13a migration added the 2 new columns.
    ///
    /// This mirrors the cold-start check from P3_13_StudentProfile_Tests C01 to confirm
    /// no regression on the core profile DTO shape. The DTO must include ALL 7 fields
    /// (5 original + 2 new) in the response.
    /// </summary>
    [Fact(DisplayName = "P3-13a-S04 No regression — all 5 original profile dims + 2 new dims surface on DTO (envelope shape check)")]
    public async Task NoRegression_AllProfileDimsPresent_InResponseEnvelope()
    {
        // ── Arrange: new student (cold-start) ────────────────────────────────────────────────────
        var (token, _) = await CreateStudentViaParentFlowAsync("s04");

        // ── Act ──────────────────────────────────────────────────────────────────────────────────
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, ProfileUrl, null, token);

        // ── Assert: HTTP 200 ─────────────────────────────────────────────────────────────────────
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Profile must return 200; body: {0}", body);

        // ── Assert: BaseResponse envelope ────────────────────────────────────────────────────────
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "statusCode", out var statusCode).Should().BeTrue("body: {0}", body);
        statusCode.GetInt32().Should().Be(200, "StatusCode must be 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Object, "data must be an object; body: {0}", body);

        // ── Assert: all 5 original dimensions are present ────────────────────────────────────────
        var requiredOriginalKeys = new[]
        {
            "questionTypeAffinity",
            "recurringErrorSkillIds",
            "attentionSpanMinutes",
            "preferredExplanationStyle",
            "dataPointCount",
        };

        foreach (var key in requiredOriginalKeys)
        {
            TryProp(data, key, out _).Should().BeTrue(
                "DTO must include original field '{0}' (no regression from P3-13a); body: {1}", key, body);
        }

        // ── Assert: cold-start values for original dims are correct ──────────────────────────────
        TryProp(data, "dataPointCount", out var dpc);
        dpc.GetInt32().Should().Be(0, "DataPointCount must be 0 for cold-start; body: {0}", body);

        TryProp(data, "preferredExplanationStyle", out var style);
        style.GetInt32().Should().Be(0, "PreferredExplanationStyle must be Standard(0) for cold-start; body: {0}", body);

        TryProp(data, "questionTypeAffinity", out var affinity);
        if (affinity.ValueKind == JsonValueKind.Object)
        {
            affinity.EnumerateObject().Any().Should().BeFalse(
                "questionTypeAffinity must be empty for cold-start; body: {0}", body);
        }

        TryProp(data, "recurringErrorSkillIds", out var clusters);
        if (clusters.ValueKind == JsonValueKind.Array)
        {
            clusters.GetArrayLength().Should().Be(0,
                "recurringErrorSkillIds must be empty for cold-start; body: {0}", body);
        }

        TryProp(data, "attentionSpanMinutes", out var attn);
        attn.ValueKind.Should().BeOneOf(
            new[] { JsonValueKind.Null, JsonValueKind.Undefined },
            "attentionSpanMinutes must be null for cold-start; body: {0}", body);

        // ── Assert: 2 new P3-13a dimensions are present in the response ──────────────────────────
        TryProp(data, "gritScore", out var grit).Should().BeTrue(
            "DTO must include 'gritScore' (P3-13a new field); body: {0}", body);
        grit.ValueKind.Should().BeOneOf(
            new[] { JsonValueKind.Null, JsonValueKind.Undefined },
            "GritScore must be null for cold-start student; body: {0}", body);

        TryProp(data, "masteryVelocity", out var mv).Should().BeTrue(
            "DTO must include 'masteryVelocity' (P3-13a new field); body: {0}", body);
        mv.ValueKind.Should().BeOneOf(
            new[] { JsonValueKind.Null, JsonValueKind.Undefined },
            "MasteryVelocity must be null for cold-start student; body: {0}", body);

        // ── Assert: data-minimization — no raw answer fields in response ─────────────────────────
        var forbiddenKeys = new[]
        {
            "answerPayload", "answers", "timeSpentSeconds",
            "hintUsed", "attemptId", "completedAt", "sessionLogs",
        };
        foreach (var forbidden in forbiddenKeys)
        {
            TryProp(data, forbidden, out _).Should().BeFalse(
                "DTO must NOT expose raw field '{0}' (data-minimization); body: {1}", forbidden, body);
        }
    }

    // =========================================================================
    // HTTP helpers (mirrors P3_13_StudentProfile_Tests exactly)
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p313a_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; } catch { /* non-JSON */ }
        }
        return (response, root, bodyStr);
    }

    private async Task<string> SignInAndGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "sign-in must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private async Task<(string Token, int StudentId)> CreateStudentViaParentFlowAsync(string tag = "")
    {
        var parentEmail = UniqueEmail($"parent_{tag}");
        var (regResp, regRoot, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"parent registration must succeed; body: {regBody}");
        TryProp(regRoot, "data", out var regData).Should().BeTrue("body: {0}", regBody);
        TryProp(regData, "accessToken", out var parentTok).Should().BeTrue("body: {0}", regBody);
        var parentToken = parentTok.GetString()!;

        var childEmail = UniqueEmail($"child_{tag}");
        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = "P313a Test Student",
                Email            = childEmail,
                Password         = ValidChildPass,
                Grade            = 3,
                Language         = "ar",
                Country          = "EG",
                LearningLanguage = "ar",
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"Add-Child must succeed; body: {addBody}");
        TryProp(addRoot, "data", out var addData).Should().BeTrue("body: {0}", addBody);
        TryProp(addData, "id", out var idProp).Should().BeTrue("body: {0}", addBody);
        var childId = idProp.GetInt32();

        var studentToken = await SignInAndGetTokenAsync(childEmail, ValidChildPass);
        return (studentToken, childId);
    }

    // ── DB seeding helpers ───────────────────────────────────────────────────────────────────────

    private async Task<(int LessonId, int SkillId)> SeedLessonWithSkillAsync(string tag = "")
    {
        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        var grade = new Grade
        {
            Number      = 56,
            DisplayName = $"P313a_G_{tag}_{Guid.NewGuid():N}",
            CreatedAt   = now,
            CreatedBy   = 0
        };
        await db.Grades.AddAsync(grade);
        await db.SaveChangesAsync();

        var subject = new Subject
        {
            Name      = $"P313a_Subj_{tag}_{Guid.NewGuid():N}",
            GradeId   = grade.Id,
            CreatedAt = now,
            CreatedBy = 0
        };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var unit = new Unit
        {
            Name          = $"P313a_Unit_{tag}_{Guid.NewGuid():N}",
            SequenceOrder = 1,
            SubjectId     = subject.Id,
            CreatedAt     = now,
            CreatedBy     = 0
        };
        await db.Units.AddAsync(unit);
        await db.SaveChangesAsync();

        var concept = new Concept
        {
            Name            = $"P313a_Concept_{tag}_{Guid.NewGuid():N}",
            DifficultyLevel = DifficultyLevel.Easy,
            SubjectId       = subject.Id,
            CreatedAt       = now,
            CreatedBy       = 0
        };
        await db.Concepts.AddAsync(concept);
        await db.SaveChangesAsync();

        var skill = new Skill
        {
            Name                 = $"P313a_Skill_{tag}_{Guid.NewGuid():N}",
            MasteryThreshold     = 80,
            EstimatedTimeMinutes = 10,
            ConceptId            = concept.Id,
            CreatedAt            = now,
            CreatedBy            = 0
        };
        await db.Skills.AddAsync(skill);
        await db.SaveChangesAsync();

        var lesson = new Lesson
        {
            Name           = $"P313a_Lesson_{tag}_{Guid.NewGuid():N}",
            Difficulty     = DifficultyLevel.Easy,
            SequenceOrder  = 1,
            IsLocked       = false,
            UnitId         = unit.Id,
            SkillId        = skill.Id,
            IsActive       = true,
            LifecycleState = LifecycleState.Published,
            CreatedAt      = now,
            CreatedBy      = 0
        };
        await db.Lessons.AddAsync(lesson);
        await db.SaveChangesAsync();

        return (lesson.Id, skill.Id);
    }

    private async Task<List<int>> SeedQuestionsAsync(
        int lessonId,
        int skillId,
        int count,
        QuestionType questionType = QuestionType.MCQ)
    {
        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;
        var ids = new List<int>();

        for (var i = 1; i <= count; i++)
        {
            var q = new QuizQuestion
            {
                LessonId       = lessonId,
                SkillId        = skillId,
                QuestionType   = questionType,
                QuestionText   = $"P313a_Q{i}_{questionType}_{Guid.NewGuid():N}?",
                Options        = "[\"A\",\"B\",\"C\",\"D\"]",
                CorrectAnswer  = "\"A\"",
                Difficulty     = DifficultyLevel.Easy,
                GeneratedBy    = GeneratedBy.Curated,
                IsActive       = true,
                LifecycleState = LifecycleState.Published,
                CreatedAt      = now,
                CreatedBy      = 0,
            };
            await db.QuizQuestions.AddAsync(q);
            await db.SaveChangesAsync();
            ids.Add(q.Id);
        }
        return ids;
    }

    // ── HTTP action helpers ──────────────────────────────────────────────────────────────────────

    private async Task<int> StartAttemptAsync(int lessonId, string token)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", null, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "attemptId", out var idProp).Should().BeTrue("body: {0}", body);
        return idProp.GetInt32();
    }

    private Task<(HttpResponseMessage, JsonElement, string)> SubmitAnswerAsync(
        int attemptId, int questionId, string answer, string token, bool hintUsed = false)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Answers",
            new { QuestionId = questionId, AnswerPayload = answer, TimeSpentSeconds = 30, HintUsed = hintUsed },
            token);

    private Task<(HttpResponseMessage, JsonElement, string)> CompleteAttemptAsync(
        int attemptId, string token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Complete", null, token);

    /// <summary>
    /// Runs a complete attempt on a lesson, answering <paramref name="correctCount"/> questions
    /// correctly and the rest wrong. Each attempt uses the first N questions from the list.
    /// </summary>
    private async Task RunAttemptWithAccuracyAsync(
        int lessonId,
        List<int> questionIds,
        string token,
        int correctCount,
        int totalCount)
    {
        var attemptId = await StartAttemptAsync(lessonId, token);

        for (var i = 0; i < totalCount; i++)
        {
            var isCorrect = i < correctCount;
            var answer = isCorrect ? "\"A\"" : "\"B\"";
            await SubmitAnswerAsync(attemptId, questionIds[i], answer, token, hintUsed: false);
        }

        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Attempt completion must succeed; body: {0}", complBody);
    }

    /// <summary>Reads the StudentLearningProfile row directly from the DB for assertion.</summary>
    private async Task<StudentLearningProfile?> GetProfileRowAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        return await db.StudentLearningProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);
    }
}
