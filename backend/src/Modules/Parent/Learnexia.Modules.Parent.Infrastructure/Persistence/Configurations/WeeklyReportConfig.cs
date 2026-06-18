using Learnexia.Modules.Parent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Parent.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="WeeklyReport"/> (schema <c>parent</c>, auto-inherited via
/// <c>HasDefaultSchema</c> on <see cref="ParentDbContext"/>).
///
/// <para>Key decisions:</para>
/// <list type="bullet">
///   <item><see cref="WeeklyReport.ChildId"/> is a plain <c>int</c> — no cross-module FK
///   (module isolation rule 1).</item>
///   <item><see cref="WeeklyReport.WeakAreasJson"/> and <see cref="WeeklyReport.RecommendationsJson"/>
///   use column type <c>jsonb</c> — Npgsql maps <c>string</c> properties to <c>jsonb</c> when the
///   column type is set explicitly. Application layer owns the JSON shape (same pattern as
///   <c>SafetyEvent.FailedChecks</c> / <c>StudentLearningProfile</c>).</item>
///   <item>UNIQUE index on <c>(ChildId, WeekStartUtc)</c> — enforces the idempotent per-week upsert
///   contract: one row per child per week.</item>
///   <item>Supporting index on <c>ChildId</c> — covers the E9 "latest report for child" query pattern.</item>
///   <item>Audit columns (<c>CreatedAt</c>, <c>UpdatedAt</c>, etc.) come from <see cref="FullAuditedEntity"/>
///   and are stamped by <c>ParentDbContext.SaveChangesAsync(int userId)</c>.</item>
/// </list>
///
/// <para>Auto-discovered via <c>ApplyConfigurationsFromAssembly</c> in <see cref="ParentDbContext"/>.</para>
/// </summary>
public class WeeklyReportConfig : IEntityTypeConfiguration<WeeklyReport>
{
    public void Configure(EntityTypeBuilder<WeeklyReport> builder)
    {
        builder.ToTable("WeeklyReport", ParentDbContext.Schema);

        builder.HasKey(x => x.Id);

        // ChildId — plain int, no cross-module FK (module isolation rule 1).
        builder.Property(x => x.ChildId)
            .IsRequired()
            .HasComment("Id of the child (student) this report belongs to. Plain int — no cross-module FK.");

        // WeekStartUtc — Monday 00:00 UTC of the report week.
        // timestamptz (UTC). Part of the unique (ChildId, WeekStartUtc) business key.
        builder.Property(x => x.WeekStartUtc)
            .IsRequired()
            .HasColumnType("timestamptz")
            .HasComment("Monday 00:00 UTC of the report week. Combined with ChildId forms the unique per-week key.");

        // XpEarned — aggregate XP for the week.
        builder.Property(x => x.XpEarned)
            .IsRequired()
            .HasDefaultValue(0)
            .HasComment("XP earned by the child during the report week.");

        // SkillsImproved — count of skills whose mastery improved during the week.
        builder.Property(x => x.SkillsImproved)
            .IsRequired()
            .HasDefaultValue(0)
            .HasComment("Count of distinct skills whose mastery improved during the report week.");

        // WeakAreasJson — JSON snapshot of weak areas as jsonb.
        // Default "[]" = no weak areas (e.g. first-week / no-activity-week state).
        builder.Property(x => x.WeakAreasJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'")
            .HasComment("JSON snapshot of the child's weak areas as of the end of the report week (jsonb).");

        // RecommendationsJson — JSON array of recommendations as jsonb.
        // Default "[]" = no recommendations generated yet.
        builder.Property(x => x.RecommendationsJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'")
            .HasComment("JSON array of recommendations generated for the child based on the week's weak areas (jsonb).");

        // GeneratedAtUtc — UTC timestamp of report generation / last idempotent re-generation.
        builder.Property(x => x.GeneratedAtUtc)
            .IsRequired()
            .HasColumnType("timestamptz")
            .HasComment("UTC timestamp when this report row was generated or last regenerated.");

        // ── Indexes ─────────────────────────────────────────────────────────────────────

        // UNIQUE (ChildId, WeekStartUtc) — enforces one report per child per week; drives
        // the idempotent upsert in the report-generator service (P5-01-BE-2).
        builder.HasIndex(x => new { x.ChildId, x.WeekStartUtc })
            .IsUnique()
            .HasDatabaseName("IX_WeeklyReport_ChildId_WeekStartUtc");

        // Supporting index on ChildId alone — covers the E9 "latest report for child" pattern
        // (ORDER BY WeekStartUtc DESC LIMIT 1 WHERE ChildId = @id) and IDOR queries.
        builder.HasIndex(x => x.ChildId)
            .HasDatabaseName("IX_WeeklyReport_ChildId");
    }
}
