using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace Learnexia.Modules.Curriculum.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCurriculum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "curriculum");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "CurriculumVersions",
                schema: "curriculum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
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
                    table.PrimaryKey("PK_CurriculumVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumChunks",
                schema: "curriculum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    GradeId = table.Column<int>(type: "integer", nullable: false),
                    ConceptId = table.Column<int>(type: "integer", nullable: true),
                    SkillId = table.Column<int>(type: "integer", nullable: true),
                    SkillKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    CurriculumVersionId = table.Column<int>(type: "integer", nullable: false),
                    ProvenanceRef = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_CurriculumChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumChunks_CurriculumVersions_CurriculumVersionId",
                        column: x => x.CurriculumVersionId,
                        principalSchema: "curriculum",
                        principalTable: "CurriculumVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chunk_embeddings_bge_m3",
                schema: "curriculum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChunkId = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ModelVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Dimension = table.Column<int>(type: "integer", nullable: false),
                    Vector = table.Column<Vector>(type: "vector(1024)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunk_embeddings_bge_m3", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chunk_embeddings_bge_m3_CurriculumChunks_ChunkId",
                        column: x => x.ChunkId,
                        principalSchema: "curriculum",
                        principalTable: "CurriculumChunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chunk_embeddings_bge_m3_chunk_id",
                schema: "curriculum",
                table: "chunk_embeddings_bge_m3",
                column: "ChunkId");

            migrationBuilder.CreateIndex(
                name: "ix_chunk_embeddings_bge_m3_is_active",
                schema: "curriculum",
                table: "chunk_embeddings_bge_m3",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "ix_chunk_embeddings_bge_m3_vector_hnsw_cosine",
                schema: "curriculum",
                table: "chunk_embeddings_bge_m3",
                column: "Vector")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_chunks_grade_subject",
                schema: "curriculum",
                table: "CurriculumChunks",
                columns: new[] { "GradeId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_chunks_skill_id",
                schema: "curriculum",
                table: "CurriculumChunks",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_chunks_skill_key",
                schema: "curriculum",
                table: "CurriculumChunks",
                column: "SkillKey");

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_chunks_version_id",
                schema: "curriculum",
                table: "CurriculumChunks",
                column: "CurriculumVersionId");

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_versions_active_subject_language",
                schema: "curriculum",
                table: "CurriculumVersions",
                columns: new[] { "SubjectId", "Language" },
                unique: true,
                filter: "\"Status\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chunk_embeddings_bge_m3",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "CurriculumChunks",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "CurriculumVersions",
                schema: "curriculum");
        }
    }
}
