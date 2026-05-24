using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Parent.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "parent");

            migrationBuilder.CreateTable(
                name: "ParentStudent",
                schema: "parent",
                columns: table => new
                {
                    ParentId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()", comment: "UTC timestamp when the parent-student link was created"),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true, comment: "Id of the user who created the link (plain int, no FK constraint)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentStudent", x => new { x.ParentId, x.StudentId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudent_StudentId",
                schema: "parent",
                table: "ParentStudent",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParentStudent",
                schema: "parent");
        }
    }
}
