using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ContentBlock"/>. ContentBlock *—1 Lesson (Cascade).
///
/// P7-02: New table in schema <c>learning</c>. Key design decisions:
/// <list type="bullet">
///   <item><c>BlockType</c> stored as int via <c>HasConversion&lt;int&gt;()</c> (enum-as-int rule).</item>
///   <item><c>Payload</c> mapped as <c>jsonb</c> — per-type shape is validated in FluentValidation
///     (app layer), not the DB layer. Same pattern as <c>QuizQuestion.Options</c>.</item>
///   <item>FK to <c>Lessons</c> uses <c>DeleteBehavior.Cascade</c> — blocks are removed when their
///     lesson is physically deleted. Soft-deletes cascade explicitly in the handler (P7-02-BE-5).</item>
///   <item>Composite index on <c>(LessonId, SequenceOrder)</c> mirrors the Lesson/Unit ordering index.</item>
///   <item><c>IsActive</c> DEFAULT true (backfill-safe); NOT in the global soft-delete filter —
///     admins must see inactive blocks to re-activate them; student reads filter per-query.</item>
/// </list>
/// </summary>
public class ContentBlockConfig : IEntityTypeConfiguration<ContentBlock>
{
    public void Configure(EntityTypeBuilder<ContentBlock> builder)
    {
        builder.ToTable("ContentBlocks", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LessonId)
            .IsRequired();

        // BlockType stored as int (enum-as-int convention — no free-text rule).
        builder.Property(x => x.BlockType)
            .HasConversion<int>()
            .IsRequired();

        // Payload stored as jsonb — per-type shape validated in FluentValidation, not DB.
        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.SequenceOrder)
            .IsRequired();

        // IsActive — admin-controlled visible/hidden toggle (distinct from soft-delete).
        // DEFAULT true so any future backfill rows remain visible without a data migration.
        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // FK configured here as a belt-and-suspenders explicit mapping (LessonConfig also
        // configures the HasMany side). Both sides must agree on DeleteBehavior.Cascade.
        builder.HasOne(cb => cb.Lesson)
            .WithMany(l => l.ContentBlocks)
            .HasForeignKey(cb => cb.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite index for ordered reads within a lesson — mirrors IX_Lessons_UnitId_SequenceOrder.
        builder.HasIndex(x => new { x.LessonId, x.SequenceOrder })
            .HasDatabaseName("IX_ContentBlocks_LessonId_SequenceOrder");
    }
}
