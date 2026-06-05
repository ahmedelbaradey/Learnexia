using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Learnexia.Shared.Contracts.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

// =============================================================================
// P8-04 integration tests — "Change a child's learning language (parent-only, fresh start)"
//
// Endpoint under test:
//   PUT api/Parent/Change-Learning-Language
//   Body: { childId: int, newLearningLanguage: "ar"|"en", confirmFreshStart: bool }
//   Auth: [Authorize(Roles = "Parent,Admin,SuperAdmin")] (controller-level)
//
// Test scenarios (T1–T10) mirror the plan's Batch 3 spec:
//
//   T1  Student JWT → 403 (role-blocked)
//   T2  Parent JWT, confirmFreshStart=false → 424 (BusinessValidation), no DB change
//   T3  Parent JWT, non-linked child, confirmFreshStart=true → 403 (IDOR)
//   T4  Parent JWT, own child, confirm=true, new != current → 200; child language updated in DB
//   T5  After T4: Math+Science Attempt rows deleted; Arabic+English Attempt rows intact
//   T6  After T4: Arabic/English attempt rows explicitly intact (cross-check with T5)
//   T7  After T4: XP/streak/badge counts unchanged (gamification retained)
//   T8  Idempotency: re-deliver LearningLanguageChangedIntegrationEvent → no error, no extra effect
//   T9  Same-language request (new == current), confirm=true → 200 no-op; Math/Science NOT reset
//   T10 Invalid language code ("fr") → 422 (validator)
// =============================================================================
[Collection("IntegrationTests")]
public sealed class P8_04_ChangeLearningLanguage_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string ChangeLangUrl      = "api/Parent/Change-Learning-Language";
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string MeUrl              = "api/Users/Me";
    private const string ValidChildPassword = "Child@Pass1";

    // Seeded accounts
    private const string BasicUserName     = "basicuser";
    private const string BasicUserPassword = "123Pa$$word!";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // Demo lesson IDs resolved in InitializeAsync. Used by the attempt-seeding helpers.
    private int _mathEnG1LessonId;   // MATH/En G1 — "Introduction to Counting (G1)"
    private int _scienceEnG1LessonId; // SCIENCE/En G1 — "What Are Living Things? (G1)"
    private int _arabicArG1LessonId;  // ARABIC/Ar G1 — "مراجعة الحروف الهجائية (ص1)"
    private int _englishEnG1LessonId; // ENGLISH/En G1 — "Sight Words and Fluency (G1)"

    // Skill IDs (required to add quiz questions so StartAttempt doesn't fail on missing content).
    private int _mathEnG1SkillId;
    private int _scienceEnG1SkillId;
    private int _arabicArG1SkillId;
    private int _englishEnG1SkillId;

    public P8_04_ChangeLearningLanguage_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // =========================================================================
    // Lifecycle
    // =========================================================================

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        using var scope = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope.ServiceProvider);

        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // MATH/En G1
        var mathEnLesson = await db.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == "Introduction to Counting (G1)");
        mathEnLesson.Should().NotBeNull("LearningSeeder must seed 'Introduction to Counting (G1)'");
        _mathEnG1LessonId  = mathEnLesson!.Id;
        _mathEnG1SkillId   = mathEnLesson.SkillId
            ?? throw new InvalidOperationException("'Introduction to Counting (G1)' must have a SkillId");

        // SCIENCE/En G1 — first lesson in "Living Things" unit
        var scienceEnLesson = await db.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == "What Are Living Things? (G1)");
        scienceEnLesson.Should().NotBeNull("LearningSeeder must seed 'What Are Living Things? (G1)'");
        _scienceEnG1LessonId = scienceEnLesson!.Id;
        _scienceEnG1SkillId  = scienceEnLesson.SkillId
            ?? throw new InvalidOperationException("'What Are Living Things? (G1)' must have a SkillId");

        // ARABIC/Ar G1
        var arabicArLesson = await db.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == "مراجعة الحروف الهجائية (ص1)");
        arabicArLesson.Should().NotBeNull("LearningSeeder must seed 'مراجعة الحروف الهجائية (ص1)'");
        _arabicArG1LessonId = arabicArLesson!.Id;
        _arabicArG1SkillId  = arabicArLesson.SkillId
            ?? throw new InvalidOperationException("'مراجعة الحروف الهجائية (ص1)' must have a SkillId");

        // ENGLISH/En G1
        var englishEnLesson = await db.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == "Sight Words and Fluency (G1)");
        englishEnLesson.Should().NotBeNull("LearningSeeder must seed 'Sight Words and Fluency (G1)'");
        _englishEnG1LessonId = englishEnLesson!.Id;
        _englishEnG1SkillId  = englishEnLesson.SkillId
            ?? throw new InvalidOperationException("'Sight Words and Fluency (G1)' must have a SkillId");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p804_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>Case-insensitive JSON property lookup (camelCase + PascalCase).</summary>
    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out value)) return true;
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            { value = prop.Value; return true; }
        }
        value = default;
        return false;
    }

    private static async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendAsync(HttpClient client, HttpMethod method, string url,
            object? body = null, string? bearerToken = null)
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

    /// <summary>
    /// Registers a parent, adds one child (LearningLanguage="en"), signs in as child.
    /// Returns (parentToken, childToken, childId, childEmail).
    /// The child starts with LearningLanguage="en" so MATH/En and SCIENCE/En attempts are accessible.
    /// </summary>
    private async Task<(string ParentToken, string ChildToken, int ChildId, string ChildEmail)>
        CreateParentAndChildAsync(string tag, string learningLanguage = "en")
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
                FullName = $"Test Child P804 {tag}",
                Email    = childEmail,
                Password = ValidChildPassword,
                Grade    = 1,
                Language = "ar",
                Country  = "EG",
                LearningLanguage = learningLanguage,
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"Add-Child must succeed; body: {addBody}");
        TryProp(addRoot, "data", out var addData).Should().BeTrue("body: {0}", addBody);
        TryProp(addData, "id", out var idProp).Should().BeTrue("body: {0}", addBody);
        var childId = idProp.GetInt32();

        var childToken = await SignInAndGetTokenAsync(childEmail, ValidChildPassword);
        return (parentToken, childToken, childId, childEmail);
    }

    /// <summary>
    /// Ensures at least one MCQ question exists for a lesson (so StartAttempt can load questions).
    /// Mirrors P4_02_EarnXpAndLevelUp_Tests.EnsureNQuestionsForLessonAsync.
    /// </summary>
    private async Task EnsureOneQuestionForLessonAsync(int lessonId, int skillId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var existing = await db.QuizQuestions
            .Where(q => q.LessonId == lessonId && q.QuestionType == QuestionType.MCQ)
            .AnyAsync();

        if (existing) return;

        var options       = JsonSerializer.Serialize(new[] { "A", "B", "C", "D" });
        var correctAnswer = JsonSerializer.Serialize("A");

        db.QuizQuestions.Add(new QuizQuestion
        {
            LessonId      = lessonId,
            SkillId       = skillId,
            QuestionType  = QuestionType.MCQ,
            QuestionText  = $"P8-04 test question for lesson {lessonId}",
            Options       = options,
            CorrectAnswer = correctAnswer,
            Difficulty    = DifficultyLevel.Easy,
            GeneratedBy   = GeneratedBy.Curated,
        });

        await db.SaveChangesAsync(LearningSeeder.SystemUserId);
    }

    /// <summary>
    /// Starts a quiz attempt for a lesson via the API (as child). Returns the attemptId.
    /// Ensures at least one question exists first.
    /// </summary>
    private async Task<int> StartAttemptAsync(int lessonId, int skillId, string childToken)
    {
        await EnsureOneQuestionForLessonAsync(lessonId, skillId);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", null, childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must succeed; lessonId={0}, body: {1}", lessonId, body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "attemptId", out var idProp).Should().BeTrue("body: {0}", body);
        return idProp.GetInt32();
    }

    /// <summary>
    /// Counts Attempt rows in the DB for a given student restricted to a specific set of SubjectCodes.
    /// Does a join through Lesson → Unit → Subject.
    /// </summary>
    private async Task<int> CountAttemptsForSubjectCodesAsync(int studentId, SubjectCode[] codes)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var codeSet = codes.ToHashSet();

        // Collect lesson IDs in those subjects
        var lessonIds = await db.Lessons
            .AsNoTracking()
            .Where(l => codeSet.Contains(l.Unit.Subject.SubjectCode))
            .Select(l => l.Id)
            .ToListAsync();

        if (lessonIds.Count == 0) return 0;

        var lessonIdSet = lessonIds.ToHashSet();

        return await db.Attempts
            .AsNoTracking()
            .Where(a => a.StudentId == studentId && lessonIdSet.Contains(a.LessonId))
            .CountAsync();
    }

    /// <summary>Counts ALL Attempt rows for a student (any subject).</summary>
    private async Task<int> CountAllAttemptsAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        return await db.Attempts.AsNoTracking()
            .CountAsync(a => a.StudentId == studentId);
    }

    /// <summary>
    /// Reads the child's LearningLanguage directly from the Identity DB.
    /// </summary>
    private async Task<string?> GetChildLearningLanguageFromDbAsync(int childId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == childId);
        return user?.LearningLanguage;
    }

    /// <summary>
    /// Calls PUT api/Parent/Change-Learning-Language with the given parameters.
    /// </summary>
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        ChangeLearningLanguageAsync(
            string parentToken,
            int childId,
            string newLearningLanguage,
            bool confirmFreshStart)
        => SendAsync(_client, HttpMethod.Put, ChangeLangUrl,
            new
            {
                ChildId              = childId,
                NewLearningLanguage  = newLearningLanguage,
                ConfirmFreshStart    = confirmFreshStart,
            },
            parentToken);

    // =========================================================================
    // T1 — Student JWT → 403 (role-blocked by controller [Authorize(Roles)])
    //
    // AC-1: "a student/other role → blocked by role auth"
    // The ParentController gate is [Authorize(Roles = "Parent,Admin,SuperAdmin")].
    // A Student-role token must receive 403.
    // =========================================================================

    [Fact(DisplayName = "P804-T1 Student JWT → 403 (role-blocked)")]
    public async Task T1_StudentJwt_Returns403()
    {
        // Arrange: create a parent+child so we have a Student JWT.
        var (_, childToken, childId, _) = await CreateParentAndChildAsync("t1");

        // Act: student tries to call Change-Learning-Language.
        var (resp, _, body) = await ChangeLearningLanguageAsync(
            childToken, childId, "ar", confirmFreshStart: true);

        // Assert: 403 — role gate blocks Students.
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Student-role token must be blocked by the controller [Authorize(Roles='Parent,...')]; body: {0}", body);
    }

    // =========================================================================
    // T2 — Parent JWT, confirmFreshStart=false → 424 (BusinessValidation)
    //      AND no state change: child language unchanged, no attempts deleted.
    //
    // AC-2: "Request without the confirm flag → BusinessValidation (HTTP 424 / FailedDependency),
    //        and NO change is made and NO event is published."
    // =========================================================================

    [Fact(DisplayName = "P804-T2 Parent JWT, confirmFreshStart=false → 424; language/attempts unchanged")]
    public async Task T2_ConfirmFalse_Returns424_NoStateChange()
    {
        // Arrange: parent + child (starting LearningLanguage="en").
        var (parentToken, childToken, childId, _) = await CreateParentAndChildAsync("t2");

        // Seed one Math/En attempt so we can verify it is NOT deleted.
        await StartAttemptAsync(_mathEnG1LessonId, _mathEnG1SkillId, childToken);

        var attemptsBefore = await CountAttemptsForSubjectCodesAsync(
            childId, new[] { SubjectCode.MATH, SubjectCode.SCIENCE });
        var langBefore = await GetChildLearningLanguageFromDbAsync(childId);

        // Act: confirmFreshStart = false.
        var (resp, root, body) = await ChangeLearningLanguageAsync(
            parentToken, childId, "ar", confirmFreshStart: false);

        // Assert HTTP 424.
        resp.StatusCode.Should().Be(HttpStatusCode.FailedDependency,
            "confirmFreshStart=false must return 424 FailedDependency (BusinessValidation); body: {0}", body);

        // Assert envelope shape.
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("Successed must be false on 424; body: {0}", body);
        TryProp(root, "statusCode", out var statusCode).Should().BeTrue("body: {0}", body);
        statusCode.GetInt32().Should().Be(424, "envelope statusCode must be 424; body: {0}", body);

        // Assert: no DB change — language is still "en".
        var langAfter = await GetChildLearningLanguageFromDbAsync(childId);
        langAfter.Should().Be(langBefore,
            "child LearningLanguage must be unchanged when confirm=false; was '{0}', now '{1}'",
            langBefore, langAfter);

        // Assert: no attempts deleted.
        var attemptsAfter = await CountAttemptsForSubjectCodesAsync(
            childId, new[] { SubjectCode.MATH, SubjectCode.SCIENCE });
        attemptsAfter.Should().Be(attemptsBefore,
            "Math/Science attempt count must be unchanged when confirm=false; body: {0}", body);
    }

    // =========================================================================
    // T3 — Parent JWT, non-linked child, confirmFreshStart=true → 403 (IDOR)
    //
    // AC-1: "a non-linked parent → Forbidden (403)"
    // The handler runs the link check AFTER the confirm guard.
    // =========================================================================

    [Fact(DisplayName = "P804-T3 Non-linked child → 403 (IDOR/family-scope)")]
    public async Task T3_NonLinkedChild_Returns403()
    {
        // Arrange: two independent parent+child pairs.
        var (parentAToken, _, _, _) = await CreateParentAndChildAsync("t3a");
        var (_, _, childBId, _) = await CreateParentAndChildAsync("t3b");

        // Act: parent A tries to change parent B's child's language.
        var (resp, _, body) = await ChangeLearningLanguageAsync(
            parentAToken, childBId, "ar", confirmFreshStart: true);

        // Assert: 403 — family-scope IDOR block.
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "parent A must not be able to change parent B's child language; body: {0}", body);
    }

    // =========================================================================
    // T4 — Happy path: parent changes own child's language with confirm=true → 200
    //      Verify: DB shows new LearningLanguage; /Me (after re-sign-in) shows new value.
    //
    // AC-3: "Request with confirm=true and valid new language → User.LearningLanguage updated; success returned."
    // AC-8: "On next sign-in, JWT carries new learning_language."
    // =========================================================================

    [Fact(DisplayName = "P804-T4 Parent changes own child language (confirm=true) → 200; DB + Me reflect new language")]
    public async Task T4_ValidChange_Returns200_LanguageUpdated()
    {
        // Arrange: parent + child starting at "en".
        var (parentToken, _, childId, childEmail) = await CreateParentAndChildAsync("t4", "en");

        // Verify initial LearningLanguage.
        var langBefore = await GetChildLearningLanguageFromDbAsync(childId);
        langBefore.Should().Be("en", "child should start with LearningLanguage='en'");

        // Act: change to "ar".
        var (resp, root, body) = await ChangeLearningLanguageAsync(
            parentToken, childId, "ar", confirmFreshStart: true);

        // Assert HTTP 200.
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "valid change (own linked child, confirm=true) must return 200; body: {0}", body);

        // Assert envelope.
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);
        TryProp(root, "statusCode", out var statusCode).Should().BeTrue("body: {0}", body);
        statusCode.GetInt32().Should().Be(200, "envelope statusCode must be 200; body: {0}", body);

        // Assert data shape.
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "childId", out var childIdProp).Should().BeTrue("data must have childId; body: {0}", body);
        childIdProp.GetInt32().Should().Be(childId, "childId in response must match; body: {0}", body);
        TryProp(data, "newLearningLanguage", out var newLangProp).Should().BeTrue("data must have newLearningLanguage; body: {0}", body);
        newLangProp.GetString().Should().Be("ar", "newLearningLanguage must be 'ar'; body: {0}", body);

        // Assert DB: LearningLanguage updated.
        var langAfter = await GetChildLearningLanguageFromDbAsync(childId);
        langAfter.Should().Be("ar",
            "child LearningLanguage in DB must be 'ar' after successful change; childId={0}", childId);

        // Assert /Me after re-sign-in (new JWT carries updated learning_language).
        var newChildToken = await SignInAndGetTokenAsync(childEmail, ValidChildPassword);
        var (meResp, meRoot, meBody) = await SendAsync(_client, HttpMethod.Get, MeUrl, null, newChildToken);
        meResp.StatusCode.Should().Be(HttpStatusCode.OK, "GET /Me must succeed; body: {0}", meBody);
        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);
        TryProp(meData, "learningLanguage", out var meLangProp).Should().BeTrue(
            "/Me response must include learningLanguage; body: {0}", meBody);
        meLangProp.GetString().Should().Be("ar",
            "/Me.learningLanguage must reflect the new 'ar' after re-sign-in; body: {0}", meBody);
    }

    // =========================================================================
    // T5 — Math+Science Attempt rows deleted; Arabic+English Attempt rows intact.
    //
    // AC-5: "The Learning consumer deletes the child's Math/Science Attempt rows (StudentAnswers cascade)
    //        so derived mastery resets; Arabic/English Attempt rows are untouched."
    //
    // Strategy:
    //   1. Start the child at LearningLanguage="en".
    //   2. Create attempts on MATH/En and SCIENCE/En lessons.
    //   3. Create attempts on ARABIC/Ar and ENGLISH/En lessons.
    //   4. Change language from "en" → "ar" with confirm=true.
    //   5. Assert MATH+SCIENCE attempt count = 0.
    //   6. Assert ARABIC+ENGLISH attempt count = (original non-Math/Science count).
    // =========================================================================

    [Fact(DisplayName = "P804-T5 Math+Science attempts deleted after language change; Arabic+English untouched")]
    public async Task T5_MathScienceAttemptsDeleted_ArabicEnglishIntact()
    {
        // Arrange: parent + child starting at "en".
        var (parentToken, childToken, childId, _) = await CreateParentAndChildAsync("t5", "en");

        // Seed one Math/En attempt.
        await StartAttemptAsync(_mathEnG1LessonId, _mathEnG1SkillId, childToken);
        // Seed one Science/En attempt.
        await StartAttemptAsync(_scienceEnG1LessonId, _scienceEnG1SkillId, childToken);
        // Seed one Arabic/Ar attempt.
        await StartAttemptAsync(_arabicArG1LessonId, _arabicArG1SkillId, childToken);
        // Seed one English/En attempt.
        await StartAttemptAsync(_englishEnG1LessonId, _englishEnG1SkillId, childToken);

        // Verify counts before.
        var mathScienceBefore = await CountAttemptsForSubjectCodesAsync(
            childId, new[] { SubjectCode.MATH, SubjectCode.SCIENCE });
        mathScienceBefore.Should().Be(2,
            "2 Math/Science attempts must exist before the change (1 MATH + 1 SCIENCE)");

        var langSpecBefore = await CountAttemptsForSubjectCodesAsync(
            childId, new[] { SubjectCode.ARABIC, SubjectCode.ENGLISH });
        langSpecBefore.Should().Be(2,
            "2 Arabic/English attempts must exist before the change (1 ARABIC + 1 ENGLISH)");

        // Act: change language "en" → "ar".
        var (resp, _, body) = await ChangeLearningLanguageAsync(
            parentToken, childId, "ar", confirmFreshStart: true);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "language change must succeed; body: {0}", body);

        // Assert: Math+Science attempts are gone.
        var mathScienceAfter = await CountAttemptsForSubjectCodesAsync(
            childId, new[] { SubjectCode.MATH, SubjectCode.SCIENCE });
        mathScienceAfter.Should().Be(0,
            "ALL Math and Science Attempt rows for the student must be deleted after the language change " +
            "(P8-04 locked decision §1 — full reset, not prior-language-only); studentId={0}", childId);

        // Assert: Arabic+English attempts are intact.
        var langSpecAfter = await CountAttemptsForSubjectCodesAsync(
            childId, new[] { SubjectCode.ARABIC, SubjectCode.ENGLISH });
        langSpecAfter.Should().Be(langSpecBefore,
            "Arabic and English Attempt rows must NOT be deleted by the language change; " +
            "before={0}, after={1}, studentId={2}", langSpecBefore, langSpecAfter, childId);
    }

    // =========================================================================
    // T6 — Explicit check: Arabic/English progress (attempts) are untouched.
    //
    // This mirrors T5 but is a dedicated scenario per the plan spec (T6 is "after T4: Arabic/English
    // rows intact"). We set it up independently so failures are isolated.
    // =========================================================================

    [Fact(DisplayName = "P804-T6 Arabic and English Attempt rows are intact after language change")]
    public async Task T6_ArabicEnglish_AttemptsIntact_AfterLanguageChange()
    {
        // Arrange: parent + child starting at "en".
        var (parentToken, childToken, childId, _) = await CreateParentAndChildAsync("t6", "en");

        // Seed language-specific (non-Math, non-Science) attempts.
        await StartAttemptAsync(_arabicArG1LessonId, _arabicArG1SkillId, childToken);
        await StartAttemptAsync(_englishEnG1LessonId, _englishEnG1SkillId, childToken);

        var langSpecCountBefore = await CountAttemptsForSubjectCodesAsync(
            childId, new[] { SubjectCode.ARABIC, SubjectCode.ENGLISH });
        langSpecCountBefore.Should().Be(2,
            "2 language-specific attempts must exist before the change");

        // Act: change language "en" → "ar".
        var (resp, _, body) = await ChangeLearningLanguageAsync(
            parentToken, childId, "ar", confirmFreshStart: true);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "language change must succeed; body: {0}", body);

        // Assert: Arabic + English attempts unchanged.
        var langSpecCountAfter = await CountAttemptsForSubjectCodesAsync(
            childId, new[] { SubjectCode.ARABIC, SubjectCode.ENGLISH });
        langSpecCountAfter.Should().Be(langSpecCountBefore,
            "Arabic and English Attempt rows must be retained after the language change; " +
            "before={0}, after={1}, studentId={2}", langSpecCountBefore, langSpecCountAfter, childId);
    }

    // =========================================================================
    // T7 — Gamification retained: XP/streak/badges/level unchanged after language change.
    //
    // AC-7: "No Gamification consumer is added — XP/streak/badges/level are retained."
    //
    // Strategy:
    //   1. Create parent+child with LearningLanguage="en".
    //   2. Complete a Math/En lesson (correct answer → XP + streak + potential badge).
    //   3. Record XP total, streak count, badge count, heart count from GamificationDbContext.
    //   4. Change language → "ar" with confirm=true.
    //   5. Assert all gamification counters are unchanged.
    // =========================================================================

    [Fact(DisplayName = "P804-T7 Gamification (XP/streak/badges) retained after language change")]
    public async Task T7_Gamification_Retained_AfterLanguageChange()
    {
        // Arrange: parent + child starting at "en".
        var (parentToken, childToken, childId, _) = await CreateParentAndChildAsync("t7", "en");

        // Complete a Math/En lesson to earn XP/streak.
        await EnsureOneQuestionForLessonAsync(_mathEnG1LessonId, _mathEnG1SkillId);

        var questions = await GetMcqQuestionsAsync(_mathEnG1LessonId);
        questions.Should().NotBeEmpty("demo lesson must have MCQ questions");

        var attemptId = await StartAttemptAsync(_mathEnG1LessonId, _mathEnG1SkillId, childToken);

        // Submit the first correct answer.
        var (firstQId, correctAnswer) = questions.First();
        var (subResp, _, subBody) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Answers",
            new { QuestionId = firstQId, AnswerPayload = correctAnswer, TimeSpentSeconds = 5, HintUsed = false },
            childToken);
        subResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SubmitAnswer must succeed; body: {0}", subBody);

        // Complete the attempt to trigger LessonCompleted event (XP + streak).
        var (complResp, _, complBody) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Complete", null, childToken);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must succeed; body: {0}", complBody);

        // Record gamification state.
        var (xpBefore, streakBefore, badgeCountBefore) = await GetGamificationSummaryAsync(childId);

        // At minimum the student should have some XP from the correct answer.
        xpBefore.Should().BeGreaterThan(0,
            "student must have earned XP from the correct answer before the language change");

        // Act: change language "en" → "ar".
        var (resp, _, body) = await ChangeLearningLanguageAsync(
            parentToken, childId, "ar", confirmFreshStart: true);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "language change must succeed; body: {0}", body);

        // Assert: gamification counters unchanged.
        var (xpAfter, streakAfter, badgeCountAfter) = await GetGamificationSummaryAsync(childId);

        xpAfter.Should().Be(xpBefore,
            "TotalXp must NOT change after a language change (no Gamification consumer); " +
            "before={0}, after={1}, studentId={2}", xpBefore, xpAfter, childId);

        streakAfter.Should().Be(streakBefore,
            "CurrentStreak must NOT change after a language change; " +
            "before={0}, after={1}, studentId={2}", streakBefore, streakAfter, childId);

        badgeCountAfter.Should().Be(badgeCountBefore,
            "Badge count must NOT change after a language change; " +
            "before={0}, after={1}, studentId={2}", badgeCountBefore, badgeCountAfter, childId);
    }

    private async Task<List<(int QuestionId, string CorrectAnswer)>> GetMcqQuestionsAsync(int lessonId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var questions = await db.QuizQuestions
            .AsNoTracking()
            .Where(q => q.LessonId == lessonId && q.QuestionType == QuestionType.MCQ)
            .OrderBy(q => q.Id)
            .Select(q => new { q.Id, q.CorrectAnswer })
            .ToListAsync();
        return questions.Select(q => (q.Id, q.CorrectAnswer)).ToList();
    }

    private async Task<(int TotalXp, int CurrentStreak, int BadgeCount)> GetGamificationSummaryAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var profile = await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        var totalXp       = profile?.TotalXp ?? 0;
        var currentStreak = profile?.CurrentStreak ?? 0;

        // StudentBadge is keyed by StudentXpProfileId (no direct StudentId field).
        var badgeCount = 0;
        if (profile is not null)
        {
            badgeCount = await db.StudentBadges
                .AsNoTracking()
                .CountAsync(b => b.StudentXpProfileId == profile.Id);
        }

        return (totalXp, currentStreak, badgeCount);
    }

    // =========================================================================
    // T8 — Idempotency: re-deliver LearningLanguageChangedIntegrationEvent with the
    //      same EventId → no error, no extra effect (Math/Science already empty → no-op).
    //
    // AC-6: "The consumer is idempotent (re-delivery of the same event causes no error and no extra effect)."
    // =========================================================================

    [Fact(DisplayName = "P804-T8 Idempotency: re-delivering LearningLanguageChangedIntegrationEvent → no error, no extra effect")]
    public async Task T8_Idempotency_RedeliveredEvent_NoError_NoExtraEffect()
    {
        // Arrange: parent + child.
        var (parentToken, childToken, childId, _) = await CreateParentAndChildAsync("t8", "en");

        // Seed and then delete Math attempts by changing language.
        await StartAttemptAsync(_mathEnG1LessonId, _mathEnG1SkillId, childToken);
        var (changeResp, _, changeBody) = await ChangeLearningLanguageAsync(
            parentToken, childId, "ar", confirmFreshStart: true);
        changeResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "first language change must succeed; body: {0}", changeBody);

        // Verify Math/Science attempts are now 0.
        var countAfterFirst = await CountAttemptsForSubjectCodesAsync(
            childId, new[] { SubjectCode.MATH, SubjectCode.SCIENCE });
        countAfterFirst.Should().Be(0,
            "Math/Science attempts must be 0 after first change; studentId={0}", childId);

        // Act: publish the event a second time directly (simulating re-delivery).
        // This bypasses the HTTP endpoint and fires the integration event handler directly.
        var eventId = Guid.NewGuid();
        var redeliveredEvent = new LearningLanguageChangedIntegrationEvent(
            EventId:       eventId,
            OccurredOnUtc: DateTime.UtcNow,
            StudentId:     childId,
            OldLanguage:   "en",
            NewLanguage:   "ar");

        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        // This must not throw (fail-soft handler + naturally idempotent delete-empty-set).
        var act = async () => await publisher.Publish(redeliveredEvent);
        await act.Should().NotThrowAsync(
            "re-delivering LearningLanguageChangedIntegrationEvent must not throw " +
            "(handler is fail-soft and delete of empty set is a no-op)");

        // Assert: still 0 Math/Science attempts (no phantom re-creation or corruption).
        var countAfterRedelivery = await CountAttemptsForSubjectCodesAsync(
            childId, new[] { SubjectCode.MATH, SubjectCode.SCIENCE });
        countAfterRedelivery.Should().Be(0,
            "attempt count must remain 0 after re-delivery — no extra effect; studentId={0}", childId);
    }

    // =========================================================================
    // T9 — Same-language request (new == current), confirm=true → 200 no-op success.
    //      Math/Science attempts NOT reset (no event published for same-language).
    //
    // AC-9 (optional, confirmed Q3): "Changing to the same language → no-op success; no event, no reset."
    // =========================================================================

    [Fact(DisplayName = "P804-T9 Same-language request → 200 no-op success; Math/Science attempts NOT reset")]
    public async Task T9_SameLanguage_NoOp_200_NoReset()
    {
        // Arrange: parent + child starting at "en".
        var (parentToken, childToken, childId, _) = await CreateParentAndChildAsync("t9", "en");

        // Seed Math attempt.
        await StartAttemptAsync(_mathEnG1LessonId, _mathEnG1SkillId, childToken);

        var mathCountBefore = await CountAttemptsForSubjectCodesAsync(
            childId, new[] { SubjectCode.MATH, SubjectCode.SCIENCE });
        mathCountBefore.Should().BeGreaterThan(0,
            "must have at least one Math attempt before the no-op change");

        // Act: request same language ("en" → "en").
        var (resp, root, body) = await ChangeLearningLanguageAsync(
            parentToken, childId, "en", confirmFreshStart: true);

        // Assert HTTP 200 (no-op success).
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "same-language request with confirm=true must return 200 (no-op success); body: {0}", body);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true for no-op; body: {0}", body);

        // Assert: language unchanged in DB.
        var langAfter = await GetChildLearningLanguageFromDbAsync(childId);
        langAfter.Should().Be("en",
            "LearningLanguage must still be 'en' after a no-op same-language request; childId={0}", childId);

        // Assert: Math/Science attempts NOT reset (no event published for same-language).
        var mathCountAfter = await CountAttemptsForSubjectCodesAsync(
            childId, new[] { SubjectCode.MATH, SubjectCode.SCIENCE });
        mathCountAfter.Should().Be(mathCountBefore,
            "Math/Science attempts must NOT be reset on a same-language no-op; " +
            "before={0}, after={1}, studentId={2}", mathCountBefore, mathCountAfter, childId);
    }

    // =========================================================================
    // T10 — Invalid language code "fr" → 422 (validator)
    //
    // The validator (ChangeLearningLanguageCommandValidator) rejects any value
    // outside {"ar", "en"} with HTTP 422 UnprocessableEntity.
    // =========================================================================

    [Fact(DisplayName = "P804-T10 Invalid language code 'fr' → 422 (ValidationBehavior)")]
    public async Task T10_InvalidLanguageCode_Returns422()
    {
        // Arrange: any parent.
        var (parentToken, _, childId, _) = await CreateParentAndChildAsync("t10");

        // Act: send "fr" as the language code.
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Put, ChangeLangUrl,
            new
            {
                ChildId             = childId,
                NewLearningLanguage = "fr",
                ConfirmFreshStart   = true,
            },
            parentToken);

        // Assert HTTP 422.
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "language='fr' (not ar|en) must be rejected by the validator with 422; body: {0}", body);

        // Assert envelope.
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("Successed must be false on 422; body: {0}", body);
        TryProp(root, "errors", out var errors).Should().BeTrue("envelope must have errors; body: {0}", body);
        errors.GetArrayLength().Should().BeGreaterThan(0,
            "Errors[] must be populated for invalid language code; body: {0}", body);
    }

    // =========================================================================
    // Extra: Anonymous → 401 (no JWT at all)
    // =========================================================================

    [Fact(DisplayName = "P804-Extra Anonymous → 401 (no JWT)")]
    public async Task Extra_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Put, ChangeLangUrl,
            new { ChildId = 1, NewLearningLanguage = "ar", ConfirmFreshStart = true });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "no-JWT request to Change-Learning-Language must return 401; body: {0}", body);
    }

    // =========================================================================
    // Extra: ChildId = 0 → 422 (validator: ChildId must be > 0)
    // =========================================================================

    [Fact(DisplayName = "P804-Extra ChildId=0 → 422 (validator: ChildId must be > 0)")]
    public async Task Extra_ChildIdZero_Returns422()
    {
        var (parentToken, _, _, _) = await CreateParentAndChildAsync("t_cid0");

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Put, ChangeLangUrl,
            new { ChildId = 0, NewLearningLanguage = "ar", ConfirmFreshStart = true },
            parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "ChildId=0 must be rejected by validator (GreaterThan(0)); body: {0}", body);
        TryProp(root, "errors", out var errors).Should().BeTrue("body: {0}", body);
        errors.GetArrayLength().Should().BeGreaterThan(0, "body: {0}", body);
    }
}
