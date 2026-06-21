using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="QuestionRecalibrationProposal"/> (P5-07 BE-6).
///
/// Key decisions:
/// - <c>QuestionId</c> is a real intra-module FK to <c>QuizQuestion</c> (DeleteBehavior.Restrict) —
///   consistent with <c>StudentAnswer.QuestionId</c>. Cannot delete a question while an open or
///   historical proposal exists (preserves the full audit trail per AC7).
/// - <c>AuthoredDifficulty</c>, <c>ProposedDifficulty</c>, <c>Status</c> all stored as
///   <c>integer</c> via <c>HasConversion&lt;int&gt;()</c> — same pattern as every other enum in
///   the Learning module (no magic string/int).
/// - <c>ResolvedAtUtc</c> stored as <c>timestamptz</c> nullable — mirrors
///   <c>StudentRecommendation.GeneratedAtUtc</c> and <c>QuestionDifficultyStats.ComputedAtUtc</c>.
/// - <c>ResolvedByUserId</c> is a plain <c>int?</c> — NO cross-module FK to Identity (module
///   isolation rule 1).
/// - Composite index <c>(QuestionId, Status)</c> supports "open proposal for this question"
///   lookups and the admin list-by-status filter.
/// - <b>Partial unique index</b> on <c>QuestionId WHERE "Status" = 1</c> (Pending) enforces the
///   one-open-proposal-per-question rule at the database level (OQ-3 decision: DB-level is safer
///   against races). Filter uses <c>HasFilter("\"Status\" = 1")</c> — same quoting style as
///   <c>SeatLedgerEntryConfig.IdempotencyKey</c> filtered unique index in the Billing module.
/// - Discovered automatically by <c>LearningDbContext.ApplyConfigurationsFromAssembly</c>.
/// </summary>
public class QuestionRecalibrationProposalConfig : IEntityTypeConfiguration<QuestionRecalibrationProposal>
{
    public void Configure(EntityTypeBuilder<QuestionRecalibrationProposal> builder)
    {
        builder.ToTable("QuestionRecalibrationProposals", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        // ── QuestionId — real intra-module FK (Restrict) ─────────────────────────────────────────
        builder.Property(x => x.QuestionId)
            .IsRequired();

        builder.HasOne(x => x.Question)
            .WithMany()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Enum columns — stored as integer ─────────────────────────────────────────────────────
        builder.Property(x => x.AuthoredDifficulty)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.ProposedDifficulty)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>();

        // ── Evidence columns ──────────────────────────────────────────────────────────────────────
        builder.Property(x => x.EvidencePValue)
            .IsRequired();

        builder.Property(x => x.EvidenceSampleSize)
            .IsRequired();

        // ── ReasonCode — stable machine-generated code, NOT localized prose ───────────────────────
        builder.Property(x => x.ReasonCode)
            .IsRequired();

        // ── ResolvedAtUtc — timestamptz nullable ─────────────────────────────────────────────────
        builder.Property(x => x.ResolvedAtUtc)
            .IsRequired(false)
            .HasColumnType("timestamptz");

        // ── ResolvedByUserId — plain int?, no cross-module FK ────────────────────────────────────
        builder.Property(x => x.ResolvedByUserId)
            .IsRequired(false);

        // ── Indexes ───────────────────────────────────────────────────────────────────────────────

        // Composite — "open proposal for this question" lookups + admin list-by-status filter.
        builder.HasIndex(x => new { x.QuestionId, x.Status })
            .HasDatabaseName("IX_QuestionRecalibrationProposals_QuestionId_Status");

        // Partial unique — at most ONE Pending proposal per question (OQ-3, DB-level enforcement).
        // HasFilter uses double-quoted column name (PostgreSQL identifier quoting) — same style as
        // SeatLedgerEntryConfig: HasFilter("\"IdempotencyKey\" IS NOT NULL").
        // Status = 1 corresponds to ProposalStatus.Pending (non-zero start enum).
        builder.HasIndex(x => x.QuestionId)
            .IsUnique()
            .HasFilter("\"Status\" = 1")
            .HasDatabaseName("UX_QuestionRecalibrationProposals_QuestionId_Pending");
    }
}
