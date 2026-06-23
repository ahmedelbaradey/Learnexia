using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Curriculum.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="KGSuggestion"/>.
///
/// Key rules (BL-04 BE-10, curriculum-system-of-record.md §5):
/// - KGSuggestion rows are auto-inferred proposals — NEVER auto-published to KnowledgeEdge.
///   Admin approval required. See curriculum-system-of-record.md §5.
/// - <c>SourceNodeId</c> and <c>TargetNodeId</c> are plain ints (no cross-module FK/nav).
/// - <c>SourceCurriculumDocumentId</c> is nullable plain int, NO FK (Q-C decision).
/// - Indexes: on <c>Status</c> (queue scans) and composite <c>(SourceNodeId, TargetNodeId, RelationshipType)</c> (idempotency).
/// </summary>
public class KGSuggestionConfig : IEntityTypeConfiguration<KGSuggestion>
{
    public void Configure(EntityTypeBuilder<KGSuggestion> builder)
    {
        builder.ToTable("KGSuggestions", CurriculumDbContext.Schema);

        builder.HasKey(s => s.Id);

        // Plain int cross-module refs — no FK/nav (module isolation).
        builder.Property(s => s.SourceNodeId).IsRequired();
        builder.Property(s => s.TargetNodeId).IsRequired();

        builder.Property(s => s.RelationshipType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.Strength)
            .HasColumnType("decimal(5,4)")
            .IsRequired();

        // Nullable plain int — no FK (BL-01 CurriculumDocument not implemented yet; Q-C decision).
        builder.Property(s => s.SourceCurriculumDocumentId).IsRequired(false);

        builder.Property(s => s.InferenceModel)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.ReviewedAt).IsRequired(false);
        builder.Property(s => s.ReviewedByUserId).IsRequired(false);

        // Index on Status — fast queue scans (admin review queue, pending count).
        builder.HasIndex(s => s.Status)
            .HasDatabaseName("ix_kg_suggestions_status");

        // Composite index on (SourceNodeId, TargetNodeId, RelationshipType) — idempotency check.
        builder.HasIndex(s => new { s.SourceNodeId, s.TargetNodeId, s.RelationshipType })
            .HasDatabaseName("ix_kg_suggestions_source_target_type");
    }
}
