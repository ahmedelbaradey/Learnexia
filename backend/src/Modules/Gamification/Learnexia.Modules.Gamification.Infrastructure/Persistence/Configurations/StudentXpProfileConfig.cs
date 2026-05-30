using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="StudentXpProfile"/>. One row per student (unique on
/// <c>StudentId</c>). No cross-module FK — <c>StudentId</c> is a plain int (module isolation rule 1).
/// </summary>
public class StudentXpProfileConfig : IEntityTypeConfiguration<StudentXpProfile>
{
    public void Configure(EntityTypeBuilder<StudentXpProfile> builder)
    {
        builder.ToTable("StudentXpProfiles", GamificationDbContext.Schema);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentId)
            .IsRequired();

        builder.Property(x => x.TotalXp)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.CurrentLevel)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.LastAwardAtUtc)
            .IsRequired();

        // Streak columns (P4-03-B1-1)
        builder.Property(p => p.CurrentStreak)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.LongestStreak)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.LastActivityDateUtc)
            .HasColumnName("LastActivityDateUtc")
            .HasColumnType("date")
            .IsRequired(false);

        // Unique index: one profile row per student. Also the primary read-path index.
        builder.HasIndex(x => x.StudentId)
            .IsUnique()
            .HasDatabaseName("UX_StudentXpProfiles_StudentId");

        // Navigation: one profile has many awards (cascade delete)
        builder.HasMany(x => x.Awards)
            .WithOne(a => a.StudentXpProfile)
            .HasForeignKey(a => a.StudentXpProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
