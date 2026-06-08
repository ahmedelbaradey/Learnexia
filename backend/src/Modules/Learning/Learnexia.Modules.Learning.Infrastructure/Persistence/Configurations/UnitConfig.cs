using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Unit"/>. Unit *—1 Subject (Restrict); Unit 1—* Lesson.
///
/// P7-01: Added <c>IsActive</c> (bool, DEFAULT true) for admin-controlled active/inactive toggle.
/// <c>SequenceOrder</c> was already present. Soft-delete columns are inherited from
/// <c>FullAuditedEntity</c> and were already mapped by convention.
/// </summary>
public class UnitConfig : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired();

        builder.Property(x => x.SequenceOrder)
            .IsRequired();

        // P7-01: IsActive — admin-controlled visible/hidden toggle (distinct from soft-delete).
        // DEFAULT true so existing/backfilled rows remain visible without a data migration.
        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // P7-05: LifecycleState — editorial lifecycle. DB DEFAULT 2 (Published) backfills existing rows.
        // C# initializer Draft ensures new inserts default to Draft at the application layer.
        builder.Property(x => x.LifecycleState)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(LifecycleState.Published);

        builder.HasOne(u => u.Subject)
            .WithMany(s => s.Units)
            .HasForeignKey(u => u.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SubjectId)
            .HasDatabaseName("IX_Units_SubjectId");

        // Composite index for ordered reads within a subject.
        builder.HasIndex(x => new { x.SubjectId, x.SequenceOrder })
            .HasDatabaseName("IX_Units_SubjectId_SequenceOrder");
    }
}
