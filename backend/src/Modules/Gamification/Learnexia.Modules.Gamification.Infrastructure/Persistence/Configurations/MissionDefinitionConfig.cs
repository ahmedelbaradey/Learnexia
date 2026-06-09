using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="MissionDefinition"/>. Mission catalog — seeded by
/// <c>MissionSeeder</c>, not via migration <c>InsertData</c>.
///
/// Unique index <c>UX_MissionDefinitions_Code</c> on <c>Code</c> allows the engine and
/// tests to reference missions by their stable string code, and prevents duplicate seeding.
///
/// <see cref="MissionType"/> and <see cref="MissionTargetType"/> enums are stored as int
/// (EF convention; explicit HasConversion avoids string storage overhead).
/// </summary>
public class MissionDefinitionConfig : IEntityTypeConfiguration<MissionDefinition>
{
    public void Configure(EntityTypeBuilder<MissionDefinition> builder)
    {
        builder.ToTable("MissionDefinitions", GamificationDbContext.Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();

        builder.Property(x => x.Code)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.IconKey)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.TitleKey)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.Cadence)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.TargetType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Target)
            .IsRequired();

        builder.Property(x => x.RewardXp)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .HasDefaultValue(0)
            .IsRequired();

        // P7-13: admin-controlled active/inactive toggle. Default true (backfill existing rows to active).
        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Stable code index — mission engine + tests reference by Code string; backstop for seeder idempotency.
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_MissionDefinitions_Code");
    }
}
