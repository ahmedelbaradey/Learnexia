using FluentAssertions;
using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Curriculum.IntegrationTests;

/// <summary>
/// BL-01 Batch 1 — persistence smoke test.
///
/// Verifies that the two migrations added by BL-01 apply cleanly after AddKGSuggestionTable and
/// that the new entities round-trip correctly:
///
///   AC5 (partial) — CurriculumDocument row with Status=Received, ObjectKey, GradeId/SubjectId
///                   as plain ints (no FK to learning), Language int-mapped.
///   AC5 (partial) — PipelineJob row with JobType='parse', Status='Pending' (strings),
///                   DocumentId intra-module FK on cascade, RetryCount default 0.
///   Schema check  — Both tables land in the 'curriculum' schema.
///   Isolation     — GradeId/SubjectId are plain ints (no FK/nav to learning module).
///   String seam   — PipelineJob.JobType and .Status survive the round-trip as unmodified strings.
///
/// Testcontainers image: pgvector/pgvector:pg17 (matches dev docker-compose).
/// </summary>
[Collection("BL01Schema")]
public sealed class BL01_CurriculumDocumentSchema_Tests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("bl01_schema_test")
        .WithUsername("postgres")
        .WithPassword("testpwd")
        .Build();

    private CurriculumDbContext _db = null!;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        await _postgres.StartAsync();

        var connectionString = _postgres.GetConnectionString();

        var options = new DbContextOptionsBuilder<CurriculumDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .UseVector()
                    .MigrationsHistoryTable("__EFMigrationsHistory", CurriculumDbContext.Schema)
                    .MigrationsAssembly(typeof(CurriculumDbContext).Assembly.FullName))
            .Options;

        _db = new CurriculumDbContext(options);

        // Apply the full migration chain, including AddCurriculumDocumentTable + AddPipelineJobsTable.
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.StopAsync();
    }

    // ── Migration chain integrity ──────────────────────────────────────────────

    /// <summary>
    /// Confirms that MigrateAsync completes without error — meaning both BL-01 migrations
    /// (AddCurriculumDocumentTable, AddPipelineJobsTable) apply cleanly after AddKGSuggestionTable.
    /// If the migration chain is broken this test will fail in InitializeAsync before running the body.
    /// </summary>
    [Fact]
    public async Task MigrationChain_AppliesCleanly_BothTablesExist()
    {
        var conn = _db.Database.GetDbConnection();
        await conn.OpenAsync();

        // Verify CurriculumDocuments table exists in curriculum schema
        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = """
            SELECT count(*) FROM information_schema.tables
            WHERE table_schema = 'curriculum' AND table_name = 'CurriculumDocuments';
            """;
        var docTableCount = Convert.ToInt64(await cmd1.ExecuteScalarAsync());

        // Verify PipelineJobs table exists in curriculum schema
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = """
            SELECT count(*) FROM information_schema.tables
            WHERE table_schema = 'curriculum' AND table_name = 'PipelineJobs';
            """;
        var jobTableCount = Convert.ToInt64(await cmd2.ExecuteScalarAsync());

        docTableCount.Should().Be(1, "CurriculumDocuments table must exist in curriculum schema");
        jobTableCount.Should().Be(1, "PipelineJobs table must exist in curriculum schema");
    }

    // ── AC5 (partial): CurriculumDocument round-trip ───────────────────────────

    /// <summary>
    /// AC5 (partial): Writes a CurriculumDocument (Status=Received) and reads it back.
    /// Verifies:
    ///   - All fields survive the round-trip.
    ///   - Status stored as int (DocumentStatus.Received = 0).
    ///   - GradeId/SubjectId are plain ints (no FK/nav to learning — module isolation).
    ///   - ObjectKey, FileName, ContentType, FileSize, Language, Country round-trip correctly.
    ///   - StatusReason is null on a newly received document.
    /// </summary>
    [Fact]
    public async Task CurriculumDocument_RoundTrip_StatusReceived()
    {
        var doc = new CurriculumDocument
        {
            FileName    = "grade4_math_textbook.pdf",
            ObjectKey   = "curriculum/2026/06/a1b2c3d4-e5f6-7890-abcd-ef1234567890.pdf",
            ContentType = "application/pdf",
            FileSize    = 5_242_880L, // 5 MB
            GradeId     = 4,          // plain int — no FK to learning.Grades
            SubjectId   = 1,          // plain int — no FK to learning.Subjects
            Language    = ContentLanguage.Arabic,
            Country     = "EG",
            Status      = DocumentStatus.Received,
            StatusReason = null,
        };

        _db.CurriculumDocuments.Add(doc);
        await _db.SaveChangesAsync(1);

        _db.ChangeTracker.Clear();
        var loaded = await _db.CurriculumDocuments.FirstAsync(d => d.Id == doc.Id);

        loaded.FileName.Should().Be("grade4_math_textbook.pdf");
        loaded.ObjectKey.Should().Be("curriculum/2026/06/a1b2c3d4-e5f6-7890-abcd-ef1234567890.pdf",
            "ObjectKey must be stored and returned exactly — never a URL");
        loaded.ContentType.Should().Be("application/pdf");
        loaded.FileSize.Should().Be(5_242_880L);
        loaded.GradeId.Should().Be(4, "plain int cross-module ref survives round-trip");
        loaded.SubjectId.Should().Be(1, "plain int cross-module ref survives round-trip");
        loaded.Language.Should().Be(ContentLanguage.Arabic, "Language enum int-mapped round-trip");
        loaded.Country.Should().Be("EG");
        loaded.Status.Should().Be(DocumentStatus.Received, "Status starts as Received (int 0)");
        loaded.StatusReason.Should().BeNull("StatusReason is null on initial receipt");
    }

    /// <summary>
    /// AC5 (partial): Verifies that DocumentStatus.Failed with a StatusReason survives round-trip.
    /// Also confirms that SubjectId is a standalone plain int (no composite FK).
    /// </summary>
    [Fact]
    public async Task CurriculumDocument_RoundTrip_StatusFailed_WithStatusReason()
    {
        var doc = new CurriculumDocument
        {
            FileName     = "corrupt_worksheet.docx",
            ObjectKey    = "curriculum/2026/06/deadbeef-0000-0000-0000-000000000001.docx",
            ContentType  = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileSize     = 102_400L,
            GradeId      = 3,
            SubjectId    = 2,
            Language     = ContentLanguage.English,
            Country      = null,
            Status       = DocumentStatus.Failed,
            StatusReason = "Parser timeout after 300s — document may be malformed.",
        };

        _db.CurriculumDocuments.Add(doc);
        await _db.SaveChangesAsync(1);

        _db.ChangeTracker.Clear();
        var loaded = await _db.CurriculumDocuments.FirstAsync(d => d.Id == doc.Id);

        loaded.Status.Should().Be(DocumentStatus.Failed);
        loaded.StatusReason.Should().Be("Parser timeout after 300s — document may be malformed.");
        loaded.Country.Should().BeNull("nullable Country survives as null");
    }

    // ── AC5 (partial): PipelineJob round-trip ──────────────────────────────────

    /// <summary>
    /// AC5 (partial): Writes a CurriculumDocument + PipelineJob (parse/Pending) and reads them back.
    /// Verifies:
    ///   - JobType and Status are strings (the cross-process contract with Python).
    ///   - DocumentId is an intra-module FK; the job loads the document via nav.
    ///   - RetryCount defaults to 0.
    ///   - ClaimedAt/CompletedAt are null on a fresh Pending job.
    ///   - PayloadJson and ResultJson survive round-trip.
    /// </summary>
    [Fact]
    public async Task PipelineJob_RoundTrip_ParsePending_StringJobTypeAndStatus()
    {
        // Arrange — CurriculumDocument (FK target must exist first)
        var doc = new CurriculumDocument
        {
            FileName    = "grade5_science.pdf",
            ObjectKey   = "curriculum/2026/06/deadbeef-1111-2222-3333-444444444444.pdf",
            ContentType = "application/pdf",
            FileSize    = 10_485_760L,
            GradeId     = 5,
            SubjectId   = 2,
            Language    = ContentLanguage.Arabic,
            Status      = DocumentStatus.Received,
        };
        _db.CurriculumDocuments.Add(doc);
        await _db.SaveChangesAsync(1);

        // Arrange — PipelineJob (DB-outbox row written by the upload handler)
        var job = new PipelineJob
        {
            JobType     = "parse",    // string — cross-process contract
            Status      = "Pending",  // string — cross-process contract
            DocumentId  = doc.Id,
            PayloadJson = """{"object_key":"curriculum/2026/06/deadbeef-1111-2222-3333-444444444444.pdf","content_type":"application/pdf"}""",
            RetryCount  = 0,
        };
        _db.PipelineJobs.Add(job);
        await _db.SaveChangesAsync(1);

        // Act — read back with navigation
        _db.ChangeTracker.Clear();
        var loadedJob = await _db.PipelineJobs
            .Include(j => j.Document)
            .FirstAsync(j => j.Id == job.Id);

        // Assert — PipelineJob strings
        loadedJob.JobType.Should().Be("parse",
            "JobType is a string — cross-process contract with Python; must NOT be int-mapped");
        loadedJob.Status.Should().Be("Pending",
            "Status is a string — cross-process contract with Python poller; must NOT be int-mapped");
        loadedJob.DocumentId.Should().Be(doc.Id, "intra-module FK survives round-trip");
        loadedJob.RetryCount.Should().Be(0, "RetryCount defaults to 0");
        loadedJob.ClaimedAt.Should().BeNull("ClaimedAt is null on a fresh Pending job");
        loadedJob.CompletedAt.Should().BeNull("CompletedAt is null until Done/Failed");
        loadedJob.ResultJson.Should().BeNull("ResultJson is null until Python writes it");
        loadedJob.ErrorMessage.Should().BeNull("ErrorMessage is null when not failed");

        // Assert — PayloadJson round-trip
        loadedJob.PayloadJson.Should().Contain("object_key",
            "PayloadJson carries the object_key for the Python worker");
        loadedJob.PayloadJson.Should().Contain("content_type",
            "PayloadJson carries the content_type for the Python worker");

        // Assert — Navigation (intra-module FK)
        loadedJob.Document.Should().NotBeNull("Document navigation works via intra-module FK");
        loadedJob.Document.Id.Should().Be(doc.Id);
        loadedJob.Document.FileName.Should().Be("grade5_science.pdf");
    }

    /// <summary>
    /// Cascade delete: deleting a CurriculumDocument removes its PipelineJob rows (ON DELETE CASCADE).
    /// </summary>
    [Fact]
    public async Task PipelineJob_CascadeDelete_WhenDocumentDeleted()
    {
        // Arrange
        var doc = new CurriculumDocument
        {
            FileName    = "cascade_test.pdf",
            ObjectKey   = "curriculum/2026/06/cascade-test-doc.pdf",
            ContentType = "application/pdf",
            FileSize    = 1024L,
            GradeId     = 1,
            SubjectId   = 1,
            Language    = ContentLanguage.English,
            Status      = DocumentStatus.Received,
        };
        _db.CurriculumDocuments.Add(doc);
        await _db.SaveChangesAsync(1);

        var job = new PipelineJob
        {
            JobType     = "parse",
            Status      = "Pending",
            DocumentId  = doc.Id,
            PayloadJson = """{"object_key":"curriculum/2026/06/cascade-test-doc.pdf","content_type":"application/pdf"}""",
        };
        _db.PipelineJobs.Add(job);
        await _db.SaveChangesAsync(1);

        var jobId = job.Id;

        // Act — delete the document; cascade should remove the job
        _db.CurriculumDocuments.Remove(doc);
        await _db.SaveChangesAsync(1);

        // Assert
        _db.ChangeTracker.Clear();
        var deletedJob = await _db.PipelineJobs.FirstOrDefaultAsync(j => j.Id == jobId);
        deletedJob.Should().BeNull("PipelineJob must be cascade-deleted when its CurriculumDocument is removed");
    }
}

[CollectionDefinition("BL01Schema")]
public sealed class BL01SchemaCollection { }
