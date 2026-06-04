using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P8_01_AddLearningLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LearningLanguage",
                schema: "identity",
                table: "AspNetUsers",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "ar",
                comment: "Medium-of-instruction language for curriculum delivery (ar or en). Distinct from PreferredLanguage (UI language). Set by parent at onboarding; student cannot change it.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LearningLanguage",
                schema: "identity",
                table: "AspNetUsers");
        }
    }
}
