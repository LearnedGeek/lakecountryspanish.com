using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTestimonialsAndScheduledClassFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tips_ScheduledClassId",
                table: "Tips");

            migrationBuilder.AddColumn<DateTime>(
                name: "FeedbackSubmittedAt",
                table: "ScheduledClasses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentComment",
                table: "ScheduledClasses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentRating",
                table: "ScheduledClasses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Testimonials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ShowFullName = table.Column<bool>(type: "bit", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Occupation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StudyDuration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RelatedClassId = table.Column<int>(type: "int", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Testimonials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Testimonials_AspNetUsers_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Testimonials_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Testimonials_ScheduledClasses_RelatedClassId",
                        column: x => x.RelatedClassId,
                        principalTable: "ScheduledClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tips_ScheduledClassId",
                table: "Tips",
                column: "ScheduledClassId",
                unique: true,
                filter: "[ScheduledClassId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Testimonials_RelatedClassId",
                table: "Testimonials",
                column: "RelatedClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Testimonials_ReviewedById",
                table: "Testimonials",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Testimonials_Status_IsFeatured_DisplayOrder",
                table: "Testimonials",
                columns: new[] { "Status", "IsFeatured", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Testimonials_StudentId",
                table: "Testimonials",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Testimonials");

            migrationBuilder.DropIndex(
                name: "IX_Tips_ScheduledClassId",
                table: "Tips");

            migrationBuilder.DropColumn(
                name: "FeedbackSubmittedAt",
                table: "ScheduledClasses");

            migrationBuilder.DropColumn(
                name: "StudentComment",
                table: "ScheduledClasses");

            migrationBuilder.DropColumn(
                name: "StudentRating",
                table: "ScheduledClasses");

            migrationBuilder.CreateIndex(
                name: "IX_Tips_ScheduledClassId",
                table: "Tips",
                column: "ScheduledClassId");
        }
    }
}
