using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropParentStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParentStudents",
                schema: "identity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParentStudents",
                schema: "identity",
                columns: table => new
                {
                    ParentId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()", comment: "UTC timestamp when the parent-student link was created"),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true, comment: "Id of the user who created the link (plain int, no FK constraint)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentStudents", x => new { x.ParentId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_ParentStudents_AspNetUsers_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentStudents_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudents_StudentId",
                schema: "identity",
                table: "ParentStudents",
                column: "StudentId");
        }
    }
}
