using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="BadgeDefinition"/>. Badge catalog — seeded by
/// <c>BadgeSeeder</c>, not via migration <c>InsertData</c>.
///
/// Unique index <c>UX_BadgeDefinitions_Code</c> on <c>Code</c> allows the engine and
/// tests to reference badges by their stable string code.
///
/// <see cref="BadgeRarity"/> and <see cref="BadgeTriggerType"/> enums are stored as int (EF
/// convention for enum-typed properties; explicit cast avoids string storage overhead).
/// </summary>
public class BadgeDefinitionConfig : IEntityTypeConfiguration<BadgeDefinition>
{
    public void Configure(EntityTypeBuilder<BadgeDefinition> builder)
    {
        builder.ToTable("BadgeDefinitions", GamificationDbContext.Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();

        builder.Property(x => x.Code)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(240)
            .IsRequired();

        builder.Property(x => x.IconKey)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Rarity)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.TriggerType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Threshold)
            .IsRequired(false);

        builder.Property(x => x.RewardXp)
            .IsRequired();

        // Stable code index — badge engine + tests reference by Code string.
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_BadgeDefinitions_Code");
    }
}
