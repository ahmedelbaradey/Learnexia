using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Curriculum.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="PipelineJob"/>.
///
/// PipelineJobs is the DB-outbox seam consumed by the Python poller (curriculum-system-of-record.md §4b).
/// JobType/Status are STRINGS — a cross-process contract; do not convert to int enums.
///
/// Key rules (BL-01 BE-9):
/// - <c>JobType</c> and <c>Status</c> are VARCHAR strings — the cross-process contract with Python.
///   Python polls via SELECT WHERE Status='Pending' and claims via UPDATE SET Status='Processing'.
///   NEVER convert these to int columns — Python would need to know the int mapping.
/// - <c>DocumentId</c> is an intra-module FK → curriculum.CurriculumDocuments.Id (real FK allowed;
///   same module/schema). ON DELETE CASCADE: removing a document removes its jobs.
/// - Indexes: composite <c>(Status, JobType)</c> for the polling query; single <c>DocumentId</c>
///   for the FK + admin lookups.
/// </summary>
public class PipelineJobConfig : IEntityTypeConfiguration<PipelineJob>
{
    public void Configure(EntityTypeBuilder<PipelineJob> builder)
    {
        builder.ToTable("PipelineJobs", CurriculumDbContext.Schema);

        builder.HasKey(j => j.Id);

        // JobType: string — cross-process contract with Python. Max 64 chars.
        // Valid values: 'parse', 'ingest', 'embed'.
        builder.Property(j => j.JobType)
            .HasMaxLength(64)
            .IsRequired();

        // Status: string — cross-process contract with Python poller. Max 32 chars.
        // Valid values: 'Pending', 'Processing', 'Done', 'Failed', 'PermanentlyFailed'.
        builder.Property(j => j.Status)
            .HasMaxLength(32)
            .IsRequired();

        // DocumentId: OPTIONAL intra-module FK → CurriculumDocuments.Id (same curriculum schema).
        // Null for 'infer_edges' jobs (graph-level, no source document).
        // Set for 'parse'/'ingest' jobs.
        builder.Property(j => j.DocumentId).IsRequired(false);

        builder.HasOne(j => j.Document)
            .WithMany(d => d.PipelineJobs)
            .HasForeignKey(j => j.DocumentId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        // Index on DocumentId — FK lookups and admin queries per document.
        builder.HasIndex(j => j.DocumentId)
            .HasDatabaseName("ix_pipeline_jobs_document_id");

        // Composite index on (Status, JobType) — the Python poller's primary scan:
        // SELECT * FROM PipelineJobs WHERE Status='Pending' AND JobType='parse' ...
        builder.HasIndex(j => new { j.Status, j.JobType })
            .HasDatabaseName("ix_pipeline_jobs_status_job_type");

        builder.Property(j => j.PayloadJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(j => j.ResultJson)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(2048)
            .IsRequired(false);

        // CreatedAt is inherited from CreationAuditedEntity (DateTime) — not re-declared here.
        builder.Property(j => j.ClaimedAt).IsRequired(false);
        builder.Property(j => j.CompletedAt).IsRequired(false);

        builder.Property(j => j.RetryCount)
            .HasDefaultValue(0)
            .IsRequired();
    }
}
