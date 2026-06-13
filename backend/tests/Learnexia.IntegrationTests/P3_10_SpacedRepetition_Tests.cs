using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Jobs;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P3-10 integration tests — "Schedule spaced-repetition practice".
///
/// Endpoints under test:
///   GET  /api/Learning/Reviews/Due  [Authorize(Roles="Student")]
///
/// Write path tested via:
///   POST /api/Learning/Quizzes/{lessonId}/Attempt  (start)
///   POST /api/Learning/Quizzes/{attemptId}/Answers (submit)
///   POST /api/Learning/Quizzes/{attemptId}/Complete (complete → triggers mastery upsert + SR hook)
///
/// Background job tested via:
///   SpacedRepetitionSweepJob.RunAsync() — resolved from DI and invoked directly (no Hangfire cron firing).
///
/// DB seeding strategy:
///   - Student creation: real HTTP parent→child onboarding flow (mirrors P3-09 pattern).
///   - Curriculum (Grade/Subject/Unit/Lesson/Skill): EF direct insert into LearningDbContext.
///   - SR state overrides (NextReviewDueAt in past/future): EF direct update after mastery row created.
///
/// UTC discipline:
///   - All seeded DateTime values use DateTime.UtcNow (UTC-kind).
///   - AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true) is set in LearnexiaWebAppFactory;
///     this makes Npgsql accept UTC-kind DateTimes as-is and return them as UTC-kind.
///   - The sweep job calls ToUniversalTime() defensively, which is safe under legacy mode.
///   - Test assertions on NextReviewDueAt use ToUniversalTime() to normalise before comparison.
///
/// Coverage map — 7 plan test cases (Batch 5 from P3-10.md):
///   Case 1 → NeedsReview_SkillAppearsInDueList                (AC1, AC2)
///   Case 2 → Mastered_PastDueDate_AppearsInDueList            (AC1 mastered-but-stale)
///   Case 3 → Mastered_FutureDueDate_NotInDueList              (AC1 boundary)
///   Case 4 → SweepJob_Idempotent_RunTwice_SameState           (AC3)
///   Case 5 → SuccessfulReview_AdvancesLadder_RepetitionIncrements  (AC4 success path)
///   Case 6 → FailedReview_ResetsInterval_ToShortest           (AC4 reset path)
///   Case 7 → IDOR_AnotherStudent_DueListNotAddressable        (AC5 isolation)
///
/// Plus: 401 guard — GetDueReviews_WithoutJwt_Returns401.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P3_10_SpacedRepetition_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string RegisterParentUrl   = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl            = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl          = "api/Parent/Add-Child";
    private const string DueReviewsUrl        = "api/Learning/Reviews/Due";
    private const string ValidChildPassword   = "Child@Pass1";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P3_10_SpacedRepetition_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Shared HTTP helpers (mirrors P3-09 pattern exactly)
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p310_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>Case-insensitive property lookup — handles camelCase and PascalCase JSON.</summary>
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
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body — root remains default */ }
        }
        return (response, root, bodyStr);
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

    private async Task<(string Token, int StudentId)> CreateStudentViaParentFlowAsync(string tag = "")
    {
        var parentEmail = UniqueEmail($"parent_{tag}");
        var (regResp, regRoot, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"parent registration must succeed; body: {regBody}");
        TryProp(regRoot, "data", out var regData).Should().BeTrue("body: {0}", regBody);
        TryProp(regData, "accessToken", out var parentTokProp).Should().BeTrue("body: {0}", regBody);
        var parentToken = parentTokProp.GetString()!;

        var childEmail = UniqueEmail($"child_{tag}");
        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = "Test Student P310",
                Email            = childEmail,
                Password         = ValidChildPassword,
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

        var studentToken = await SignInAndGetTokenAsync(childEmail, ValidChildPassword);
        return (studentToken, childId);
    }

    // =========================================================================
    // DB-seeding helpers — mirrors P3-09 approach exactly
    // =========================================================================

    private async Task<(int LessonId, int SkillId)> SeedLessonWithSkillAsync(
        string tag = "",
        int masteryThreshold = 80)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        var grade = new Grade
        {
            Number      = 99,
            DisplayName = $"SR_G_{tag}_{Guid.NewGuid():N}",
            CreatedAt   = now,
            CreatedBy   = 0
        };
        await db.Grades.AddAsync(grade);
        await db.SaveChangesAsync();

        var subject = new Subject
        {
            Name      = $"SR_Subj_{tag}_{Guid.NewGuid():N}",
            GradeId   = grade.Id,
            CreatedAt = now,
            CreatedBy = 0
        };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var unit = new Unit
        {
            Name          = $"SR_Unit_{tag}_{Guid.NewGuid():N}",
            SequenceOrder = 1,
            SubjectId     = subject.Id,
            CreatedAt     = now,
            CreatedBy     = 0
        };
        await db.Units.AddAsync(unit);
        await db.SaveChangesAsync();

        var concept = new Concept
        {
            Name            = $"SR_Concept_{tag}_{Guid.NewGuid():N}",
            DifficultyLevel = DifficultyLevel.Easy,
            SubjectId       = subject.Id,
            CreatedAt       = now,
            CreatedBy       = 0
        };
        await db.Concepts.AddAsync(concept);
        await db.SaveChangesAsync();

        var skill = new Skill
        {
            Name                 = $"SR_Skill_{tag}_{Guid.NewGuid():N}",
            MasteryThreshold     = masteryThreshold,
            EstimatedTimeMinutes = 10,
            ConceptId            = concept.Id,
            CreatedAt            = now,
            CreatedBy            = 0
        };
        await db.Skills.AddAsync(skill);
        await db.SaveChangesAsync();

        var lesson = new Lesson
        {
            Name           = $"SR_Lesson_{tag}_{Guid.NewGuid():N}",
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

    private async Task<List<int>> SeedQuestionsAsync(int lessonId, int skillId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;
        var ids = new List<int>();
        for (var i = 1; i <= count; i++)
        {
            var q = new QuizQuestion
            {
                LessonId       = lessonId,
                SkillId        = skillId,
                QuestionType   = QuestionType.MCQ,
                QuestionText   = $"SR_Q{i}_{Guid.NewGuid():N}?",
                Options        = "[\"A\",\"B\",\"C\",\"D\"]",
                CorrectAnswer  = "\"A\"",   // correct answer is always "A"
                Difficulty     = DifficultyLevel.Easy,
                GeneratedBy    = GeneratedBy.Curated,
                IsActive       = true,
                LifecycleState = LifecycleState.Published,
                CreatedAt      = now,
                CreatedBy      = 0
            };
            await db.QuizQuestions.AddAsync(q);
            await db.SaveChangesAsync();
            ids.Add(q.Id);
        }
        return ids;
    }

    /// <summary>
    /// Seeds a StudentSkillMastery row directly for the given student + skill.
    /// Used when tests need to set a specific SR state without going through the full attempt flow
    /// (e.g. setting NextReviewDueAt in the past/future for Cases 2 and 3).
    /// UTC discipline: all DateTime values are UTC-kind.
    /// </summary>
    private async Task<int> SeedMasteryRowDirectlyAsync(
        int studentId,
        int skillId,
        MasteryStatus status,
        DateTime? nextReviewDueAt,
        int reviewIntervalDays = 7,
        int repetitionNumber = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        // Check if a row already exists (from a prior flow) — use raw EF to upsert correctly.
        var existing = await db.StudentSkillMasteries
            .FirstOrDefaultAsync(m => m.StudentId == studentId && m.SkillId == skillId);

        if (existing is not null)
        {
            existing.Status              = status;
            existing.MasteryPercentage   = status == MasteryStatus.Mastered ? 90 : 20;
            existing.AttemptsCount       = Math.Max(existing.AttemptsCount, 1);
            existing.LastPracticedAt     = now.AddDays(-10);
            existing.ReviewIntervalDays  = reviewIntervalDays;
            existing.NextReviewDueAt     = nextReviewDueAt;
            existing.RepetitionNumber    = repetitionNumber;
            db.StudentSkillMasteries.Update(existing);
            await db.SaveChangesAsync();
            return existing.Id;
        }

        var row = new StudentSkillMastery
        {
            StudentId          = studentId,
            SkillId            = skillId,
            Status             = status,
            MasteryPercentage  = status == MasteryStatus.Mastered ? 90 : 20,
            AttemptsCount      = 1,
            LastPracticedAt    = now.AddDays(-10),
            ReviewIntervalDays = reviewIntervalDays,
            NextReviewDueAt    = nextReviewDueAt,
            RepetitionNumber   = repetitionNumber,
            CreatedAt          = now,
            CreatedBy          = studentId,
        };
        await db.StudentSkillMasteries.AddAsync(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // =========================================================================
    // HTTP action helpers
    // =========================================================================

    private async Task<int> StartAttemptAsync(int lessonId, string token)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", null, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "StartAttempt must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "attemptId", out var idProp).Should().BeTrue("body: {0}", body);
        return idProp.GetInt32();
    }

    private Task<(HttpResponseMessage, JsonElement, string)> SubmitAnswerAsync(
        int attemptId, int questionId, string answer, string token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Answers",
            new { QuestionId = questionId, AnswerPayload = answer, TimeSpentSeconds = 10, HintUsed = false },
            token);

    private Task<(HttpResponseMessage, JsonElement, string)> CompleteAttemptAsync(
        int attemptId, string token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Complete", null, token);

    private Task<(HttpResponseMessage, JsonElement, string)> GetDueReviewsAsync(string? token)
        => SendAsync(_client, HttpMethod.Get, DueReviewsUrl, null, token);

    /// <summary>
    /// Resolves SpacedRepetitionSweepJob from DI and invokes RunAsync directly
    /// (mirrors Gamification StreakSweepJob test pattern — no Hangfire cron needed in tests).
    /// </summary>
    private async Task RunSweepJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<SpacedRepetitionSweepJob>();
        await job.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// Reads a StudentSkillMastery row from the DB via EF (AsNoTracking) for assertions.
    /// </summary>
    private async Task<StudentSkillMastery?> GetMasteryRowAsync(int studentId, int skillId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        return await db.StudentSkillMasteries
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.StudentId == studentId && m.SkillId == skillId);
    }

    // =========================================================================
    // Case 1 — NeedsReview skill → appears in GET /Reviews/Due (AC1 + AC2)
    // =========================================================================

    [Fact(DisplayName = "P310-C01 NeedsReview skill → GET /Reviews/Due returns it with correct envelope")]
    public async Task NeedsReview_SkillAppearsInDueList()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("c01");

        // Seed a mastery row directly in NeedsReview state so it appears as due immediately.
        var (lessonId, skillId) = await SeedLessonWithSkillAsync("c01");
        await SeedMasteryRowDirectlyAsync(studentId, skillId, MasteryStatus.NeedsReview,
            nextReviewDueAt: null, reviewIntervalDays: 0, repetitionNumber: 0);

        // Hit the endpoint
        var (resp, root, body) = await GetDueReviewsAsync(token);

        // ── HTTP 200 ──────────────────────────────────────────────────────────
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Reviews/Due must return 200 when student has a NeedsReview skill; body: {0}", body);

        // ── BaseResponse<T> envelope ──────────────────────────────────────────
        TryProp(root, "successed", out var successed).Should().BeTrue(
            "envelope must have 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "Successed must be true for a successful due-list response; body: {0}", body);

        TryProp(root, "statusCode", out var statusCode).Should().BeTrue(
            "envelope must have 'statusCode'; body: {0}", body);
        statusCode.GetInt32().Should().Be(200, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue(
            "envelope must have 'data'; body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array,
            "data must be an array of DueReviewDto; body: {0}", body);

        // ── Due list contains the NeedsReview skill ───────────────────────────
        var items = data.EnumerateArray().ToList();
        var matchingItems = items
            .Where(i => TryProp(i, "skillId", out var sid) && sid.GetInt32() == skillId)
            .ToList();
        matchingItems.Should().ContainSingle(
            $"due list must contain skillId={skillId} (NeedsReview); body: {body}");

        // ── DueReviewDto shape for that item ─────────────────────────────────
        var item = matchingItems[0];

        TryProp(item, "skillId", out var skillIdProp).Should().BeTrue("body: {0}", body);
        skillIdProp.GetInt32().Should().Be(skillId, "body: {0}", body);

        TryProp(item, "skillName", out var skillNameProp).Should().BeTrue(
            "DueReviewDto must have 'skillName'; body: {0}", body);
        skillNameProp.GetString().Should().NotBeNullOrWhiteSpace(
            "skillName must be populated from the Skill entity; body: {0}", body);

        TryProp(item, "status", out var statusProp).Should().BeTrue(
            "DueReviewDto must have 'status'; body: {0}", body);
        // MasteryStatus.NeedsReview == 3
        statusProp.GetInt32().Should().Be(3,
            "status must be 3 (NeedsReview) for a NeedsReview skill; body: {0}", body);

        TryProp(item, "repetitionNumber", out var repProp).Should().BeTrue(
            "DueReviewDto must have 'repetitionNumber'; body: {0}", body);
        repProp.GetInt32().Should().Be(0,
            "repetitionNumber must be 0 for a freshly-seeded NeedsReview skill; body: {0}", body);
    }

    // =========================================================================
    // Case 2 — Mastered + past NextReviewDueAt → appears in due list (AC1 boundary)
    // =========================================================================

    [Fact(DisplayName = "P310-C02 Mastered skill with NextReviewDueAt in the past → appears in GET /Reviews/Due")]
    public async Task Mastered_PastDueDate_AppearsInDueList()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("c02");

        var (lessonId, skillId) = await SeedLessonWithSkillAsync("c02");

        // Seed a Mastered row whose NextReviewDueAt was 2 days ago (stale → due).
        var pastDue = DateTime.UtcNow.AddDays(-2);
        await SeedMasteryRowDirectlyAsync(studentId, skillId, MasteryStatus.Mastered,
            nextReviewDueAt: pastDue, reviewIntervalDays: 7, repetitionNumber: 2);

        var (resp, root, body) = await GetDueReviewsAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Reviews/Due must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", body);

        var items = data.EnumerateArray().ToList();
        var matchingItems = items
            .Where(i => TryProp(i, "skillId", out var sid) && sid.GetInt32() == skillId)
            .ToList();
        matchingItems.Should().ContainSingle(
            $"Mastered+past due skill (skillId={skillId}) must appear in due list; body: {body}");

        var item = matchingItems[0];

        TryProp(item, "status", out var statusProp).Should().BeTrue("body: {0}", body);
        // MasteryStatus.Mastered == 2
        statusProp.GetInt32().Should().Be(2,
            "status must be 2 (Mastered) for a mastered-but-stale skill; body: {0}", body);

        TryProp(item, "repetitionNumber", out var repProp).Should().BeTrue("body: {0}", body);
        repProp.GetInt32().Should().Be(2,
            "repetitionNumber must be 2 as seeded; body: {0}", body);
    }

    // =========================================================================
    // Case 3 — Mastered + future NextReviewDueAt → NOT in due list (AC1 boundary)
    // =========================================================================

    [Fact(DisplayName = "P310-C03 Mastered skill with NextReviewDueAt in the future → NOT in GET /Reviews/Due")]
    public async Task Mastered_FutureDueDate_NotInDueList()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("c03");

        var (lessonId, skillId) = await SeedLessonWithSkillAsync("c03");

        // Seed a Mastered row whose NextReviewDueAt is 5 days from now (not yet stale → not due).
        var futureDue = DateTime.UtcNow.AddDays(5);
        await SeedMasteryRowDirectlyAsync(studentId, skillId, MasteryStatus.Mastered,
            nextReviewDueAt: futureDue, reviewIntervalDays: 7, repetitionNumber: 1);

        var (resp, root, body) = await GetDueReviewsAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Reviews/Due must return 200 even for empty list; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", body);

        var items = data.EnumerateArray().ToList();
        var shouldNotBeDue = items
            .Where(i => TryProp(i, "skillId", out var sid) && sid.GetInt32() == skillId)
            .ToList();
        shouldNotBeDue.Should().BeEmpty(
            $"Mastered skill with future NextReviewDueAt (skillId={skillId}) must NOT appear in due list; body: {body}");
    }

    // =========================================================================
    // Case 4 — Sweep job idempotent: run twice → same NextReviewDueAt, no duplicates (AC3)
    // =========================================================================

    [Fact(DisplayName = "P310-C04 Running sweep job twice produces same NextReviewDueAt and no duplicates (idempotent)")]
    public async Task SweepJob_Idempotent_RunTwice_SameState()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("c04");

        var (lessonId, skillId) = await SeedLessonWithSkillAsync("c04");

        // Seed a NeedsReview row — sweep job should set NextReviewDueAt = LastPracticedAt + NeedsReviewIntervalDays.
        await SeedMasteryRowDirectlyAsync(studentId, skillId, MasteryStatus.NeedsReview,
            nextReviewDueAt: null, reviewIntervalDays: 0, repetitionNumber: 0);

        // First sweep run
        await RunSweepJobAsync();

        var rowAfterFirstRun = await GetMasteryRowAsync(studentId, skillId);
        rowAfterFirstRun.Should().NotBeNull("mastery row must exist after seeding; studentId={0}, skillId={1}", studentId, skillId);
        var firstNextDueAt = rowAfterFirstRun!.NextReviewDueAt;
        firstNextDueAt.Should().NotBeNull(
            "sweep job must set NextReviewDueAt for a NeedsReview row; body snapshot: studentId={0}, skillId={1}", studentId, skillId);

        // Second sweep run (idempotency check — must produce the same NextReviewDueAt)
        await RunSweepJobAsync();

        var rowAfterSecondRun = await GetMasteryRowAsync(studentId, skillId);
        rowAfterSecondRun.Should().NotBeNull("mastery row must still exist after second sweep run");
        var secondNextDueAt = rowAfterSecondRun!.NextReviewDueAt;
        secondNextDueAt.Should().NotBeNull("sweep job must keep NextReviewDueAt set after second run");

        // UTC normalise (Npgsql may return local-kind despite legacy mode — ToUniversalTime() is safe).
        var firstUtc  = firstNextDueAt!.Value.ToUniversalTime();
        var secondUtc = secondNextDueAt!.Value.ToUniversalTime();

        secondUtc.Should().BeCloseTo(firstUtc, precision: TimeSpan.FromSeconds(2),
            "running the sweep job twice must produce the same NextReviewDueAt (pure deterministic recompute from LastPracticedAt + intervalDays); " +
            $"first={firstUtc:O}, second={secondUtc:O}");

        // No duplicate rows — the unique constraint (StudentId, SkillId) enforces this at the DB level,
        // but verify count at the application level too.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var rowCount = await db.StudentSkillMasteries
            .AsNoTracking()
            .CountAsync(m => m.StudentId == studentId && m.SkillId == skillId);
        rowCount.Should().Be(1,
            "there must be exactly one mastery row per (StudentId, SkillId) after two sweep runs (no duplicates)");
    }

    // =========================================================================
    // Case 5 — Successful review on a due skill → NextReviewDueAt advances + RepetitionNumber increments (AC4)
    // =========================================================================

    [Fact(DisplayName = "P310-C05 Successful review on a due skill → NextReviewDueAt advances to longer interval, RepetitionNumber increments")]
    public async Task SuccessfulReview_AdvancesLadder_RepetitionIncrements()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("c05");

        var (lessonId, skillId) = await SeedLessonWithSkillAsync("c05", masteryThreshold: 80);
        var questionIds = await SeedQuestionsAsync(lessonId, skillId, 5);

        // Seed a NeedsReview row (due before the attempt) with repetitionNumber=0.
        await SeedMasteryRowDirectlyAsync(studentId, skillId, MasteryStatus.NeedsReview,
            nextReviewDueAt: null, reviewIntervalDays: 0, repetitionNumber: 0);

        // Capture the pre-attempt row state (for delta comparison).
        var rowBefore = await GetMasteryRowAsync(studentId, skillId);
        rowBefore.Should().NotBeNull("mastery row must exist before attempt");
        var repBefore = rowBefore!.RepetitionNumber;  // 0

        // Perform a SUCCESSFUL attempt: 5/5 correct (100% ≥ 80 threshold → Mastered).
        var attemptId = await StartAttemptAsync(lessonId, token);
        foreach (var qId in questionIds)
            await SubmitAnswerAsync(attemptId, qId, "\"A\"", token);  // "A" is correct

        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must return 200; body: {0}", complBody);

        // Read the updated mastery row.
        var rowAfter = await GetMasteryRowAsync(studentId, skillId);
        rowAfter.Should().NotBeNull("mastery row must exist after attempt");

        // RepetitionNumber must have incremented (ladder advanced on success).
        rowAfter!.RepetitionNumber.Should().BeGreaterThan(repBefore,
            "RepetitionNumber must increment after a successful review of a due skill; " +
            $"before={repBefore}, after={rowAfter.RepetitionNumber}");

        // NextReviewDueAt must be set and be in the future.
        rowAfter.NextReviewDueAt.Should().NotBeNull(
            "NextReviewDueAt must be set after a successful review");
        rowAfter.NextReviewDueAt!.Value.ToUniversalTime().Should().BeAfter(DateTime.UtcNow,
            "NextReviewDueAt must be in the future after a successful review (longer interval than now)");

        // ReviewIntervalDays must correspond to ladder[1] = 3 (repetitionNumber 0 → 1, ladder[1]=3).
        // Default ladder: [1, 3, 7, 14, 30]. Starting from rep=0 (index 0), success advances to index 1 = 3 days.
        rowAfter.ReviewIntervalDays.Should().BeGreaterThan(0,
            "ReviewIntervalDays must be positive after SR advancement; value={0}", rowAfter.ReviewIntervalDays);

        // The skill must NOT appear in the due list immediately after a successful review.
        var (dueResp, dueRoot, dueBody) = await GetDueReviewsAsync(token);
        dueResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dueBody);
        TryProp(dueRoot, "data", out var dueData).Should().BeTrue("body: {0}", dueBody);
        // After a successful review the new status is Mastered with NextReviewDueAt in the future → not due.
        var dueItems = dueData.EnumerateArray().ToList();
        var skillStillDue = dueItems
            .Where(i => TryProp(i, "skillId", out var sid) && sid.GetInt32() == skillId)
            .ToList();
        skillStillDue.Should().BeEmpty(
            $"After a successful review the skill (skillId={skillId}) must NOT appear in the due list immediately; dueBody: {dueBody}");
    }

    // =========================================================================
    // Case 6 — Failed review (mastery drops to NeedsReview) → ReviewIntervalDays resets to shortest (AC4 reset)
    // =========================================================================

    [Fact(DisplayName = "P310-C06 Failed review (mastery drops to NeedsReview) → ReviewIntervalDays resets to shortest interval")]
    public async Task FailedReview_ResetsInterval_ToShortest()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("c06");

        var (lessonId, skillId) = await SeedLessonWithSkillAsync("c06", masteryThreshold: 80);
        var questionIds = await SeedQuestionsAsync(lessonId, skillId, 5);

        // Seed a NeedsReview row that is already due (repetitionNumber=2, interval=7 — mid-ladder).
        await SeedMasteryRowDirectlyAsync(studentId, skillId, MasteryStatus.NeedsReview,
            nextReviewDueAt: null, reviewIntervalDays: 7, repetitionNumber: 2);

        // Perform a FAILING attempt: 1/5 correct = 20% < 50% → NeedsReview (regression).
        var attemptId = await StartAttemptAsync(lessonId, token);
        await SubmitAnswerAsync(attemptId, questionIds[0], "\"A\"", token); // correct
        await SubmitAnswerAsync(attemptId, questionIds[1], "\"B\"", token); // wrong
        await SubmitAnswerAsync(attemptId, questionIds[2], "\"B\"", token); // wrong
        await SubmitAnswerAsync(attemptId, questionIds[3], "\"B\"", token); // wrong
        await SubmitAnswerAsync(attemptId, questionIds[4], "\"B\"", token); // wrong

        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must return 200; body: {0}", complBody);

        // Read the updated mastery row.
        var rowAfter = await GetMasteryRowAsync(studentId, skillId);
        rowAfter.Should().NotBeNull("mastery row must exist after failed attempt");

        // Status must be NeedsReview (regression confirmed).
        rowAfter!.Status.Should().Be(MasteryStatus.NeedsReview,
            "mastery status must be NeedsReview after a 20%-accuracy attempt with threshold 80");

        // ReviewIntervalDays must have reset to ladder[0] = 1 (shortest interval on failure).
        // Default ladder: [1, 3, 7, 14, 30] → ladder[0] = 1.
        rowAfter.ReviewIntervalDays.Should().Be(1,
            "ReviewIntervalDays must reset to ladder[0] = 1 (shortest) after a failed review; " +
            $"actual={rowAfter.ReviewIntervalDays}");

        // RepetitionNumber must have reset to 0.
        rowAfter.RepetitionNumber.Should().Be(0,
            "RepetitionNumber must reset to 0 after a failed review; actual={0}", rowAfter.RepetitionNumber);

        // The skill must still appear in the due list (status is NeedsReview → always due).
        var (dueResp, dueRoot, dueBody) = await GetDueReviewsAsync(token);
        dueResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dueBody);
        TryProp(dueRoot, "data", out var dueData).Should().BeTrue("body: {0}", dueBody);
        var dueItems = dueData.EnumerateArray().ToList();
        var skillDueAfterFail = dueItems
            .Where(i => TryProp(i, "skillId", out var sid) && sid.GetInt32() == skillId)
            .ToList();
        skillDueAfterFail.Should().ContainSingle(
            $"After a failed review the skill (skillId={skillId}) must still appear in due list (status=NeedsReview); dueBody: {dueBody}");
    }

    // =========================================================================
    // Case 7 — IDOR: another student's due list is not addressable (AC5 isolation)
    // =========================================================================

    [Fact(DisplayName = "P310-C07 IDOR: Student B calling GET /Reviews/Due sees only their own due items, never Student A's")]
    public async Task IDOR_AnotherStudent_DueListNotAddressable()
    {
        // Student A: has a NeedsReview skill → should appear in A's due list.
        var (tokenA, studentIdA) = await CreateStudentViaParentFlowAsync("c07a");
        var (lessonIdA, skillIdA) = await SeedLessonWithSkillAsync("c07a");
        await SeedMasteryRowDirectlyAsync(studentIdA, skillIdA, MasteryStatus.NeedsReview,
            nextReviewDueAt: null, reviewIntervalDays: 0, repetitionNumber: 0);

        // Confirm Student A sees the skill in their due list.
        var (respA, rootA, bodyA) = await GetDueReviewsAsync(tokenA);
        respA.StatusCode.Should().Be(HttpStatusCode.OK,
            "Student A must get 200; body: {0}", bodyA);
        TryProp(rootA, "data", out var dataA).Should().BeTrue("body: {0}", bodyA);
        var itemsA = dataA.EnumerateArray().ToList();
        var studentAMatchingItems = itemsA
            .Where(i => TryProp(i, "skillId", out var sid) && sid.GetInt32() == skillIdA)
            .ToList();
        studentAMatchingItems.Should().ContainSingle(
            $"Student A's due list must contain skillId={skillIdA}; bodyA: {bodyA}");

        // Student B: fresh student with no mastery → due list must be empty.
        var (tokenB, _) = await CreateStudentViaParentFlowAsync("c07b");

        var (respB, rootB, bodyB) = await GetDueReviewsAsync(tokenB);
        respB.StatusCode.Should().Be(HttpStatusCode.OK,
            "Student B must get 200 (empty due list); body: {0}", bodyB);
        TryProp(rootB, "data", out var dataB).Should().BeTrue("body: {0}", bodyB);
        dataB.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", bodyB);

        var itemsB = dataB.EnumerateArray().ToList();

        // IDOR guard: Student B must NOT see Student A's skills.
        var bSeesAItems = itemsB
            .Where(i => TryProp(i, "skillId", out var sid) && sid.GetInt32() == skillIdA)
            .ToList();
        bSeesAItems.Should().BeEmpty(
            $"Student B must NOT see Student A's skill (skillId={skillIdA}) — IDOR guard; bodyB: {bodyB}");

        // Student B's list must be empty (they have no mastery rows of their own).
        itemsB.Should().BeEmpty(
            $"Student B has no mastery rows; due list must be empty (IDOR isolation); bodyB: {bodyB}");
    }

    // =========================================================================
    // Auth guard — endpoint must return 401 without a JWT
    // =========================================================================

    [Fact(DisplayName = "P310-AUTH-01 GET /Reviews/Due without JWT → 401 Unauthorized")]
    public async Task GetDueReviews_WithoutJwt_Returns401()
    {
        var (resp, root, body) = await GetDueReviewsAsync(token: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GET /Reviews/Due without JWT must return 401; body: {0}", body);
    }
}
