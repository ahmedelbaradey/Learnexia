using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="StudentBadge"/>. Append-only ledger — no update/delete paths.
///
/// AC3 idempotency backstop: <c>UX_StudentBadges_StudentXpProfileId_BadgeDefinitionId</c> composite
/// unique index ensures a badge can be awarded at most once per student.
///
/// FK to <c>StudentXpProfiles</c> is intra-module (gamification schema) with cascade delete
/// (deleting a student profile removes their badge ledger rows).
/// FK to <c>BadgeDefinitions</c> is intra-module with <c>Restrict</c> delete — badge catalog
/// rows must not cascade-delete student achievement records.
/// No cross-module FK constraints (module isolation rule 1).
/// </summary>
public class StudentBadgeConfig : IEntityTypeConfiguration<StudentBadge>
{
    public void Configure(EntityTypeBuilder<StudentBadge> builder)
    {
        builder.ToTable("StudentBadges", GamificationDbContext.Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();

        builder.Property(x => x.StudentXpProfileId)
            .IsRequired();

        builder.Property(x => x.BadgeDefinitionId)
            .IsRequired();

        builder.Property(x => x.OriginEventId)
            .IsRequired();

        builder.Property(x => x.OriginEventType)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.AwardedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // AC3 idempotency backstop — one badge per student.
        builder.HasIndex(x => new { x.StudentXpProfileId, x.BadgeDefinitionId })
            .IsUnique()
            .HasDatabaseName("UX_StudentBadges_StudentXpProfileId_BadgeDefinitionId");

        // Non-unique index for read-path: dashboard recent-N + collection screen.
        builder.HasIndex(x => x.StudentXpProfileId)
            .HasDatabaseName("IX_StudentBadges_StudentXpProfileId");

        // Non-unique index for query performance on BadgeDefinitionId.
        builder.HasIndex(x => x.BadgeDefinitionId)
            .HasDatabaseName("IX_StudentBadges_BadgeDefinitionId");

        // Intra-module FK to StudentXpProfiles — cascade delete removes badge rows with the profile.
        builder.HasOne(x => x.StudentXpProfile)
            .WithMany()
            .HasForeignKey(x => x.StudentXpProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Intra-module FK to BadgeDefinitions — restrict: catalog rows must not remove student records.
        builder.HasOne(x => x.BadgeDefinition)
            .WithMany()
            .HasForeignKey(x => x.BadgeDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
