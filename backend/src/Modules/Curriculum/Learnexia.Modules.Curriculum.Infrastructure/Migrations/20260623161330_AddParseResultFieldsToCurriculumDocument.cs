using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Curriculum.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParseResultFieldsToCurriculumDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParsedArtifactObjectKey",
                schema: "curriculum",
                table: "CurriculumDocuments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ParsedAt",
                schema: "curriculum",
                table: "CurriculumDocuments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParsedArtifactObjectKey",
                schema: "curriculum",
                table: "CurriculumDocuments");

            migrationBuilder.DropColumn(
                name: "ParsedAt",
                schema: "curriculum",
                table: "CurriculumDocuments");
        }
    }
}
