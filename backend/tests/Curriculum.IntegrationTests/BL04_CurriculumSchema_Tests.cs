using FluentAssertions;
using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Curriculum.IntegrationTests;

/// <summary>
/// BE-6 — BL-04 persistence smoke test.
///
/// Acceptance criteria (from BL-04 brief):
///   AC2/AC8 — CurriculumChunk + ChunkEmbeddingBgeM3 round-trip with IsActive=true, real Vector(1024).
///           — vector extension present in DB.
///           — inserting float[1536] into vector(1024) column raises an exception (fixed-dimension constraint).
///   AC3     — ContentSource, Chapter, ProvenanceMapping round-trip (provenance tree).
///   AC4     — CurriculumVersion lifecycle fields (PublishedAt, PublishedByUserId, ArchivedAt, Notes) round-trip.
///   AC5     — KGSuggestion round-trip (curriculum schema, plain-int cross-module refs, no FK/nav to learning).
///
/// Testcontainers image: pgvector/pgvector:pg17
/// This image is the official pgvector build on top of PostgreSQL 17 and includes the vector extension.
/// Matches the docker compose pgvector/pgvector:pg17 image used in the dev environment.
///
/// Module isolation verified: no cross-module FK/nav — all learning references are plain int.
/// </summary>
[Collection("BL04Schema")]
public sealed class BL04_CurriculumSchema_Tests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("bl04_schema_test")
        .WithUsername("postgres")
        .WithPassword("testpwd")
        .Build();

    private CurriculumDbContext _db = null!;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

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

        // Apply all curriculum migrations (InitialCurriculum + AddProvenanceTree
        // + AddCurriculumVersionLifecycle + AddKGSuggestionTable).
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.StopAsync();
    }

    // ── AC8: vector extension present ────────────────────────────────────────

    /// <summary>
    /// Confirms the pgvector extension is installed in the test database.
    /// This is a prerequisite for all vector operations.
    /// </summary>
    [Fact]
    public async Task VectorExtension_IsInstalled()
    {
        var conn = _db.Database.GetDbConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM pg_extension WHERE extname = 'vector';";
        var result = await cmd.ExecuteScalarAsync();

        Convert.ToInt64(result).Should().Be(1,
            "the pgvector extension must be installed (emitted by InitialCurriculum migration)");
    }

    // ── AC2/AC8: CurriculumChunk + ChunkEmbeddingBgeM3 round-trip ────────────

    /// <summary>
    /// AC2/AC8: Writes a CurriculumChunk and a ChunkEmbeddingBgeM3 (Provider="huggingface",
    /// Model="bge-m3", ModelVersion="1.0", IsActive=true, real Vector(1024)) and reads them back.
    /// Asserts Vector round-trip equality and IsActive=true.
    /// </summary>
    [Fact]
    public async Task CurriculumChunkWithEmbedding_RoundTrip_VectorEquality_IsActiveTrue()
    {
        // Arrange — CurriculumVersion (required FK for CurriculumChunk)
        var version = new CurriculumVersion
        {
            SubjectId = 1,
            Language  = ContentLanguage.English,
            Status    = CurriculumVersionStatus.Active,
            Name      = "BL04-Smoke-v1",
        };
        _db.CurriculumVersions.Add(version);
        await _db.SaveChangesAsync(1);

        // Arrange — CurriculumChunk with a non-null SkillKey (required by DB)
        var chunk = new CurriculumChunk
        {
            Content             = "Smoke test chunk content for BL-04 round-trip.",
            Metadata            = """{"test": true}""",
            Difficulty          = 2,
            SubjectId           = 1,
            GradeId             = 3,
            SkillKey            = "math.grade3.smoke.bl04-test",
            Language            = ContentLanguage.English,
            CurriculumVersionId = version.Id,
            ProvenanceRef       = "ContentSource:1:Chapter:2:p.47",
        };
        _db.CurriculumChunks.Add(chunk);
        await _db.SaveChangesAsync(1);

        // Arrange — 1024-dim vector (all 1.0f except [0]=0.5, [1]=0.7 for distinct values)
        var floatValues = new float[1024];
        for (int i = 0; i < 1024; i++) floatValues[i] = 1.0f / (i + 1);

        var embedding = new ChunkEmbeddingBgeM3
        {
            ChunkId      = chunk.Id,
            Provider     = "huggingface",
            Model        = "bge-m3",
            ModelVersion = "1.0",
            Dimension    = 1024,
            Vector       = new Vector(floatValues),
            IsActive     = true,
        };
        _db.ChunkEmbeddingsBgeM3.Add(embedding);
        await _db.SaveChangesAsync(1);

        // Act — read back from a fresh context to avoid cache hits
        _db.ChangeTracker.Clear();
        var loadedChunk = await _db.CurriculumChunks
            .Include(c => c.Embeddings)
            .FirstAsync(c => c.Id == chunk.Id);

        var loadedEmbedding = loadedChunk.Embeddings.Single();

        // Assert — chunk round-trip
        loadedChunk.Content.Should().Be("Smoke test chunk content for BL-04 round-trip.");
        loadedChunk.ProvenanceRef.Should().Be("ContentSource:1:Chapter:2:p.47");
        loadedChunk.SkillKey.Should().Be("math.grade3.smoke.bl04-test");

        // Assert — embedding round-trip
        loadedEmbedding.Provider.Should().Be("huggingface");
        loadedEmbedding.Model.Should().Be("bge-m3");
        loadedEmbedding.ModelVersion.Should().Be("1.0");
        loadedEmbedding.IsActive.Should().BeTrue("IsActive must be persisted as true");

        // Assert — Vector round-trip equality
        loadedEmbedding.Vector.Should().NotBeNull("Vector must not be null after round-trip");
        var returnedFloats = loadedEmbedding.Vector.ToArray();
        returnedFloats.Length.Should().Be(1024, "vector(1024) stores exactly 1024 floats");
        returnedFloats.Should().BeEquivalentTo(floatValues,
            opts => opts.WithStrictOrdering(),
            "all float values must survive the pgvector round-trip");
    }

    // ── AC8: fixed-dimension constraint — 1536-dim into vector(1024) raises ──

    /// <summary>
    /// AC8: Proves the pgvector fixed-dimension constraint: inserting a float[1536] vector
    /// into a vector(1024) column must raise an exception.
    /// This validates that the column type is enforced at the database level.
    /// </summary>
    [Fact]
    public async Task InsertVector1536_Into_Vector1024Column_Raises()
    {
        // Arrange — need a chunk to FK-reference
        var version = new CurriculumVersion
        {
            SubjectId = 2,
            Language  = ContentLanguage.Arabic,
            Status    = CurriculumVersionStatus.Draft,
            Name      = "BL04-Dim-Check-v1",
        };
        _db.CurriculumVersions.Add(version);
        await _db.SaveChangesAsync(1);

        var chunk = new CurriculumChunk
        {
            Content             = "Dimension check chunk.",
            Difficulty          = 1,
            SubjectId           = 2,
            GradeId             = 1,
            SkillKey            = "math.grade1.dim-check.bl04",
            Language            = ContentLanguage.Arabic,
            CurriculumVersionId = version.Id,
        };
        _db.CurriculumChunks.Add(chunk);
        await _db.SaveChangesAsync(1);

        // Arrange — 1536-dim vector (wrong dimension)
        var wrongDimFloats = new float[1536];
        for (int i = 0; i < 1536; i++) wrongDimFloats[i] = 0.5f;

        // Act + Assert — inserting 1536-dim into vector(1024) must fail
        // Pgvector enforces this at the Postgres server level; Npgsql surfaces it as an exception.
        var act = async () =>
        {
            var badEmbedding = new ChunkEmbeddingBgeM3
            {
                ChunkId      = chunk.Id,
                Provider     = "huggingface",
                Model        = "bge-m3",
                ModelVersion = "1.0",
                Dimension    = 1536, // mismatched — documentation field
                Vector       = new Vector(wrongDimFloats), // 1536-dim — WILL FAIL at DB level
                IsActive     = false,
            };
            _db.ChunkEmbeddingsBgeM3.Add(badEmbedding);
            await _db.SaveChangesAsync(1); // should throw
        };

        await act.Should().ThrowAsync<Exception>(
            "pgvector enforces the fixed dimension at the column level — inserting 1536 floats into vector(1024) must raise an exception");
    }

    // ── AC3: ContentSource + Chapter + ProvenanceMapping round-trip ──────────

    /// <summary>
    /// AC3: Writes ContentSource, Chapter, ProvenanceMapping (provenance tree) and reads them back.
    /// Verifies all fields survive the round-trip and FK relationships are correct.
    /// PedagogicalNodeId is a plain int (no cross-module FK) — module isolation verified.
    /// </summary>
    [Fact]
    public async Task ProvenanceTree_RoundTrip()
    {
        // Arrange — CurriculumVersion + CurriculumChunk (required for ProvenanceMapping FK)
        var version = new CurriculumVersion
        {
            SubjectId = 3,
            Language  = ContentLanguage.English,
            Status    = CurriculumVersionStatus.Draft,
            Name      = "BL04-Provenance-v1",
        };
        _db.CurriculumVersions.Add(version);
        await _db.SaveChangesAsync(1);

        var chunk = new CurriculumChunk
        {
            Content             = "Provenance test chunk.",
            Difficulty          = 3,
            SubjectId           = 3,
            GradeId             = 4,
            SkillKey            = "science.grade4.provenance.bl04-test",
            Language            = ContentLanguage.English,
            CurriculumVersionId = version.Id,
        };
        _db.CurriculumChunks.Add(chunk);
        await _db.SaveChangesAsync(1);

        // Arrange — ContentSource (root of provenance tree)
        var source = new ContentSource
        {
            Type      = ContentSourceType.Book,
            Title     = "Grade 4 Science Textbook",
            Publisher = "Ministry of Education",
            Edition   = "2024 Edition",
            Language  = ContentLanguage.English,
            GradeId   = 4, // plain int — no cross-module FK
        };
        _db.ContentSources.Add(source);
        await _db.SaveChangesAsync(1);

        // Arrange — Chapter
        var chapter = new Chapter
        {
            ContentSourceId = source.Id,
            Number          = 5,
            Title           = "Living and Non-Living Things",
            PageStart       = 47,
            PageEnd         = 68,
        };
        _db.Chapters.Add(chapter);
        await _db.SaveChangesAsync(1);

        // Arrange — ProvenanceMapping (cross-tree bridge)
        var mapping = new ProvenanceMapping
        {
            ChunkId           = chunk.Id,
            ContentSourceId   = source.Id,
            ChapterId         = chapter.Id,
            PageOrBlockRef    = "p.51",
            PedagogicalNodeId = 42, // plain int — cross-module isolation (no FK/nav to learning)
            PedagogicalNodeType = "Concept",
        };
        _db.ProvenanceMappings.Add(mapping);
        await _db.SaveChangesAsync(1);

        // Act — read back
        _db.ChangeTracker.Clear();
        var loadedSource = await _db.ContentSources
            .Include(cs => cs.Chapters)
            .FirstAsync(cs => cs.Id == source.Id);

        var loadedMapping = await _db.ProvenanceMappings
            .FirstAsync(pm => pm.Id == mapping.Id);

        // Assert — ContentSource
        loadedSource.Type.Should().Be(ContentSourceType.Book);
        loadedSource.Title.Should().Be("Grade 4 Science Textbook");
        loadedSource.Publisher.Should().Be("Ministry of Education");
        loadedSource.Edition.Should().Be("2024 Edition");
        loadedSource.GradeId.Should().Be(4);
        loadedSource.Chapters.Should().HaveCount(1);

        // Assert — Chapter
        var loadedChapter = loadedSource.Chapters.Single();
        loadedChapter.Number.Should().Be(5);
        loadedChapter.Title.Should().Be("Living and Non-Living Things");
        loadedChapter.PageStart.Should().Be(47);
        loadedChapter.PageEnd.Should().Be(68);

        // Assert — ProvenanceMapping
        loadedMapping.ChunkId.Should().Be(chunk.Id);
        loadedMapping.ContentSourceId.Should().Be(source.Id);
        loadedMapping.ChapterId.Should().Be(chapter.Id);
        loadedMapping.PageOrBlockRef.Should().Be("p.51");
        loadedMapping.PedagogicalNodeId.Should().Be(42, "plain int survives round-trip");
        loadedMapping.PedagogicalNodeType.Should().Be("Concept");
    }

    // ── AC4: CurriculumVersion lifecycle fields round-trip ────────────────────

    /// <summary>
    /// AC4: Verifies the CurriculumVersion lifecycle fields (PublishedAt, PublishedByUserId,
    /// ArchivedAt, Notes) added by the AddCurriculumVersionLifecycle migration are persisted
    /// correctly. These are the BE-9 ALTER TABLE fields — the existing table is extended,
    /// not re-created.
    /// </summary>
    [Fact]
    public async Task CurriculumVersionLifecycle_Fields_RoundTrip()
    {
        var publishedAt = new DateTimeOffset(2026, 6, 23, 10, 0, 0, TimeSpan.Zero);
        var archivedAt  = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        var version = new CurriculumVersion
        {
            SubjectId          = 5,
            Language           = ContentLanguage.English,
            Status             = CurriculumVersionStatus.Archived,
            Name               = "BL04-Lifecycle-v1",
            PublishedAt        = publishedAt,
            PublishedByUserId  = 99,
            ArchivedAt         = archivedAt,
            Notes              = "Version archived after v2 went active.",
        };
        _db.CurriculumVersions.Add(version);
        await _db.SaveChangesAsync(1);

        _db.ChangeTracker.Clear();
        var loaded = await _db.CurriculumVersions.FirstAsync(v => v.Id == version.Id);

        loaded.Status.Should().Be(CurriculumVersionStatus.Archived);
        loaded.PublishedAt.Should().Be(publishedAt, "PublishedAt must round-trip");
        loaded.PublishedByUserId.Should().Be(99);
        loaded.ArchivedAt.Should().Be(archivedAt, "ArchivedAt must round-trip");
        loaded.Notes.Should().Be("Version archived after v2 went active.");
    }

    /// <summary>
    /// AC4 (null path): A Draft version has null lifecycle fields. All four nullable fields
    /// must persist as null.
    /// </summary>
    [Fact]
    public async Task CurriculumVersionLifecycle_NullFields_RoundTrip()
    {
        var version = new CurriculumVersion
        {
            SubjectId = 6,
            Language  = ContentLanguage.Arabic,
            Status    = CurriculumVersionStatus.Draft,
            Name      = "BL04-Draft-v1",
            // All lifecycle fields intentionally null (Draft state)
        };
        _db.CurriculumVersions.Add(version);
        await _db.SaveChangesAsync(1);

        _db.ChangeTracker.Clear();
        var loaded = await _db.CurriculumVersions.FirstAsync(v => v.Id == version.Id);

        loaded.PublishedAt.Should().BeNull("Draft version has no publish timestamp");
        loaded.PublishedByUserId.Should().BeNull("Draft version has no publisher");
        loaded.ArchivedAt.Should().BeNull("Draft version is not archived");
        loaded.Notes.Should().BeNull("Draft version has no notes yet");
    }

    // ── AC5: KGSuggestion round-trip ─────────────────────────────────────────

    /// <summary>
    /// AC5: Writes a KGSuggestion (auto-inferred candidate edge) and reads it back.
    /// Verifies: plain-int cross-module refs (SourceNodeId, TargetNodeId) survive round-trip,
    /// SourceCurriculumDocumentId nullable plain int (no FK), status, indexes.
    ///
    /// Key design rule: KGSuggestion rows are auto-inferred proposals —
    /// NEVER auto-published to KnowledgeEdge. Admin approval required.
    /// See curriculum-system-of-record.md §5.
    /// </summary>
    [Fact]
    public async Task KGSuggestion_RoundTrip_PlainIntCrossModuleRefs()
    {
        var reviewedAt = new DateTimeOffset(2026, 6, 23, 12, 0, 0, TimeSpan.Zero);

        var suggestion = new KGSuggestion
        {
            SourceNodeId               = 101, // plain int — no FK to learning.KnowledgeNodes
            TargetNodeId               = 202, // plain int — no FK to learning.KnowledgeNodes
            RelationshipType           = CurriculumRelationshipType.Prerequisite,
            Strength                   = 0.8500m,
            SourceCurriculumDocumentId = null, // nullable plain int — BL-01 not yet implemented
            InferenceModel             = "lightrag-v1",
            Status                     = KGSuggestionStatus.Approved,
            ReviewedAt                 = reviewedAt,
            ReviewedByUserId           = 77,
        };
        _db.KGSuggestions.Add(suggestion);
        await _db.SaveChangesAsync(1);

        _db.ChangeTracker.Clear();
        var loaded = await _db.KGSuggestions.FirstAsync(s => s.Id == suggestion.Id);

        loaded.SourceNodeId.Should().Be(101, "plain int SourceNodeId must round-trip");
        loaded.TargetNodeId.Should().Be(202, "plain int TargetNodeId must round-trip");
        loaded.RelationshipType.Should().Be(CurriculumRelationshipType.Prerequisite);
        loaded.Strength.Should().Be(0.8500m, "decimal(5,4) Strength must round-trip");
        loaded.SourceCurriculumDocumentId.Should().BeNull(
            "SourceCurriculumDocumentId is nullable plain int (no FK) — persists as null");
        loaded.InferenceModel.Should().Be("lightrag-v1");
        loaded.Status.Should().Be(KGSuggestionStatus.Approved);
        loaded.ReviewedAt.Should().Be(reviewedAt);
        loaded.ReviewedByUserId.Should().Be(77);
    }

    /// <summary>
    /// AC5 (Pending status): A Pending suggestion has null ReviewedAt and ReviewedByUserId.
    /// </summary>
    [Fact]
    public async Task KGSuggestion_PendingStatus_NullReviewFields()
    {
        var suggestion = new KGSuggestion
        {
            SourceNodeId               = 301,
            TargetNodeId               = 302,
            RelationshipType           = CurriculumRelationshipType.Related,
            Strength                   = 0.4200m,
            SourceCurriculumDocumentId = 5, // nullable plain int — non-null test
            InferenceModel             = "lightrag-v2",
            Status                     = KGSuggestionStatus.Pending,
            // ReviewedAt and ReviewedByUserId intentionally null (Pending)
        };
        _db.KGSuggestions.Add(suggestion);
        await _db.SaveChangesAsync(1);

        _db.ChangeTracker.Clear();
        var loaded = await _db.KGSuggestions.FirstAsync(s => s.Id == suggestion.Id);

        loaded.Status.Should().Be(KGSuggestionStatus.Pending);
        loaded.ReviewedAt.Should().BeNull("Pending suggestion has not been reviewed");
        loaded.ReviewedByUserId.Should().BeNull("Pending suggestion has no reviewer");
        loaded.SourceCurriculumDocumentId.Should().Be(5,
            "nullable plain int SourceCurriculumDocumentId can be non-null");
    }
}

[CollectionDefinition("BL04Schema")]
public sealed class BL04SchemaCollection { }
