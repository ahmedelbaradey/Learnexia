using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillKeyToKnowledgeNode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SkillKey",
                schema: "learning",
                table: "KnowledgeNodes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_KnowledgeNodes_SkillKey",
                schema: "learning",
                table: "KnowledgeNodes",
                column: "SkillKey",
                unique: true,
                filter: "\"learning\".\"KnowledgeNodes\".\"SkillKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_KnowledgeNodes_SkillKey",
                schema: "learning",
                table: "KnowledgeNodes");

            migrationBuilder.DropColumn(
                name: "SkillKey",
                schema: "learning",
                table: "KnowledgeNodes");
        }
    }
}
