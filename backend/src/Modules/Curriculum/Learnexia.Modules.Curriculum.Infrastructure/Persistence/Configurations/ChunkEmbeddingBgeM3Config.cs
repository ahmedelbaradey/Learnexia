using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Curriculum.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="ChunkEmbeddingBgeM3"/>.
///
/// Key rules (P3-07 BE-4, curriculum-system-of-record.md §4 Decision D):
/// - The pgvector extension is declared in <see cref="CurriculumDbContext.OnModelCreating"/>
///   via <c>modelBuilder.HasPostgresExtension("vector")</c> — emits
///   <c>CREATE EXTENSION IF NOT EXISTS vector</c> in the migration.
/// - Maps <c>Vector</c> to <c>vector(1024)</c> (BGE-M3, 1024-dim).
/// - HNSW index on <c>Vector</c> with <c>vector_cosine_ops</c> (OQ-2 cosine, OQ-4 HNSW).
/// - Index on <c>IsActive</c> — fast filter for the currently-served model.
/// - Index on <c>ChunkId</c> — JOIN path from chunk to embeddings.
/// - FK to CurriculumChunk with ON DELETE CASCADE (intra-module FK only).
/// </summary>
public class ChunkEmbeddingBgeM3Config : IEntityTypeConfiguration<ChunkEmbeddingBgeM3>
{
    public void Configure(EntityTypeBuilder<ChunkEmbeddingBgeM3> builder)
    {
        builder.ToTable("chunk_embeddings_bge_m3", CurriculumDbContext.Schema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ChunkId).IsRequired();

        builder.Property(e => e.Provider)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.Model)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.ModelVersion)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.Dimension).IsRequired();

        builder.Property(e => e.IsActive).IsRequired();

        // Map the Vector property to vector(1024) column type.
        // Pgvector.EntityFrameworkCore provides the HasColumnType extension for this.
        builder.Property(e => e.Vector)
            .HasColumnType("vector(1024)")
            .IsRequired();

        // HNSW cosine index on Vector — enables approximate nearest-neighbour search.
        // vector_cosine_ops = cosine distance (<=>), matching the BGE-M3 normalized embeddings (OQ-2/OQ-4).
        builder.HasIndex(e => e.Vector)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasDatabaseName("ix_chunk_embeddings_bge_m3_vector_hnsw_cosine");

        // Index on IsActive — fast filter for the currently-served model.
        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("ix_chunk_embeddings_bge_m3_is_active");

        // Intra-module FK: ChunkEmbeddingBgeM3 → CurriculumChunk (ON DELETE CASCADE).
        // CurriculumChunkConfig.Configure() also declares this relationship from the principal side.
        builder.HasIndex(e => e.ChunkId)
            .HasDatabaseName("ix_chunk_embeddings_bge_m3_chunk_id");
    }
}
