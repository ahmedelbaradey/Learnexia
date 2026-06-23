using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Curriculum.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="CurriculumDocument"/>.
///
/// Key rules (BL-01 BE-2, curriculum-system-of-record.md §4b):
/// - <c>GradeId</c> and <c>SubjectId</c> are plain indexed ints — cross-module refs (no FK/nav).
///   Module isolation: they reference learning.Grades.Id and learning.Subjects.Id respectively.
/// - <c>ObjectKey</c> is required (GUID-based storage key, never a URL).
/// - <c>Status</c> (<see cref="DocumentStatus"/>) stored as int via HasConversion&lt;int&gt;().
///   This is the .NET-internal lifecycle — NOT the cross-process string used in PipelineJob.Status.
/// - <c>Language</c> stored as int via HasConversion&lt;int&gt;() (mirrors ContentSourceConfig).
/// - Indexes: on <c>Status</c> (queue/admin scans), composite on <c>(GradeId, SubjectId)</c> (admin filtering).
/// </summary>
public class CurriculumDocumentConfig : IEntityTypeConfiguration<CurriculumDocument>
{
    public void Configure(EntityTypeBuilder<CurriculumDocument> builder)
    {
        builder.ToTable("CurriculumDocuments", CurriculumDbContext.Schema);

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName)
            .HasMaxLength(512)
            .IsRequired();

        // ObjectKey: required GUID-based storage key — never a URL.
        builder.Property(d => d.ObjectKey)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(d => d.ContentType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(d => d.FileSize)
            .IsRequired();

        // GradeId: plain int cross-module ref. No FK/nav (module isolation).
        builder.Property(d => d.GradeId).IsRequired();
        builder.HasIndex(d => d.GradeId)
            .HasDatabaseName("ix_curriculum_documents_grade_id");

        // SubjectId: plain int cross-module ref. No FK/nav (module isolation).
        builder.Property(d => d.SubjectId).IsRequired();

        // Composite index on (GradeId, SubjectId) — admin filtering.
        builder.HasIndex(d => new { d.GradeId, d.SubjectId })
            .HasDatabaseName("ix_curriculum_documents_grade_subject");

        // Language: reuse ContentLanguage enum, stored as int.
        builder.Property(d => d.Language)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.Country)
            .HasMaxLength(64)
            .IsRequired(false);

        // Status: DocumentStatus stored as int — internal .NET lifecycle, never read by Python.
        builder.Property(d => d.Status)
            .HasConversion<int>()
            .IsRequired();

        // Index on Status — queue scans and admin list filtering.
        builder.HasIndex(d => d.Status)
            .HasDatabaseName("ix_curriculum_documents_status");

        builder.Property(d => d.StatusReason)
            .HasMaxLength(1024)
            .IsRequired(false);

        // ParsedArtifactObjectKey: nullable MinIO object key written when the parse job completes.
        // Max 512 mirrors ObjectKey. Column type: character varying(512), nullable.
        builder.Property(d => d.ParsedArtifactObjectKey)
            .HasMaxLength(512)
            .IsRequired(false);

        // ParsedAt: nullable timestamptz — stamped by the .NET advance poller on parse completion.
        builder.Property(d => d.ParsedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);
    }
}
