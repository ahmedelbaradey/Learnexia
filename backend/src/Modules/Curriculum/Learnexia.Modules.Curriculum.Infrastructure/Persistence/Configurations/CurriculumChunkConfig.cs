using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Curriculum.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="CurriculumChunk"/>.
///
/// Key rules (P3-07 BE-4):
/// - NO vector column on this table. Vectors live in <c>chunk_embeddings_bge_m3</c> (Decision D).
/// - Hierarchy refs (SubjectId, GradeId, ConceptId, SkillId) are plain int — no cross-module FK.
/// - CurriculumVersionId is an intra-module FK to CurriculumVersions.
/// - Composite index on (GradeId, SubjectId) + individual indexes on SkillId, SkillKey, CurriculumVersionId.
/// </summary>
public class CurriculumChunkConfig : IEntityTypeConfiguration<CurriculumChunk>
{
    public void Configure(EntityTypeBuilder<CurriculumChunk> builder)
    {
        builder.ToTable("CurriculumChunks", CurriculumDbContext.Schema);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content)
            .IsRequired();

        builder.Property(c => c.Metadata)
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(c => c.Difficulty)
            .IsRequired();

        builder.Property(c => c.SubjectId).IsRequired();
        builder.Property(c => c.GradeId).IsRequired();

        builder.Property(c => c.ConceptId);
        builder.Property(c => c.SkillId);

        builder.Property(c => c.SkillKey)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.Language)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(c => c.CurriculumVersionId).IsRequired();
        builder.Property(c => c.ProvenanceRef).HasMaxLength(512);

        // Intra-module FK: CurriculumChunk → CurriculumVersion.
        // No cross-module FKs; hierarchy refs remain plain int.
        builder.HasOne(c => c.CurriculumVersion)
            .WithMany(v => v.Chunks)
            .HasForeignKey(c => c.CurriculumVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Navigation: one chunk has many embeddings.
        builder.HasMany(c => c.Embeddings)
            .WithOne(e => e.Chunk)
            .HasForeignKey(e => e.ChunkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite index on (GradeId, SubjectId) — primary retrieval filter.
        builder.HasIndex(c => new { c.GradeId, c.SubjectId })
            .HasDatabaseName("ix_curriculum_chunks_grade_subject");

        // Index on SkillId — weak-area filter key for RAG retrieval.
        builder.HasIndex(c => c.SkillId)
            .HasDatabaseName("ix_curriculum_chunks_skill_id");

        // Index on SkillKey — stable semantic identity for version-aware matching.
        builder.HasIndex(c => c.SkillKey)
            .HasDatabaseName("ix_curriculum_chunks_skill_key");

        // Index on CurriculumVersionId — join path from version to chunks.
        builder.HasIndex(c => c.CurriculumVersionId)
            .HasDatabaseName("ix_curriculum_chunks_version_id");
    }
}
