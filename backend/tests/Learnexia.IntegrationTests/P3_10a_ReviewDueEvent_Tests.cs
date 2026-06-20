using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Jobs;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

// ============================================================================
// Capturing / spy infrastructure — test-local INotificationHandler + IPublisher
// ============================================================================

/// <summary>
/// Capturing MediatR notification handler for <see cref="ReviewDueIntegrationEvent"/>.
/// Static <see cref="Captured"/> ConcurrentBag is thread-safe and must be cleared
/// in each test's <c>InitializeAsync</c>.
/// </summary>
public sealed class CapturingReviewDueHandler
    : INotificationHandler<ReviewDueIntegrationEvent>
{
    public static readonly ConcurrentBag<ReviewDueIntegrationEvent> Captured = new();

    public Task Handle(ReviewDueIntegrationEvent e, CancellationToken ct)
    {
        Captured.Add(e);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Deliberately-throwing MediatR notification handler for <see cref="ReviewDueIntegrationEvent"/>.
/// Used by T5 (fail-soft).
///
/// Architecture note — TWO layers of fail-soft in this pipeline:
///
///   Layer 1 (handler-level): The host uses <c>IsolatedNotificationPublisher</c> (ADR 0002 §4).
///     When this handler throws, <c>IsolatedNotificationPublisher</c> catches the exception
///     and continues to fan out to the next handler (e.g. <see cref="CapturingReviewDueHandler"/>).
///     The <c>publisher.Publish()</c> call in the sweep job does NOT re-throw.
///     → T5 verifies that both <see cref="CapturingReviewDueHandler"/> still fires for ALL students
///       AND that the sweep job completes without exception even when a handler throws.
///
///   Layer 2 (student-group-level, sweep job): If <c>publisher.Publish()</c> itself throws
///     (not the handler, but the publisher — e.g. a DI failure, framework bug, or if the host
///     were configured with a re-throwing publisher), the sweep job's per-student try/catch
///     (SpacedRepetitionSweepJob.cs lines 139–174) catches it, logs it, and continues to the
///     next student. This layer cannot be easily exercised without replacing IPublisher in DI
///     (complex, risks unregistering MediatR's Mediator); therefore Layer 2 is verified by
///     reading the code + the existing unit pattern, and Layer 1 is the integration proof.
/// </summary>
public sealed class ThrowingReviewDueHandler
    : INotificationHandler<ReviewDueIntegrationEvent>
{
    public Task Handle(ReviewDueIntegrationEvent e, CancellationToken ct)
        => throw new InvalidOperationException(
            "P3-10a T5: deliberate throw from ThrowingReviewDueHandler to test fail-soft");
}

/// <summary>
/// P3-10a integration tests — "ReviewDueIntegrationEvent fires correctly from the SR sweep".
///
/// Tests the P3-10 addition to <see cref="SpacedRepetitionSweepJob.RunAsync"/>: after the
/// per-row SR column update loop, it publishes one <see cref="ReviewDueIntegrationEvent"/>
/// per eligible student (AC1, AC3–AC6).
///
/// This is a Hangfire job integration test — NOT an HTTP-endpoint test.
/// The job is invoked directly via DI (<c>job.RunAsync()</c>) following the pattern established
/// by <see cref="P3_10_SpacedRepetition_Tests.RunSweepJobAsync"/>.
///
/// Event capture uses <see cref="CapturingReviewDueHandler"/> (a static-bag INotificationHandler)
/// registered in a forked <see cref="LearnexiaWebAppFactory"/> via <c>WithWebHostBuilder</c>,
/// following the pattern in <see cref="P2_07_InstantAnswerFeedback_Tests"/>.
///
/// Coverage:
///   T1 — fires exactly once per eligible student (2 students → 2 events, correct StudentId + DueCount)
///   T2 — zero events when no due rows
///   T3 — payload correctness: SkillName, EstimatedTimeMinutes, DueCount, UTC kind, no PII
///   T4 — idempotent / non-spammy: second run produces same per-student digest
///   T5 — fail-soft: publish throw for student A does not abort sweep; student B event still published
///   T6 — top-N ordering + cap at 3: NeedsReview ranked first, cap enforced, most-overdue first
/// </summary>
[Collection("IntegrationTests")]
public sealed class P3_10a_ReviewDueEvent_Tests : IAsyncLifetime
{
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    private readonly LearnexiaWebAppFactory _factory;

    // Fork of the shared factory that adds the capturing handler to DI.
    // The capturing handler uses a static ConcurrentBag so tests can assert on it.
    // IMPORTANT: RunSweepJobAsync() must use _fork.Services (not _factory.Services) so the job's
    // IPublisher is the fork's publisher which has CapturingReviewDueHandler registered.
    private readonly WebApplicationFactory<Program> _fork;
    private readonly HttpClient _client;

    public P3_10a_ReviewDueEvent_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;

        _fork = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<
                    INotificationHandler<ReviewDueIntegrationEvent>,
                    CapturingReviewDueHandler>();
            });
        });

        _client = _fork.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        // Clear the capturing bag so each test starts from a clean state.
        CapturingReviewDueHandler.Captured.Clear();
    }

    public Task DisposeAsync()
    {
        CapturingReviewDueHandler.Captured.Clear();
        return Task.CompletedTask;
    }

    // =========================================================================
    // Helpers — mirrors P3_10_SpacedRepetition_Tests exactly
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p310a_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>Case-insensitive JSON property lookup — handles camelCase and PascalCase.</summary>
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
        SendAsync(HttpClient client, HttpMethod method, string url,
            object? body = null, string? bearerToken = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearerToken is not null)
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

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

    /// <summary>
    /// Creates a parent + child via HTTP and returns the student (child) identity user Id.
    /// </summary>
    private async Task<int> CreateStudentAsync(string tag)
    {
        var parentEmail = UniqueEmail($"par_{tag}");
        var (regResp, regRoot, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"parent registration must succeed; body: {regBody}");
        TryProp(regRoot, "data", out var regData).Should().BeTrue($"body: {regBody}");
        TryProp(regData, "accessToken", out var parentTokProp).Should().BeTrue($"body: {regBody}");
        var parentToken = parentTokProp.GetString()!;

        var childEmail = UniqueEmail($"chd_{tag}");
        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = $"P310a Student {tag}",
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
        TryProp(addRoot, "data", out var addData).Should().BeTrue($"body: {addBody}");
        TryProp(addData, "id", out var idProp).Should().BeTrue($"body: {addBody}");
        return idProp.GetInt32();
    }

    /// <summary>
    /// Seeds a Skill (with EstimatedTimeMinutes) directly into LearningDbContext.
    /// Returns the Skill.Id.
    /// </summary>
    private async Task<int> SeedSkillWithTimeAsync(string name, int estimatedMinutes = 10)
    {
        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        var grade = new Grade
        {
            Number      = 99,
            DisplayName = $"P310a_G_{Guid.NewGuid():N}",
            CreatedAt   = now,
            CreatedBy   = 0
        };
        await db.Grades.AddAsync(grade);
        await db.SaveChangesAsync();

        var subject = new Subject
        {
            Name      = $"P310a_Subj_{Guid.NewGuid():N}",
            GradeId   = grade.Id,
            CreatedAt = now,
            CreatedBy = 0
        };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var concept = new Concept
        {
            Name            = $"P310a_Concept_{Guid.NewGuid():N}",
            DifficultyLevel = DifficultyLevel.Easy,
            SubjectId       = subject.Id,
            CreatedAt       = now,
            CreatedBy       = 0
        };
        await db.Concepts.AddAsync(concept);
        await db.SaveChangesAsync();

        var skill = new Skill
        {
            Name                 = name,
            MasteryThreshold     = 80,
            EstimatedTimeMinutes = estimatedMinutes,
            ConceptId            = concept.Id,
            CreatedAt            = now,
            CreatedBy            = 0
        };
        await db.Skills.AddAsync(skill);
        await db.SaveChangesAsync();

        return skill.Id;
    }

    /// <summary>
    /// Seeds a StudentSkillMastery row directly for the given (studentId, skillId).
    /// <paramref name="nextReviewDueAt"/>: null for NeedsReview (always due);
    ///   past DateTime for Mastered-stale (due); future DateTime for Mastered-not-yet-due (not due).
    /// </summary>
    private async Task SeedMasteryRowAsync(
        int studentId,
        int skillId,
        MasteryStatus status,
        DateTime? nextReviewDueAt,
        int reviewIntervalDays = 7,
        int repetitionNumber   = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        // Use upsert-style: remove any existing row (tests run in a shared DB).
        var existing = await db.StudentSkillMasteries
            .FirstOrDefaultAsync(m => m.StudentId == studentId && m.SkillId == skillId);

        if (existing is not null)
        {
            existing.Status             = status;
            existing.MasteryPercentage  = status == MasteryStatus.Mastered ? 90 : 20;
            existing.LastPracticedAt    = now.AddDays(-10);
            existing.ReviewIntervalDays = reviewIntervalDays;
            existing.NextReviewDueAt    = nextReviewDueAt;
            existing.RepetitionNumber   = repetitionNumber;
            db.StudentSkillMasteries.Update(existing);
        }
        else
        {
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
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Resolves <see cref="SpacedRepetitionSweepJob"/> from the <em>forked</em> factory's DI scope
    /// and invokes RunAsync directly (no Hangfire cron needed in tests).
    ///
    /// CRITICAL: must use <c>_fork.Services</c> (not <c>_factory.Services</c>) so that the job's
    /// <c>IServiceScopeFactory</c> — and therefore its <c>IPublisher</c> — is the fork's publisher,
    /// which has <see cref="CapturingReviewDueHandler"/> registered. Using the base factory's scope
    /// would give the job a publisher with no test handler, so events would fire but not be captured.
    ///
    /// Note: DB seeding helpers (<see cref="SeedSkillWithTimeAsync"/>, <see cref="SeedMasteryRowAsync"/>)
    /// still use <c>_factory.Services</c> to access <see cref="LearningDbContext"/> — this is fine
    /// because all forks share the same underlying Testcontainers Postgres instance.
    /// </summary>
    private async Task RunSweepJobAsync()
    {
        using var scope = _fork.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<SpacedRepetitionSweepJob>();
        await job.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// Waits briefly for any still-in-flight async handlers to complete.
    /// The MediatR publish inside the sweep job is awaited, so in practice no extra delay
    /// is needed — but 50 ms guards against any test-container latency.
    /// </summary>
    private static Task SettleAsync() => Task.Delay(50);

    // =========================================================================
    // T1 — fires exactly once per eligible student
    // =========================================================================

    [Fact(DisplayName = "P310a-T1 Sweep fires exactly one ReviewDueIntegrationEvent per eligible student (2 students → 2 events, correct DueCount each)")]
    public async Task T1_FiresOncePerEligibleStudent_TwoStudentsTwoEvents()
    {
        // Arrange: student A with 2 due skills, student B with 1 due skill.
        var studentIdA = await CreateStudentAsync("t1a");
        var studentIdB = await CreateStudentAsync("t1b");

        var skillA1 = await SeedSkillWithTimeAsync($"P310a-T1-SkillA1-{Guid.NewGuid():N}", estimatedMinutes: 5);
        var skillA2 = await SeedSkillWithTimeAsync($"P310a-T1-SkillA2-{Guid.NewGuid():N}", estimatedMinutes: 8);
        var skillB1 = await SeedSkillWithTimeAsync($"P310a-T1-SkillB1-{Guid.NewGuid():N}", estimatedMinutes: 12);

        // Student A: 2 NeedsReview rows (always due)
        await SeedMasteryRowAsync(studentIdA, skillA1, MasteryStatus.NeedsReview, null);
        await SeedMasteryRowAsync(studentIdA, skillA2, MasteryStatus.NeedsReview, null);
        // Student B: 1 Mastered-but-stale row (due = past NextReviewDueAt)
        await SeedMasteryRowAsync(studentIdB, skillB1, MasteryStatus.Mastered, DateTime.UtcNow.AddDays(-1));

        CapturingReviewDueHandler.Captured.Clear();

        // Act
        await RunSweepJobAsync();
        await SettleAsync();

        // Assert: exactly 2 events total (one per student)
        var captured = CapturingReviewDueHandler.Captured.ToList();
        var eventsForA = captured.Where(e => e.StudentId == studentIdA).ToList();
        var eventsForB = captured.Where(e => e.StudentId == studentIdB).ToList();

        eventsForA.Should().ContainSingle(
            $"sweep must publish exactly 1 ReviewDueIntegrationEvent for studentA (id={studentIdA})");
        eventsForB.Should().ContainSingle(
            $"sweep must publish exactly 1 ReviewDueIntegrationEvent for studentB (id={studentIdB})");

        // DueCount: A has 2 skills, B has 1
        eventsForA[0].DueCount.Should().Be(2,
            $"student A has 2 due skills; DueCount must be 2 (got {eventsForA[0].DueCount})");
        eventsForB[0].DueCount.Should().Be(1,
            $"student B has 1 due skill; DueCount must be 1 (got {eventsForB[0].DueCount})");

        // TopSkills must not be empty
        eventsForA[0].TopSkills.Should().NotBeEmpty("TopSkills for student A must contain at least one entry");
        eventsForB[0].TopSkills.Should().NotBeEmpty("TopSkills for student B must contain at least one entry");
    }

    // =========================================================================
    // T2 — zero events when no due rows
    // =========================================================================

    [Fact(DisplayName = "P310a-T2 Sweep fires zero events when there are no due rows")]
    public async Task T2_NoEvents_WhenNoDueRows()
    {
        // Arrange: a student with only a Mastered+FUTURE NextReviewDueAt (not due)
        var studentId = await CreateStudentAsync("t2");
        var skillId   = await SeedSkillWithTimeAsync($"P310a-T2-Skill-{Guid.NewGuid():N}");

        // Mastered + future date → NOT due
        await SeedMasteryRowAsync(studentId, skillId, MasteryStatus.Mastered,
            nextReviewDueAt: DateTime.UtcNow.AddDays(7));

        CapturingReviewDueHandler.Captured.Clear();

        // Act
        await RunSweepJobAsync();
        await SettleAsync();

        // Assert: no events for this student (or any other from this test's seeds)
        var captured = CapturingReviewDueHandler.Captured.ToList();
        var eventsForStudent = captured.Where(e => e.StudentId == studentId).ToList();

        eventsForStudent.Should().BeEmpty(
            $"no ReviewDueIntegrationEvent must be published when all of student {studentId}'s " +
            "mastery rows have a future NextReviewDueAt (not due)");
    }

    // =========================================================================
    // T3 — payload correctness
    // =========================================================================

    [Fact(DisplayName = "P310a-T3 Payload correctness: SkillName, EstimatedTimeMinutes, DueCount, UTC kind; NO PII fields")]
    public async Task T3_PayloadCorrectness()
    {
        // Arrange: one student with one NeedsReview skill with a known name + time.
        var studentId  = await CreateStudentAsync("t3");
        var skillName  = $"P310a-T3-KnownSkill-{Guid.NewGuid():N}";
        const int estimatedMinutes = 17;

        var skillId = await SeedSkillWithTimeAsync(skillName, estimatedMinutes);
        await SeedMasteryRowAsync(studentId, skillId, MasteryStatus.NeedsReview, null);

        CapturingReviewDueHandler.Captured.Clear();

        // Act
        await RunSweepJobAsync();
        await SettleAsync();

        // Assert: exactly one event for this student
        var captured = CapturingReviewDueHandler.Captured.ToList();
        var evtOrNull = captured.SingleOrDefault(e => e.StudentId == studentId);

        evtOrNull.Should().NotBeNull(
            $"exactly one ReviewDueIntegrationEvent must be published for studentId={studentId}; " +
            $"captured={captured.Count} events total");

        // Non-null assertion: we already asserted above; use null-forgiving for subsequent assertions.
        var evt = evtOrNull!;

        // EventId must be non-empty
        evt.EventId.Should().NotBeEmpty("EventId must be a non-empty Guid (unique per publish)");

        // StudentId: opaque int — no PII (name, email, DOB absent from record)
        evt.StudentId.Should().Be(studentId, "StudentId must match the seeded student");

        // DueCount
        evt.DueCount.Should().Be(1, "DueCount must match the 1 due row for this student");

        // TopSkills carries the real SkillName and EstimatedTimeMinutes
        evt.TopSkills.Should().ContainSingle("one due skill → TopSkills must have one entry");
        var top = evt.TopSkills[0];
        top.SkillName.Should().Be(skillName,
            $"SkillName must match the seeded Skill.Name ('{skillName}')");
        top.EstimatedTimeMinutes.Should().Be(estimatedMinutes,
            $"EstimatedTimeMinutes must match Skill.EstimatedTimeMinutes ({estimatedMinutes})");
        top.SkillId.Should().Be(skillId, "SkillId must be the seeded Skill.Id");

        // OccurredOnUtc must be UTC kind (no local-kind datetimes in payloads)
        evt.OccurredOnUtc.Kind.Should().Be(DateTimeKind.Utc,
            "OccurredOnUtc must be DateTimeKind.Utc — producer uses DateTime.UtcNow");
        evt.OccurredOnUtc.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(30),
            "OccurredOnUtc must be near UtcNow (producer captures it at job start)");

        // No PII: the record type has only EventId (Guid), OccurredOnUtc (DateTime),
        // StudentId (opaque int), DueCount (int), TopSkills (curriculum data).
        // Verify by reflection that no string member exists that could be email/name.
        var recordType = typeof(ReviewDueIntegrationEvent);
        var stringProps = recordType.GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .ToList();
        stringProps.Should().BeEmpty(
            "ReviewDueIntegrationEvent must carry NO string fields (opaque ints only at the top level) — " +
            $"NO PII; found string properties: [{string.Join(", ", stringProps)}]");

        // Nested DueSkillSnapshot: SkillName is allowed (curriculum content, not PII).
        // Verify no "email", "name" (personal), or "dob" string fields are on the snapshot.
        var snapshotType = typeof(DueSkillSnapshot);
        var snapshotStringProps = snapshotType.GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .ToList();
        snapshotStringProps.Should().BeEquivalentTo(new[] { "SkillName" },
            "DueSkillSnapshot must expose ONLY SkillName as a string field (no email, no student name); " +
            $"found: [{string.Join(", ", snapshotStringProps)}]");
    }

    // =========================================================================
    // T4 — idempotent / non-spammy per sweep
    // =========================================================================

    [Fact(DisplayName = "P310a-T4 Running sweep twice produces same per-student digest (same DueCount + skills — idempotent recompute)")]
    public async Task T4_Idempotent_SweepTwice_SameDigest()
    {
        // Arrange: one student with one NeedsReview due skill.
        var studentId = await CreateStudentAsync("t4");
        var skillName = $"P310a-T4-Skill-{Guid.NewGuid():N}";
        var skillId   = await SeedSkillWithTimeAsync(skillName, estimatedMinutes: 6);
        await SeedMasteryRowAsync(studentId, skillId, MasteryStatus.NeedsReview, null);

        CapturingReviewDueHandler.Captured.Clear();

        // Act — first sweep
        await RunSweepJobAsync();
        await SettleAsync();

        // Snapshot after first sweep (filter to this test's student to avoid cross-test pollution).
        var afterFirstSweep = CapturingReviewDueHandler.Captured
            .Where(e => e.StudentId == studentId).ToList();
        afterFirstSweep.Should().ContainSingle("first sweep must produce exactly 1 event for this student");
        var firstEvt = afterFirstSweep[0];

        // Act — second sweep (back-to-back; same due row state since NeedsReview always due).
        // Note: ConcurrentBag is NOT cleared between sweeps — we want to count cumulative events.
        await RunSweepJobAsync();
        await SettleAsync();

        // Snapshot after second sweep.
        var afterBothSweeps = CapturingReviewDueHandler.Captured
            .Where(e => e.StudentId == studentId).ToList();
        afterBothSweeps.Should().HaveCount(2,
            "two sweeps on the same due data must produce exactly 2 events (one per sweep) — " +
            "not 0 (would mean second sweep skipped the student) and not 3+ (would mean double-publish)");

        // Identify the second-sweep event as the one whose EventId differs from the first event's.
        // NOTE: ConcurrentBag ordering is NOT guaranteed (LIFO per thread-local stack).
        // We therefore find the second event by EventId inequality, NOT by index.
        var secondEvt = afterBothSweeps.SingleOrDefault(e => e.EventId != firstEvt.EventId);
        secondEvt.Should().NotBeNull(
            "the two events in the bag after two sweeps must have DIFFERENT EventIds " +
            "(each sweep calls Guid.NewGuid() — same Guid would mean the same event object was " +
            "captured twice, indicating a double-call to the handler for one publish)");

        secondEvt = secondEvt!;

        // The digest content must be identical across runs (same DueCount, same TopSkills).
        secondEvt.DueCount.Should().Be(firstEvt.DueCount,
            "DueCount must be the same across consecutive sweeps (pure recompute, no accumulation)");
        secondEvt.TopSkills.Should().HaveCount(firstEvt.TopSkills.Count,
            "TopSkills count must be the same across consecutive sweeps");

        // TopSkills content must be the same (same skillId, same name) — pure recompute.
        var firstSkillIds  = firstEvt.TopSkills.Select(s => s.SkillId).OrderBy(x => x).ToList();
        var secondSkillIds = secondEvt.TopSkills.Select(s => s.SkillId).OrderBy(x => x).ToList();
        secondSkillIds.Should().BeEquivalentTo(firstSkillIds,
            "TopSkills SkillIds must be the same across consecutive sweeps (same due rows, same ordering)");
    }

    // =========================================================================
    // T5 — fail-soft: a handler throw does not abort the sweep
    // =========================================================================

    [Fact(DisplayName = "P310a-T5 Fail-soft: a throwing INotificationHandler does not abort the sweep; SR columns committed for both students; both students' events captured by other handlers")]
    public async Task T5_FailSoft_HandlerThrow_SweepContinues_BothStudentsCommittedAndCaptured()
    {
        // Architecture note (also in ThrowingReviewDueHandler XML doc):
        // The fail-soft tested here is Layer 1 — handler-level isolation via IsolatedNotificationPublisher
        // (ADR 0002 §4). When ThrowingReviewDueHandler throws, IsolatedNotificationPublisher catches it,
        // logs it, and fans out to CapturingReviewDueHandler. The publisher.Publish() call in the sweep
        // job does NOT re-throw. Both students' events reach CapturingReviewDueHandler, and the sweep
        // completes without exception. SR column writes are committed before publish (post-commit guarantee).
        //
        // Layer 2 (sweep job per-student try/catch on publisher.Publish itself) is verified by code review:
        // SpacedRepetitionSweepJob.cs lines 139–174 wrap each student group's publish call in try/catch.
        // Simulating a publisher-level throw would require replacing IPublisher in DI (complex, outside
        // our scope-safe boundaries); documented and deferred per the brief's judgment guidance.

        // Arrange: two students, each with one due NeedsReview skill.
        var studentIdA = await CreateStudentAsync("t5a");
        var studentIdB = await CreateStudentAsync("t5b");

        var skillIdA = await SeedSkillWithTimeAsync($"P310a-T5-SkillA-{Guid.NewGuid():N}");
        var skillIdB = await SeedSkillWithTimeAsync($"P310a-T5-SkillB-{Guid.NewGuid():N}");

        await SeedMasteryRowAsync(studentIdA, skillIdA, MasteryStatus.NeedsReview, null);
        await SeedMasteryRowAsync(studentIdB, skillIdB, MasteryStatus.NeedsReview, null);

        // Build a forked factory with BOTH the throwing handler AND the capturing handler.
        // IsolatedNotificationPublisher will absorb the throw from ThrowingReviewDueHandler
        // and still deliver the event to CapturingReviewDueHandler.
        var fork = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<
                    INotificationHandler<ReviewDueIntegrationEvent>,
                    CapturingReviewDueHandler>();
                services.AddSingleton<
                    INotificationHandler<ReviewDueIntegrationEvent>,
                    ThrowingReviewDueHandler>();
            });
        });

        // Fork client isn't needed for HTTP in T5 — we use the forked DI scope for the job.
        _ = fork.CreateClient();

        CapturingReviewDueHandler.Captured.Clear();

        // Act: run the sweep job — should complete without exception despite the throwing handler.
        var ex = await Record.ExceptionAsync(async () =>
        {
            using var scope = fork.Services.CreateScope();
            var job = scope.ServiceProvider.GetRequiredService<SpacedRepetitionSweepJob>();
            await job.RunAsync(CancellationToken.None);
        });

        await SettleAsync();

        // Assert 1: sweep job must NOT throw despite the handler exception.
        ex.Should().BeNull(
            "SpacedRepetitionSweepJob.RunAsync must not propagate a handler exception " +
            "(IsolatedNotificationPublisher catches per-handler throws; sweep job's own per-student " +
            "try/catch provides the second layer); " +
            $"got exception: {ex?.Message}");

        // Assert 2: SR column updates committed for BOTH students before publish attempt.
        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var rowA = await db.StudentSkillMasteries.AsNoTracking()
            .FirstOrDefaultAsync(m => m.StudentId == studentIdA && m.SkillId == skillIdA);
        var rowB = await db.StudentSkillMasteries.AsNoTracking()
            .FirstOrDefaultAsync(m => m.StudentId == studentIdB && m.SkillId == skillIdB);

        rowA.Should().NotBeNull($"mastery row must exist for student A (studentId={studentIdA})");
        rowB.Should().NotBeNull($"mastery row must exist for student B (studentId={studentIdB})");

        rowA!.NextReviewDueAt.Should().NotBeNull(
            "sweep must have set NextReviewDueAt for student A (post-commit; SR write before publish)");
        rowB!.NextReviewDueAt.Should().NotBeNull(
            "sweep must have set NextReviewDueAt for student B (post-commit; SR write before publish)");

        // Assert 3: Both students' events reached the capturing handler
        // (IsolatedNotificationPublisher ran CapturingReviewDueHandler for every student despite
        // ThrowingReviewDueHandler throwing for each one).
        var capturedA = CapturingReviewDueHandler.Captured
            .Where(e => e.StudentId == studentIdA).ToList();
        var capturedB = CapturingReviewDueHandler.Captured
            .Where(e => e.StudentId == studentIdB).ToList();

        capturedA.Should().ContainSingle(
            $"CapturingReviewDueHandler must have received student A's (id={studentIdA}) event " +
            "even though ThrowingReviewDueHandler threw " +
            "(IsolatedNotificationPublisher isolates per-handler failures)");
        capturedB.Should().ContainSingle(
            $"CapturingReviewDueHandler must have received student B's (id={studentIdB}) event " +
            "— sweep continued past student A's group (fail-soft at the loop level)");
    }

    // =========================================================================
    // T6 — top-N ordering + cap at 3
    // =========================================================================

    [Fact(DisplayName = "P310a-T6 TopSkills: NeedsReview ranked first, cap at 3, most-overdue first within status bucket")]
    public async Task T6_TopSkillsOrdering_NeedsReviewFirst_CapAt3_MostOverdueFirst()
    {
        // Arrange: seed a student with >3 due skills:
        //   - 1 NeedsReview skill (most urgent; should rank #1 regardless of overdue date)
        //   - 3 Mastered-stale skills with decreasing overdue-ness
        //     (most overdue → least overdue, to test within-bucket ordering)
        // Total: 4 due skills → TopSkills must be capped at 3 (NeedsReview + 2 most-overdue Mastered)

        var studentId = await CreateStudentAsync("t6");

        // NeedsReview skill — must always rank first (regardless of date)
        var skillNeedsReview = await SeedSkillWithTimeAsync($"P310a-T6-NeedsReview-{Guid.NewGuid():N}", estimatedMinutes: 5);

        // Mastered-stale skills — most overdue first in expectations
        var skillMastered1 = await SeedSkillWithTimeAsync($"P310a-T6-Mastered1-MostOverdue-{Guid.NewGuid():N}", estimatedMinutes: 8);
        var skillMastered2 = await SeedSkillWithTimeAsync($"P310a-T6-Mastered2-{Guid.NewGuid():N}", estimatedMinutes: 10);
        var skillMastered3 = await SeedSkillWithTimeAsync($"P310a-T6-Mastered3-LeastOverdue-{Guid.NewGuid():N}", estimatedMinutes: 12);

        // NeedsReview row — null NextReviewDueAt (always due)
        await SeedMasteryRowAsync(studentId, skillNeedsReview, MasteryStatus.NeedsReview, null);

        // Mastered-stale rows — most overdue first (largest gap from now)
        await SeedMasteryRowAsync(studentId, skillMastered1, MasteryStatus.Mastered,
            nextReviewDueAt: DateTime.UtcNow.AddDays(-10));  // most overdue
        await SeedMasteryRowAsync(studentId, skillMastered2, MasteryStatus.Mastered,
            nextReviewDueAt: DateTime.UtcNow.AddDays(-5));   // middle
        await SeedMasteryRowAsync(studentId, skillMastered3, MasteryStatus.Mastered,
            nextReviewDueAt: DateTime.UtcNow.AddDays(-1));   // least overdue

        CapturingReviewDueHandler.Captured.Clear();

        // Act
        await RunSweepJobAsync();
        await SettleAsync();

        // Assert: one event for this student
        var captured = CapturingReviewDueHandler.Captured.ToList();
        var evtOrNull = captured.SingleOrDefault(e => e.StudentId == studentId);

        evtOrNull.Should().NotBeNull(
            $"one ReviewDueIntegrationEvent must be published for studentId={studentId}; " +
            $"captured {captured.Count} events total");

        var evt = evtOrNull!;

        // DueCount: 4 (NeedsReview + 3 Mastered-stale)
        evt.DueCount.Should().Be(4,
            "DueCount must reflect all 4 due skills (1 NeedsReview + 3 Mastered-stale)");

        // TopSkills capped at 3 (TopSkillsCap = 3 in SpacedRepetitionSweepJob)
        evt.TopSkills.Should().HaveCount(3,
            "TopSkills must be capped at 3 (TopSkillsCap constant in SpacedRepetitionSweepJob)");

        // First entry must be the NeedsReview skill (status bucket 0 → ranked before Mastered)
        evt.TopSkills[0].SkillId.Should().Be(skillNeedsReview,
            "TopSkills[0] must be the NeedsReview skill — NeedsReview rows are ranked first " +
            "(OrderBy status==NeedsReview ? 0 : 1)");

        // Remaining 2 entries must be the 2 most-overdue Mastered rows (oldest NextReviewDueAt first)
        evt.TopSkills[1].SkillId.Should().Be(skillMastered1,
            "TopSkills[1] must be skillMastered1 — most overdue (NextReviewDueAt = -10 days)");
        evt.TopSkills[2].SkillId.Should().Be(skillMastered2,
            "TopSkills[2] must be skillMastered2 — second-most overdue (NextReviewDueAt = -5 days)");

        // skillMastered3 (least overdue) must NOT appear (capped at 3)
        var mastered3InTop = evt.TopSkills.Any(s => s.SkillId == skillMastered3);
        mastered3InTop.Should().BeFalse(
            "skillMastered3 (least overdue, NextReviewDueAt=-1 day) must NOT appear in TopSkills " +
            "(evicted by cap=3 after NeedsReview + 2 more-overdue Mastered skills)");
    }
}
