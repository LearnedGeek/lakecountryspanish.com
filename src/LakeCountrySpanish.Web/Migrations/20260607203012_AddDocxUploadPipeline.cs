using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDocxUploadPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Days",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "Days",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Sessions",
                table: "Days",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Days",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "LessonVideos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonVideos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonVideos_Days_DayId",
                        column: x => x.DayId,
                        principalTable: "Days",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Shortlinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DestinationType = table.Column<int>(type: "integer", nullable: false),
                    DestinationId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClickCount = table.Column<int>(type: "integer", nullable: false),
                    LastClickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shortlinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Days_Slug",
                table: "Days",
                column: "Slug",
                unique: true,
                filter: "\"Slug\" IS NOT NULL AND \"Slug\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_LessonVideos_DayId_DisplayOrder",
                table: "LessonVideos",
                columns: new[] { "DayId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Shortlinks_Code",
                table: "Shortlinks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shortlinks_DestinationType_DestinationId",
                table: "Shortlinks",
                columns: new[] { "DestinationType", "DestinationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LessonVideos");

            migrationBuilder.DropTable(
                name: "Shortlinks");

            migrationBuilder.DropIndex(
                name: "IX_Days_Slug",
                table: "Days");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Days");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "Days");

            migrationBuilder.DropColumn(
                name: "Sessions",
                table: "Days");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Days");
        }
    }
}
