using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P2-04 integration tests — Learning Path Engine (unlock rules, per-student NodeState).
///
/// Endpoints under test:
///   GET /api/learning/Subjects/{id}/SkillTree   [Authorize]   (P2-04: was anonymous in P2-02)
///   GET /api/learning/Subjects/{id}/Lessons     [Authorize]   (P2-04: was anonymous in P2-02)
///   GET /api/learning/Subjects/ForGrade?grade=  [Authorize] since P8 (security fix)
///
/// Seeding: Uses LearningSeeder.SeedAsync which creates the Math G1 prereq chain:
///   Count to 1000 (G1)  →  Compare and Order Numbers (G1)  →  Add Single-Digit Numbers (G1)
///   ...and beyond to G6 (7 edges total).
///
/// Student auth: parent-driven onboarding flow (Register Parent → Add-Child → Sign-In as child).
///   Each test creates its own unique student to ensure zero cross-test contamination.
///
/// Coverage map (12 test cases):
///   TC-01  Anonymous SkillTree → 401
///   TC-02  Anonymous Lessons → 401
///   TC-03  Fresh student SkillTree — root Available, downstream Locked
///   TC-04  Fresh student Lessons — root Available, downstream Locked
///   TC-05  Master root skill → next skill in chain Available
///   TC-06  MissingPrerequisites shape — 5 fields all non-null/non-default
///   TC-07  Completed lesson → state == Completed
///   TC-08  Cross-student isolation (Student A's mastery does NOT unlock for Student B)
///   TC-09  Authenticated ForGrade returns 200 (P8: requires JWT now)
///   TC-10  Unknown subject id with auth → 404
///   TC-11  Lesson with null SkillId → Available
///   TC-12  Envelope "successed": key is camelCase + boolean true
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_04_LearningPath_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // Seeded Math G1 subject id — resolved once in InitializeAsync.
    private int _mathG1SubjectId;

    public P2_04_LearningPath_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        // Run the Learning seeder directly (it does not run in the Testing environment).
        // Idempotent — safe to call multiple times across test-class lifecycle.
        using var scope = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope.ServiceProvider);

        // Resolve the Math G1 subject id for tests that need a real subject id.
        // P8-02: query by SubjectCode + Language — names are now grade-suffixed and language-specific.
        // Students use learning_language="en" so the MATH/En tree is the relevant one.
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var subject = await db.Subjects
            .Include(s => s.Grade)
            .FirstAsync(s =>
                s.SubjectCode == SubjectCode.MATH &&
                s.Language == ContentLanguage.En &&
                s.Grade.Number == 1);
        _mathG1SubjectId = subject.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p204_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>Case-insensitive property lookup — handles both camelCase and PascalCase JSON.</summary>
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
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
    }

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetAsync(string url, string? bearerToken = null)
        => await SendAsync(_client, HttpMethod.Get, url, null, bearerToken);

    /// <summary>
    /// Creates a parent, adds a child, signs in as the child. Returns (StudentToken, StudentId).
    /// Uses a unique email each call to prevent collisions across test methods.
    /// Mirrors the P2-08 pattern exactly.
    /// </summary>
    private async Task<(string StudentToken, int StudentId)> CreateStudentViaParentFlowAsync(string? suffix = null)
    {
        var tag = suffix ?? Guid.NewGuid().ToString("N")[..6];

        // 1. Register parent
        var parentEmail = UniqueEmail($"parent_{tag}");
        var (regResp, regRoot, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"parent registration must succeed; body: {regBody}");
        TryProp(regRoot, "data", out var regData).Should().BeTrue("body: {0}", regBody);
        TryProp(regData, "accessToken", out var parentTokProp).Should().BeTrue("body: {0}", regBody);
        var parentToken = parentTokProp.GetString()!;

        // 2. Add child
        var childEmail = UniqueEmail($"child_{tag}");
        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName = "Test Student P204",
                Email    = childEmail,
                Password = ValidChildPassword,
                Grade    = 1,
                Language = "ar",
                Country  = "EG",
                LearningLanguage = "en", // P8-01: "en" so handler resolves MATH/En tree
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"Add-Child must succeed; body: {addBody}");
        TryProp(addRoot, "data", out var addData).Should().BeTrue("body: {0}", addBody);
        TryProp(addData, "id", out var idProp).Should().BeTrue("body: {0}", addBody);
        var childId = idProp.GetInt32();

        // 3. Sign in as child
        var (signInResp, signInRoot, signInBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        signInResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"child sign-in must succeed; body: {signInBody}");
        TryProp(signInRoot, "data", out var signInData).Should().BeTrue("body: {0}", signInBody);
        TryProp(signInData, "accessToken", out var studentTokProp).Should().BeTrue("body: {0}", signInBody);

        return (studentTokProp.GetString()!, childId);
    }

    /// <summary>
    /// Seeds a QuizQuestion for the given lesson+skill, starts an attempt, then seeds StudentAnswer rows
    /// directly via DbContext so that skill accuracy >= MasteryThreshold is achieved.
    /// Returns the attemptId.
    ///
    /// DB-direct seeding is the approved approach for setup (P2-08 pattern); faster than round-tripping
    /// the full SubmitAnswer/CompleteAttempt API flow.
    /// </summary>
    private async Task<int> SeedMasteredSkillAnswersAsync(int studentId, int lessonId, int skillId, int masteryThreshold)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var now = DateTime.UtcNow;

        // Create a TrueFalse question linked to the skill in the lesson.
        var question = new QuizQuestion
        {
            LessonId      = lessonId,
            SkillId       = skillId,
            QuestionType  = QuestionType.TrueFalse,
            QuestionText  = $"Mastery seed question for skill {skillId}",
            Options       = "[\"True\",\"False\"]",
            CorrectAnswer = "\"True\"",
            Difficulty    = DifficultyLevel.Easy,
            GeneratedBy   = GeneratedBy.Curated,
            CreatedAt     = now,
            CreatedBy     = 0,
        };
        db.QuizQuestions.Add(question);
        await db.SaveChangesAsync(0);

        // Create an InProgress attempt for the student.
        var attempt = new Attempt
        {
            StudentId  = studentId,
            LessonId   = lessonId,
            Status     = AttemptStatus.InProgress,
            StartedAt  = now,
            CreatedAt  = now,
            CreatedBy  = 0,
        };
        db.Attempts.Add(attempt);
        await db.SaveChangesAsync(0);

        // Seed enough correct answers so accuracy = 100%, which always meets any mastery threshold.
        // TotalAnswers >= 1 is the Q2 requirement.
        var answer = new StudentAnswer
        {
            AttemptId        = attempt.Id,
            QuestionId       = question.Id,
            AnswerPayload    = "\"True\"",
            IsCorrect        = true,
            TimeSpentSeconds = 5,
            HintUsed         = false,
            CreatedAt        = now,
            CreatedBy        = 0,
        };
        db.StudentAnswers.Add(answer);
        await db.SaveChangesAsync(0);

        return attempt.Id;
    }

    /// <summary>
    /// Seeds a StudentAnswer with partial accuracy (some correct, some wrong) for a given skill.
    /// Returns the attemptId created.
    /// </summary>
    private async Task<int> SeedPartialSkillAnswersAsync(
        int studentId, int lessonId, int skillId,
        int correctCount, int wrongCount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var now = DateTime.UtcNow;

        // Create questions: (correctCount + wrongCount) questions, all linked to the skill.
        var questionIds = new List<int>();
        for (int i = 0; i < correctCount + wrongCount; i++)
        {
            var q = new QuizQuestion
            {
                LessonId      = lessonId,
                SkillId       = skillId,
                QuestionType  = QuestionType.TrueFalse,
                QuestionText  = $"Partial seed q{i} for skill {skillId}",
                Options       = "[\"True\",\"False\"]",
                CorrectAnswer = "\"True\"",
                Difficulty    = DifficultyLevel.Easy,
                GeneratedBy   = GeneratedBy.Curated,
                CreatedAt     = now,
                CreatedBy     = 0,
            };
            db.QuizQuestions.Add(q);
            await db.SaveChangesAsync(0);
            questionIds.Add(q.Id);
        }

        var attempt = new Attempt
        {
            StudentId  = studentId,
            LessonId   = lessonId,
            Status     = AttemptStatus.InProgress,
            StartedAt  = now,
            CreatedAt  = now,
            CreatedBy  = 0,
        };
        db.Attempts.Add(attempt);
        await db.SaveChangesAsync(0);

        // Submit correct answers for the first `correctCount` questions.
        for (int i = 0; i < correctCount; i++)
        {
            db.StudentAnswers.Add(new StudentAnswer
            {
                AttemptId        = attempt.Id,
                QuestionId       = questionIds[i],
                AnswerPayload    = "\"True\"",
                IsCorrect        = true,
                TimeSpentSeconds = 5,
                HintUsed         = false,
                CreatedAt        = now,
                CreatedBy        = 0,
            });
        }

        // Submit wrong answers for the remaining questions.
        for (int i = correctCount; i < correctCount + wrongCount; i++)
        {
            db.StudentAnswers.Add(new StudentAnswer
            {
                AttemptId        = attempt.Id,
                QuestionId       = questionIds[i],
                AnswerPayload    = "\"False\"",
                IsCorrect        = false,
                TimeSpentSeconds = 5,
                HintUsed         = false,
                CreatedAt        = now,
                CreatedBy        = 0,
            });
        }

        await db.SaveChangesAsync(0);
        return attempt.Id;
    }

    /// <summary>
    /// Seeds a Completed Attempt for the given student+lesson directly in the DB.
    /// Returns the attemptId.
    /// </summary>
    private async Task<int> SeedCompletedAttemptAsync(int studentId, int lessonId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var now = DateTime.UtcNow;
        var attempt = new Attempt
        {
            StudentId           = studentId,
            LessonId            = lessonId,
            Status              = AttemptStatus.Completed,
            AccuracyPercentage  = 0.0,  // deliberately low accuracy — Q5: Completed != Mastered
            DurationSeconds     = 60,
            StartedAt           = now,
            CompletedAt         = now,
            CreatedAt           = now,
            CreatedBy           = 0,
        };
        db.Attempts.Add(attempt);
        await db.SaveChangesAsync(0);
        return attempt.Id;
    }

    /// <summary>
    /// Look up a skill's ID by its exact name (case-sensitive), scoped to Math G1 English tree.
    /// P8-02: uses SubjectCode + Language instead of Subject.Name (names are now grade-suffixed).
    /// </summary>
    private async Task<int> GetSkillIdByNameAsync(string skillName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var skill = await db.Skills
            .AsNoTracking()
            .Include(s => s.Concept)
                .ThenInclude(c => c.Subject)
                    .ThenInclude(sub => sub.Grade)
            .FirstOrDefaultAsync(s =>
                s.Name == skillName &&
                s.Concept.Subject.SubjectCode == SubjectCode.MATH &&
                s.Concept.Subject.Language == ContentLanguage.En &&
                s.Concept.Subject.Grade.Number == 1);

        skill.Should().NotBeNull(
            $"seeder must have created skill '{skillName}' for Math/En G1; " +
            $"check that LearningSeeder.SeedAsync ran.");

        return skill!.Id;
    }

    /// <summary>
    /// Finds the first lesson whose SkillId == skillId, within Math G1 English tree.
    /// P8-02: uses SubjectCode + Language instead of Subject.Name (names are now grade-suffixed).
    /// </summary>
    private async Task<int> GetLessonIdForSkillAsync(int skillId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var lesson = await db.Lessons
            .AsNoTracking()
            .Include(l => l.Unit)
                .ThenInclude(u => u.Subject)
                    .ThenInclude(s => s.Grade)
            .FirstOrDefaultAsync(l =>
                l.SkillId == skillId &&
                l.Unit.Subject.SubjectCode == SubjectCode.MATH &&
                l.Unit.Subject.Language == ContentLanguage.En &&
                l.Unit.Subject.Grade.Number == 1);

        lesson.Should().NotBeNull(
            $"there must be at least one Math/En G1 lesson with SkillId={skillId}");

        return lesson!.Id;
    }

    /// <summary>
    /// Finds any lesson whose SkillId IS NULL in Math G1 English tree.
    /// P8-02: uses SubjectCode + Language instead of Subject.Name (names are now grade-suffixed).
    /// </summary>
    private async Task<int> GetNullSkillLessonIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // The English-tree seeder creates 3 null-SkillId lessons per grade in Math/En:
        //   "Word Problems: Add and Subtract (G1)"
        //   "Division as Equal Groups (G1)"
        //   "Word Problems: Multiply and Divide (G1)"
        var lesson = await db.Lessons
            .AsNoTracking()
            .Include(l => l.Unit)
                .ThenInclude(u => u.Subject)
                    .ThenInclude(s => s.Grade)
            .FirstOrDefaultAsync(l =>
                l.SkillId == null &&
                l.Unit.Subject.SubjectCode == SubjectCode.MATH &&
                l.Unit.Subject.Language == ContentLanguage.En &&
                l.Unit.Subject.Grade.Number == 1);

        lesson.Should().NotBeNull("Math/En G1 seeder must create lessons with null SkillId (fill-in content)");
        return lesson!.Id;
    }

    // =========================================================================
    // TC-01: Anonymous SkillTree → 401
    // =========================================================================

    [Fact(DisplayName = "P204-TC01 Anonymous GET /{id}/SkillTree returns 401 (no JWT)")]
    public async Task SkillTree_Anonymous_Returns401()
    {
        // No bearer token — must receive 401.
        var (response, _, body) = await GetAsync($"/api/learning/Subjects/{_mathG1SubjectId}/SkillTree");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "SubjectsController.GetSkillTree now has [Authorize] (P2-04 tightening); " +
            "anonymous caller must get 401; body: {0}", body);
    }

    // =========================================================================
    // TC-02: Anonymous Lessons → 401
    // =========================================================================

    [Fact(DisplayName = "P204-TC02 Anonymous GET /{id}/Lessons returns 401 (no JWT)")]
    public async Task Lessons_Anonymous_Returns401()
    {
        var (response, _, body) = await GetAsync($"/api/learning/Subjects/{_mathG1SubjectId}/Lessons");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "SubjectsController.GetLessons now has [Authorize] (P2-04 tightening); " +
            "anonymous caller must get 401; body: {0}", body);
    }

    // =========================================================================
    // TC-03: Fresh student SkillTree — root Available, downstream Locked
    // =========================================================================

    [Fact(DisplayName = "P204-TC03 Fresh student SkillTree — root skill Available, downstream skills Locked")]
    public async Task SkillTree_FreshStudent_RootAvailableDownstreamLocked()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("tc03");

        var (response, root, body) = await GetAsync(
            $"/api/learning/Subjects/{_mathG1SubjectId}/SkillTree", token);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "authenticated student must get 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", body);

        // Find the "Count to 1000 (G1)" skill — this is the root node (no incoming prereq edges).
        // It MUST be Available for a fresh student (Q4: root = Available).
        SkillNodeDto? rootSkill = null;
        SkillNodeDto? downstreamSkill = null; // "Compare and Order Numbers (G1)" — has prereq

        foreach (var concept in data.EnumerateArray())
        {
            TryProp(concept, "skills", out var skills);
            foreach (var skill in skills.EnumerateArray())
            {
                TryProp(skill, "name", out var nameProp);
                var name = nameProp.GetString() ?? "";

                TryProp(skill, "state", out var stateProp);
                var stateVal = stateProp.ValueKind == JsonValueKind.Number
                    ? stateProp.GetInt32()
                    : -1;

                if (name == "Count to 1000 (G1)")
                {
                    rootSkill = new SkillNodeDto(name, stateVal);
                }
                else if (name == "Compare and Order Numbers (G1)")
                {
                    downstreamSkill = new SkillNodeDto(name, stateVal);
                }
            }
        }

        rootSkill.Should().NotBeNull(
            "'Count to 1000 (G1)' skill must be present in Math G1 SkillTree; body: {0}", body);
        rootSkill!.StateValue.Should().Be((int)NodeState.Available,
            "'Count to 1000 (G1)' is a root node (no prereqs) — must be Available=1 for a fresh student; body: {0}", body);

        downstreamSkill.Should().NotBeNull(
            "'Compare and Order Numbers (G1)' skill must be present; body: {0}", body);
        downstreamSkill!.StateValue.Should().Be((int)NodeState.Locked,
            "'Compare and Order Numbers (G1)' requires 'Count to 1000 (G1)' to be mastered first — must be Locked=0; body: {0}", body);
    }

    // =========================================================================
    // TC-04: Fresh student Lessons — root Available, downstream Locked
    // =========================================================================

    [Fact(DisplayName = "P204-TC04 Fresh student Lessons — root lesson Available, downstream Locked with non-empty missingPrerequisites")]
    public async Task Lessons_FreshStudent_RootAvailableDownstreamLocked()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("tc04");

        var (response, root, body) = await GetAsync(
            $"/api/learning/Subjects/{_mathG1SubjectId}/Lessons", token);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "authenticated student must get 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", body);

        // "Introduction to Counting (G1)" is linked to "Count to 1000 (G1)" — root skill → Available.
        // "Place Value: Tens and Hundreds (G1)" is linked to "Compare and Order Numbers (G1)" — downstream → Locked.
        LessonInfo? rootLesson     = null;
        LessonInfo? downstreamLesson = null;

        foreach (var unit in data.EnumerateArray())
        {
            TryProp(unit, "lessons", out var lessons);
            foreach (var lesson in lessons.EnumerateArray())
            {
                TryProp(lesson, "name", out var nameProp);
                var name = nameProp.GetString() ?? "";
                TryProp(lesson, "state", out var stateProp);
                var stateVal = stateProp.ValueKind == JsonValueKind.Number ? stateProp.GetInt32() : -1;
                TryProp(lesson, "missingPrerequisites", out var mpProp);

                if (name == "Introduction to Counting (G1)")
                    rootLesson = new LessonInfo(name, stateVal, mpProp);
                else if (name == "Place Value: Tens and Hundreds (G1)")
                    downstreamLesson = new LessonInfo(name, stateVal, mpProp);
            }
        }

        rootLesson.Should().NotBeNull(
            "'Introduction to Counting (G1)' must be present; body: {0}", body);
        rootLesson!.StateValue.Should().Be((int)NodeState.Available,
            "'Introduction to Counting (G1)' is linked to the root skill → must be Available=1; body: {0}", body);

        downstreamLesson.Should().NotBeNull(
            "'Place Value: Tens and Hundreds (G1)' must be present; body: {0}", body);
        downstreamLesson!.StateValue.Should().Be((int)NodeState.Locked,
            "'Place Value: Tens and Hundreds (G1)' has a prereq — must be Locked=0 for fresh student; body: {0}", body);

        // Locked lesson must have non-empty missingPrerequisites.
        downstreamLesson.MissingPrerequisites.ValueKind.Should().Be(JsonValueKind.Array,
            "missingPrerequisites must be an array; body: {0}", body);
        downstreamLesson.MissingPrerequisites.GetArrayLength().Should().BeGreaterThan(0,
            "a Locked lesson must list its missing prereqs; body: {0}", body);
    }

    // =========================================================================
    // TC-05: Master root skill → next skill in chain Available
    // =========================================================================

    [Fact(DisplayName = "P204-TC05 Master root skill ('Count to 1000 G1') → 'Compare and Order Numbers G1' flips to Available")]
    public async Task SkillTree_MasterRootSkill_NextSkillBecomesAvailable()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("tc05");

        // Verify the downstream skill is Locked BEFORE mastery.
        var (resp1, root1, body1) = await GetAsync(
            $"/api/learning/Subjects/{_mathG1SubjectId}/SkillTree", token);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "pre-mastery call must succeed; body: {0}", body1);

        var compareSkillStateBefore = GetSkillStateFromSkillTreeResponse(root1, "Compare and Order Numbers (G1)");
        compareSkillStateBefore.Should().Be((int)NodeState.Locked,
            "'Compare and Order Numbers (G1)' must be Locked before root is mastered; body: {0}", body1);

        // Seed mastery for "Count to 1000 (G1)".
        var rootSkillId  = await GetSkillIdByNameAsync("Count to 1000 (G1)");
        var rootLessonId = await GetLessonIdForSkillAsync(rootSkillId);
        await SeedMasteredSkillAnswersAsync(studentId, rootLessonId, rootSkillId, 70);

        // Re-call the endpoint — downstream skill must now be Available.
        var (resp2, root2, body2) = await GetAsync(
            $"/api/learning/Subjects/{_mathG1SubjectId}/SkillTree", token);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "post-mastery call must succeed; body: {0}", body2);

        var compareSkillStateAfter = GetSkillStateFromSkillTreeResponse(root2, "Compare and Order Numbers (G1)");
        compareSkillStateAfter.Should().Be((int)NodeState.Available,
            "'Compare and Order Numbers (G1)' must be Available AFTER 'Count to 1000 (G1)' is mastered (AC2); body: {0}", body2);
    }

    // =========================================================================
    // TC-06: MissingPrerequisiteDto shape — all 5 fields populated
    // =========================================================================

    [Fact(DisplayName = "P204-TC06 MissingPrerequisites shape: all 5 fields (prereqSkillId, prereqSkillName, prereqNodeId, requiredAccuracy, currentAccuracy) present and non-null/non-default")]
    public async Task Lessons_LockedLesson_MissingPrerequisitesDtoShapeComplete()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("tc06");

        // Seed partial mastery on root skill (so currentAccuracy > 0 but < threshold — root stays not mastered).
        // 3 correct / 5 total = 60%, MasteryThreshold for "Count to 1000 (G1)" = 70 → not mastered.
        var rootSkillId  = await GetSkillIdByNameAsync("Count to 1000 (G1)");
        var rootLessonId = await GetLessonIdForSkillAsync(rootSkillId);
        await SeedPartialSkillAnswersAsync(studentId, rootLessonId, rootSkillId, correctCount: 3, wrongCount: 2);

        // Get Lessons — "Place Value: Tens and Hundreds (G1)" should be Locked.
        var (response, root, body) = await GetAsync(
            $"/api/learning/Subjects/{_mathG1SubjectId}/Lessons", token);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // Find the locked "Place Value: Tens and Hundreds (G1)" lesson and inspect its missingPrerequisites.
        JsonElement? missingPrereqsForLockedLesson = null;
        foreach (var unit in data.EnumerateArray())
        {
            TryProp(unit, "lessons", out var lessons);
            foreach (var lesson in lessons.EnumerateArray())
            {
                TryProp(lesson, "name", out var nameProp);
                if (nameProp.GetString() == "Place Value: Tens and Hundreds (G1)")
                {
                    TryProp(lesson, "missingPrerequisites", out var mp);
                    missingPrereqsForLockedLesson = mp;
                    break;
                }
            }
        }

        missingPrereqsForLockedLesson.Should().NotBeNull(
            "'Place Value: Tens and Hundreds (G1)' must be present in the response; body: {0}", body);

        var mpArray = missingPrereqsForLockedLesson!.Value;
        mpArray.ValueKind.Should().Be(JsonValueKind.Array, "missingPrerequisites must be an array; body: {0}", body);
        mpArray.GetArrayLength().Should().BeGreaterThan(0,
            "locked lesson must have at least one missing prerequisite; body: {0}", body);

        // Inspect each item for the 5 required fields.
        foreach (var item in mpArray.EnumerateArray())
        {
            // 1. prereqSkillId — must be a positive int.
            TryProp(item, "prereqSkillId", out var prereqSkillId).Should().BeTrue(
                "MissingPrerequisiteDto must have 'prereqSkillId'; body: {0}", body);
            prereqSkillId.ValueKind.Should().Be(JsonValueKind.Number, "prereqSkillId must be a number; body: {0}", body);
            prereqSkillId.GetInt32().Should().BeGreaterThan(0, "prereqSkillId must be positive; body: {0}", body);

            // 2. prereqSkillName — must be a non-empty string.
            TryProp(item, "prereqSkillName", out var prereqSkillName).Should().BeTrue(
                "MissingPrerequisiteDto must have 'prereqSkillName'; body: {0}", body);
            prereqSkillName.GetString().Should().NotBeNullOrWhiteSpace(
                "prereqSkillName must be non-empty; body: {0}", body);

            // 3. prereqNodeId — must be a positive int.
            TryProp(item, "prereqNodeId", out var prereqNodeId).Should().BeTrue(
                "MissingPrerequisiteDto must have 'prereqNodeId'; body: {0}", body);
            prereqNodeId.ValueKind.Should().Be(JsonValueKind.Number, "prereqNodeId must be a number; body: {0}", body);
            prereqNodeId.GetInt32().Should().BeGreaterThan(0, "prereqNodeId must be positive; body: {0}", body);

            // 4. requiredAccuracy — must be an int in 0..100.
            TryProp(item, "requiredAccuracy", out var requiredAccuracy).Should().BeTrue(
                "MissingPrerequisiteDto must have 'requiredAccuracy'; body: {0}", body);
            requiredAccuracy.ValueKind.Should().Be(JsonValueKind.Number, "requiredAccuracy must be a number; body: {0}", body);
            requiredAccuracy.GetInt32().Should().BeInRange(0, 100,
                "requiredAccuracy must be 0..100 (MasteryThreshold percentage); body: {0}", body);

            // 5. currentAccuracy — must be a number in 0..100; since we seeded 3/5 = 60% it should be ~60.
            TryProp(item, "currentAccuracy", out var currentAccuracy).Should().BeTrue(
                "MissingPrerequisiteDto must have 'currentAccuracy'; body: {0}", body);
            currentAccuracy.ValueKind.Should().Be(JsonValueKind.Number, "currentAccuracy must be a number; body: {0}", body);
            currentAccuracy.GetDouble().Should().BeInRange(0.0, 100.0,
                "currentAccuracy must be 0..100; body: {0}", body);
            // We seeded 3 correct / 5 total = 60%.
            currentAccuracy.GetDouble().Should().BeApproximately(60.0, 1.0,
                "currentAccuracy must reflect the student's actual accuracy (3/5 = 60%); body: {0}", body);
        }
    }

    // =========================================================================
    // TC-07: Completed lesson → state == Completed
    // =========================================================================

    [Fact(DisplayName = "P204-TC07 Completed Attempt for a lesson → lesson state == Completed (2) regardless of mastery")]
    public async Task Lessons_CompletedAttempt_LessonStateIsCompleted()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("tc07");

        // Pick the root lesson (Introduction to Counting G1) — linked to root skill (no prereqs → Available).
        var rootSkillId  = await GetSkillIdByNameAsync("Count to 1000 (G1)");
        var rootLessonId = await GetLessonIdForSkillAsync(rootSkillId);

        // Seed a Completed attempt with 0% accuracy (Q5: Completed != Mastered — accuracy irrelevant).
        await SeedCompletedAttemptAsync(studentId, rootLessonId);

        var (response, root, body) = await GetAsync(
            $"/api/learning/Subjects/{_mathG1SubjectId}/Lessons", token);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // Find the lesson by ID and assert its state == Completed (2).
        int? lessonState = null;
        foreach (var unit in data.EnumerateArray())
        {
            TryProp(unit, "lessons", out var lessons);
            foreach (var lesson in lessons.EnumerateArray())
            {
                TryProp(lesson, "lessonId", out var lessonIdProp);
                if (lessonIdProp.GetInt32() == rootLessonId)
                {
                    TryProp(lesson, "state", out var stateProp);
                    lessonState = stateProp.ValueKind == JsonValueKind.Number ? stateProp.GetInt32() : -1;
                    break;
                }
            }
        }

        lessonState.Should().NotBeNull(
            $"lesson with id={rootLessonId} must appear in the Lessons response; body: {body}");
        lessonState!.Value.Should().Be((int)NodeState.Completed,
            "lesson with a Completed Attempt must have state == Completed (2) (Q5 decision); body: {0}", body);
    }

    // =========================================================================
    // TC-08: Cross-student isolation
    // =========================================================================

    [Fact(DisplayName = "P204-TC08 Cross-student isolation — Student A mastery does NOT unlock for Student B")]
    public async Task SkillTree_CrossStudentIsolation_StudentBRemainsLocked()
    {
        var (tokenA, studentIdA) = await CreateStudentViaParentFlowAsync("tc08a");
        var (tokenB, _)          = await CreateStudentViaParentFlowAsync("tc08b");

        // Student A masters the root skill.
        var rootSkillId  = await GetSkillIdByNameAsync("Count to 1000 (G1)");
        var rootLessonId = await GetLessonIdForSkillAsync(rootSkillId);
        await SeedMasteredSkillAnswersAsync(studentIdA, rootLessonId, rootSkillId, 70);

        // Student A's downstream skill must be Available.
        var (respA, rootA, bodyA) = await GetAsync(
            $"/api/learning/Subjects/{_mathG1SubjectId}/SkillTree", tokenA);
        respA.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyA);
        var compareStateA = GetSkillStateFromSkillTreeResponse(rootA, "Compare and Order Numbers (G1)");
        compareStateA.Should().Be((int)NodeState.Available,
            "Student A has mastered root skill — downstream must be Available; body: {0}", bodyA);

        // Student B (no answers) must still see the downstream skill as Locked.
        var (respB, rootB, bodyB) = await GetAsync(
            $"/api/learning/Subjects/{_mathG1SubjectId}/SkillTree", tokenB);
        respB.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyB);
        var compareStateB = GetSkillStateFromSkillTreeResponse(rootB, "Compare and Order Numbers (G1)");
        compareStateB.Should().Be((int)NodeState.Locked,
            "Student B has no mastery — downstream must remain Locked; body: {0}", bodyB);
    }

    // =========================================================================
    // TC-09: ForGrade with JWT returns 200 + 4 subjects
    //        P8 BREAKING CHANGE: GetForGrade now requires [Authorize] — anonymous callers get 401.
    //        Test updated to authenticate and verify the happy path still works.
    // =========================================================================

    [Fact(DisplayName = "P204-TC09 Authenticated GET /Subjects/ForGrade?grade=1 returns 200 + 4 subjects (P8: now requires JWT)")]
    public async Task ForGrade_Authenticated_Returns200With4Subjects()
    {
        // P8: GetForGrade now has [Authorize] — an anonymous call returns 401.
        // Create a fresh student and use their JWT for the ForGrade call.
        var (token, _) = await CreateStudentViaParentFlowAsync("tc09");

        var (response, root, body) = await GetAsync("/api/learning/Subjects/ForGrade?grade=1", token);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "authenticated GET /ForGrade?grade=1 must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", body);
        data.GetArrayLength().Should().Be(4,
            "handler resolves exactly 4 subjects (one per SubjectCode) for the student's language; body: {0}", body);
    }

    // =========================================================================
    // TC-10: Unknown subject id with auth → 404
    // =========================================================================

    [Fact(DisplayName = "P204-TC10 GET /Subjects/999999/SkillTree with valid JWT returns 404 (not 500)")]
    public async Task SkillTree_UnknownSubjectId_WithAuth_Returns404()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("tc10");

        var (response, root, body) = await GetAsync("/api/learning/Subjects/999999/SkillTree", token);

        ((int)response.StatusCode).Should().NotBe(500,
            "unknown subject id must never produce 500; body: {0}", body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "handler returns NotFound for unknown subject; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("successed must be false for 404; body: {0}", body);
    }

    // =========================================================================
    // TC-11: Lesson with null SkillId → Available
    // =========================================================================

    [Fact(DisplayName = "P204-TC11 Lesson with null SkillId (fill-in content) always returns Available regardless of mastery")]
    public async Task Lessons_NullSkillIdLesson_AlwaysAvailable()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("tc11");

        // No mastery data for this student — fresh student.
        var (response, root, body) = await GetAsync(
            $"/api/learning/Subjects/{_mathG1SubjectId}/Lessons", token);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // Math G1 has 3 null-SkillId lessons:
        //   "Word Problems: Add and Subtract (G1)"
        //   "Division as Equal Groups (G1)"
        //   "Word Problems: Multiply and Divide (G1)"
        // All must be Available (Q3 rule).
        var nullSkillLessonNames = new[]
        {
            "Word Problems: Add and Subtract (G1)",
            "Division as Equal Groups (G1)",
            "Word Problems: Multiply and Divide (G1)",
        };

        var foundCount = 0;
        foreach (var unit in data.EnumerateArray())
        {
            TryProp(unit, "lessons", out var lessons);
            foreach (var lesson in lessons.EnumerateArray())
            {
                TryProp(lesson, "name", out var nameProp);
                var name = nameProp.GetString() ?? "";

                TryProp(lesson, "skillId", out var skillIdProp);
                var skillIdIsNull = skillIdProp.ValueKind == JsonValueKind.Null
                                    || skillIdProp.ValueKind == JsonValueKind.Undefined;

                if (nullSkillLessonNames.Contains(name))
                {
                    // The lesson is a known null-SkillId lesson — assert Available.
                    skillIdIsNull.Should().BeTrue(
                        $"'{name}' must have null skillId in the seeder; body: {body}");

                    TryProp(lesson, "state", out var stateProp);
                    var stateVal = stateProp.ValueKind == JsonValueKind.Number ? stateProp.GetInt32() : -1;
                    stateVal.Should().Be((int)NodeState.Available,
                        $"'{name}' has null SkillId and must always be Available (Q3); body: {body}");

                    foundCount++;
                }
            }
        }

        foundCount.Should().Be(3,
            "exactly 3 null-SkillId lessons must be found in Math G1 (per seeder); body: {0}", body);
    }

    // =========================================================================
    // TC-12: Envelope shape — "successed": camelCase, value true
    // =========================================================================

    [Fact(DisplayName = "P204-TC12 SkillTree success response contains 'successed': (camelCase) with value true")]
    public async Task SkillTree_SuccessResponse_EnvelopeHasSuccessedCamelCase()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("tc12");

        var (response, root, body) = await GetAsync(
            $"/api/learning/Subjects/{_mathG1SubjectId}/SkillTree", token);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // Critical: the literal string "successed": must appear (camelCase of Successed — CONVENTIONS §5 load-bearing).
        body.Should().Contain("\"successed\":",
            "BaseResponse<T> uses the misspelled-but-canonical key 'successed' (not 'succeeded'); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true for 200; body: {0}", body);

        // Full envelope shape.
        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have 'statusCode'; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have 'message'; body: {0}", body);
        TryProp(root, "data", out _).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue("envelope must have 'errors'; body: {0}", body);
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Scans the SkillTree response's data array for a skill by exact name and returns its state int.
    /// Returns -1 if the skill is not found.
    /// </summary>
    private static int GetSkillStateFromSkillTreeResponse(JsonElement root, string skillName)
    {
        if (!TryProp(root, "data", out var data)) return -1;
        foreach (var concept in data.EnumerateArray())
        {
            if (!TryProp(concept, "skills", out var skills)) continue;
            foreach (var skill in skills.EnumerateArray())
            {
                TryProp(skill, "name", out var nameProp);
                if (nameProp.GetString() == skillName)
                {
                    TryProp(skill, "state", out var stateProp);
                    return stateProp.ValueKind == JsonValueKind.Number ? stateProp.GetInt32() : -1;
                }
            }
        }
        return -1;
    }

    // -------------------------------------------------------------------------
    // Helper records for cleaner test readability
    // -------------------------------------------------------------------------

    private sealed record SkillNodeDto(string Name, int StateValue);

    private sealed record LessonInfo(string Name, int StateValue, JsonElement MissingPrerequisites);
}
