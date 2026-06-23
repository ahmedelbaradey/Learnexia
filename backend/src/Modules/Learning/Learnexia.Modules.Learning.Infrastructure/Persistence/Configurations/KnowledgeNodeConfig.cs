using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="KnowledgeNode"/>.
///
/// Key design choices:
/// - <c>SkillId</c> uses a filtered unique index (WHERE SkillId IS NOT NULL) so that one-to-one
///   wrapping of Skill nodes is enforced at the database level while still allowing many unlinked nodes.
/// - FKs to Subject and Grade use Restrict to prevent accidental cascade deletes.
/// - FK to Skill uses SetNull so deleting a Skill detaches the node rather than deleting it.
/// - <c>NodeType</c> stored as int (no free-text).
/// </summary>
public class KnowledgeNodeConfig : IEntityTypeConfiguration<KnowledgeNode>
{
    public void Configure(EntityTypeBuilder<KnowledgeNode> builder)
    {
        builder.ToTable("KnowledgeNodes", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.NodeType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.SubjectId)
            .IsRequired();

        builder.Property(x => x.GradeId)
            .IsRequired();

        builder.Property(x => x.Difficulty)
            .IsRequired();

        builder.Property(x => x.SkillId)
            .IsRequired(false);

        // ── FK: Subject (same-module, Restrict) ─────────────────────────────────────────────────
        builder.HasOne<Subject>()
            .WithMany()
            .HasForeignKey(n => n.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SubjectId)
            .HasDatabaseName("IX_KnowledgeNodes_SubjectId");

        // ── FK: Grade (same-module, Restrict) ───────────────────────────────────────────────────
        builder.HasOne<Grade>()
            .WithMany()
            .HasForeignKey(n => n.GradeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.GradeId)
            .HasDatabaseName("IX_KnowledgeNodes_GradeId");

        // ── FK: Skill (nullable, SetNull so node survives skill deletion) ───────────────────────
        builder.HasOne(n => n.Skill)
            .WithMany()
            .HasForeignKey(n => n.SkillId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Filtered unique index: at most one node per Skill when SkillId is set.
        builder.HasIndex(x => x.SkillId)
            .IsUnique()
            .HasFilter($"\"{LearningSchema.Name}\".\"KnowledgeNodes\".\"SkillId\" IS NOT NULL")
            .HasDatabaseName("UX_KnowledgeNodes_SkillId");

        // ── BL-04 BE-11/12 — SkillKey (stable semantic identity, Decision C) ──────────────────────
        // Format: {SubjectCode}.grade{N}.{unitSlug}.{skillSlug}
        // Example: math.grade4.fractions.add-unlike-denominators
        // Slugify = lowercase, spaces→hyphens, strip non-alphanumeric except hyphens.
        // Nullable (Q3 decision): NOT NULL deferred until all KnowledgeNode writers confirm readiness.
        builder.Property(x => x.SkillKey)
            .HasMaxLength(200)
            .IsRequired(false);

        // Partial unique index mirroring UX_KnowledgeNodes_SkillId pattern.
        // Filter syntax matches the existing UX_KnowledgeNodes_SkillId filter exactly.
        builder.HasIndex(x => x.SkillKey)
            .IsUnique()
            .HasFilter($"\"{LearningSchema.Name}\".\"KnowledgeNodes\".\"SkillKey\" IS NOT NULL")
            .HasDatabaseName("UX_KnowledgeNodes_SkillKey");
    }
}
