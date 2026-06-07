using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class DropVocabTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop AI-generated MediaAsset orphans (Source=3 → AIGenerated).
            // Worksheet-vocab POC is being retired; these rows have no consumer
            // once VocabTerms is gone. Physical files under
            // wwwroot/uploads/media/ai/ are left for manual filesystem cleanup
            // (they only exist on the dev machine — stg/prod never had the
            // Replicate token configured).
            migrationBuilder.Sql("DELETE FROM \"MediaAssets\" WHERE \"Source\" = 3;");

            migrationBuilder.DropTable(
                name: "VocabTerms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VocabTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaAssetId = table.Column<int>(type: "integer", nullable: true),
                    Article = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    English = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Spanish = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Theme = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabTerms_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VocabTerms_MediaAssetId",
                table: "VocabTerms",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabTerms_Theme",
                table: "VocabTerms",
                column: "Theme");

            migrationBuilder.CreateIndex(
                name: "IX_VocabTerms_Theme_Spanish",
                table: "VocabTerms",
                columns: new[] { "Theme", "Spanish" },
                unique: true);
        }
    }
}
