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

        // Filtered unique index: at most one Active version per (SubjectId, Language).
        // Draft and Archived versions are not constrained by this index.
        builder.HasIndex(v => new { v.SubjectId, v.Language })
            .IsUnique()
            .HasFilter($"\"Status\" = {(int)CurriculumVersionStatus.Active}")
            .HasDatabaseName("ix_curriculum_versions_active_subject_language");
    }
}
