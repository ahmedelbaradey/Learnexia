using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="XpAward"/>. Append-only ledger — no update/delete paths.
///
/// Idempotency constraint: <c>UX_XpAwards_OriginEventId_Reason</c> on <c>(OriginEventId, Reason)</c>
/// ensures a redelivered integration event with the same EventId + Reason cannot double-award (AC4).
///
/// Soft references: <c>LessonId</c> and <c>SkillId</c> are plain nullable int columns
/// with no FK constraints (module isolation rule 1 — no cross-module FKs).
///
/// FK to <c>StudentXpProfiles</c> is handled by <c>StudentXpProfileConfig.HasMany()</c>
/// with cascade delete — no redundant config here.
/// </summary>
public class XpAwardConfig : IEntityTypeConfiguration<XpAward>
{
    public void Configure(EntityTypeBuilder<XpAward> builder)
    {
        builder.ToTable("XpAwards", GamificationDbContext.Schema);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentXpProfileId)
            .IsRequired();

        builder.Property(x => x.OriginEventId)
            .IsRequired();

        // Enum stored as int — consistent with Learning module convention (LessonConfig.Difficulty).
        builder.Property(x => x.Reason)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.XpAmount)
            .IsRequired();

        builder.Property(x => x.OccurredAtUtc)
            .IsRequired();

        // Soft cross-module refs — nullable, no FK constraint.
        builder.Property(x => x.LessonId)
            .IsRequired(false);

        builder.Property(x => x.SkillId)
            .IsRequired(false);

        // AC4 idempotency: unique across (OriginEventId, Reason) — redelivered events are rejected at DB level.
        builder.HasIndex(x => new { x.OriginEventId, x.Reason })
            .IsUnique()
            .HasDatabaseName("UX_XpAwards_OriginEventId_Reason");

        // Non-unique query performance index on the FK column.
        builder.HasIndex(x => x.StudentXpProfileId)
            .HasDatabaseName("IX_XpAwards_StudentXpProfileId");
    }
}
