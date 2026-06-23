using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Curriculum.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="CurriculumVersion"/>.
///
/// Filtered unique index ensures at most one Active version per (SubjectId, Language)
/// — enforces curriculum-system-of-record.md §3 rule at the DB level.
///
/// Note (BL-04 Q4): <c>CurriculumVersion</c> is the curriculum-tree publish unit (keyed on SubjectId+Language,
/// status Draft/Active/Archived). It is SEPARATE from <c>learning.ContentVersions</c> (P7-05 per-entity snapshot
/// log). Do NOT merge or rename either entity. See curriculum-system-of-record.md §3.
///
/// Note (BL-04 Q-A): SubjectId (int) is the canonical keying dimension.
/// </summary>
public class CurriculumVersionConfig : IEntityTypeConfiguration<CurriculumVersion>
{
    public void Configure(EntityTypeBuilder<CurriculumVersion> builder)
    {
        builder.ToTable("CurriculumVersions", CurriculumDbContext.Schema);

        builder.HasKey(v => v.Id);

        builder.Property(v => v.SubjectId).IsRequired();

        builder.Property(v => v.Language)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(v => v.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(v => v.Name)
            .HasMaxLength(256)
            .IsRequired();

        // ── BL-04 lifecycle fields (BE-9, ALTER TABLE only — never re-create) ─────────────────────
        builder.Property(v => v.PublishedAt)
            .IsRequired(false);

        builder.Property(v => v.PublishedByUserId)
            .IsRequired(false);

        builder.Property(v => v.ArchivedAt)
            .IsRequired(false);

        builder.Property(v => v.Notes)
            .HasMaxLength(1024)
            .IsRequired(false);

        // Filtered unique index: at most one Active version per (SubjectId, Language).
        // Draft and Archived versions are not constrained by this index.
        builder.HasIndex(v => new { v.SubjectId, v.Language })
            .IsUnique()
            .HasFilter($"\"Status\" = {(int)CurriculumVersionStatus.Active}")
            .HasDatabaseName("ix_curriculum_versions_active_subject_language");
    }
}
