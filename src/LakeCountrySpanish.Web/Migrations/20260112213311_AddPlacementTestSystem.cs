using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPlacementTestSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFromLibrary",
                table: "Assignments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SourceAssignmentId",
                table: "Assignments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlacementTestSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartingLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeterminedLevel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuestionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LevelProgressJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalQuestionsAnswered = table.Column<int>(type: "int", nullable: false),
                    TotalCorrect = table.Column<int>(type: "int", nullable: false),
                    FailedAdvanceCount = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlacementTestSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlacementTestSessions_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_SourceAssignmentId",
                table: "Assignments",
                column: "SourceAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PlacementTestSessions_StudentId_Status",
                table: "PlacementTestSessions",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_Assignments_SourceAssignmentId",
                table: "Assignments",
                column: "SourceAssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_Assignments_SourceAssignmentId",
                table: "Assignments");

            migrationBuilder.DropTable(
                name: "PlacementTestSessions");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_SourceAssignmentId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "IsFromLibrary",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "SourceAssignmentId",
                table: "Assignments");
        }
    }
}
