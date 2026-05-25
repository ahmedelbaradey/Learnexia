using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillGraphTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeNodes",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NodeType = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    GradeId = table.Column<int>(type: "integer", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_KnowledgeNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeNodes_Grades_GradeId",
                        column: x => x.GradeId,
                        principalSchema: "learning",
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeNodes_Skills_SkillId",
                        column: x => x.SkillId,
                        principalSchema: "learning",
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_KnowledgeNodes_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "learning",
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeEdges",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceNodeId = table.Column<int>(type: "integer", nullable: false),
                    TargetNodeId = table.Column<int>(type: "integer", nullable: false),
                    RelationshipType = table.Column<int>(type: "integer", nullable: false),
                    Strength = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false, defaultValue: 1.0m),
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
                    table.PrimaryKey("PK_KnowledgeEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeEdges_KnowledgeNodes_SourceNodeId",
                        column: x => x.SourceNodeId,
                        principalSchema: "learning",
                        principalTable: "KnowledgeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeEdges_KnowledgeNodes_TargetNodeId",
                        column: x => x.TargetNodeId,
                        principalSchema: "learning",
                        principalTable: "KnowledgeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEdges_SourceNodeId",
                schema: "learning",
                table: "KnowledgeEdges",
                column: "SourceNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEdges_TargetNodeId",
                schema: "learning",
                table: "KnowledgeEdges",
                column: "TargetNodeId");

            migrationBuilder.CreateIndex(
                name: "UX_KnowledgeEdges_SourceTarget_Type",
                schema: "learning",
                table: "KnowledgeEdges",
                columns: new[] { "SourceNodeId", "TargetNodeId", "RelationshipType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeNodes_GradeId",
                schema: "learning",
                table: "KnowledgeNodes",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeNodes_SubjectId",
                schema: "learning",
                table: "KnowledgeNodes",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "UX_KnowledgeNodes_SkillId",
                schema: "learning",
                table: "KnowledgeNodes",
                column: "SkillId",
                unique: true,
                filter: "\"learning\".\"KnowledgeNodes\".\"SkillId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeEdges",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "KnowledgeNodes",
                schema: "learning");
        }
    }
}
