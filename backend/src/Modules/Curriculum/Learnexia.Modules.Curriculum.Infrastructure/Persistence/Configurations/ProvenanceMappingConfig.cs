using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Curriculum.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="ProvenanceMapping"/>.
///
/// Key rules (BL-04 BE-8, curriculum-system-of-record.md §1c):
/// - <c>ChunkId</c> is an intra-module FK to <see cref="CurriculumChunk"/> (Cascade — mappings die with the chunk).
/// - <c>ContentSourceId</c> is an intra-module FK to <see cref="ContentSource"/> (Restrict).
/// - <c>ChapterId</c> is an optional intra-module FK to <see cref="Chapter"/> (SetNull — mapping survives if chapter deleted).
/// - <c>PedagogicalNodeId</c> is a plain int (no cross-module FK/nav — module isolation).
/// - Indexes: <c>ChapterId</c>, <c>ContentSourceId</c>, composite <c>(ChunkId, ContentSourceId)</c>.
/// </summary>
public class ProvenanceMappingConfig : IEntityTypeConfiguration<ProvenanceMapping>
{
    public void Configure(EntityTypeBuilder<ProvenanceMapping> builder)
    {
        builder.ToTable("ProvenanceMappings", CurriculumDbContext.Schema);

        builder.HasKey(pm => pm.Id);

        builder.Property(pm => pm.PageOrBlockRef)
            .HasMaxLength(256)
            .IsRequired();

        // PedagogicalNodeId: plain int cross-module ref. No FK/nav (module isolation).
        builder.Property(pm => pm.PedagogicalNodeId).IsRequired();

        builder.Property(pm => pm.PedagogicalNodeType)
            .HasMaxLength(64)
            .IsRequired();

        // Intra-module FK: ProvenanceMapping → CurriculumChunk (Cascade).
        builder.HasOne(pm => pm.Chunk)
            .WithMany()
            .HasForeignKey(pm => pm.ChunkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Intra-module FK: ProvenanceMapping → ContentSource (Restrict).
        builder.HasOne(pm => pm.ContentSource)
            .WithMany()
            .HasForeignKey(pm => pm.ContentSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Intra-module nullable FK: ProvenanceMapping → Chapter (SetNull).
        builder.HasOne(pm => pm.Chapter)
            .WithMany()
            .HasForeignKey(pm => pm.ChapterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Index on ChapterId — join path from chapter to its provenance mappings.
        builder.HasIndex(pm => pm.ChapterId)
            .HasDatabaseName("ix_provenance_mappings_chapter_id");

        // Index on ContentSourceId — join path from source to its provenance mappings.
        builder.HasIndex(pm => pm.ContentSourceId)
            .HasDatabaseName("ix_provenance_mappings_content_source_id");

        // Composite index on (ChunkId, ContentSourceId) — idempotency / dedupe queries.
        builder.HasIndex(pm => new { pm.ChunkId, pm.ContentSourceId })
            .HasDatabaseName("ix_provenance_mappings_chunk_source");
    }
}
