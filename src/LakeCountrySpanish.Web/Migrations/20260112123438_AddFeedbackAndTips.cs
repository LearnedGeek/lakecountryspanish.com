using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackAndTips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClassFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduledClassId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    PrivateComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublicTestimonial = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AllowPublicDisplay = table.Column<bool>(type: "bit", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassFeedbacks_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassFeedbacks_ScheduledClasses_ScheduledClassId",
                        column: x => x.ScheduledClassId,
                        principalTable: "ScheduledClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ScheduledClassId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StripeSessionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tips_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tips_ScheduledClasses_ScheduledClassId",
                        column: x => x.ScheduledClassId,
                        principalTable: "ScheduledClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassFeedbacks_ScheduledClassId_StudentId",
                table: "ClassFeedbacks",
                columns: new[] { "ScheduledClassId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassFeedbacks_StudentId",
                table: "ClassFeedbacks",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tips_ScheduledClassId",
                table: "Tips",
                column: "ScheduledClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Tips_StudentId",
                table: "Tips",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassFeedbacks");

            migrationBuilder.DropTable(
                name: "Tips");
        }
    }
}
