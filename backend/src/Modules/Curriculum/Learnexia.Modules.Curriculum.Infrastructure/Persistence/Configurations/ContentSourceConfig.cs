using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Curriculum.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="ContentSource"/>.
///
/// Key rules (BL-04 BE-8, curriculum-system-of-record.md §1b):
/// - <c>GradeId</c> is a plain indexed int — cross-module ref (no FK/nav to learning.Grades).
/// - <c>Type</c> and <c>Language</c> stored as int via HasConversion&lt;int&gt;().
/// - Has many <see cref="Chapter"/>s; FK is declared on the Chapter side.
/// </summary>
public class ContentSourceConfig : IEntityTypeConfiguration<ContentSource>
{
    public void Configure(EntityTypeBuilder<ContentSource> builder)
    {
        builder.ToTable("ContentSources", CurriculumDbContext.Schema);

        builder.HasKey(cs => cs.Id);

        builder.Property(cs => cs.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(cs => cs.Title)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(cs => cs.Publisher)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(cs => cs.Edition)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.Property(cs => cs.Language)
            .HasConversion<int>()
            .IsRequired();

        // GradeId: plain int cross-module ref. No FK/nav (module isolation).
        builder.Property(cs => cs.GradeId).IsRequired();
        builder.HasIndex(cs => cs.GradeId)
            .HasDatabaseName("ix_content_sources_grade_id");
    }
}
