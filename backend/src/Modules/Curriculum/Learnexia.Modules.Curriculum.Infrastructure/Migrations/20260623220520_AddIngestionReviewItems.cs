using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Curriculum.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestionReviewItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IngestedAt",
                schema: "curriculum",
                table: "CurriculumDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IngestionDiagnostics",
                schema: "curriculum",
                table: "CurriculumDocuments",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IngestionStatus",
                schema: "curriculum",
                table: "CurriculumDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "IngestionReviewItems",
                schema: "curriculum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CurriculumDocumentId = table.Column<int>(type: "integer", nullable: false),
                    CurriculumVersionId = table.Column<int>(type: "integer", nullable: true),
                    SourceReference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SuggestedClassification = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
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
                    table.PrimaryKey("PK_IngestionReviewItems", x => x.Id);
                    table.ForeignKey(
                        name: "fk_ingestion_review_items_document",
                        column: x => x.CurriculumDocumentId,
                        principalSchema: "curriculum",
                        principalTable: "CurriculumDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_documents_ingestion_status",
                schema: "curriculum",
                table: "CurriculumDocuments",
                column: "IngestionStatus");

            migrationBuilder.CreateIndex(
                name: "ix_ingestion_review_items_document_id",
                schema: "curriculum",
                table: "IngestionReviewItems",
                column: "CurriculumDocumentId");

            migrationBuilder.CreateIndex(
                name: "ix_ingestion_review_items_document_status",
                schema: "curriculum",
                table: "IngestionReviewItems",
                columns: new[] { "CurriculumDocumentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_ingestion_review_items_status",
                schema: "curriculum",
                table: "IngestionReviewItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_ingestion_review_items_version_id",
                schema: "curriculum",
                table: "IngestionReviewItems",
                column: "CurriculumVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngestionReviewItems",
                schema: "curriculum");

            migrationBuilder.DropIndex(
                name: "ix_curriculum_documents_ingestion_status",
                schema: "curriculum",
                table: "CurriculumDocuments");

            migrationBuilder.DropColumn(
                name: "IngestedAt",
                schema: "curriculum",
                table: "CurriculumDocuments");

            migrationBuilder.DropColumn(
                name: "IngestionDiagnostics",
                schema: "curriculum",
                table: "CurriculumDocuments");

            migrationBuilder.DropColumn(
                name: "IngestionStatus",
                schema: "curriculum",
                table: "CurriculumDocuments");
        }
    }
}
