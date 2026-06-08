using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Lesson"/>. Lesson *—1 Unit (Restrict) and
/// Lesson *—o1 Skill (optional; SetNull on delete). <c>Difficulty</c> stored as int.
///
/// P7-02: Added <c>IsActive</c> (bool, DEFAULT true) for admin-controlled active/inactive toggle
/// and <c>EstimatedMinutes</c> (int, DEFAULT 0) for estimated lesson duration.
/// The <c>ContentBlocks</c> one-to-many relationship is configured here (Cascade delete
/// so that EF-level deletes propagate; soft-deletes are handled in-handler atomically).
/// </summary>
public class LessonConfig : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("Lessons", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired();

        builder.Property(x => x.SequenceOrder)
            .IsRequired();

        builder.Property(x => x.IsLocked)
            .IsRequired();

        // Enum stored as int (no free-text rule).
        builder.Property(x => x.Difficulty)
            .HasConversion<int>()
            .IsRequired();

        // P7-02: IsActive — admin-controlled visible/hidden toggle (distinct from soft-delete).
        // DEFAULT true so existing/backfilled rows remain visible without a data migration.
        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // P7-02: EstimatedMinutes — lesson duration hint for admin UI. DEFAULT 0 (backfill-safe).
        builder.Property(x => x.EstimatedMinutes)
            .IsRequired()
            .HasDefaultValue(0);

        // P7-05: LifecycleState — editorial lifecycle. DB DEFAULT 2 (Published) backfills existing rows.
        // C# initializer Draft ensures new inserts default to Draft at the application layer.
        builder.Property(x => x.LifecycleState)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(LifecycleState.Published);

        builder.HasOne(l => l.Unit)
            .WithMany(u => u.Lessons)
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional "Lesson teaches Skill" — nullable FK, SetNull on delete (lead decision).
        builder.HasOne(l => l.Skill)
            .WithMany(s => s.Lessons)
            .HasForeignKey(l => l.SkillId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // P7-02: ContentBlocks one-to-many. DeleteBehavior.Cascade so physical deletes propagate
        // to blocks automatically; soft-deletes are handled explicitly in handlers (P7-02-BE-5).
        builder.HasMany(l => l.ContentBlocks)
            .WithOne(cb => cb.Lesson)
            .HasForeignKey(cb => cb.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UnitId)
            .HasDatabaseName("IX_Lessons_UnitId");

        builder.HasIndex(x => x.SkillId)
            .HasDatabaseName("IX_Lessons_SkillId");

        // Composite index for ordered reads within a unit.
        builder.HasIndex(x => new { x.UnitId, x.SequenceOrder })
            .HasDatabaseName("IX_Lessons_UnitId_SequenceOrder");

        // P2-05: lesson content columns — nullable; no index needed (never a filter target).
        builder.Property(x => x.Explanation)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(x => x.Visual)
            .HasMaxLength(1024)
            .IsRequired(false);

        // P2-03: boss flag — non-nullable; defaultValue:false is set in the migration.
        builder.Property(x => x.IsBoss)
            .IsRequired();
    }
}
