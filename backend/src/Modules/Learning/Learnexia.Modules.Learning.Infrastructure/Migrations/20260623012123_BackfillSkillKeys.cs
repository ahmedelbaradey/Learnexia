using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Learning.Infrastructure.Migrations
{
    /// <summary>
    /// BE-13 — Backfills SkillKey on P2-11 hand-seeded KnowledgeNode rows.
    ///
    /// Derivation (BL-04 Q3 decision — NULLABLE, NO NOT NULL constraint):
    ///   SkillKey = {subjectCode}.grade{gradeNumber}.{slugified_name}
    ///   where subjectCode is derived from learning.Subjects.SubjectCode (MATH=0→"math", etc.)
    ///         gradeNumber is from learning.Grades.Number
    ///         slugified_name = lower(Node.Name), spaces→hyphens, non-alphanumeric stripped.
    ///
    /// Format: {SubjectCode}.grade{N}.{slugified_name}
    /// The backfill uses the node's Name as the slug (P2-11 nodes seeded from Skill.Name directly).
    /// A separate unit-slug join would require multi-hop traversal — Name-only for backfill.
    ///
    /// Slug rules: lowercase, spaces→hyphens, strip non-alphanumeric except hyphens.
    ///
    /// SAFE: rows where slug derivation yields a conflict are LEFT NULL rather than aborting.
    /// Two dedup guards are applied:
    ///   1. Within-batch dedup: if two or more rows in the same UPDATE batch would receive the
    ///      same candidate key (e.g. nodes with slugifiable names that produce identical keys
    ///      within the same subject+grade partition), ALL of them are left NULL.
    ///   2. Cross-existing-row guard: if the candidate key already exists on a row that already
    ///      has a SkillKey set, the new row is left NULL.
    /// Both guards together ensure the partial unique index UX_KnowledgeNodes_SkillKey is never
    /// violated. Ambiguous/duplicate nodes simply stay NULL — acceptable per Q3 decision.
    ///
    /// Down(): clears backfilled SkillKey values via pattern match on the backfill format.
    /// </summary>
    public partial class BackfillSkillKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Compute candidate SkillKey values for all KnowledgeNodes where SkillKey IS NULL.
            // Build key from: subjectCodeStr + ".grade" + gradeNumber + "." + slugified_node_name
            // Only updates rows where the derived key would not violate the partial unique index.
            //
            // Slug derivation (PostgreSQL):
            //   1. Strip non-alphanumeric except spaces and hyphens
            //   2. Trim
            //   3. Replace spaces with hyphens
            //   4. Collapse consecutive hyphens
            //   5. Lowercase
            //
            // SubjectCode mapping (mirrors learning.SubjectCode enum):
            //   0=MATH→"math", 1=SCIENCE→"science", 2=ARABIC→"arabic", 3=ENGLISH→"english"
            migrationBuilder.Sql(@"
WITH candidates AS (
    SELECT
        kn.""Id"",
        LOWER(
            REGEXP_REPLACE(
                REGEXP_REPLACE(
                    TRIM(REGEXP_REPLACE(kn.""Name"", '[^a-zA-Z0-9 \-]', '', 'g')),
                    ' +', '-', 'g'
                ),
                '-+', '-', 'g'
            )
        ) AS name_slug,
        CASE s.""SubjectCode""
            WHEN 0 THEN 'math'
            WHEN 1 THEN 'science'
            WHEN 2 THEN 'arabic'
            WHEN 3 THEN 'english'
            ELSE 'subject-' || s.""SubjectCode""::text
        END AS subject_code_str,
        g.""Number"" AS grade_number
    FROM learning.""KnowledgeNodes"" kn
    INNER JOIN learning.""Subjects"" s ON kn.""SubjectId"" = s.""Id""
    INNER JOIN learning.""Grades"" g ON kn.""GradeId"" = g.""Id""
    WHERE kn.""SkillKey"" IS NULL
      AND (s.""IsDeleted"" IS NOT TRUE)
      AND (g.""IsDeleted"" IS NOT TRUE)
),
keyed AS (
    SELECT
        ""Id"",
        subject_code_str || '.grade' || grade_number || '.' || name_slug AS candidate_key,
        COUNT(*) OVER (
            PARTITION BY subject_code_str || '.grade' || grade_number || '.' || name_slug
        ) AS dup_count
    FROM candidates
    WHERE name_slug <> '' AND name_slug IS NOT NULL
)
UPDATE learning.""KnowledgeNodes"" kn
SET ""SkillKey"" = k.candidate_key
FROM keyed k
WHERE kn.""Id"" = k.""Id""
  AND kn.""SkillKey"" IS NULL
  AND k.dup_count = 1
  AND NOT EXISTS (
      SELECT 1 FROM learning.""KnowledgeNodes"" existing
      WHERE existing.""SkillKey"" = k.candidate_key AND existing.""Id"" <> kn.""Id""
  );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Clear SkillKey values backfilled by this migration.
            // Identifies backfilled rows by the format: {code}.grade{N}.{slug}
            // Does NOT affect manually-set SkillKey values outside this pattern.
            // SkillKey remains NULLABLE — no constraint is removed.
            migrationBuilder.Sql(@"
UPDATE learning.""KnowledgeNodes""
SET ""SkillKey"" = NULL
WHERE ""SkillKey"" IS NOT NULL
  AND ""SkillKey"" ~ '^(math|science|arabic|english|subject-\d+)\.grade\d+\..+$';
");
        }
    }
}
