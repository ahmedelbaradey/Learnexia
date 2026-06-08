using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P7_05_AddLifecycleStateAndContentVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LifecycleState",
                schema: "learning",
                table: "Units",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "LifecycleState",
                schema: "learning",
                table: "Subjects",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "LifecycleState",
                schema: "learning",
                table: "QuizQuestions",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "LifecycleState",
                schema: "learning",
                table: "Lessons",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateTable(
                name: "ContentVersions",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                    DeletedBy = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentVersions_EntityType_EntityId",
                schema: "learning",
                table: "ContentVersions",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentVersions_EntityType_EntityId_VersionNumber",
                schema: "learning",
                table: "ContentVersions",
                columns: new[] { "EntityType", "EntityId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentVersions",
                schema: "learning");

            migrationBuilder.DropColumn(
                name: "LifecycleState",
                schema: "learning",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "LifecycleState",
                schema: "learning",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "LifecycleState",
                schema: "learning",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "LifecycleState",
                schema: "learning",
                table: "Lessons");
        }
    }
}
