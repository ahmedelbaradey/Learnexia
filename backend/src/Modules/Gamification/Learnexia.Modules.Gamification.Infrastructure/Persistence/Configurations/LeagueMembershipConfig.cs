using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="LeagueMembership"/>. Per-student per-week row.
///
/// Two unique constraints:
///   - <c>UX_LeagueMemberships_PeriodKey_StudentXpProfileId</c> — one membership per student per week
///     (primary enforcement, uses denormalized PeriodKey to avoid JOIN).
///   - <c>UX_LeagueMemberships_LeagueId_StudentXpProfileId</c> — backstop for concurrent placement
///     races (ensures one student per cohort even if two placements race to the same League row).
///
/// Standings query index: <c>IX_LeagueMemberships_LeagueId_WeeklyXp_JoinedAtUtc</c> supports
/// ORDER BY WeeklyXp DESC, JoinedAtUtc ASC tiebreak (D4).
///
/// FK to Leagues is RESTRICT (preserve history); FK to StudentXpProfiles is CASCADE.
/// No cross-module FK constraints (module isolation rule 1).
/// </summary>
public class LeagueMembershipConfig : IEntityTypeConfiguration<LeagueMembership>
{
    public void Configure(EntityTypeBuilder<LeagueMembership> builder)
    {
        builder.ToTable("LeagueMemberships", GamificationDbContext.Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();

        builder.Property(x => x.LeagueId)
            .IsRequired();

        builder.Property(x => x.StudentXpProfileId)
            .IsRequired();

        builder.Property(x => x.PeriodKey)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.JoinOrder)
            .IsRequired();

        builder.Property(x => x.JoinedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.WeeklyXp)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.FinalRank)
            .IsRequired(false);

        builder.Property(x => x.TierAfter)
            .HasConversion<int?>()
            .IsRequired(false);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(MembershipStatus.Active)
            .IsRequired();

        // Primary uniqueness: one membership per student per week (uses denormalized PeriodKey).
        builder.HasIndex(x => new { x.PeriodKey, x.StudentXpProfileId })
            .IsUnique()
            .HasDatabaseName("UX_LeagueMemberships_PeriodKey_StudentXpProfileId");

        // Secondary backstop: one student per cohort.
        builder.HasIndex(x => new { x.LeagueId, x.StudentXpProfileId })
            .IsUnique()
            .HasDatabaseName("UX_LeagueMemberships_LeagueId_StudentXpProfileId");

        // Standings ranking ORDER BY (WeeklyXp DESC, JoinedAtUtc ASC tiebreak).
        builder.HasIndex(x => new { x.LeagueId, x.WeeklyXp, x.JoinedAtUtc })
            .HasDatabaseName("IX_LeagueMemberships_LeagueId_WeeklyXp_JoinedAtUtc");

        // Find-caller's-active-membership read path (placement + standings).
        builder.HasIndex(x => new { x.StudentXpProfileId, x.JoinedAtUtc })
            .HasDatabaseName("IX_LeagueMemberships_StudentXpProfileId_JoinedAtUtc");

        // FK to Leagues: RESTRICT delete — preserve membership history even if league row were deleted.
        builder.HasOne(x => x.League)
            .WithMany(l => l.Memberships)
            .HasForeignKey(x => x.LeagueId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to StudentXpProfiles: CASCADE delete — remove memberships when profile is removed.
        builder.HasOne(x => x.StudentXpProfile)
            .WithMany()
            .HasForeignKey(x => x.StudentXpProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
