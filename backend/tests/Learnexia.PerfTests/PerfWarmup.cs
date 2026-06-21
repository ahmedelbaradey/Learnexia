using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Learnexia.IntegrationTests;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.PerfTests;

/// <summary>
/// One-shot warmup that:
///   1. Applies migrations + seeds identity roles / superadmin (idempotent).
///   2. Registers a parent, adds a child (student), and signs in all three accounts.
///   3. Discovers a seeded lessonId with questions via the ForGrade endpoint.
///   4. Calls StartAttempt once to cache a live attemptId (resume-safe in later loops).
///   5. Populates <see cref="PerfContext"/> for use by all NBomber scenarios.
///
/// The factory runs in Testing environment with the rate-limiter neutralised (int.MaxValue cap),
/// so the warmup registration calls never hit 429.
/// </summary>
internal static class PerfWarmup
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    // ------------------------------------------------------------------
    // Entry point
    // ------------------------------------------------------------------

    public static async Task<PerfContext> InitAsync(
        LearnexiaWebAppFactory factory,
        HttpClient client,
        CancellationToken ct = default)
    {
        // Apply migrations + seed (idempotent).
        await factory.ApplyMigrationsAndSeedAsync();

        var ctx = new PerfContext();

        // 1. Admin JWT — sign in as the seeded superadmin.
        ctx.AdminToken = await SignInAsync(client, PerfConstants.SuperAdminUserName, PerfConstants.SuperAdminPassword, ct);

        // 2. Parent — register + JWT.
        (ctx.ParentToken, ctx.ParentUserId) = await RegisterParentAsync(client, ct);

        // 3. Child (student) — add via parent, then sign in.
        ctx.ChildId = await AddChildAsync(client, ctx.ParentToken, ct);
        ctx.StudentToken = await SignInAsync(client, PerfConstants.ChildEmail, PerfConstants.ChildPassword, ct);
        ctx.StudentUserId = ctx.ChildId;

        // 4. Seed a real curriculum slice (Grade 1 → Subject → Unit → Lesson + questions) directly via
        //    LearningDbContext. The Testing environment does NOT run the Dev curriculum seeder, so the
        //    ForGrade API would return empty and the quiz/learning-read scenarios would be no-ops. This
        //    mirrors P2_06_StartAttempt_Tests.SeedLessonAsync/SeedQuestionsAsync so the hot paths are
        //    actually exercised. Sets ctx.SubjectId + ctx.LessonId.
        await SeedCurriculumViaDbContextAsync(factory, ctx);

        // 5. If we have a lesson, call StartAttempt to get a live attemptId.
        if (ctx.LessonId > 0)
        {
            ctx.AttemptId = await StartAttemptAsync(client, ctx.StudentToken, ctx.LessonId, ct);
        }

        return ctx;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    internal static async Task<string> SignInAsync(
        HttpClient client,
        string userName,
        string password,
        CancellationToken ct = default)
    {
        var body = new { UserName = userName, Password = password };
        var resp = await PostJsonAsync(client, "api/Users/Authentication/Sign-In", body, null, ct);
        var token = resp
            .GetProperty("data")
            .GetProperty("accessToken")
            .GetString()
            ?? throw new InvalidOperationException($"Sign-in for '{userName}' did not return an accessToken.");
        return token;
    }

    private static async Task<(string Token, int UserId)> RegisterParentAsync(
        HttpClient client,
        CancellationToken ct)
    {
        var body = new
        {
            Email        = PerfConstants.ParentEmail,
            Password     = PerfConstants.ParentPassword,
            AcceptedTerms = true,
        };

        JsonElement root;
        try
        {
            root = await PostJsonAsync(client, "api/Users/Authentication/Register-Parent", body, null, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("successed=false"))
        {
            // Parent already registered from a previous run — just sign in.
            var token = await SignInAsync(client, PerfConstants.ParentEmail, PerfConstants.ParentPassword, ct);
            return (token, 0);
        }

        var data    = root.GetProperty("data");
        var tok     = data.GetProperty("accessToken").GetString()!;
        var userId  = data.TryGetProperty("userId", out var uid) ? uid.GetInt32() : 0;
        return (tok, userId);
    }

    private static async Task<int> AddChildAsync(
        HttpClient client,
        string parentToken,
        CancellationToken ct)
    {
        var body = new
        {
            FullName         = "Perf Student",
            Email            = PerfConstants.ChildEmail,
            Password         = PerfConstants.ChildPassword,
            Grade            = 1,
            Language         = "ar",
            Country          = "EG",
            LearningLanguage = "ar",
        };

        JsonElement root;
        try
        {
            root = await PostJsonAsync(client, "api/Parent/Add-Child", body, parentToken, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("successed=false"))
        {
            // Child already exists — discover the childId from My-Children.
            return await GetFirstChildIdAsync(client, parentToken, ct);
        }

        var data = root.GetProperty("data");
        return data.GetProperty("id").GetInt32();
    }

    private static async Task<int> GetFirstChildIdAsync(
        HttpClient client,
        string parentToken,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "api/Parent/My-Children");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);
        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var root = JsonDocument.Parse(body).RootElement;
        var data = root.GetProperty("data");
        if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
            return data[0].GetProperty("id").GetInt32();
        throw new InvalidOperationException("No children found for parent and Add-Child failed. Cannot proceed.");
    }

    /// <summary>
    /// Seeds a real Grade(1)→Subject→Unit→Lesson(Published)+3 MCQ questions directly via
    /// <see cref="LearningDbContext"/> (idempotent: reuses an existing perf subject if present), and
    /// sets <c>ctx.SubjectId</c> + <c>ctx.LessonId</c>. Mirrors
    /// <c>P2_06_StartAttempt_Tests.SeedLessonAsync</c>/<c>SeedQuestionsAsync</c>. Required because the
    /// Testing environment does not run the Dev curriculum seeder — without this the quiz + learning-read
    /// scenarios would be no-ops (lessonId=0). Seeding under Grade Number=1 lets the ForGrade?grade=1
    /// scenario return real data too.
    /// </summary>
    private static async Task SeedCurriculumViaDbContextAsync(LearnexiaWebAppFactory factory, PerfContext ctx)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        // Idempotent: if a prior run already seeded the perf subject, reuse it.
        const string perfSubjectName = "Perf Subject (P6-01)";
        var existing = await db.Subjects
            .Where(s => s.Name == perfSubjectName)
            .Select(s => new { s.Id })
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            ctx.SubjectId = existing.Id;
            var existingLessonId = await db.Lessons
                .Where(l => l.Unit!.SubjectId == existing.Id)
                .OrderBy(l => l.Id)
                .Select(l => l.Id)
                .FirstOrDefaultAsync();
            ctx.LessonId = existingLessonId;
            return;
        }

        // Grade Number=1 so ForGrade?grade=1 (S04) returns this subject. Reuse if one already exists.
        var grade = await db.Grades.FirstOrDefaultAsync(g => g.Number == 1);
        if (grade is null)
        {
            grade = new Grade { Number = 1, DisplayName = "Grade 1 (Perf)", CreatedAt = now, CreatedBy = 0 };
            await db.Grades.AddAsync(grade);
            await db.SaveChangesAsync();
        }

        // Subject + Unit MUST be IsActive + LifecycleState.Published — the student-facing
        // GetActivePublishedSubjectAsync / GetUnitsWithLessonsAsync filters reject Draft/inactive with 404.
        // Language="ar" matches the child's LearningLanguage so the P8 redirect path stays a no-op.
        var subject = new Subject
        {
            Name           = perfSubjectName,
            GradeId        = grade.Id,
            SubjectCode    = SubjectCode.MATH,
            Language       = ContentLanguage.Ar,
            IsActive       = true,
            LifecycleState = LifecycleState.Published,
            CreatedAt      = now,
            CreatedBy      = 0,
        };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var unit = new Unit
        {
            Name           = "Perf Unit",
            SequenceOrder  = 1,
            SubjectId      = subject.Id,
            IsActive       = true,
            LifecycleState = LifecycleState.Published,
            CreatedAt      = now,
            CreatedBy      = 0,
        };
        await db.Units.AddAsync(unit);
        await db.SaveChangesAsync();

        var lesson = new Lesson
        {
            Name           = "Perf Lesson",
            Difficulty     = DifficultyLevel.Easy,
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

        for (var i = 1; i <= 3; i++)
        {
            await db.QuizQuestions.AddAsync(new QuizQuestion
            {
                LessonId       = lesson.Id,
                QuestionType   = QuestionType.MCQ,
                QuestionText   = $"Perf question {i}?",
                Options        = "[\"A\",\"B\",\"C\",\"D\"]",
                CorrectAnswer  = "\"A\"",
                Difficulty     = DifficultyLevel.Easy,
                GeneratedBy    = GeneratedBy.Curated,
                IsActive       = true,
                LifecycleState = LifecycleState.Published,
                CreatedAt      = now,
                CreatedBy      = 0,
            });
        }
        await db.SaveChangesAsync();

        ctx.SubjectId = subject.Id;
        ctx.LessonId = lesson.Id;
    }

    private static async Task<int> StartAttemptAsync(
        HttpClient client,
        string studentToken,
        int lessonId,
        CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"api/Learning/Quizzes/{lessonId}/Attempt");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            return 0;

        var root = JsonDocument.Parse(body).RootElement;
        if (!root.TryGetProperty("data", out var data))
            return 0;
        if (!data.TryGetProperty("attemptId", out var attemptIdProp))
            return 0;

        var attemptId = attemptIdProp.GetInt32();

        // Also capture the first questionId for SubmitAnswer / Hint scenarios.
        if (data.TryGetProperty("questions", out var questionsEl)
            && questionsEl.ValueKind == JsonValueKind.Array
            && questionsEl.GetArrayLength() > 0)
        {
            // We'll return the questionId via a separate mechanism; caller must read ctx.QuestionId.
            // We store it in context — but we only have the local return here.
            // A second pass by the caller sets ctx.QuestionId after this method returns.
            // We embed the first questionId in the return value by encoding it: not clean.
            // Instead: the caller should re-parse. For simplicity, we expose a shared method.
        }

        return attemptId;
    }

    /// <summary>
    /// Calls StartAttempt and populates both <c>ctx.AttemptId</c> and <c>ctx.QuestionId</c>.
    /// </summary>
    internal static async Task PopulateAttemptAsync(
        HttpClient client,
        PerfContext ctx,
        CancellationToken ct = default)
    {
        if (ctx.LessonId <= 0) return;

        var req = new HttpRequestMessage(HttpMethod.Post, $"api/Learning/Quizzes/{ctx.LessonId}/Attempt");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.StudentToken);
        var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode) return;

        var root = JsonDocument.Parse(body).RootElement;
        if (!root.TryGetProperty("data", out var data)) return;

        if (data.TryGetProperty("attemptId", out var attemptIdProp))
            ctx.AttemptId = attemptIdProp.GetInt32();

        if (data.TryGetProperty("questions", out var questionsEl)
            && questionsEl.ValueKind == JsonValueKind.Array
            && questionsEl.GetArrayLength() > 0)
        {
            ctx.QuestionId = questionsEl[0].GetProperty("id").GetInt32();
        }
    }

    // ------------------------------------------------------------------
    // Low-level HTTP helper
    // ------------------------------------------------------------------

    private static async Task<JsonElement> PostJsonAsync(
        HttpClient client,
        string url,
        object body,
        string? bearerToken,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOpts),
                Encoding.UTF8,
                "application/json")
        };
        if (bearerToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var response = await client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(responseBody))
            throw new InvalidOperationException($"POST {url} returned empty body (HTTP {(int)response.StatusCode}).");

        var root = JsonDocument.Parse(responseBody).RootElement;

        if (root.TryGetProperty("successed", out var succeedProp) && !succeedProp.GetBoolean())
        {
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "(no message)";
            throw new InvalidOperationException(
                $"POST {url} returned successed=false. Message: {msg}. Body: {responseBody}");
        }

        return root;
    }
}
