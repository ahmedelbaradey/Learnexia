using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Curriculum.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="IngestionReviewItem"/>.
///
/// Key rules (BL-05 BE-2, Q5 decision):
/// - Low-confidence ingestion outputs are withheld from the published curriculum tree pending
///   human (admin) approval. The default confidence threshold is 0.7 (config-driven — not hardcoded).
///   Items below the threshold are routed here by the ingest-advance service (BE-6 review routing).
///
/// - <see cref="IngestionReviewItem.CurriculumDocumentId"/> — intra-module FK (same curriculum schema),
///   <see cref="DeleteBehavior.Cascade"/>: removing the source document removes all its review items.
///
/// - <see cref="IngestionReviewItem.CurriculumVersionId"/> — intra-module FK, nullable (resolved after
///   CurriculumVersionResolver runs); no cascade (versions are not deleted routinely).
///
/// - <see cref="IngestionReviewItem.ReviewedByUserId"/> — plain int, no FK (cross-module isolation).
///
/// - <see cref="IngestionReviewItem.PayloadJson"/> — stored as "jsonb" for Postgres-native JSON query support.
///
/// - <see cref="IngestionReviewItem.Confidence"/> — decimal(5,4) matching KGSuggestion.Strength precision.
///
/// Indexes:
/// - <c>ix_ingestion_review_items_status</c> — fast queue scans (pending count, admin list).
/// - <c>ix_ingestion_review_items_document_id</c> — per-document lookups.
/// - <c>ix_ingestion_review_items_document_status</c> — composite (CurriculumDocumentId, Status) for
///   idempotency checks and filtered pending queues (the primary operational access pattern).
/// - <c>ix_ingestion_review_items_version_id</c> — per-version lookups.
/// </summary>
public class IngestionReviewItemConfig : IEntityTypeConfiguration<IngestionReviewItem>
{
    public void Configure(EntityTypeBuilder<IngestionReviewItem> builder)
    {
        builder.ToTable("IngestionReviewItems", CurriculumDbContext.Schema);

        builder.HasKey(r => r.Id);

        // Intra-module FK to CurriculumDocument — cascade delete.
        builder.Property(r => r.CurriculumDocumentId).IsRequired();
        builder.HasOne(r => r.CurriculumDocument)
            .WithMany(d => d.IngestionReviewItems)
            .HasForeignKey(r => r.CurriculumDocumentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ingestion_review_items_document");

        // Intra-module FK to CurriculumVersion — nullable, no cascade (versions outlive review items).
        builder.Property(r => r.CurriculumVersionId).IsRequired(false);

        // SourceReference: source page/block ref for the admin reviewer to trace back to the PDF.
        builder.Property(r => r.SourceReference)
            .HasMaxLength(512)
            .IsRequired();

        // SuggestedClassification: human-readable LLM proposal string.
        builder.Property(r => r.SuggestedClassification)
            .HasMaxLength(1024)
            .IsRequired();

        // PayloadJson: full structured LLM output stored as jsonb for Postgres-native query support.
        builder.Property(r => r.PayloadJson)
            .HasColumnType("jsonb")
            .IsRequired();

        // Confidence: decimal(5,4) — matches KGSuggestion.Strength precision; range [0.0000, 1.0000].
        builder.Property(r => r.Confidence)
            .HasColumnType("decimal(5,4)")
            .IsRequired();

        // Status: ReviewStatus stored as int; defaults to Pending on creation.
        builder.Property(r => r.Status)
            .HasConversion<int>()
            .IsRequired();

        // ReviewedByUserId: plain int — cross-module isolation (no FK to identity.Users).
        builder.Property(r => r.ReviewedByUserId).IsRequired(false);

        builder.Property(r => r.ReviewedAt).IsRequired(false);

        // ReviewNotes: optional rejection reason or approval notes.
        builder.Property(r => r.ReviewNotes)
            .HasMaxLength(2048)
            .IsRequired(false);

        // Index on Status — fast queue scans (admin pending list, count).
        builder.HasIndex(r => r.Status)
            .HasDatabaseName("ix_ingestion_review_items_status");

        // Index on CurriculumDocumentId — per-document lookups.
        builder.HasIndex(r => r.CurriculumDocumentId)
            .HasDatabaseName("ix_ingestion_review_items_document_id");

        // Composite index (CurriculumDocumentId, Status) — primary operational access pattern:
        // "give me all Pending review items for document X" (used by review routing and re-ingest cleanup).
        builder.HasIndex(r => new { r.CurriculumDocumentId, r.Status })
            .HasDatabaseName("ix_ingestion_review_items_document_status");

        // Index on CurriculumVersionId — per-version lookups.
        builder.HasIndex(r => r.CurriculumVersionId)
            .HasDatabaseName("ix_ingestion_review_items_version_id");
    }
}
