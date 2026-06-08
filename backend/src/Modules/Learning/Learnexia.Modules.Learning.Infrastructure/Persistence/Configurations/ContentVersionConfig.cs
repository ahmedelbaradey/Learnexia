using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ContentVersion"/>. New table in schema <c>learning</c>.
///
/// P7-05 design decisions:
/// <list type="bullet">
///   <item><c>EntityType</c> stored as int via <c>HasConversion&lt;int&gt;()</c> (enum-as-int rule).</item>
///   <item><c>EntityId</c> is a plain int with no FK constraint — module-isolation rule; the
///     application layer resolves the (EntityType, EntityId) pair.</item>
///   <item><c>Snapshot</c> mapped as <c>jsonb</c> — per-entity serialized state at publish time.
///     Same jsonb column pattern as <c>QuizQuestion.Options</c> / <c>ContentBlock.Payload</c>.</item>
///   <item><c>Language</c> stored as int via <c>HasConversion&lt;int&gt;()</c> for the owning tree's
///     language tag — enables the publication-coverage query.</item>
///   <item><c>PublishedAtUtc</c> mapped as <c>timestamp with time zone</c> (timestamptz) per the
///     project PostgreSQL convention. Callers must call <c>.ToUniversalTime()</c> at the mapping
///     boundary under <c>Npgsql.EnableLegacyTimestampBehavior=true</c> (HANDOFF P4-11).</item>
///   <item>Unique composite index on <c>(EntityType, EntityId, VersionNumber)</c> enforces the
///     monotonic-per-partition ordering and enables O(1) lookup of a specific version.</item>
///   <item>Global soft-delete filter (<c>IsDeleted != true</c>) is registered on
///     <c>ContentVersion</c> in <see cref="LearningDbContext.OnModelCreating"/>. Because
///     <c>ContentVersion</c> has NO required FK navigations to other filtered entities, there is
///     NO required-relationship-filter warning to handle here.</item>
/// </list>
/// </summary>
public class ContentVersionConfig : IEntityTypeConfiguration<ContentVersion>
{
    public void Configure(EntityTypeBuilder<ContentVersion> builder)
    {
        builder.ToTable("ContentVersions", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        // EntityType: which entity type this snapshot belongs to (Subject/Unit/Lesson/QuizQuestion).
        builder.Property(x => x.EntityType)
            .IsRequired()
            .HasConversion<int>();

        // EntityId: PK of the versioned entity. Plain int — no FK constraint (module isolation).
        builder.Property(x => x.EntityId)
            .IsRequired();

        // VersionNumber: monotonically increasing per (EntityType, EntityId) partition.
        builder.Property(x => x.VersionNumber)
            .IsRequired();

        // Snapshot: full serialized entity state at publish time, stored as jsonb.
        builder.Property(x => x.Snapshot)
            .IsRequired()
            .HasColumnType("jsonb");

        // PublishedByUserId: admin user id from JWT claim. Plain int — no FK.
        builder.Property(x => x.PublishedByUserId)
            .IsRequired();

        // PublishedAtUtc: timestamptz. Callers call .ToUniversalTime() at the mapping boundary
        // under Npgsql.EnableLegacyTimestampBehavior=true (HANDOFF P4-11).
        builder.Property(x => x.PublishedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        // Language: owning Subject tree's ContentLanguage at publish time. Stored as int.
        builder.Property(x => x.Language)
            .IsRequired()
            .HasConversion<int>();

        // Unique index: one version number per (EntityType, EntityId) — enforces monotonic history
        // and enables efficient lookup of a specific version or ordered version history.
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.VersionNumber })
            .IsUnique()
            .HasDatabaseName("IX_ContentVersions_EntityType_EntityId_VersionNumber");

        // Non-unique index on (EntityType, EntityId) for history scans without version projection.
        builder.HasIndex(x => new { x.EntityType, x.EntityId })
            .HasDatabaseName("IX_ContentVersions_EntityType_EntityId");
    }
}
