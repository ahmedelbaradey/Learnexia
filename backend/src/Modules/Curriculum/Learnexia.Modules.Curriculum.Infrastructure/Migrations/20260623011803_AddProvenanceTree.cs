using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Curriculum.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProvenanceTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                schema: "curriculum",
                table: "CurriculumVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "curriculum",
                table: "CurriculumVersions",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                schema: "curriculum",
                table: "CurriculumVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublishedByUserId",
                schema: "curriculum",
                table: "CurriculumVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentSources",
                schema: "curriculum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Publisher = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Edition = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    GradeId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ContentSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KGSuggestions",
                schema: "curriculum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceNodeId = table.Column<int>(type: "integer", nullable: false),
                    TargetNodeId = table.Column<int>(type: "integer", nullable: false),
                    RelationshipType = table.Column<int>(type: "integer", nullable: false),
                    Strength = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    SourceCurriculumDocumentId = table.Column<int>(type: "integer", nullable: true),
                    InferenceModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_KGSuggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Chapters",
                schema: "curriculum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContentSourceId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PageStart = table.Column<int>(type: "integer", nullable: true),
                    PageEnd = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_Chapters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chapters_ContentSources_ContentSourceId",
                        column: x => x.ContentSourceId,
                        principalSchema: "curriculum",
                        principalTable: "ContentSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProvenanceMappings",
                schema: "curriculum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChunkId = table.Column<int>(type: "integer", nullable: false),
                    ContentSourceId = table.Column<int>(type: "integer", nullable: false),
                    ChapterId = table.Column<int>(type: "integer", nullable: true),
                    PageOrBlockRef = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PedagogicalNodeId = table.Column<int>(type: "integer", nullable: false),
                    PedagogicalNodeType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProvenanceMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProvenanceMappings_Chapters_ChapterId",
                        column: x => x.ChapterId,
                        principalSchema: "curriculum",
                        principalTable: "Chapters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProvenanceMappings_ContentSources_ContentSourceId",
                        column: x => x.ContentSourceId,
                        principalSchema: "curriculum",
                        principalTable: "ContentSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvenanceMappings_CurriculumChunks_ChunkId",
                        column: x => x.ChunkId,
                        principalSchema: "curriculum",
                        principalTable: "CurriculumChunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chapters_content_source_id",
                schema: "curriculum",
                table: "Chapters",
                column: "ContentSourceId");

            migrationBuilder.CreateIndex(
                name: "ix_content_sources_grade_id",
                schema: "curriculum",
                table: "ContentSources",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "ix_kg_suggestions_source_target_type",
                schema: "curriculum",
                table: "KGSuggestions",
                columns: new[] { "SourceNodeId", "TargetNodeId", "RelationshipType" });

            migrationBuilder.CreateIndex(
                name: "ix_kg_suggestions_status",
                schema: "curriculum",
                table: "KGSuggestions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_provenance_mappings_chapter_id",
                schema: "curriculum",
                table: "ProvenanceMappings",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "ix_provenance_mappings_chunk_source",
                schema: "curriculum",
                table: "ProvenanceMappings",
                columns: new[] { "ChunkId", "ContentSourceId" });

            migrationBuilder.CreateIndex(
                name: "ix_provenance_mappings_content_source_id",
                schema: "curriculum",
                table: "ProvenanceMappings",
                column: "ContentSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KGSuggestions",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "ProvenanceMappings",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "Chapters",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "ContentSources",
                schema: "curriculum");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                schema: "curriculum",
                table: "CurriculumVersions");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "curriculum",
                table: "CurriculumVersions");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                schema: "curriculum",
                table: "CurriculumVersions");

            migrationBuilder.DropColumn(
                name: "PublishedByUserId",
                schema: "curriculum",
                table: "CurriculumVersions");
        }
    }
}
