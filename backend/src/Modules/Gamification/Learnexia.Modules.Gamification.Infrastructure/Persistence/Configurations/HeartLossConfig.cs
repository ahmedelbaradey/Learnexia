using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="HeartLoss"/>. Append-only audit ledger — no update/delete paths.
///
/// Idempotency constraint: <c>UX_HeartLosses_OriginEventId</c> on <c>OriginEventId</c>
/// ensures a redelivered integration event cannot create a duplicate heart-loss row.
///
/// FK to <c>StudentXpProfiles</c> is intra-module (gamification schema) with cascade delete.
/// No cross-module FK constraints (module isolation rule 1).
/// </summary>
public class HeartLossConfig : IEntityTypeConfiguration<HeartLoss>
{
    public void Configure(EntityTypeBuilder<HeartLoss> builder)
    {
        builder.ToTable("HeartLosses", GamificationDbContext.Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();

        builder.Property(x => x.StudentXpProfileId)
            .IsRequired();

        builder.Property(x => x.OriginEventId)
            .IsRequired();

        builder.Property(x => x.HeartsAfter)
            .IsRequired();

        builder.Property(x => x.OccurredAtUtc)
            .IsRequired();

        // Soft cross-module refs — nullable, no FK constraint (module isolation rule 1).
        // Mirrors XpAwardConfig.LessonId / SkillId pattern.
        builder.Property(x => x.LessonId)
            .IsRequired(false);

        builder.Property(x => x.SkillId)
            .IsRequired(false);

        // AC4 idempotency: unique on OriginEventId — redelivered events are rejected at DB level.
        builder.HasIndex(x => x.OriginEventId)
            .IsUnique()
            .HasDatabaseName("UX_HeartLosses_OriginEventId");

        // Non-unique FK lookup index.
        builder.HasIndex(x => x.StudentXpProfileId)
            .HasDatabaseName("IX_HeartLosses_StudentXpProfileId");

        // Intra-module FK to StudentXpProfiles with cascade delete.
        builder.HasOne(h => h.StudentXpProfile)
            .WithMany()
            .HasForeignKey(h => h.StudentXpProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
