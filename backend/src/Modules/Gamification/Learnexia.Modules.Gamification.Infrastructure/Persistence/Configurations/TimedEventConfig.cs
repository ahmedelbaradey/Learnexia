using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="TimedEvent"/>. Time-bounded XP multiplier event catalog.
///
/// Table: <c>gamification.TimedEvents</c>.
///
/// Indexes:
/// <list type="bullet">
///   <item><c>UX_TimedEvents_Code</c> — unique on <see cref="TimedEvent.Code"/> (seeder idempotency + domain-event correlation).</item>
///   <item><c>IX_TimedEvents_IsActive_StartUtc</c> — composite on (IsActive, StartUtc) for <c>TimedEventSweepJob</c> pass-1 scans.</item>
///   <item><c>IX_TimedEvents_StartUtc_EndUtc</c> — composite on (StartUtc, EndUtc) for active-event range queries and <c>IActiveTimedEventsQuery</c>.</item>
/// </list>
///
/// CHECK constraints (defence in depth alongside application-layer validation):
/// <list type="bullet">
///   <item><c>CK_TimedEvents_Multiplier</c> — <c>Multiplier BETWEEN 1.00 AND 5.00</c>.</item>
///   <item><c>CK_TimedEvents_DateRange</c> — <c>StartUtc &lt; EndUtc</c>.</item>
/// </list>
///
/// No cross-module FK constraints (module isolation rule 1).
/// </summary>
public class TimedEventConfig : IEntityTypeConfiguration<TimedEvent>
{
    public void Configure(EntityTypeBuilder<TimedEvent> builder)
    {
        builder.ToTable("TimedEvents", GamificationDbContext.Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();

        builder.Property(x => x.Code)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.NameEn)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.NameAr)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.DescriptionEn)
            .HasMaxLength(512)
            .IsRequired(false);

        builder.Property(x => x.DescriptionAr)
            .HasMaxLength(512)
            .IsRequired(false);

        builder.Property(x => x.StartUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.EndUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // numeric(4,2) stores values like 1.50, 2.00, 5.00.
        builder.Property(x => x.Multiplier)
            .HasColumnType("numeric(4,2)")
            .IsRequired();

        builder.Property(x => x.Scope)
            .HasConversion<int>()
            .IsRequired();

        // Sweep-job idempotency latch — false until the sweep job transitions the event into its window.
        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Optional per-event participation goal. Nullable — existing rows remain untouched (zero backfill).
        builder.Property(x => x.ParticipationTarget)
            .IsRequired(false);

        // -----------------------------------------------------------------------
        // Indexes
        // -----------------------------------------------------------------------

        // Unique index: stable code key — seeder idempotency + domain-event correlation.
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_TimedEvents_Code");

        // Composite index: sweep-job pass-1 scan (IsActive, StartUtc).
        builder.HasIndex(x => new { x.IsActive, x.StartUtc })
            .HasDatabaseName("IX_TimedEvents_IsActive_StartUtc");

        // Composite index: active-event range query (StartUtc, EndUtc).
        builder.HasIndex(x => new { x.StartUtc, x.EndUtc })
            .HasDatabaseName("IX_TimedEvents_StartUtc_EndUtc");

        // -----------------------------------------------------------------------
        // CHECK constraints (defence in depth)
        // -----------------------------------------------------------------------

        builder.ToTable(tb =>
        {
            // Multiplier ceiling: [1.00 .. 5.00]. Mirrors MaxMultiplierCeiling in TimedEventOptions.
            tb.HasCheckConstraint(
                "CK_TimedEvents_Multiplier",
                "\"Multiplier\" >= 1.0 AND \"Multiplier\" <= 5.0");

            // Temporal sanity: end must be strictly after start.
            tb.HasCheckConstraint(
                "CK_TimedEvents_DateRange",
                "\"StartUtc\" < \"EndUtc\"");

            // ParticipationTarget must be positive when set (null = use config default).
            tb.HasCheckConstraint(
                "CK_TimedEvents_ParticipationTarget",
                "\"ParticipationTarget\" IS NULL OR \"ParticipationTarget\" > 0");
        });
    }
}
