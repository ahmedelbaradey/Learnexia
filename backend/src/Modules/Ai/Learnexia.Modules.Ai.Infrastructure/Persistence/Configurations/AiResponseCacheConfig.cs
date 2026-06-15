using Learnexia.Modules.Ai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Ai.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="AiResponseCache"/> (schema <c>ai</c>).
///
/// Key decisions:
/// - <see cref="AiResponseCache.CacheKey"/> is UNIQUE (primary lookup path).
/// - <see cref="AiResponseCache.QuestionId"/> and <see cref="AiResponseCache.ApprovedBy"/>
///   are plain <c>int</c> — no cross-module FK (module isolation rule 1).
/// - <see cref="AiResponseCache.Type"/> and <see cref="AiResponseCache.ReviewStatus"/> are
///   stored as <c>smallint</c> via <c>HasConversion&lt;int&gt;()</c>.
/// - <see cref="AiResponseCache.CreatedAt"/> and audit timestamps use <c>timestamptz</c> (UTC).
/// - Four indexes: UNIQUE(CacheKey), (Type, ReviewStatus), (SkillKey, CurriculumVersion),
///   (QuestionId) — mirrors the cost-routing §5 spec.
///
/// Mirrors <see cref="SafetyEventConfig"/> conventions.
/// </summary>
public class AiResponseCacheConfig : IEntityTypeConfiguration<AiResponseCache>
{
    public void Configure(EntityTypeBuilder<AiResponseCache> builder)
    {
        builder.ToTable("AiResponseCache");

        builder.HasKey(x => x.Id);

        // PK — bigserial (identity).
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
            .HasColumnType("bigint");

        // CacheKey — SHA-256 hex (64 chars) but allow up to 512 for future algo changes.
        builder.Property(x => x.CacheKey)
            .IsRequired()
            .HasMaxLength(512);

        // Response — unbounded text; no column-type restriction.
        builder.Property(x => x.Response)
            .IsRequired()
            .HasColumnType("text");

        // Type — stored as smallint.
        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnType("smallint");

        // SkillKey — stable curriculum-scoped identifier.
        builder.Property(x => x.SkillKey)
            .IsRequired()
            .HasMaxLength(256);

        // QuestionId — plain int, nullable, no FK (module isolation).
        builder.Property(x => x.QuestionId)
            .IsRequired(false);

        builder.Property(x => x.CurriculumVersion)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.PromptVersion)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.ModelVersion)
            .IsRequired()
            .HasMaxLength(100);

        // ReviewStatus — stored as smallint; R5 gate filter: WHERE ReviewStatus = 1 (Approved).
        builder.Property(x => x.ReviewStatus)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnType("smallint")
            .HasDefaultValue(AiCacheReviewStatus.PendingReview);

        // Confidence — optional decimal(5,4) range 0.0000–1.0000.
        builder.Property(x => x.Confidence)
            .IsRequired(false)
            .HasColumnType("decimal(5,4)");

        // CreatedAt — timestamptz (UTC). Set by the cache-write path at runtime.
        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        // ApprovedBy — plain int, nullable, no cross-module FK.
        builder.Property(x => x.ApprovedBy)
            .IsRequired(false);

        // ApprovedAt — nullable timestamptz.
        builder.Property(x => x.ApprovedAt)
            .IsRequired(false)
            .HasColumnType("timestamptz");

        // InvalidatedAt — nullable timestamptz; set on soft-invalidation (R6).
        builder.Property(x => x.InvalidatedAt)
            .IsRequired(false)
            .HasColumnType("timestamptz");

        // ── Indexes ──────────────────────────────────────────────────────────────────

        // Primary lookup: UNIQUE on CacheKey.
        builder.HasIndex(x => x.CacheKey)
            .IsUnique()
            .HasDatabaseName("IX_AiResponseCache_CacheKey");

        // Admin queue filtering by type + review status.
        builder.HasIndex(x => new { x.Type, x.ReviewStatus })
            .HasDatabaseName("IX_AiResponseCache_Type_ReviewStatus");

        // Bulk invalidation by skill + curriculum version.
        builder.HasIndex(x => new { x.SkillKey, x.CurriculumVersion })
            .HasDatabaseName("IX_AiResponseCache_SkillKey_CurriculumVersion");

        // Targeted per-question invalidation (Decision 4 — filters Hint/WhyWrong by QuestionId).
        builder.HasIndex(x => x.QuestionId)
            .HasDatabaseName("IX_AiResponseCache_QuestionId");
    }
}
