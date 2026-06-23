using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Curriculum.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="Chapter"/>.
///
/// Key rules (BL-04 BE-8, curriculum-system-of-record.md §1b):
/// - <c>ContentSourceId</c> is an intra-module FK to <see cref="ContentSource"/> (Restrict delete).
/// - Index on <c>ContentSourceId</c> — join path from ContentSource to its Chapters.
/// </summary>
public class ChapterConfig : IEntityTypeConfiguration<Chapter>
{
    public void Configure(EntityTypeBuilder<Chapter> builder)
    {
        builder.ToTable("Chapters", CurriculumDbContext.Schema);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Number).IsRequired();

        builder.Property(c => c.Title)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(c => c.PageStart).IsRequired(false);
        builder.Property(c => c.PageEnd).IsRequired(false);

        // Intra-module FK: Chapter → ContentSource (Restrict — do not cascade-delete chapters).
        builder.HasOne(c => c.ContentSource)
            .WithMany(cs => cs.Chapters)
            .HasForeignKey(c => c.ContentSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.ContentSourceId)
            .HasDatabaseName("ix_chapters_content_source_id");
    }
}
