using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VocabTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Spanish = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Article = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    English = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Theme = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MediaAssetId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VocabTerms");
        }
    }
}
