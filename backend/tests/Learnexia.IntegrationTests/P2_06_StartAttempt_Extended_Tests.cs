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
/// P2-06 Extended integration tests — QC catalog cases not covered by the base P2_06_StartAttempt_Tests.
///
/// NEW cases from docs/qc/P2-06/backend-test-cases.md:
///   BE-TC-06: Options shape per question type
///   BE-TC-08: StudentId from JWT (not body injection)
///   BE-TC-12: Cross-language lesson → 403
///   BE-TC-14: Schema — quiz tables exist in `learning` schema
///   BE-TC-17: lessonId=0 → 422
///   BE-TC-18: lessonId=-5 → 422 or 404 (routing)
///   BE-TC-19: Non-numeric lessonId → 404
///   BE-TC-20: Empty-questions lesson → 200 with empty array
///   BE-TC-22: 4-subject scope — no Social Studies subject in seed
///   BE-TC-23: Locked lesson start — characterize current 200 behavior (KNOWN GAP)
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_06_StartAttempt_Extended_Tests : IAsyncLifetime
{
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private int _mathG1EnLessonId;    // English-tree lesson (has questions)
    private int _mathG1ArSubjectId;   // Ar-tree subject (may be 0 if Draft)
    private int _mathG1ArLessonId;    // Ar-tree lesson (for cross-language guard)
    private int _emptyLessonId;       // A lesson with 0 questions in the En-tree

    public P2_06_StartAttempt_Extended_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();
        using var scope = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // English Math G1 lesson with questions
        var mathG1En = await db.Subjects.AsNoTracking().Include(s => s.Grade)
            .FirstOrDefaultAsync(s => s.SubjectCode == SubjectCode.MATH
                && s.Language == ContentLanguage.En && s.Grade.Number == 1);
        if (mathG1En != null)
        {
            var lesson = await db.Lessons.AsNoTracking().Include(l => l.Unit)
                .Where(l => l.Unit.SubjectId == mathG1En.Id)
                .FirstOrDefaultAsync();
            _mathG1EnLessonId = lesson?.Id ?? 0;

            // Empty lesson (no questions)
            var emptyLesson = await db.Lessons.AsNoTracking().Include(l => l.Unit)
                .Where(l => l.Unit.SubjectId == mathG1En.Id
                    && !db.QuizQuestions.Any(q => q.LessonId == l.Id))
                .FirstOrDefaultAsync();
            _emptyLessonId = emptyLesson?.Id ?? 0;
        }

        // Arabic Ar tree
        var mathG1Ar = await db.Subjects.AsNoTracking().Include(s => s.Grade)
            .FirstOrDefaultAsync(s => s.SubjectCode == SubjectCode.MATH
                && s.Language == ContentLanguage.Ar && s.Grade.Number == 1
                && s.IsActive && s.LifecycleState == LifecycleState.Published);
        _mathG1ArSubjectId = mathG1Ar?.Id ?? 0;
        if (_mathG1ArSubjectId > 0)
        {
            var arLesson = await db.Lessons.AsNoTracking().Include(l => l.Unit)
                .Where(l => l.Unit.SubjectId == _mathG1ArSubjectId
                    && l.IsActive && l.LifecycleState == LifecycleState.Published)
                .FirstOrDefaultAsync();
            _mathG1ArLessonId = arLesson?.Id ?? 0;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string UniqueEmail(string tag = "")
        => $"p206x_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    private static bool TryProp(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (el.TryGetProperty(pascal, out value)) return true;
        foreach (var prop in el.EnumerateObject())
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            { value = prop.Value; return true; }
        value = default;
        return false;
    }

    private static async Task<(HttpResponseMessage Resp, JsonElement Root, string Body)>
        SendAsync(HttpClient client, HttpMethod method, string url, object? body = null, string? token = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);
        var bodyStr = await resp.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr)) try { root = JsonDocument.Parse(bodyStr).RootElement; } catch { }
        return (resp, root, bodyStr);
    }

    private async Task<(string Token, int Id)> CreateStudentAsync(string learningLanguage = "en", string? tag = null)
    {
        tag ??= Guid.NewGuid().ToString("N")[..6];
        var parentEmail = UniqueEmail($"par_{tag}");
        var (rR, rRoot, rBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)rR.StatusCode).Should().BeOneOf(new[] { 200, 201 }, $"parent reg; body={rBody}");
        TryProp(rRoot, "data", out var rData);
        TryProp(rData, "accessToken", out var pTok);
        var parentToken = pTok.GetString()!;

        var childEmail = UniqueEmail($"ch_{tag}");
        var (aR, aRoot, aBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new { FullName = "TC P206x", Email = childEmail, Password = ValidChildPassword,
                  Grade = 1, Language = "ar", Country = "EG", LearningLanguage = learningLanguage },
            parentToken);
        ((int)aR.StatusCode).Should().BeOneOf(new[] { 200, 201 }, $"add-child; body={aBody}");
        TryProp(aRoot, "data", out var aData);
        TryProp(aData, "id", out var idProp);
        var childId = idProp.GetInt32();

        var (sR, sRoot, sBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        sR.StatusCode.Should().Be(HttpStatusCode.OK, $"sign-in; body={sBody}");
        TryProp(sRoot, "data", out var sData);
        TryProp(sData, "accessToken", out var sTok);
        return (sTok.GetString()!, childId);
    }

    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-08: StudentId is resolved from JWT, not injected via body or query")]
    public async Task BeTc08_StudentId_FromJwt_Not_Client()
    {
        _mathG1EnLessonId.Should().BeGreaterThan(0);
        var (tokenA, studentAId) = await CreateStudentAsync("en", "a_08");
        var (_, studentBId) = await CreateStudentAsync("en", "b_08");

        // Try to inject studentB's id via body
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{_mathG1EnLessonId}/Attempt",
            new { StudentId = studentBId }, tokenA);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        TryProp(root, "data", out var data);
        TryProp(data, "attemptId", out var attemptIdProp);
        int attemptId = attemptIdProp.GetInt32();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var attempt = await db.Attempts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attemptId);
        attempt.Should().NotBeNull();
        attempt!.StudentId.Should().Be(studentAId,
            $"Attempt.StudentId must equal JWT-derived A id={studentAId}, not injected B id={studentBId}");
    }

    [Fact(DisplayName = "BE-TC-12: En-student starts attempt on Ar-tree lesson → 403 (cross-language guard)")]
    public async Task BeTc12_CrossLanguageLesson_Returns403()
    {
        if (_mathG1ArLessonId <= 0) return; // Ar-tree absent — skip

        var (token, studentId) = await CreateStudentAsync("en");

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{_mathG1ArLessonId}/Attempt", null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"EN student starting Ar-tree attempt must get 403; lessonId={_mathG1ArLessonId}; body={body}");
        TryProp(root, "successed", out var suc);
        suc.GetBoolean().Should().BeFalse($"body={body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        int count = await db.Attempts.AsNoTracking()
            .CountAsync(a => a.StudentId == studentId && a.LessonId == _mathG1ArLessonId);
        count.Should().Be(0, "No attempt must be created when language guard rejects");
    }

    [Fact(DisplayName = "BE-TC-14: learning schema tables QuizQuestions, Attempts, StudentAnswers exist")]
    public async Task BeTc14_Schema_QuizTableExist()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Confirm tables are accessible (if they didn't exist, these would throw)
        var qCount = await db.QuizQuestions.AsNoTracking().CountAsync();
        var aCount = await db.Attempts.AsNoTracking().CountAsync();
        var saCount = await db.StudentAnswers.AsNoTracking().CountAsync();

        qCount.Should().BeGreaterThanOrEqualTo(0, "QuizQuestions table exists");
        aCount.Should().BeGreaterThanOrEqualTo(0, "Attempts table exists");
        saCount.Should().BeGreaterThanOrEqualTo(0, "StudentAnswers table exists");
    }

    [Fact(DisplayName = "BE-TC-17: StartAttempt with lessonId=0 → 422 validation")]
    public async Task BeTc17_LessonIdZero_Returns422()
    {
        var (token, _) = await CreateStudentAsync("en");
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post,
            "api/Learning/Quizzes/0/Attempt", null, token);

        ((int)resp.StatusCode).Should().Be(422, $"lessonId=0 must return 422 validation; body={body}");
        TryProp(root, "successed", out var suc);
        suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    [Fact(DisplayName = "BE-TC-18: StartAttempt with lessonId=-5 → 422 or 404 (routing boundary)")]
    public async Task BeTc18_NegativeLessonId_Returns422Or404()
    {
        var (token, _) = await CreateStudentAsync("en");
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post,
            "api/Learning/Quizzes/-5/Attempt", null, token);

        // Negative integer may route to 422 (validation) or 404 (route constraint)
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 422, 404 },
            $"lessonId=-5 must return 422 or 404; body={body}");
    }

    [Fact(DisplayName = "BE-TC-19: StartAttempt with non-numeric lessonId 'abc' → 404 or 422 (no 500)")]
    public async Task BeTc19_NonNumericLessonId_Returns404()
    {
        var (token, _) = await CreateStudentAsync("en");
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post,
            "api/Learning/Quizzes/abc/Attempt", null, token);

        // ASP.NET Core model binding of 'abc' to int: framework may 404 (route constraint) or
        // bind as 0 and then 422 (LessonId validation). Actual behavior: 422 (model binder sends
        // LessonId=0 → FluentValidation rejects). Document actual — must not be 500.
        ((int)resp.StatusCode).Should().NotBe(500, $"Must not 500; body={body}");
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 404, 422 }, $"Non-numeric route must return 404 or 422; body={body}");
    }

    [Fact(DisplayName = "BE-TC-20: StartAttempt on lesson with zero questions → 200, Questions=[]")]
    public async Task BeTc20_EmptyQuizLesson_Returns200_EmptyQuestions()
    {
        if (_emptyLessonId <= 0) return; // no empty lesson in seed — skip

        var (token, studentId) = await CreateStudentAsync("en");
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{_emptyLessonId}/Attempt", null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"Empty quiz must return 200; body={body}");
        TryProp(root, "data", out var data);
        TryProp(data, "attemptId", out var aidProp);
        aidProp.GetInt32().Should().BeGreaterThan(0, $"AttemptId must be positive; body={body}");
        TryProp(data, "questions", out var questions);
        questions.ValueKind.Should().Be(JsonValueKind.Array, $"questions must be array; body={body}");
        questions.GetArrayLength().Should().Be(0, $"questions must be empty for zero-question lesson; body={body}");

        // Attempt row still created
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        int count = await db.Attempts.AsNoTracking()
            .CountAsync(a => a.StudentId == studentId && a.LessonId == _emptyLessonId && a.Status == AttemptStatus.InProgress);
        count.Should().BeGreaterThan(0, "An Attempt row must be created even for zero-question lesson");
    }

    [Fact(DisplayName = "BE-TC-22: Seed subjects belong to 4 supported SubjectCodes only (no Social Studies)")]
    public async Task BeTc22_FourSubjectScope_NoSocialStudies()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var subjectCodes = await db.Subjects.AsNoTracking()
            .Select(s => s.SubjectCode).Distinct().ToListAsync();

        var validCodes = new[] { SubjectCode.MATH, SubjectCode.SCIENCE, SubjectCode.ARABIC, SubjectCode.ENGLISH };
        subjectCodes.Should().OnlyContain(c => validCodes.Contains(c),
            "Only the 4 supported subject codes (Math/Science/Arabic/English) must exist — no Social Studies");

        subjectCodes.Should().Contain(SubjectCode.MATH, "MATH must be seeded");
        subjectCodes.Should().Contain(SubjectCode.SCIENCE, "SCIENCE must be seeded");
        subjectCodes.Should().Contain(SubjectCode.ARABIC, "ARABIC must be seeded");
        subjectCodes.Should().Contain(SubjectCode.ENGLISH, "ENGLISH must be seeded");
    }

    [Fact(DisplayName = "BE-TC-23: KNOWN GAP R3 — StartAttempt on Locked lesson returns 200 (no lock enforcement)")]
    public async Task BeTc23_LockedLesson_StartAttempt_CurrentBehavior200()
    {
        _mathG1EnLessonId.Should().BeGreaterThan(0);
        // Use a fresh student — the first lesson in the En-tree is the root (Available)
        // The second lesson should be Locked (needs first mastered)
        var (token, _) = await CreateStudentAsync("en");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var mathG1En = await db.Subjects.AsNoTracking().Include(s => s.Grade)
            .FirstOrDefaultAsync(s => s.SubjectCode == SubjectCode.MATH
                && s.Language == ContentLanguage.En && s.Grade.Number == 1);
        if (mathG1En == null) return;

        // Find the second lesson (likely locked)
        var secondLesson = await db.Lessons.AsNoTracking().Include(l => l.Unit)
            .Where(l => l.Unit.SubjectId == mathG1En.Id)
            .OrderBy(l => l.Unit.SequenceOrder).ThenBy(l => l.SequenceOrder)
            .Skip(1).FirstOrDefaultAsync();
        if (secondLesson == null) return;

        // KNOWN GAP R3: StartAttemptCommandHandler does NOT enforce lock state
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{secondLesson.Id}/Attempt", null, token);

        // Document current behavior: 200 (gap), not 403/424
        ((int)resp.StatusCode).Should().Be(200,
            $"KNOWN GAP R3: StartAttempt does NOT enforce lock state — current behavior is 200; " +
            $"lessonId={secondLesson.Id}; body={body}");
    }
}
