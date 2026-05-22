using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Age",
                schema: "identity",
                table: "AspNetUsers",
                type: "integer",
                nullable: true,
                comment: "Child's age in years; null when unknown");

            migrationBuilder.AddColumn<int>(
                name: "Grade",
                schema: "identity",
                table: "AspNetUsers",
                type: "integer",
                nullable: true,
                comment: "Child's grade level (1–6); null for non-students");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Age",
                schema: "identity",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Grade",
                schema: "identity",
                table: "AspNetUsers");
        }
    }
}
