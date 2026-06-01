using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="MissionProgressLog"/>. Append-only idempotency ledger —
/// no update/delete paths.
///
/// AC2 idempotency backstop: <c>UX_MissionProgressLogs_StudentMissionId_OriginEventId</c>
/// composite unique index ensures each integration/domain event increments a given mission
/// at most once, even under concurrent delivery.
///
/// FK to <c>StudentMissions</c> is intra-module (gamification schema) with cascade delete.
/// No cross-module FK constraints (module isolation rule 1).
/// </summary>
public class MissionProgressLogConfig : IEntityTypeConfiguration<MissionProgressLog>
{
    public void Configure(EntityTypeBuilder<MissionProgressLog> builder)
    {
        builder.ToTable("MissionProgressLogs", GamificationDbContext.Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();

        builder.Property(x => x.StudentMissionId)
            .IsRequired();

        builder.Property(x => x.OriginEventId)
            .IsRequired();

        builder.Property(x => x.OriginEventType)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.ProgressDelta)
            .IsRequired();

        // AC2 idempotency backstop — one row per (mission, event) pair; DB-level race protection.
        builder.HasIndex(x => new { x.StudentMissionId, x.OriginEventId })
            .IsUnique()
            .HasDatabaseName("UX_MissionProgressLogs_StudentMissionId_OriginEventId");

        // Non-unique FK lookup index for per-mission history queries.
        builder.HasIndex(x => x.StudentMissionId)
            .HasDatabaseName("IX_MissionProgressLogs_StudentMissionId");

        // Intra-module FK to StudentMissions with cascade delete.
        builder.HasOne(x => x.StudentMission)
            .WithMany()
            .HasForeignKey(x => x.StudentMissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
