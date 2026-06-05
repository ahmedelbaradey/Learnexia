using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P8_02_AddSubjectCodeAndLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Step 1: Add the new columns (both default to 0, i.e. MATH/Ar). ─────────────────────
            // This is safe for any surviving rows before the cleanup in Step 2.
            migrationBuilder.AddColumn<int>(
                name: "Language",
                schema: "learning",
                table: "Subjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubjectCode",
                schema: "learning",
                table: "Subjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // ── Step 2: Re-seed-from-scratch cleanup (lead-approved). ────────────────────────────────
            // Delete all seeded curriculum rows in FK-safe dependency order before adding the
            // UNIQUE index on (GradeId, SubjectCode, Language).
            //
            // All Subject→child FKs use DeleteBehavior.Restrict, so children must be removed first:
            //   StudentAnswers  → Attempts    (Cascade on AttemptId; Restrict on QuestionId)
            //   Attempts        → Lessons     (plain int LessonId — no FK, delete freely)
            //   KnowledgeEdges  → KnowledgeNodes  (Restrict)
            //   KnowledgeNodes  → Subjects    (Restrict), → Skills (SetNull)
            //   QuizQuestions   → Lessons     (plain int LessonId — no FK)
            //   Lessons         → Units       (Restrict), → Skills (SetNull)
            //   Units           → Subjects    (Restrict)
            //   Skills          → Concepts    (Restrict)
            //   Concepts        → Subjects    (Restrict)
            //   Subjects        (delete last)
            //
            // NOTE: this data deletion is NOT reversible in Down(). Down() only drops the index
            // and columns; it cannot restore deleted rows. This is the accepted re-seed-from-scratch
            // trade-off (dev-only environment, no real student progress — lead decision, P8-02 brief §Blockers).

            // 1. StudentAnswers first (Restrict FK to QuizQuestions — must precede QuizQuestions delete;
            //    Cascade FK to Attempts — would cascade anyway, but explicit is safer).
            migrationBuilder.Sql("DELETE FROM learning.\"StudentAnswers\";");

            // 2. Attempts (plain int LessonId — no FK blocking; StudentAnswers already gone).
            migrationBuilder.Sql("DELETE FROM learning.\"Attempts\";");

            // 3. KnowledgeEdges (Restrict FK to KnowledgeNodes — must precede KnowledgeNodes delete).
            migrationBuilder.Sql("DELETE FROM learning.\"KnowledgeEdges\";");

            // 4. KnowledgeNodes (Restrict FK to Subjects — must precede Subjects delete;
            //    SetNull FK to Skills — null-safe, but KnowledgeNodes gone before Skills delete).
            migrationBuilder.Sql("DELETE FROM learning.\"KnowledgeNodes\";");

            // 5. QuizQuestions (plain int LessonId — no FK; StudentAnswers already gone so no Restrict block).
            migrationBuilder.Sql("DELETE FROM learning.\"QuizQuestions\";");

            // 6. Lessons (Restrict FK to Units — must precede Units delete;
            //    SetNull FK to Skills — null-safe).
            migrationBuilder.Sql("DELETE FROM learning.\"Lessons\";");

            // 7. Units (Restrict FK to Subjects — must precede Subjects delete).
            migrationBuilder.Sql("DELETE FROM learning.\"Units\";");

            // 8. Skills (Restrict FK to Concepts — must precede Concepts delete;
            //    Lessons.SkillId already cleared by SetNull or Lessons already deleted).
            migrationBuilder.Sql("DELETE FROM learning.\"Skills\";");

            // 9. Concepts (Restrict FK to Subjects — must precede Subjects delete).
            migrationBuilder.Sql("DELETE FROM learning.\"Concepts\";");

            // 10. Subjects — now safe; all children removed.
            migrationBuilder.Sql("DELETE FROM learning.\"Subjects\";");

            // ── Step 3: Add the UNIQUE index — safe because Subjects table is now empty. ────────────
            migrationBuilder.CreateIndex(
                name: "IX_Subjects_GradeId_SubjectCode_Language",
                schema: "learning",
                table: "Subjects",
                columns: new[] { "GradeId", "SubjectCode", "Language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the UNIQUE index and the two new columns.
            // NOTE: data deleted in Up() cannot be restored here. Down() is reversible only at
            // the schema level — the re-seed-from-scratch data cleanup is a one-way operation
            // (lead-approved, P8-02 plan §Blockers). After Down(), a fresh seed run would be needed.
            migrationBuilder.DropIndex(
                name: "IX_Subjects_GradeId_SubjectCode_Language",
                schema: "learning",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "Language",
                schema: "learning",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "SubjectCode",
                schema: "learning",
                table: "Subjects");
        }
    }
}
