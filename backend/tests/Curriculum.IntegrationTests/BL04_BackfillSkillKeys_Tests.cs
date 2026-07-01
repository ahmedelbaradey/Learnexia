using FluentAssertions;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Curriculum.IntegrationTests;

/// <summary>
/// Regression test for BL-04 migration <c>20260623012123_BackfillSkillKeys</c>.
///
/// REGRESSION GAP that was closed: the original BL-04 smoke test ran against an empty
/// Testcontainers DB where the backfill UPDATE matched 0 rows — so the within-batch
/// duplicate-key bug was never exercised. The fix adds a window COUNT(*) OVER (...) to
/// the <c>keyed</c> CTE so that candidate keys that appear more than once in the same
/// UPDATE batch are left NULL rather than causing a unique-index violation.
///
/// This test:
///   1. Starts a real PostgreSQL container (postgres:16-alpine — no pgvector needed).
///   2. Applies all Learning migrations (includes AddSkillKeyToKnowledgeNode which creates
///      the partial unique index UX_KnowledgeNodes_SkillKey, and then BackfillSkillKeys).
///   3. Seeds a Subject + Grade and several KnowledgeNodes — including two that will
///      slugify to the SAME candidate key within the same subject+grade — BEFORE the
///      BackfillSkillKeys migration has had a chance to run against live data
///      (simulated by inserting nodes with SkillKey IS NULL and then re-running the
///      migration SQL directly).
///   4. Asserts: the unique-named node gets its SkillKey set; the duplicate-named nodes
///      are LEFT NULL (no unique-index violation, no crash).
///
/// Test project: Curriculum.IntegrationTests (already has Testcontainers + Learning
/// Infrastructure reference — no new project needed).
/// </summary>
[Collection("BL04BackfillSkillKeys")]
public sealed class BL04_BackfillSkillKeys_Tests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("bl04_backfill_test")
        .WithUsername("postgres")
        .WithPassword("testpwd")
        .Build();

    private LearningDbContext _db = null!;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        await _postgres.StartAsync();

        var connectionString = _postgres.GetConnectionString();

        var options = new DbContextOptionsBuilder<LearningDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .MigrationsHistoryTable("__EFMigrationsHistory", LearningDbContext.Schema)
                    .MigrationsAssembly(typeof(LearningDbContext).Assembly.FullName))
            .Options;

        _db = new LearningDbContext(options);

        // Apply all Learning migrations — including AddSkillKeyToKnowledgeNode (creates
        // the partial unique index) and BackfillSkillKeys (the fixed migration under test).
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.StopAsync();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Core regression: seeded KnowledgeNodes including two with the SAME slugifiable name
    /// in the same subject+grade, plus one with a unique name.
    ///
    /// Running the backfill SQL against this state must NOT throw a duplicate-key exception.
    /// The unique-named node must get its SkillKey set. The two duplicate-named nodes must
    /// remain NULL (within-batch dedup guard fires).
    /// </summary>
    [Fact]
    public async Task BackfillSkillKeys_WithinBatchDuplicates_DoNotThrow_DuplicatesLeftNull()
    {
        // ── Arrange: seed a Grade + Subject via raw SQL (bypasses soft-delete filter) ──

        await _db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO learning."Grades" ("Number", "DisplayName", "CreatedAt", "CreatedBy", "IsDeleted")
            VALUES (1, 'Grade 1', NOW(), 0, false)
            ON CONFLICT DO NOTHING;
            """);

        int gradeId = await GetScalarAsync<int>(_db, """SELECT "Id" FROM learning."Grades" WHERE "Number" = 1 LIMIT 1""");

        await _db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO learning."Subjects" ("Name", "SubjectCode", "Language", "GradeId", "SequenceOrder", "IsActive", "CreatedAt", "CreatedBy", "IsDeleted")
            VALUES ('Math G1', 0, 0, {gradeId}, 1, true, NOW(), 0, false)
            ON CONFLICT DO NOTHING;
            """);

        int subjectId = await GetScalarAsync<int>(_db,
            $"""SELECT "Id" FROM learning."Subjects" WHERE "GradeId" = {gradeId} AND "SubjectCode" = 0 LIMIT 1""");

        // ── Arrange: insert three KnowledgeNodes with SkillKey IS NULL ─────────────────
        //   - node A: unique name "Unique Node Alpha" → slug "unique-node-alpha" → key "math.grade1.unique-node-alpha"
        //   - node B: name "1" → slug "1" → candidate "math.grade1.1"  (within-batch dup)
        //   - node C: name "1" → slug "1" → candidate "math.grade1.1"  (within-batch dup, same as B)

        await _db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO learning."KnowledgeNodes"
                ("Name", "NodeType", "SubjectId", "GradeId", "Difficulty", "CreatedAt", "CreatedBy", "IsDeleted")
            VALUES
                ('Unique Node Alpha', 0, {subjectId}, {gradeId}, 3, NOW(), 0, false),
                ('1',                 0, {subjectId}, {gradeId}, 3, NOW(), 0, false),
                ('1',                 0, {subjectId}, {gradeId}, 3, NOW(), 0, false);
            """);

        // ── Act: re-run the backfill SQL (the migration ran on an empty DB; now replay
        //         it against seeded data to exercise the within-batch dedup path) ──────

        var backfillSql = """
            WITH candidates AS (
                SELECT
                    kn."Id",
                    LOWER(
                        REGEXP_REPLACE(
                            REGEXP_REPLACE(
                                TRIM(REGEXP_REPLACE(kn."Name", '[^a-zA-Z0-9 \-]', '', 'g')),
                                ' +', '-', 'g'
                            ),
                            '-+', '-', 'g'
                        )
                    ) AS name_slug,
                    CASE s."SubjectCode"
                        WHEN 0 THEN 'math'
                        WHEN 1 THEN 'science'
                        WHEN 2 THEN 'arabic'
                        WHEN 3 THEN 'english'
                        ELSE 'subject-' || s."SubjectCode"::text
                    END AS subject_code_str,
                    g."Number" AS grade_number
                FROM learning."KnowledgeNodes" kn
                INNER JOIN learning."Subjects" s ON kn."SubjectId" = s."Id"
                INNER JOIN learning."Grades" g ON kn."GradeId" = g."Id"
                WHERE kn."SkillKey" IS NULL
                  AND (s."IsDeleted" IS NOT TRUE)
                  AND (g."IsDeleted" IS NOT TRUE)
            ),
            keyed AS (
                SELECT
                    "Id",
                    subject_code_str || '.grade' || grade_number || '.' || name_slug AS candidate_key,
                    COUNT(*) OVER (
                        PARTITION BY subject_code_str || '.grade' || grade_number || '.' || name_slug
                    ) AS dup_count
                FROM candidates
                WHERE name_slug <> '' AND name_slug IS NOT NULL
            )
            UPDATE learning."KnowledgeNodes" kn
            SET "SkillKey" = k.candidate_key
            FROM keyed k
            WHERE kn."Id" = k."Id"
              AND kn."SkillKey" IS NULL
              AND k.dup_count = 1
              AND NOT EXISTS (
                  SELECT 1 FROM learning."KnowledgeNodes" existing
                  WHERE existing."SkillKey" = k.candidate_key AND existing."Id" <> kn."Id"
              );
            """;

        // Must not throw Npgsql.PostgresException 23505
        var act = async () => await _db.Database.ExecuteSqlRawAsync(backfillSql);
        await act.Should().NotThrowAsync("the fixed backfill SQL must handle within-batch duplicates without violating the unique index");

        // ── Assert: reload nodes (IgnoreQueryFilters so soft-delete filter does not hide rows) ──

        var nodes = await _db.KnowledgeNodes
            .IgnoreQueryFilters()
            .Where(n => n.SubjectId == subjectId)
            .OrderBy(n => n.Id)
            .ToListAsync();

        nodes.Should().HaveCount(3, "we inserted exactly 3 KnowledgeNodes");

        // The unique-named node must have gotten its SkillKey
        var uniqueNode = nodes.Single(n => n.Name == "Unique Node Alpha");
        uniqueNode.SkillKey.Should().Be("math.grade1.unique-node-alpha",
            "the unique-named node must receive its backfilled SkillKey");

        // The two duplicate-named nodes must both have SkillKey == null
        var dupNodes = nodes.Where(n => n.Name == "1").ToList();
        dupNodes.Should().HaveCount(2, "we inserted two nodes both named '1'");
        dupNodes.Should().AllSatisfy(n =>
            n.SkillKey.Should().BeNull(
                "within-batch duplicate candidate keys must be left NULL to avoid unique-index violation"));
    }

    /// <summary>
    /// Cross-existing-row guard: a node whose candidate key already exists (set on another row)
    /// must also be left NULL. This verifies the NOT EXISTS guard still works independently
    /// of the within-batch dedup.
    /// </summary>
    [Fact]
    public async Task BackfillSkillKeys_CrossRowConflict_NewRowLeftNull()
    {
        // ── Arrange ────────────────────────────────────────────────────────────────────────

        await _db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO learning."Grades" ("Number", "DisplayName", "CreatedAt", "CreatedBy", "IsDeleted")
            VALUES (2, 'Grade 2', NOW(), 0, false)
            ON CONFLICT DO NOTHING;
            """);

        int gradeId = await GetScalarAsync<int>(_db, """SELECT "Id" FROM learning."Grades" WHERE "Number" = 2 LIMIT 1""");

        await _db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO learning."Subjects" ("Name", "SubjectCode", "Language", "GradeId", "SequenceOrder", "IsActive", "CreatedAt", "CreatedBy", "IsDeleted")
            VALUES ('Science G2', 1, 0, {gradeId}, 1, true, NOW(), 0, false)
            ON CONFLICT DO NOTHING;
            """);

        int subjectId = await GetScalarAsync<int>(_db,
            $"""SELECT "Id" FROM learning."Subjects" WHERE "GradeId" = {gradeId} AND "SubjectCode" = 1 LIMIT 1""");

        // Insert one node with SkillKey already set (simulates a row previously keyed)
        await _db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO learning."KnowledgeNodes"
                ("Name", "NodeType", "SubjectId", "GradeId", "Difficulty", "SkillKey", "CreatedAt", "CreatedBy", "IsDeleted")
            VALUES
                ('Alpha Existing', 0, {subjectId}, {gradeId}, 3, 'science.grade2.alpha-existing', NOW(), 0, false);
            """);

        // Insert a second node that would produce the SAME candidate key but has SkillKey IS NULL
        await _db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO learning."KnowledgeNodes"
                ("Name", "NodeType", "SubjectId", "GradeId", "Difficulty", "CreatedAt", "CreatedBy", "IsDeleted")
            VALUES
                ('Alpha Existing', 0, {subjectId}, {gradeId}, 3, NOW(), 0, false);
            """);

        // ── Act ────────────────────────────────────────────────────────────────────────────

        var backfillSql = """
            WITH candidates AS (
                SELECT
                    kn."Id",
                    LOWER(
                        REGEXP_REPLACE(
                            REGEXP_REPLACE(
                                TRIM(REGEXP_REPLACE(kn."Name", '[^a-zA-Z0-9 \-]', '', 'g')),
                                ' +', '-', 'g'
                            ),
                            '-+', '-', 'g'
                        )
                    ) AS name_slug,
                    CASE s."SubjectCode"
                        WHEN 0 THEN 'math'
                        WHEN 1 THEN 'science'
                        WHEN 2 THEN 'arabic'
                        WHEN 3 THEN 'english'
                        ELSE 'subject-' || s."SubjectCode"::text
                    END AS subject_code_str,
                    g."Number" AS grade_number
                FROM learning."KnowledgeNodes" kn
                INNER JOIN learning."Subjects" s ON kn."SubjectId" = s."Id"
                INNER JOIN learning."Grades" g ON kn."GradeId" = g."Id"
                WHERE kn."SkillKey" IS NULL
                  AND (s."IsDeleted" IS NOT TRUE)
                  AND (g."IsDeleted" IS NOT TRUE)
            ),
            keyed AS (
                SELECT
                    "Id",
                    subject_code_str || '.grade' || grade_number || '.' || name_slug AS candidate_key,
                    COUNT(*) OVER (
                        PARTITION BY subject_code_str || '.grade' || grade_number || '.' || name_slug
                    ) AS dup_count
                FROM candidates
                WHERE name_slug <> '' AND name_slug IS NOT NULL
            )
            UPDATE learning."KnowledgeNodes" kn
            SET "SkillKey" = k.candidate_key
            FROM keyed k
            WHERE kn."Id" = k."Id"
              AND kn."SkillKey" IS NULL
              AND k.dup_count = 1
              AND NOT EXISTS (
                  SELECT 1 FROM learning."KnowledgeNodes" existing
                  WHERE existing."SkillKey" = k.candidate_key AND existing."Id" <> kn."Id"
              );
            """;

        var act = async () => await _db.Database.ExecuteSqlRawAsync(backfillSql);
        await act.Should().NotThrowAsync("the cross-row NOT EXISTS guard must prevent a unique-index violation");

        // ── Assert ─────────────────────────────────────────────────────────────────────────

        var nodes = await _db.KnowledgeNodes
            .IgnoreQueryFilters()
            .Where(n => n.SubjectId == subjectId)
            .OrderBy(n => n.Id)
            .ToListAsync();

        nodes.Should().HaveCount(2, "we inserted exactly 2 KnowledgeNodes for this subject");

        // The already-keyed row must be unchanged
        var alreadyKeyed = nodes.First(n => n.SkillKey == "science.grade2.alpha-existing");
        alreadyKeyed.Should().NotBeNull("the pre-keyed row must retain its SkillKey");

        // The conflict row (same candidate key, SkillKey IS NULL) must stay null
        var conflictNode = nodes.First(n => n.SkillKey == null);
        conflictNode.SkillKey.Should().BeNull(
            "the node whose candidate key conflicts with an existing keyed row must remain NULL");
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static async Task<T> GetScalarAsync<T>(LearningDbContext db, string sql)
    {
        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var result = await cmd.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result!, typeof(T));
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }
    }
}

[CollectionDefinition("BL04BackfillSkillKeys")]
public sealed class BL04BackfillSkillKeysCollection { }
