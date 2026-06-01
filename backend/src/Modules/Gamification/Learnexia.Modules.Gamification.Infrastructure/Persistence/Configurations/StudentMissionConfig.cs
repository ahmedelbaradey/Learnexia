using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="StudentMission"/>. Per-period mission instance.
///
/// Unique index <c>UX_StudentMissions_StudentXpProfileId_MissionDefinitionId_PeriodKey</c> prevents
/// duplicate instantiation under lazy-instantiation races (any concurrent inserter catches a
/// <c>DbUpdateException</c> and re-reads — same pattern as badge idempotency backstop).
///
/// Non-unique indexes:
/// - <c>IX_StudentMissions_StudentXpProfileId_Status_PeriodEndUtc</c> — dashboard read path
///   ("give me my active missions for this period").
/// - <c>IX_StudentMissions_Status_PeriodEndUtc_MissionType</c> — Hangfire <c>MissionRolloverJob</c>
///   sweep path ("find expired-but-incomplete missions by type").
///
/// FK to <c>StudentXpProfiles</c> — cascade delete (profile removal clears mission history).
/// FK to <c>MissionDefinitions</c> — restrict delete (catalog rows must not cascade-delete history).
/// </summary>
public class StudentMissionConfig : IEntityTypeConfiguration<StudentMission>
{
    public void Configure(EntityTypeBuilder<StudentMission> builder)
    {
        builder.ToTable("StudentMissions", GamificationDbContext.Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();

        builder.Property(x => x.StudentXpProfileId)
            .IsRequired();

        builder.Property(x => x.MissionDefinitionId)
            .IsRequired();

        builder.Property(x => x.MissionType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.PeriodKey)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.PeriodStartUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.PeriodEndUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.Target)
            .IsRequired();

        builder.Property(x => x.Progress)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(MissionStatus.NotStarted)
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        // AC2 idempotency backstop — prevents duplicate mission instances under lazy-instantiation race.
        builder.HasIndex(x => new { x.StudentXpProfileId, x.MissionDefinitionId, x.PeriodKey })
            .IsUnique()
            .HasDatabaseName("UX_StudentMissions_StudentXpProfileId_MissionDefinitionId_PeriodKey");

        // Dashboard read path: "give me active missions for this student".
        builder.HasIndex(x => new { x.StudentXpProfileId, x.Status, x.PeriodEndUtc })
            .HasDatabaseName("IX_StudentMissions_StudentXpProfileId_Status_PeriodEndUtc");

        // Hangfire rollover sweep path: "find expired-but-incomplete missions by type".
        builder.HasIndex(x => new { x.Status, x.PeriodEndUtc, x.MissionType })
            .HasDatabaseName("IX_StudentMissions_Status_PeriodEndUtc_MissionType");

        // Intra-module FK to StudentXpProfiles — cascade delete removes mission rows with the profile.
        builder.HasOne(x => x.StudentXpProfile)
            .WithMany()
            .HasForeignKey(x => x.StudentXpProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Intra-module FK to MissionDefinitions — restrict: catalog rows must not remove student records.
        builder.HasOne(x => x.MissionDefinition)
            .WithMany()
            .HasForeignKey(x => x.MissionDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
