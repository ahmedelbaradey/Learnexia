using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Subject"/>. Subject *—1 Grade (Restrict);
/// Subject 1—* Unit and Subject 1—* Concept (configured on the child side).
///
/// P8-02: Added <c>SubjectCode</c> and <c>Language</c> columns (both stored as int per the
/// no-free-text rule) and a UNIQUE index on <c>(GradeId, SubjectCode, Language)</c> — the
/// natural key for the parallel bilingual tree model (6 roots per grade).
///
/// P7-01: Added <c>SequenceOrder</c> (int, DEFAULT 0) for ordering within a language tree and
/// <c>IsActive</c> (bool, DEFAULT true) for admin-controlled active/inactive toggle.
/// Composite index <c>(GradeId, SequenceOrder)</c> mirrors Unit's <c>(SubjectId, SequenceOrder)</c>.
/// The existing UNIQUE index on <c>(GradeId, SubjectCode, Language)</c> is unchanged and still
/// applies to all rows (soft-deleted or not — no WHERE filter, keeping it consistent with the
/// existing Unit/Subject configs that do not filter on IsDeleted in indexes).
/// </summary>
public class SubjectConfig : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subjects", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired();

        // Country nullable (single-country MVP) — explicit for clarity.
        builder.Property(x => x.Country)
            .IsRequired(false);

        // P8-02: SubjectCode and Language stored as int (enum-as-int convention).
        builder.Property(x => x.SubjectCode)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Language)
            .HasConversion<int>()
            .IsRequired();

        // P7-01: SequenceOrder for display ordering within the (GradeId, Language) tree. Default 0.
        builder.Property(x => x.SequenceOrder)
            .IsRequired()
            .HasDefaultValue(0);

        // P7-01: IsActive — admin-controlled visible/hidden toggle (distinct from soft-delete).
        // DEFAULT true so existing/backfilled rows remain visible without a data migration.
        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // P7-05: LifecycleState — editorial draft/published/archived lifecycle.
        // DB DEFAULT 2 (Published) backfills all existing rows to Published so live curriculum stays visible.
        // The C# entity initializer is Draft — new inserts always send the C# value (Draft=1), overriding
        // the DB DEFAULT. EF only applies HasDefaultValue when the property has the CLR default (0/null);
        // because the entity initializer sets it to Draft (=1, non-zero), EF sends the C# value on insert.
        builder.Property(x => x.LifecycleState)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(LifecycleState.Published);

        builder.HasIndex(x => x.GradeId)
            .HasDatabaseName("IX_Subjects_GradeId");

        // P8-02: UNIQUE natural key — one tree per (GradeId, SubjectCode, Language).
        // Unchanged: no partial/filtered version. The unique constraint covers all rows (including
        // soft-deleted) — this is intentional so a soft-deleted tree's natural key slot stays reserved
        // until physically cleaned up. The backend-feature stage must handle the UX implication.
        builder.HasIndex(x => new { x.GradeId, x.SubjectCode, x.Language })
            .IsUnique()
            .HasDatabaseName("IX_Subjects_GradeId_SubjectCode_Language");

        // P7-01: Composite index for ordered reads within a grade.
        builder.HasIndex(x => new { x.GradeId, x.SequenceOrder })
            .HasDatabaseName("IX_Subjects_GradeId_SequenceOrder");
    }
}
