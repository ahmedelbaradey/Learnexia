using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="LeagueXpDeltaLog"/>. Append-only idempotency ledger.
///
/// <c>UX_LeagueXpDeltaLogs_LeagueMembershipId_OriginEventId</c> ensures each
/// <c>XpAwardedDomainEvent</c> increments a given membership at most once, even under
/// concurrent delivery or crash-then-retry (D9).
///
/// Mirrors <see cref="MissionProgressLogConfig"/> pattern line-for-line.
/// FK to <c>LeagueMemberships</c> is intra-module (gamification schema) with cascade delete.
/// No cross-module FK constraints (module isolation rule 1).
/// </summary>
public class LeagueXpDeltaLogConfig : IEntityTypeConfiguration<LeagueXpDeltaLog>
{
    public void Configure(EntityTypeBuilder<LeagueXpDeltaLog> builder)
    {
        builder.ToTable("LeagueXpDeltaLogs", GamificationDbContext.Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();

        builder.Property(x => x.LeagueMembershipId)
            .IsRequired();

        builder.Property(x => x.OriginEventId)
            .IsRequired();

        builder.Property(x => x.Delta)
            .IsRequired();

        builder.Property(x => x.OccurredAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Idempotency backstop — one row per (membership, event) pair.
        builder.HasIndex(x => new { x.LeagueMembershipId, x.OriginEventId })
            .IsUnique()
            .HasDatabaseName("UX_LeagueXpDeltaLogs_LeagueMembershipId_OriginEventId");

        // Non-unique FK lookup index for per-membership history queries.
        builder.HasIndex(x => x.LeagueMembershipId)
            .HasDatabaseName("IX_LeagueXpDeltaLogs_LeagueMembershipId");

        // Intra-module FK to LeagueMemberships with cascade delete.
        builder.HasOne(x => x.LeagueMembership)
            .WithMany()
            .HasForeignKey(x => x.LeagueMembershipId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
