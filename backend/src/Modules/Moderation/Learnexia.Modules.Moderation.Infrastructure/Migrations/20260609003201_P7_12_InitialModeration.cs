using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Moderation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P7_12_InitialModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "moderation");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "moderation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetEntityId = table.Column<int>(type: "integer", nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                schema: "moderation",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_AdminUserId",
                schema: "moderation",
                table: "AuditLogs",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EventId_Unique",
                schema: "moderation",
                table: "AuditLogs",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAtUtc",
                schema: "moderation",
                table: "AuditLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TargetEntityType_TargetEntityId",
                schema: "moderation",
                table: "AuditLogs",
                columns: new[] { "TargetEntityType", "TargetEntityId" });

            // -----------------------------------------------------------------------
            // F-02 / AC #6 — DB-level immutability guard (append-only enforcement).
            //
            // A BEFORE UPDATE OR DELETE trigger raises an exception for any attempt to
            // modify or remove an AuditLog row.  INSERT and TRUNCATE are intentionally
            // left unrestricted (TRUNCATE is not covered by row-level triggers, which is
            // acceptable for MVP — only the app role touches this table).
            //
            // CREATE OR REPLACE FUNCTION is idempotent; DROP TRIGGER IF EXISTS + CREATE
            // TRIGGER is idempotent on re-run.
            // -----------------------------------------------------------------------
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION moderation.audit_logs_immutability_guard()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'AuditLogs is append-only; UPDATE/DELETE are not permitted';
END;
$$;

DROP TRIGGER IF EXISTS trg_audit_logs_immutability ON moderation.""AuditLogs"";

CREATE TRIGGER trg_audit_logs_immutability
BEFORE UPDATE OR DELETE ON moderation.""AuditLogs""
FOR EACH ROW EXECUTE FUNCTION moderation.audit_logs_immutability_guard();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the immutability trigger + function before the table is removed.
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_audit_logs_immutability ON moderation.""AuditLogs"";
DROP FUNCTION IF EXISTS moderation.audit_logs_immutability_guard();
");

            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "moderation");
        }
    }
}
