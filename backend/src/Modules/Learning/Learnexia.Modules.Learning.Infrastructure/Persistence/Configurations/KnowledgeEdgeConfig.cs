using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="KnowledgeEdge"/>.
///
/// Key design choices:
/// - Both FKs use <c>DeleteBehavior.Restrict</c> to prevent silent orphan edges when nodes are deleted.
/// - <c>Strength</c> is stored as decimal(5,4) with a DB-level default of 1.0.
/// - A composite unique index (SourceNodeId, TargetNodeId, RelationshipType) prevents duplicate edges.
/// - <c>RelationshipType</c> stored as int.
/// </summary>
public class KnowledgeEdgeConfig : IEntityTypeConfiguration<KnowledgeEdge>
{
    public void Configure(EntityTypeBuilder<KnowledgeEdge> builder)
    {
        builder.ToTable("KnowledgeEdges", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RelationshipType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Strength)
            .HasPrecision(5, 4)
            .HasDefaultValue(1.0m)
            .IsRequired();

        // ── Self-referential FK: SourceNode (Restrict) ──────────────────────────────────────────
        builder.HasOne(e => e.SourceNode)
            .WithMany(n => n.SourceEdges)
            .HasForeignKey(e => e.SourceNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SourceNodeId)
            .HasDatabaseName("IX_KnowledgeEdges_SourceNodeId");

        // ── Self-referential FK: TargetNode (Restrict) ──────────────────────────────────────────
        builder.HasOne(e => e.TargetNode)
            .WithMany(n => n.TargetEdges)
            .HasForeignKey(e => e.TargetNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TargetNodeId)
            .HasDatabaseName("IX_KnowledgeEdges_TargetNodeId");

        // ── Composite unique index: one edge per (Source, Target, Type) ─────────────────────────
        builder.HasIndex(e => new { e.SourceNodeId, e.TargetNodeId, e.RelationshipType })
            .IsUnique()
            .HasDatabaseName("UX_KnowledgeEdges_SourceTarget_Type");
    }
}
