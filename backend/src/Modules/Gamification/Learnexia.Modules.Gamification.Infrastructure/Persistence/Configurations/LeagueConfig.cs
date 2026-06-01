using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="League"/>. One row per (Tier, PeriodKey, GroupIndex) cohort.
///
/// Append-only after creation — no mutation paths other than the navigation collection.
/// <c>UX_Leagues_Tier_PeriodKey_GroupIndex</c> prevents duplicate cohort creation under concurrent
/// placement and serves as the primary lookup key.
/// <c>IX_Leagues_PeriodKey_Tier</c> supports the promotion job's per-period scan and the
/// fill-query during lazy placement.
///
/// No cross-module FK constraints (module isolation rule 1).
/// </summary>
public class LeagueConfig : IEntityTypeConfiguration<League>
{
    public void Configure(EntityTypeBuilder<League> builder)
    {
        builder.ToTable("Leagues", GamificationDbContext.Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();

        builder.Property(x => x.Tier)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.PeriodKey)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.PeriodStartUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.PeriodEndUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.GroupIndex)
            .IsRequired();

        builder.Property(x => x.Capacity)
            .HasDefaultValue(30)
            .IsRequired();

        // Prevents duplicate cohort creation under concurrent placement races.
        builder.HasIndex(x => new { x.Tier, x.PeriodKey, x.GroupIndex })
            .IsUnique()
            .HasDatabaseName("UX_Leagues_Tier_PeriodKey_GroupIndex");

        // Supports promotion job's per-period iteration and placement fill queries.
        builder.HasIndex(x => new { x.PeriodKey, x.Tier })
            .HasDatabaseName("IX_Leagues_PeriodKey_Tier");
    }
}
