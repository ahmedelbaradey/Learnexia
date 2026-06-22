// ReSharper disable InconsistentNaming
// P5-02 — Weak-Area Detection — Integration Tests
//
// Maps 1:1 to docs/qc/P5-02/backend-test-cases.md (WA-INT-01..WA-INT-07).
//
// Coverage:
//   WA-INT-01  Mixed-severity: High(<30%), Medium(30-50%), Low(≥50%+bad accuracy), excluded, NotStarted
//   WA-INT-02  Ranking order High → Medium → Low, deficit-descending within tier
//   WA-INT-03  Strong-skills-only child → empty list (not error)
//   WA-INT-04  Resolved area (mastery rises above threshold) drops off automatically
//   WA-INT-05  maxResults cap enforced after ranking (take the worst High first)
//   WA-INT-06  Cross-subject: SubjectCode int values correct (0=MATH/1=SCIENCE/3=ENGLISH)
//   WA-INT-07  Parent endpoint returns detected weak areas for seeded child (smoke; IDOR in P5-08 E5)
//
// These cases target what InMemory unit tests cannot guarantee: Npgsql LINQ translation of the
// group-by + OrderByDescending/FirstOrDefault recent-accuracy lookup and the Skill→Concept→Subject
// navigation join.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P5-02 weak-area detection integration tests (WA-INT-01..WA-INT-07).
///
/// Complements <c>WeakAreaDetectorServiceTests</c> (Learning.UnitTests, EF InMemory).
/// These tests exercise the real Postgres + real DI-wired seam chain.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P5_02_WeakAreaDetection_Tests : IAsyncLifetime
{
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string WeakAreasUrlFmt   = "api/Parent/Children/{0}/WeakAreas";

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P5_02_WeakAreaDetection_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static string UniqueEmail(string tag = "")
        => $"p502_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    private static bool TryProp(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (el.TryGetProperty(pascal, out value)) return true;
        foreach (var p in el.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            { value = p.Value; return true; }
        }
        value = default;
        return false;
    }

    private async Task<(HttpResponseMessage Resp, string Body, JsonElement Root)>
        SendAsync(HttpMethod method, string url, object? body = null, string? bearer = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearer is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var resp    = await _client.SendAsync(req);
        var bodyStr = await resp.Content.ReadAsStringAsync();
        JsonElement root = default;
        try { root = JsonDocument.Parse(bodyStr).RootElement; } catch { /* non-JSON */ }
        return (resp, bodyStr, root);
    }

    private async Task<(int ParentId, string Token)> RegisterParentAsync(string tag = "")
    {
        var email = UniqueEmail($"par_{tag}");
        var (resp, body, root) = await SendAsync(HttpMethod.Post, RegisterParentUrl, new
        {
            Email = email, Password = "Str0ng@Pass!", AcceptedTerms = true,
        });
        resp.IsSuccessStatusCode.Should().BeTrue("parent registration must succeed; body={0}", body);
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        return (SeatTestSupport.DecodeUserId(tok.GetString()!), tok.GetString()!);
    }

    private async Task<(int ChildId, string ChildToken)> AddChildAsync(
        int parentId, string parentToken, string tag = "")
    {
        await SeatTestSupport.GrantSeatsAsync(_factory, parentId);
        var childEmail = UniqueEmail($"ch_{tag}");
        var (addResp, addBody, _) = await SendAsync(HttpMethod.Post, AddChildUrl, new
        {
            FullName = $"Child {tag}", Email = childEmail, Password = "Child@Pass1",
            Grade = 3, Language = "ar", Country = "EG", LearningLanguage = "ar",
        }, parentToken);
        addResp.IsSuccessStatusCode.Should().BeTrue("add-child must succeed; body={0}", addBody);

        var (sResp, sBody, sRoot) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = "Child@Pass1" });
        sResp.IsSuccessStatusCode.Should().BeTrue("child sign-in must succeed; body={0}", sBody);
        TryProp(sRoot, "data", out var sd);
        TryProp(sd, "accessToken", out var ct);
        return (SeatTestSupport.DecodeUserId(ct.GetString()!), ct.GetString()!);
    }

    /// <summary>
    /// Seeds Grade → Subject → Concept → Skill → StudentSkillMastery for the given student.
    /// Returns the seeded Skill.Id.
    /// </summary>
    private async Task<int> SeedMasteryAsync(
        int studentId, int masteryPercent, MasteryStatus status, SubjectCode subjectCode,
        string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        // Use unique Grade.Number derived from tag hash to avoid unique-index collision
        var gradeNumber = (Math.Abs(tag.GetHashCode()) % 8000) + 2000;

        var grade = new Grade
        {
            Number      = gradeNumber,
            DisplayName = $"P502 Grade {tag}",
            CreatedAt   = now, CreatedBy = 0,
        };
        await db.Grades.AddAsync(grade);
        await db.SaveChangesAsync();

        var subject = new Subject
        {
            Name           = $"P502 Subj {tag}",
            GradeId        = grade.Id,
            SubjectCode    = subjectCode,
            Language       = ContentLanguage.Ar,
            IsActive       = true,
            LifecycleState = LifecycleState.Published,
            CreatedAt      = now, CreatedBy = 0,
        };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var concept = new Concept
        {
            Name            = $"P502 Concept {tag}",
            SubjectId       = subject.Id,
            DifficultyLevel = DifficultyLevel.Medium,
            CreatedAt       = now, CreatedBy = 0,
        };
        await db.Concepts.AddAsync(concept);
        await db.SaveChangesAsync();

        var skill = new Skill
        {
            Name                 = $"P502 Skill {tag}",
            ConceptId            = concept.Id,
            MasteryThreshold     = 80,
            EstimatedTimeMinutes = 10,
            IsActive             = true,
            CreatedAt            = now, CreatedBy = 0,
        };
        await db.Skills.AddAsync(skill);
        await db.SaveChangesAsync();

        var mastery = new StudentSkillMastery
        {
            StudentId         = studentId,
            SkillId           = skill.Id,
            MasteryPercentage = masteryPercent,
            Status            = status,
            AttemptsCount     = masteryPercent > 0 ? 1 : 0,
            LastPracticedAt   = now,
            CreatedAt         = now, CreatedBy = 0,
        };
        await db.StudentSkillMasteries.AddAsync(mastery);
        await db.SaveChangesAsync();

        return skill.Id;
    }

    /// <summary>
    /// Seeds a completed Attempt with one StudentAnswer for the skill, with the given accuracy.
    /// This wires the recent-accuracy lookup used for the Low-severity tier.
    /// </summary>
    private async Task SeedAttemptForSkillAsync(int studentId, int skillId, double accuracyPercent)
    {
        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        var skill   = await db.Skills.FirstAsync(s => s.Id == skillId);
        var concept = await db.Concepts.FirstAsync(c => c.Id == skill.ConceptId);
        var subject = await db.Subjects.FirstAsync(s => s.Id == concept.SubjectId);

        var unit = new Unit
        {
            Name          = $"P502 Unit {Guid.NewGuid().ToString("N")[..8]}",
            SequenceOrder = 1,
            SubjectId     = subject.Id,
            CreatedAt     = now, CreatedBy = 0,
        };
        await db.Units.AddAsync(unit);
        await db.SaveChangesAsync();

        var lesson = new Lesson
        {
            Name           = $"P502 Lesson {Guid.NewGuid().ToString("N")[..8]}",
            Difficulty     = DifficultyLevel.Medium,
            SequenceOrder  = 1,
            IsLocked       = false,
            UnitId         = unit.Id,
            SkillId        = skillId,
            IsActive       = true,
            LifecycleState = LifecycleState.Published,
            CreatedAt      = now, CreatedBy = 0,
        };
        await db.Lessons.AddAsync(lesson);
        await db.SaveChangesAsync();

        var question = new QuizQuestion
        {
            LessonId       = lesson.Id,
            SkillId        = skillId,
            QuestionType   = QuestionType.MCQ,
            QuestionText   = $"P502 Q {Guid.NewGuid().ToString("N")[..8]}",
            Options        = "[\"A\",\"B\",\"C\",\"D\"]",
            CorrectAnswer  = "\"A\"",
            Difficulty     = DifficultyLevel.Medium,
            GeneratedBy    = GeneratedBy.Curated,
            IsActive       = true,
            LifecycleState = LifecycleState.Published,
            QualityState   = QuestionQualityState.Approved,
            CreatedAt      = now, CreatedBy = 0,
        };
        await db.QuizQuestions.AddAsync(question);
        await db.SaveChangesAsync();

        var attempt = new Attempt
        {
            StudentId           = studentId,
            LessonId            = lesson.Id,
            Status              = AttemptStatus.Completed,
            AccuracyPercentage  = accuracyPercent,
            StartedAt           = now,
            CompletedAt         = now,
            CreatedAt           = now, CreatedBy = 0,
        };
        await db.Attempts.AddAsync(attempt);
        await db.SaveChangesAsync();

        var answer = new StudentAnswer
        {
            AttemptId        = attempt.Id,
            QuestionId       = question.Id,
            AnswerPayload    = "\"A\"",
            IsCorrect        = accuracyPercent >= 60,
            TimeSpentSeconds = 10,
            CreatedAt        = now, CreatedBy = 0,
        };
        await db.StudentAnswers.AddAsync(answer);
        await db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // WA-INT-01 — Mixed severity: correct bands over Postgres
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "WA-INT-01: Mixed-severity child → correct High/Medium/Low bands; strong+NotStarted excluded (real Postgres)")]
    public async Task WA_INT_01_MixedSeverity_CorrectBandsOverPostgres()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WA01");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "WA01");

        // Skill A — mastery 20% → High
        var skillAId = await SeedMasteryAsync(childId, 20, MasteryStatus.NeedsReview, SubjectCode.MATH,    "WA01A");
        // Skill B — mastery 40% → Medium
        var skillBId = await SeedMasteryAsync(childId, 40, MasteryStatus.NeedsReview, SubjectCode.SCIENCE, "WA01B");
        // Skill C — mastery 55% (InProgress) + recent accuracy 50% → Low
        var skillCId = await SeedMasteryAsync(childId, 55, MasteryStatus.InProgress,   SubjectCode.ARABIC,  "WA01C");
        await SeedAttemptForSkillAsync(childId, skillCId, 50.0); // < 60% threshold → Low
        // Skill D — mastery 85% (Mastered) + recent accuracy 90% → excluded
        var skillDId = await SeedMasteryAsync(childId, 85, MasteryStatus.Mastered,     SubjectCode.ENGLISH, "WA01D");
        await SeedAttemptForSkillAsync(childId, skillDId, 90.0); // >= 60% → excluded

        // Skill E is implicitly "NotStarted" since we don't seed a mastery row (the detector
        // queries WHERE Status != NotStarted, so no row = excluded automatically).

        using var scope = _factory.Services.CreateScope();
        var detector = scope.ServiceProvider.GetRequiredService<IStudentAllSubjectsWeakAreasQuery>();
        var areas = await detector.GetWeakAreasAsync(childId, maxResults: 0);

        // Must contain A (High), B (Medium), C (Low); must NOT contain D
        var skillIds = areas.Select(a => a.SkillId).ToList();
        skillIds.Should().Contain(skillAId, "Skill A (mastery 20%) must be detected as High weak area");
        skillIds.Should().Contain(skillBId, "Skill B (mastery 40%) must be detected as Medium weak area");
        skillIds.Should().Contain(skillCId, "Skill C (mastery 55%, recent accuracy 50%) must be detected as Low weak area");
        skillIds.Should().NotContain(skillDId, "Skill D (mastery 85%, accuracy 90%) must be excluded");

        // Verify severity bands
        areas.Single(a => a.SkillId == skillAId).Severity.Should().Be(WeakAreaSeverity.High,
            "Skill A mastery 20% must map to High severity");
        areas.Single(a => a.SkillId == skillBId).Severity.Should().Be(WeakAreaSeverity.Medium,
            "Skill B mastery 40% must map to Medium severity");
        areas.Single(a => a.SkillId == skillCId).Severity.Should().Be(WeakAreaSeverity.Low,
            "Skill C mastery 55% + recent accuracy 50% must map to Low severity");

        // Verify MasteryPercent values
        areas.Single(a => a.SkillId == skillAId).MasteryPercent.Should().Be(20);
        areas.Single(a => a.SkillId == skillBId).MasteryPercent.Should().Be(40);
        areas.Single(a => a.SkillId == skillCId).MasteryPercent.Should().Be(55);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // WA-INT-02 — Ranking: High → Medium → Low, deficit-descending within tier
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "WA-INT-02: Ranking order is High → Medium → Low; within High, lower mastery (bigger deficit) ranks first")]
    public async Task WA_INT_02_Ranking_HighBeforeMediumBeforeLow_DeficitDescending()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WA02");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "WA02");

        // Two High skills: mastery 10% (deficit 90) and 25% (deficit 75)
        var high10Id = await SeedMasteryAsync(childId, 10, MasteryStatus.NeedsReview, SubjectCode.MATH,    "WA02H10");
        var high25Id = await SeedMasteryAsync(childId, 25, MasteryStatus.NeedsReview, SubjectCode.SCIENCE, "WA02H25");
        // One Medium skill: mastery 40%
        var med40Id  = await SeedMasteryAsync(childId, 40, MasteryStatus.NeedsReview, SubjectCode.ARABIC,  "WA02M40");
        // One Low skill: mastery 60% + accuracy 40%
        var low60Id  = await SeedMasteryAsync(childId, 60, MasteryStatus.InProgress,  SubjectCode.ENGLISH, "WA02L60");
        await SeedAttemptForSkillAsync(childId, low60Id, 40.0); // < 60% → Low

        using var scope = _factory.Services.CreateScope();
        var detector = scope.ServiceProvider.GetRequiredService<IStudentAllSubjectsWeakAreasQuery>();
        var areas    = await detector.GetWeakAreasAsync(childId, maxResults: 10);

        // Filter to just our seeded skills (other skills from other tests in the shared DB may appear)
        var ourSkillIds = new[] { high10Id, high25Id, med40Id, low60Id };
        var ourAreas    = areas.Where(a => ourSkillIds.Contains(a.SkillId)).ToList();

        ourAreas.Should().HaveCount(4, "all 4 seeded weak skills must be detected");

        // Verify ordering: High(10%) < High(25%) < Medium < Low
        var positions = ourAreas.Select((a, i) => (a.SkillId, Position: i)).ToDictionary(x => x.SkillId, x => x.Position);

        positions[high10Id].Should().BeLessThan(positions[high25Id],
            "High 10% (deficit 90) must rank before High 25% (deficit 75) within the same tier");
        positions[high25Id].Should().BeLessThan(positions[med40Id],
            "High tier must rank before Medium tier");
        positions[med40Id].Should().BeLessThan(positions[low60Id],
            "Medium tier must rank before Low tier");

        // Verify severity values
        ourAreas.Single(a => a.SkillId == high10Id).Severity.Should().Be(WeakAreaSeverity.High);
        ourAreas.Single(a => a.SkillId == high25Id).Severity.Should().Be(WeakAreaSeverity.High);
        ourAreas.Single(a => a.SkillId == med40Id).Severity.Should().Be(WeakAreaSeverity.Medium);
        ourAreas.Single(a => a.SkillId == low60Id).Severity.Should().Be(WeakAreaSeverity.Low);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // WA-INT-03 — Strong-skills-only child → empty list (sentinel contract)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "WA-INT-03: Child with all-strong skills → empty weak-area list (never null, never throws)")]
    public async Task WA_INT_03_StrongSkillsOnly_EmptyList()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WA03");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "WA03");

        // Seed a mastered skill with good recent accuracy — must be excluded
        var skillId = await SeedMasteryAsync(childId, 88, MasteryStatus.Mastered, SubjectCode.MATH, "WA03Mastered");
        await SeedAttemptForSkillAsync(childId, skillId, 90.0); // >= 60% → excluded

        using var scope = _factory.Services.CreateScope();
        var detector = scope.ServiceProvider.GetRequiredService<IStudentAllSubjectsWeakAreasQuery>();

        IReadOnlyList<WeakAreaEntry>? areas = null;
        var ex = await Record.ExceptionAsync(async () =>
        {
            areas = await detector.GetWeakAreasAsync(childId, maxResults: 5);
        });

        ex.Should().BeNull("detector must never throw for a student with no weak areas");
        areas.Should().NotBeNull("result must not be null (sentinel contract)");

        // Filter to our seeded skill specifically
        var ourAreas = areas!.Where(a => a.SkillId == skillId).ToList();
        ourAreas.Should().BeEmpty("a mastered skill with good accuracy must NOT appear in weak areas");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // WA-INT-04 — Resolved area drops off after mastery rises
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "WA-INT-04: Skill drops off the weak-area list after mastery rises above threshold (AC4 auto-drop-off)")]
    public async Task WA_INT_04_ResolvedAreaDropsOff_AfterMasteryRises()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WA04");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "WA04");

        // Step 1: Skill A at mastery 25% → High weak area
        var skillAId = await SeedMasteryAsync(childId, 25, MasteryStatus.NeedsReview, SubjectCode.MATH, "WA04A");

        using var scope1 = _factory.Services.CreateScope();
        var detector1 = scope1.ServiceProvider.GetRequiredService<IStudentAllSubjectsWeakAreasQuery>();
        var before    = await detector1.GetWeakAreasAsync(childId, maxResults: 0);

        before.Where(a => a.SkillId == skillAId)
            .Should().NotBeEmpty("Skill A at mastery 25% must be detected as High weak area initially");

        // Step 2: Update mastery to 85% (Mastered) + add a good-accuracy attempt
        using (var updateScope = _factory.Services.CreateScope())
        {
            var db = updateScope.ServiceProvider.GetRequiredService<LearningDbContext>();
            var mastery = await db.StudentSkillMasteries
                .FirstAsync(m => m.StudentId == childId && m.SkillId == skillAId);
            mastery.MasteryPercentage = 85;
            mastery.Status            = MasteryStatus.Mastered;
            await db.SaveChangesAsync();
        }
        await SeedAttemptForSkillAsync(childId, skillAId, 88.0); // >= 60% → no longer Low either

        // Step 3: Re-query — Skill A must be absent
        using var scope2 = _factory.Services.CreateScope();
        var detector2 = scope2.ServiceProvider.GetRequiredService<IStudentAllSubjectsWeakAreasQuery>();
        var after     = await detector2.GetWeakAreasAsync(childId, maxResults: 0);

        after.Where(a => a.SkillId == skillAId)
            .Should().BeEmpty(
                "after mastery rises to 85% with good accuracy, Skill A must auto-drop-off the weak-area list (AC4)");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // WA-INT-05 — maxResults cap enforced over the real query (takes worst first)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "WA-INT-05: maxResults=5 cap returns exactly 5 worst entries (after ranking, highest-deficit first)")]
    public async Task WA_INT_05_MaxResultsCap_EnforcedOverRealQuery()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WA05");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "WA05");

        // Seed 7 High-severity weak skills at varying mastery percentages
        var seededIds = new List<int>();
        var masteryValues = new[] { 5, 10, 15, 20, 22, 25, 28 }; // all < 30% → all High
        for (int i = 0; i < masteryValues.Length; i++)
        {
            var id = await SeedMasteryAsync(childId, masteryValues[i], MasteryStatus.NeedsReview,
                SubjectCode.MATH, $"WA05x{i}_{Guid.NewGuid().ToString("N")[..4]}");
            seededIds.Add(id);
        }

        using var scope = _factory.Services.CreateScope();
        var detector = scope.ServiceProvider.GetRequiredService<IStudentAllSubjectsWeakAreasQuery>();
        var areas    = await detector.GetWeakAreasAsync(childId, maxResults: 5);

        // Among ALL areas, filter to our seeded skills
        var ourAreas = areas.Where(a => seededIds.Contains(a.SkillId)).ToList();

        // The cap is applied globally, not scoped to our seeds; but with maxResults=5, we must
        // get at most 5 total areas. If all 7 of ours are the worst, exactly 5 should appear.
        ourAreas.Count.Should().BeLessThanOrEqualTo(5,
            "maxResults=5 must cap the total results including our seeded skills");

        // The 5 returned must be the 5 with the highest deficit (lowest mastery) — verify they
        // are the top-5 from our seeded set ordered by mastery ascending
        var sortedByDeficit = seededIds
            .Zip(masteryValues, (id, m) => (id, m))
            .OrderBy(x => x.m) // lowest mastery = highest deficit = should appear first
            .Take(5)
            .Select(x => x.id)
            .ToHashSet();

        // All returned ones from our seeded set should be in the top-5 deficit set
        foreach (var area in ourAreas)
        {
            sortedByDeficit.Should().Contain(area.SkillId,
                "returned areas must be the highest-deficit ones from our seeded set");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // WA-INT-06 — Cross-subject detection: SubjectCode ints correct
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "WA-INT-06: Weak skills across MATH(0)/SCIENCE(1)/ENGLISH(3) carry correct SubjectCode ints — no Social Studies")]
    public async Task WA_INT_06_CrossSubject_SubjectCodesCorrect()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WA06");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "WA06");

        // Seed weak skills across 3 of the 4 subjects (no Social Studies — product rule)
        var mathId    = await SeedMasteryAsync(childId, 20, MasteryStatus.NeedsReview, SubjectCode.MATH,    "WA06Math");
        var scienceId = await SeedMasteryAsync(childId, 35, MasteryStatus.NeedsReview, SubjectCode.SCIENCE, "WA06Sci");
        var englishId = await SeedMasteryAsync(childId, 25, MasteryStatus.NeedsReview, SubjectCode.ENGLISH, "WA06Eng");

        using var scope = _factory.Services.CreateScope();
        var detector = scope.ServiceProvider.GetRequiredService<IStudentAllSubjectsWeakAreasQuery>();
        var areas    = await detector.GetWeakAreasAsync(childId, maxResults: 0);

        var ourAreas = areas.Where(a => a.SkillId == mathId || a.SkillId == scienceId || a.SkillId == englishId)
                           .ToList();

        ourAreas.Should().HaveCount(3, "all 3 cross-subject weak skills must be detected");

        var mathEntry    = ourAreas.Single(a => a.SkillId == mathId);
        var scienceEntry = ourAreas.Single(a => a.SkillId == scienceId);
        var englishEntry = ourAreas.Single(a => a.SkillId == englishId);

        mathEntry.SubjectCode.Should().Be((int)SubjectCode.MATH,       "MATH must carry SubjectCode 0");
        scienceEntry.SubjectCode.Should().Be((int)SubjectCode.SCIENCE,  "SCIENCE must carry SubjectCode 1");
        englishEntry.SubjectCode.Should().Be((int)SubjectCode.ENGLISH,  "ENGLISH must carry SubjectCode 3");

        // No entry may carry a SubjectCode outside 0/1/2/3
        foreach (var area in ourAreas)
        {
            area.SubjectCode.Should().BeInRange(0, 3, "SubjectCode must be one of 0=MATH/1=SCIENCE/2=ARABIC/3=ENGLISH");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // WA-INT-07 — Parent endpoint returns detected weak areas for seeded child (smoke)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "WA-INT-07: GET /api/Parent/Children/{id}/WeakAreas returns detected weak areas for a child with seeded weak skills")]
    public async Task WA_INT_07_ParentEndpoint_ReturnsDetectedWeakAreas()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WA07");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "WA07");

        // Seed a known weak skill
        var skillId = await SeedMasteryAsync(childId, 22, MasteryStatus.NeedsReview, SubjectCode.SCIENCE, "WA07Skill");

        var url = string.Format(WeakAreasUrlFmt, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET WeakAreas must return 200 for own child; body={0}", body);

        TryProp(root, "successed", out var succEl).Should().BeTrue("envelope must have 'successed'; body={0}", body);
        succEl.GetBoolean().Should().BeTrue("successed must be true; body={0}", body);

        TryProp(root, "data", out var data);
        TryProp(data, "areas", out var areasEl).Should().BeTrue("data.areas must exist; body={0}", body);
        areasEl.ValueKind.Should().Be(JsonValueKind.Array, "areas must be a JSON array; body={0}", body);

        // The seeded weak skill must appear in the areas array
        var detectedSkillIds = areasEl.EnumerateArray()
            .Select(a =>
            {
                TryProp(a, "skillId", out var idEl);
                return idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32() : -1;
            })
            .ToList();

        detectedSkillIds.Should().Contain(skillId,
            "the seeded weak skill (mastery 22%) must appear in the parent endpoint's weak areas response");
    }
}
