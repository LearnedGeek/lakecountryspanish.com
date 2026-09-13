using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCurriculumDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurriculumFamily",
                table: "Programs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CurriculumDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CurriculumFamily = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedById = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumDocumentGradeBands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    GradeBand = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumDocumentGradeBands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumDocumentGradeBands_CurriculumDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "CurriculumDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Programs_CurriculumFamily",
                table: "Programs",
                column: "CurriculumFamily");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumDocumentGradeBands_DocumentId_GradeBand",
                table: "CurriculumDocumentGradeBands",
                columns: new[] { "DocumentId", "GradeBand" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumDocuments_CurriculumFamily_DocumentType",
                table: "CurriculumDocuments",
                columns: new[] { "CurriculumFamily", "DocumentType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurriculumDocumentGradeBands");

            migrationBuilder.DropTable(
                name: "CurriculumDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Programs_CurriculumFamily",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "CurriculumFamily",
                table: "Programs");
        }
    }
}
