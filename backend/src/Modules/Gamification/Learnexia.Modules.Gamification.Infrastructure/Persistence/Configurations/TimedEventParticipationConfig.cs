using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="TimedEventParticipation"/>. Per-student participation
/// record for a timed XP event.
///
/// Table: <c>gamification.TimedEventParticipations</c>.
///
/// Indexes:
/// <list type="bullet">
///   <item>
///     <c>UX_TimedEventParticipations_TimedEventId_StudentXpProfileId</c> — unique on
///     (TimedEventId, StudentXpProfileId) — lazy-instantiation race backstop (mirrors
///     <c>UX_StudentMissions_…</c>).
///   </item>
///   <item>
///     <c>IX_TimedEventParticipations_State_EventEndUtc</c> — composite on (Status, EventEndUtc)
///     for the ending-soon scan (mirrors <c>IX_StudentMissions_Status_PeriodEndUtc_MissionType</c>).
///   </item>
/// </list>
///
/// FK behaviors:
/// <list type="bullet">
///   <item>→ <c>StudentXpProfiles</c>: CASCADE delete — profile removal removes participation history.</item>
///   <item>→ <c>TimedEvents</c>: RESTRICT delete — catalog rows must not cascade-delete participation history.</item>
/// </list>
///
/// CHECK constraints (defence in depth):
/// <list type="bullet">
///   <item><c>CK_TimedEventParticipations_Progress</c> — <c>Progress &gt;= 0 AND Progress &lt;= Target</c>.</item>
/// </list>
///
/// No cross-module FK constraints (module isolation rule 1).
/// Auto-discovered via <c>ApplyConfigurationsFromAssembly</c> in <see cref="GamificationDbContext"/>.
/// </summary>
public class TimedEventParticipationConfig : IEntityTypeConfiguration<TimedEventParticipation>
{
    public void Configure(EntityTypeBuilder<TimedEventParticipation> builder)
    {
        builder.ToTable("TimedEventParticipations", GamificationDbContext.Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();

        builder.Property(x => x.TimedEventId)
            .IsRequired();

        builder.Property(x => x.StudentXpProfileId)
            .IsRequired();

        builder.Property(x => x.Target)
            .IsRequired();

        builder.Property(x => x.Progress)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(TimedEventParticipationStatus.NotStarted)
            .IsRequired();

        builder.Property(x => x.JoinedUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.CompletedUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(x => x.EventEndUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // -----------------------------------------------------------------------
        // Indexes
        // -----------------------------------------------------------------------

        // Unique index — lazy-instantiation race backstop: one participation per (event, student).
        builder.HasIndex(x => new { x.TimedEventId, x.StudentXpProfileId })
            .IsUnique()
            .HasDatabaseName("UX_TimedEventParticipations_TimedEventId_StudentXpProfileId");

        // Ending-soon scan: "find in-progress participations whose event window closes soon".
        builder.HasIndex(x => new { x.Status, x.EventEndUtc })
            .HasDatabaseName("IX_TimedEventParticipations_Status_EventEndUtc");

        // -----------------------------------------------------------------------
        // FK relationships (intra-module only)
        // -----------------------------------------------------------------------

        // Cascade delete: removing a student profile removes all timed-event participation history.
        builder.HasOne(x => x.StudentXpProfile)
            .WithMany()
            .HasForeignKey(x => x.StudentXpProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict delete: catalog events must not cascade-delete participation history.
        builder.HasOne<TimedEvent>()
            .WithMany()
            .HasForeignKey(x => x.TimedEventId)
            .OnDelete(DeleteBehavior.Restrict);

        // -----------------------------------------------------------------------
        // CHECK constraints (defence in depth)
        // -----------------------------------------------------------------------

        builder.ToTable(tb =>
        {
            // Progress must be non-negative and cannot exceed Target.
            tb.HasCheckConstraint(
                "CK_TimedEventParticipations_Progress",
                "\"Progress\" >= 0 AND \"Progress\" <= \"Target\"");
        });
    }
}
